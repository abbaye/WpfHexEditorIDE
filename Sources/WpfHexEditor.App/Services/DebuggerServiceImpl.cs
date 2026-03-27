// ==========================================================
// Project: WpfHexEditor.App
// File: Services/DebuggerServiceImpl.cs
// Description:
//     Concrete implementation of IDebuggerService.
//     Orchestrates the DAP client (NetCoreDapAdapter), manages session state,
//     syncs breakpoints with the adapter, and publishes IDE events.
// Architecture:
//     UI layer: App — IDebuggerService (SDK contract) exposed to plugins.
//     State machine: Idle → Launching → Running → Paused → Stopped.
//     Thread safety: public members marshalled to UI thread via Dispatcher.
// ==========================================================

using System.IO;
using System.Windows;
using WpfHexEditor.Core.Debugger.Adapters;
using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.Core.Debugger.Protocol;
using WpfHexEditor.Core.Debugger.Services;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Core.Options;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// IDE-level integrated debugger service.
/// Created once by AppServiceCollection; disposed on IDE shutdown.
/// </summary>
public sealed class DebuggerServiceImpl : IDebuggerService, IAsyncDisposable
{
    private readonly IIDEEventBus      _eventBus;
    private readonly AppSettings       _settings;
    private readonly object            _lock = new();

    private IDapClient?      _client;
    private DebugSession     _session = DebugSession.Empty;

    // Breakpoint list (IDE-managed, synced to adapter per file)
    private readonly List<BreakpointLocation>    _breakpoints = [];
    private readonly BreakpointPersistenceManager _persistence;
    private string? _currentSolutionPath;

    // ── IDebuggerService properties ───────────────────────────────────────────

    public DebugState State          => MapState(_session.State);
    public bool       IsActive       => _session.IsActive;
    public bool       IsPaused       => _session.IsPaused;
    public string?    PausedFilePath => _session.PausedFilePath;
    public int        PausedLine     => _session.PausedLine;

    public event EventHandler? SessionChanged;
    public event EventHandler? BreakpointsChanged;

    public IReadOnlyList<DebugBreakpointInfo> Breakpoints =>
        _breakpoints.Select(b => new DebugBreakpointInfo(
            b.FilePath, b.Line, string.IsNullOrEmpty(b.Condition) ? null : b.Condition,
            b.IsEnabled, b.IsVerified, b.HitCount)).ToList();

    public DebuggerServiceImpl(IIDEEventBus eventBus, AppSettings settings)
    {
        _eventBus    = eventBus;
        _settings    = settings;
        _persistence = new BreakpointPersistenceManager(settings);
        lock (_lock) { _breakpoints.AddRange(_persistence.Load()); }
    }

    // ── Launch / Attach ───────────────────────────────────────────────────────

    /// <summary>Launch a new debug session for the given configuration.</summary>
    public async Task LaunchAsync(DebugLaunchConfig config)
    {
        if (_session.IsActive) await StopSessionAsync();

        // Reset hit counts for the new session.
        lock (_lock)
        {
            for (int i = 0; i < _breakpoints.Count; i++)
                _breakpoints[i] = _breakpoints[i] with { HitCount = 0 };
        }

        var adapterPath = DebugAdapterLocator.Locate(_settings.Debugger.NetCoreDbgPath);
        if (adapterPath is null)
        {
            MessageBox.Show(
                "Debug adapter (netcoredbg) not found.\n" +
                "Install it or set the path in Options → Debugger.",
                "Debugger", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UpdateSession(_session with { State = DebugSessionState.Launching });

        try
        {
            _client = await NetCoreDapAdapter.CreateAsync(adapterPath);
            WireClientEvents(_client);

            await _client.InitializeAsync(new InitializeRequestArgs("WpfHexEditor"));
            await SyncAllBreakpointsAsync(_client);
            await _client.LaunchAsync(NetCoreDapAdapter.BuildLaunchArgs(config));
            await _client.ConfigurationDoneAsync();

            var sessionId = Guid.NewGuid().ToString("N");
            UpdateSession(new DebugSession
            {
                SessionId   = sessionId,
                State       = DebugSessionState.Running,
                ProjectPath = config.ProjectPath,
            });

            _eventBus.Publish(new DebugSessionStartedEvent
            {
                SessionId   = sessionId,
                ProjectPath = config.ProjectPath,
                Source      = nameof(DebuggerServiceImpl)
            });
        }
        catch (Exception ex)
        {
            await CleanupClientAsync();
            UpdateSession(DebugSession.Empty);
            MessageBox.Show($"Failed to start debug session:\n{ex.Message}", "Debugger",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Attach to a running .NET process by PID.</summary>
    public async Task AttachAsync(int pid)
    {
        if (_session.IsActive) await StopSessionAsync();

        var adapterPath = DebugAdapterLocator.Locate(_settings.Debugger.NetCoreDbgPath);
        if (adapterPath is null) return;

        UpdateSession(_session with { State = DebugSessionState.Launching });

        try
        {
            _client = await NetCoreDapAdapter.CreateAsync(adapterPath);
            WireClientEvents(_client);

            await _client.InitializeAsync(new InitializeRequestArgs("WpfHexEditor"));
            await SyncAllBreakpointsAsync(_client);
            await _client.AttachAsync(new AttachRequestArgs(pid));
            await _client.ConfigurationDoneAsync();

            var sessionId = Guid.NewGuid().ToString("N");
            UpdateSession(new DebugSession
            {
                SessionId  = sessionId,
                State      = DebugSessionState.Running,
                ProcessId  = pid,
            });

            _eventBus.Publish(new DebugSessionStartedEvent
            {
                SessionId = sessionId,
                ProcessId = pid,
                Source    = nameof(DebuggerServiceImpl)
            });
        }
        catch (Exception ex)
        {
            await CleanupClientAsync();
            UpdateSession(DebugSession.Empty);
            MessageBox.Show($"Failed to attach to process {pid}:\n{ex.Message}", "Debugger",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── IDebuggerService — execution ──────────────────────────────────────────

    public async Task ContinueAsync()
    {
        if (_client is null || !_session.IsPaused) return;
        await _client.ContinueAsync(new ContinueArgs(_session.ActiveThreadId));
        UpdateSession(_session with { State = DebugSessionState.Running, PausedFilePath = null, PausedLine = 0 });
        _eventBus.Publish(new DebugSessionResumedEvent { SessionId = _session.SessionId, Source = nameof(DebuggerServiceImpl) });
    }

    public async Task StepOverAsync()
    {
        if (_client is null || !_session.IsPaused) return;
        await _client.NextAsync(new StepArgs(_session.ActiveThreadId));
    }

    public async Task StepIntoAsync()
    {
        if (_client is null || !_session.IsPaused) return;
        await _client.StepInAsync(new StepArgs(_session.ActiveThreadId));
    }

    public async Task StepOutAsync()
    {
        if (_client is null || !_session.IsPaused) return;
        await _client.StepOutAsync(new StepArgs(_session.ActiveThreadId));
    }

    public async Task PauseAsync()
    {
        if (_client is null || !_session.IsActive || _session.IsPaused) return;
        await _client.PauseAsync(new PauseArgs(_session.ActiveThreadId));
    }

    // ── IDebuggerService — inspection ─────────────────────────────────────────

    public async Task<IReadOnlyList<DebugFrameInfo>> GetCallStackAsync()
    {
        if (_client is null || !_session.IsPaused) return [];
        var body = await _client.StackTraceAsync(new StackTraceArgs(_session.ActiveThreadId, Levels: 30));
        return body?.StackFrames.Select(f => new DebugFrameInfo(
            f.Id, f.Name, f.Source?.Path, f.Line, f.Column)).ToList() ?? [];
    }

    public async Task<IReadOnlyList<DebugVariableInfo>> GetVariablesAsync(int variablesReference)
    {
        if (_client is null || !_session.IsPaused) return [];
        var body = await _client.VariablesAsync(new VariablesArgs(variablesReference));
        return body?.Variables.Select(v => new DebugVariableInfo(
            v.Name, v.Value, v.Type, v.VariablesReference)).ToList() ?? [];
    }

    public async Task<string> EvaluateAsync(string expression, int? frameId = null)
    {
        if (_client is null || !_session.IsPaused) return "<not paused>";
        var body = await _client.EvaluateAsync(new EvaluateArgs(expression, frameId ?? _session.CurrentFrameId));
        return body?.Result ?? "<null>";
    }

    // ── IDebuggerService — breakpoints ────────────────────────────────────────

    public async Task<bool> ToggleBreakpointAsync(string filePath, int line, string? condition = null)
    {
        bool isNowSet;
        lock (_lock)
        {
            var existing = _breakpoints.FirstOrDefault(
                b => string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase) && b.Line == line);

            if (existing is not null)
            {
                _breakpoints.Remove(existing);
                isNowSet = false;
            }
            else
            {
                _breakpoints.Add(new BreakpointLocation
                {
                    FilePath  = filePath,
                    Line      = line,
                    Condition = condition ?? string.Empty,
                    IsEnabled = true,
                });
                isNowSet = true;
            }
        }

        _persistence.SaveForContext(_currentSolutionPath, _breakpoints);
        BreakpointsChanged?.Invoke(this, EventArgs.Empty);

        if (_client is not null && _session.IsActive)
            await SyncBreakpointsForFileAsync(_client, filePath);

        return isNowSet;
    }

    public async Task UpdateBreakpointAsync(string filePath, int line, string? condition, bool isEnabled)
    {
        lock (_lock)
        {
            int idx = _breakpoints.FindIndex(
                b => string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase) && b.Line == line);
            if (idx < 0) return;

            // BreakpointLocation is an init-only record — replace with a `with` expression.
            _breakpoints[idx] = _breakpoints[idx] with
            {
                Condition = condition ?? string.Empty,
                IsEnabled = isEnabled,
            };
        }

        _persistence.SaveForContext(_currentSolutionPath, _breakpoints);
        BreakpointsChanged?.Invoke(this, EventArgs.Empty);

        if (_client is not null && _session.IsActive)
            await SyncBreakpointsForFileAsync(_client, filePath);
    }

    public async Task DeleteBreakpointAsync(string filePath, int line)
    {
        lock (_lock)
        {
            var bp = _breakpoints.FirstOrDefault(
                b => string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase) && b.Line == line);
            if (bp is null) return;
            _breakpoints.Remove(bp);
        }

        _persistence.SaveForContext(_currentSolutionPath, _breakpoints);
        BreakpointsChanged?.Invoke(this, EventArgs.Empty);

        if (_client is not null && _session.IsActive)
            await SyncBreakpointsForFileAsync(_client, filePath);
    }

    public async Task ClearAllBreakpointsAsync()
    {
        IReadOnlyList<string> files;
        lock (_lock)
        {
            files = _breakpoints.Select(b => b.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _breakpoints.Clear();
        }

        _persistence.SaveForContext(_currentSolutionPath, _breakpoints);
        BreakpointsChanged?.Invoke(this, EventArgs.Empty);

        if (_client is not null && _session.IsActive)
            foreach (var file in files)
                await SyncBreakpointsForFileAsync(_client, file);
    }

    // ── Solution lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Called when the active solution changes. Saves breakpoints for the old solution,
    /// clears the list, and loads breakpoints for the new solution (or global fallback).
    /// </summary>
    public void OnSolutionChanged(string? newSolutionFilePath)
    {
        lock (_lock)
        {
            // Save current breakpoints to the old context before switching.
            _persistence.SaveForContext(_currentSolutionPath, _breakpoints);
            _breakpoints.Clear();

            _currentSolutionPath = newSolutionFilePath;

            // Load breakpoints for the new context.
            _breakpoints.AddRange(_persistence.LoadForContext(newSolutionFilePath));
        }

        BreakpointsChanged?.Invoke(this, EventArgs.Empty);

        // Sync with active DAP client if a session is running.
        if (_client is not null && _session.IsActive)
            _ = SyncAllBreakpointsAsync(_client);
    }

    // ── Hit count ────────────────────────────────────────────────────────────

    public int GetHitCount(string filePath, int line)
    {
        lock (_lock)
        {
            var bp = _breakpoints.FirstOrDefault(b =>
                string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase) && b.Line == line);
            return bp?.HitCount ?? 0;
        }
    }

    /// <summary>Increment hit count for the breakpoint at the given location.</summary>
    internal void IncrementHitCount(string filePath, int line)
    {
        lock (_lock)
        {
            int idx = _breakpoints.FindIndex(b =>
                string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase) && b.Line == line);
            if (idx >= 0)
                _breakpoints[idx] = _breakpoints[idx] with { HitCount = _breakpoints[idx].HitCount + 1 };
        }
        BreakpointsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Stop ──────────────────────────────────────────────────────────────────

    public async Task StopSessionAsync()
    {
        if (_client is null) return;
        try { await _client.DisconnectAsync(new DisconnectArgs(TerminateDebuggee: true)).WaitAsync(TimeSpan.FromSeconds(3)); }
        catch { /* ignore */ }
        await CleanupClientAsync();
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private void WireClientEvents(IDapClient client)
    {
        client.Stopped   += OnAdapterStopped;
        client.Output    += OnAdapterOutput;
        client.Exited    += OnAdapterExited;
        client.Terminated += OnAdapterTerminated;
    }

    private void OnAdapterStopped(object? sender, StoppedEventBody e)
    {
        _ = Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            // Get top frame for file/line
            string filePath = string.Empty;
            int    line     = 0;

            if (_client is not null)
            {
                var stack = await _client.StackTraceAsync(new StackTraceArgs(e.ThreadId ?? 1, Levels: 1));
                var frame = stack?.StackFrames.FirstOrDefault();
                filePath = frame?.Source?.Path ?? string.Empty;
                line     = frame?.Line ?? 0;
            }

            var reason = e.Reason switch
            {
                StopReason.Breakpoint         => PauseReason.Breakpoint,
                StopReason.Step               => PauseReason.Step,
                StopReason.Exception          => PauseReason.Exception,
                StopReason.Pause              => PauseReason.Pause,
                StopReason.Entry              => PauseReason.Entry,
                _                             => PauseReason.None,
            };

            UpdateSession(_session with
            {
                State          = DebugSessionState.Paused,
                ActiveThreadId = e.ThreadId ?? 1,
                PausedFilePath = filePath,
                PausedLine     = line,
                PauseReason    = reason,
            });

            _eventBus.Publish(new DebugSessionPausedEvent
            {
                SessionId = _session.SessionId,
                FilePath  = filePath,
                Line      = line,
                Reason    = e.Reason,
                ThreadId  = e.ThreadId ?? 1,
                Source    = nameof(DebuggerServiceImpl)
            });

            if (e.Reason == StopReason.Breakpoint)
            {
                if (!string.IsNullOrEmpty(filePath) && line > 0)
                    IncrementHitCount(filePath, line);

                _eventBus.Publish(new BreakpointHitEvent
                {
                    FilePath = filePath, Line = line, ThreadId = e.ThreadId ?? 1,
                    Source   = nameof(DebuggerServiceImpl)
                });
            }
            else if (e.Reason == StopReason.Exception)
                _eventBus.Publish(new ExceptionHitEvent
                {
                    ExceptionType = e.Description ?? "Exception",
                    Message       = e.Text ?? string.Empty,
                    FilePath      = filePath, Line = line,
                    Source        = nameof(DebuggerServiceImpl)
                });
            else if (e.Reason is StopReason.Step or StopReason.Goto)
                _eventBus.Publish(new StepCompletedEvent
                {
                    FilePath = filePath, Line = line,
                    Source   = nameof(DebuggerServiceImpl)
                });
        });
    }

    private void OnAdapterOutput(object? sender, OutputEventBody e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
            _eventBus.Publish(new DebugOutputReceivedEvent
            {
                Category = e.Category,
                Output   = e.Output,
                Source   = nameof(DebuggerServiceImpl)
            }));
    }

    private void OnAdapterExited(object? sender, ExitedEventBody e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var sessionId = _session.SessionId;
            UpdateSession(DebugSession.Empty);
            _eventBus.Publish(new DebugSessionEndedEvent
            {
                SessionId = sessionId,
                ExitCode  = e.ExitCode,
                Source    = nameof(DebuggerServiceImpl)
            });
        });
    }

    private void OnAdapterTerminated(object? sender, EventArgs e) =>
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var sessionId = _session.SessionId;
            UpdateSession(DebugSession.Empty);
            _eventBus.Publish(new DebugSessionEndedEvent
            {
                SessionId = sessionId,
                Source    = nameof(DebuggerServiceImpl)
            });
        });

    private void UpdateSession(DebugSession session)
    {
        _session = session;
        Application.Current?.Dispatcher.Invoke(() => SessionChanged?.Invoke(this, EventArgs.Empty));
    }

    private async Task SyncAllBreakpointsAsync(IDapClient client)
    {
        IReadOnlyList<string> files;
        lock (_lock)
            files = _breakpoints.Select(b => b.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var file in files)
            await SyncBreakpointsForFileAsync(client, file);
    }

    private async Task SyncBreakpointsForFileAsync(IDapClient client, string filePath)
    {
        List<BreakpointLocation> forFile;
        lock (_lock)
            forFile = _breakpoints
                .Where(b => string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase) && b.IsEnabled)
                .ToList();

        var bpArgs = new SetBreakpointsArgs(
            Source:      new SourceDto(Path.GetFileName(filePath), filePath),
            Breakpoints: forFile.Select(b => new SourceBreakpointDto(b.Line,
                string.IsNullOrEmpty(b.Condition) ? null : b.Condition)).ToArray());

        var result = await client.SetBreakpointsAsync(bpArgs);

        // Update IsVerified on matching breakpoints
        if (result is not null)
        {
            lock (_lock)
            {
                for (int i = 0; i < Math.Min(forFile.Count, result.Breakpoints.Length); i++)
                {
                    var idx = _breakpoints.IndexOf(forFile[i]);
                    if (idx >= 0)
                        _breakpoints[idx] = forFile[i] with { IsVerified = result.Breakpoints[i].Verified };
                }
            }
            Application.Current?.Dispatcher.Invoke(() => BreakpointsChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    private async Task CleanupClientAsync()
    {
        if (_client is null) return;
        _client.Stopped   -= OnAdapterStopped;
        _client.Output    -= OnAdapterOutput;
        _client.Exited    -= OnAdapterExited;
        _client.Terminated -= OnAdapterTerminated;
        await _client.DisposeAsync();
        _client = null;
    }

    // ── State mapping ─────────────────────────────────────────────────────────

    private static DebugState MapState(DebugSessionState s) => s switch
    {
        DebugSessionState.Idle      => DebugState.Idle,
        DebugSessionState.Launching => DebugState.Launching,
        DebugSessionState.Running   => DebugState.Running,
        DebugSessionState.Paused    => DebugState.Paused,
        DebugSessionState.Stopped   => DebugState.Stopped,
        _                           => DebugState.Idle
    };

    // ── IAsyncDisposable ──────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await StopSessionAsync();
    }
}
