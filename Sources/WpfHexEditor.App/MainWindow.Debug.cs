// ==========================================================
// Project: WpfHexEditor.App
// File: MainWindow.Debug.cs
// Description:
//     Partial class handling DAP-based debugger integration.
//     Wires BreakpointSourceAdapter to CodeEditor gutter controls,
//     subscribes to debug IDE events, and provides command handlers
//     for F5/F9/F10/F11/Shift+F11/Shift+F5/Ctrl+Shift+F5/Ctrl+Alt+P.
// Architecture:
//     App layer only — no reference to Core.Debugger from this file.
//     BreakpointSourceAdapter implements IBreakpointSource (CodeEditor contract)
//     and bridges to IDebuggerService (SDK contract) without creating
//     compile-time coupling between CodeEditor and Core.Debugger.
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.Editor.CodeEditor;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Core.Options;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.App.Services;
using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // Shared breakpoint source adapter — injected into every CodeEditor gutter.
    private BreakpointSourceAdapter? _bpSourceAdapter;

    // Shared debug hover adapter — injected into every CodeEditor for Data Tips.
    private DebugHoverAdapter? _debugHoverAdapter;

    // IDisposable subscriptions for debug events (disposed on shutdown).
    private IDisposable[]? _debugSubs;

    // Stored handler reference for BreakpointsChanged (so we can unsubscribe on shutdown).
    private EventHandler? _bpChangedHandler;

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Called from InitializePluginSystemAsync after _debuggerService is created.
    /// Wires the gutter adapter and subscribes to debug lifecycle events.
    /// </summary>
    private void InitDebugIntegration()
    {
        if (_debuggerService is null || _ideEventBus is null) return;

        _bpSourceAdapter   = new BreakpointSourceAdapter(_debuggerService);
        _debugHoverAdapter = new DebugHoverAdapter(_debuggerService);

        // When breakpoints change, refresh all open CodeEditor gutters.
        _bpChangedHandler = (_, _) => Dispatcher.InvokeAsync(RefreshAllBreakpointGutters);
        _debuggerService.BreakpointsChanged += _bpChangedHandler;

        _debugSubs =
        [
            // Pause → highlight execution line in matching CodeEditor + update status bar + toolbar.
            _ideEventBus.Subscribe<DebugSessionPausedEvent>(e =>
                Dispatcher.InvokeAsync(() => OnDebugSessionPaused(e))),

            // Session started → show debug toolbar, enable Data Tips.
            _ideEventBus.Subscribe<DebugSessionStartedEvent>(_ =>
                Dispatcher.InvokeAsync(() =>
                {
                    UpdateDebugToolbarState(isActive: true, isPaused: false);
                    SetDebugHoverProviderOnAllEditors(_debugHoverAdapter);
                })),

            // Resume → update toolbar state + clear status / execution line, disable Data Tips.
            _ideEventBus.Subscribe<DebugSessionResumedEvent>(_ =>
                Dispatcher.InvokeAsync(() =>
                {
                    UpdateDebugToolbarState(isActive: true, isPaused: false);
                    ClearAllExecutionLines();
                    UpdateDbgStatusBar(null);
                    _debugHoverAdapter?.SetPaused(false);
                })),

            // End → hide toolbar + clear everything, disable Data Tips.
            _ideEventBus.Subscribe<DebugSessionEndedEvent>(_ =>
                Dispatcher.InvokeAsync(() =>
                {
                    UpdateDebugToolbarState(isActive: false, isPaused: false);
                    ClearAllExecutionLines();
                    UpdateDbgStatusBar(null);
                    _debugHoverAdapter?.SetPaused(false);
                })),

            // Run to cursor — plugin publishes, App executes with active editor's caret.
            _ideEventBus.Subscribe<RunToCursorRequestedEvent>(_ =>
                Dispatcher.InvokeAsync(OnRunToCursor)),
        ];

        // Wire any editors that were already open from layout restore.
        // WireBreakpointSourceToEditor() is a no-op when _bpSourceAdapter is null (it runs before
        // this method), so we must explicitly push the adapter to all existing CodeEditors now.
        RefreshAllBreakpointGutters();

        // Solution lifecycle → load/save per-solution breakpoints.
        _solutionManager.SolutionChanged += OnSolutionChangedForBreakpoints;

        // If a solution is already open (e.g. workspace restore), load its breakpoints now.
        if (_solutionManager.CurrentSolution is not null)
            (_debuggerService as DebuggerServiceImpl)?.OnSolutionChanged(_solutionManager.CurrentSolution.FilePath);

        // Register keyboard shortcuts for debug commands.
        // InputGestureText on MenuItems is display-only — actual WPF InputBindings are required.
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => OnDebugStartOrContinue()),           Key.F5,  ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => _ = _debuggerService?.StopSessionAsync()),  Key.F5,  ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => OnDebugRestart()),                   Key.F5,  ModifierKeys.Control | ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => OnToggleBreakpoint()),               Key.F9,  ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => _ = _debuggerService?.ClearAllBreakpointsAsync()), Key.F9, ModifierKeys.Control | ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => _ = _debuggerService?.StepOverAsync()),     Key.F10, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => _ = _debuggerService?.StepIntoAsync()),     Key.F11, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => _ = _debuggerService?.StepOutAsync()),      Key.F11, ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => OnRunToCursor()),                    Key.F10, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => OnAttachToProcess()),                Key.P,   ModifierKeys.Control | ModifierKeys.Alt));
    }

    // ── Gutter wiring ─────────────────────────────────────────────────────────

    /// <summary>
    /// Iterates all open documents and wires the breakpoint source into every
    /// CodeEditor control. Safe to call multiple times (idempotent).
    /// </summary>
    private void RefreshAllBreakpointGutters()
    {
        if (_bpSourceAdapter is null) return;
        foreach (var ce in GetAllCodeEditors())
        {
            ce.SetBreakpointSource(_bpSourceAdapter);
            WireBreakpointSettingsHandler(ce);
            ce.DebugHoverProvider = _debugHoverAdapter;
            ce.InvalidateVisual();
        }
    }

    private void SetDebugHoverProviderOnAllEditors(IDebugHoverProvider? provider)
    {
        foreach (var ce in GetAllCodeEditors())
            ce.DebugHoverProvider = provider;
    }

    /// <summary>
    /// Wire the breakpoint adapter into a newly created CodeEditor control.
    /// Called by the content-creation pipeline so the gutter is ready immediately.
    /// </summary>
    internal void WireBreakpointSourceToEditor(WpfHexEditor.Editor.Core.IDocumentEditor editor)
    {
        if (_bpSourceAdapter is null) return;
        var ce = GetCodeEditorControl(editor);
        if (ce is null) return;
        ce.SetBreakpointSource(_bpSourceAdapter);
        ce.DebugHoverProvider = _debugHoverAdapter;
        WireBreakpointSettingsHandler(ce);
    }

    /// <summary>
    /// Subscribe to <see cref="CodeEditor.BreakpointSettingsRequested"/> so the
    /// Debugger plugin can open <c>BreakpointConditionDialog</c> from the gutter popup.
    /// Uses a named field delegate to avoid duplicate subscriptions.
    /// </summary>
    private void WireBreakpointSettingsHandler(CodeEditor ce)
    {
        if (_debuggerService is null) return;
        // Unsubscribe first to prevent duplicate wiring if called multiple times.
        ce.BreakpointSettingsRequested -= OnCodeEditorBreakpointSettings;
        ce.BreakpointSettingsRequested += OnCodeEditorBreakpointSettings;
    }

    private void OnCodeEditorBreakpointSettings(string filePath, int line)
    {
        // BreakpointConditionDialog lives in the Debugger plugin assembly — App has no
        // project reference to it.  Publish an IDE event; the plugin subscribes and
        // opens the dialog on the UI thread, then calls UpdateBreakpointSettingsAsync.
        _ideEventBus?.Publish(new OpenBreakpointSettingsRequestedEvent
        {
            FilePath = filePath,
            Line     = line,
        });
    }

    private void OnDebugSessionPaused(DebugSessionPausedEvent e)
    {
        if (string.IsNullOrEmpty(e.FilePath)) return;

        foreach (var ce in GetAllCodeEditors())
        {
            if (IsEditorForFile(ce, e.FilePath))
            {
                ce.SetExecutionLine(e.Line);                          // 1-based
                (ce as WpfHexEditor.Editor.Core.INavigableDocument)
                    ?.NavigateTo(e.Line - 1, 0);                      // 0-based
            }
        }

        _debugHoverAdapter?.SetPaused(true);
        UpdateDebugToolbarState(isActive: true, isPaused: true);
        UpdateDbgStatusBar(e);
    }

    private void ClearAllExecutionLines()
    {
        foreach (var ce in GetAllCodeEditors())
            ce.SetExecutionLine(null);
    }

    // ── Command handlers ──────────────────────────────────────────────────────

    /// <summary>F5 — Start debugging or continue if already paused.</summary>
    internal void OnDebugStartOrContinue()
    {
        if (_debuggerService is null) return;

        if (_debuggerService.IsPaused)
        {
            _ = _debuggerService.ContinueAsync();
            return;
        }

        if (_debuggerService.IsActive) return; // already running — wait for pause

        _ = LaunchDebugSessionAsync();
    }

    /// <summary>Ctrl+Shift+F5 — Restart (stop + start).</summary>
    internal void OnDebugRestart()
    {
        if (_debuggerService is null) return;
        _ = RestartDebugSessionAsync();
    }

    /// <summary>F9 — Toggle breakpoint on the active CodeEditor's caret line.</summary>
    internal void OnToggleBreakpoint()
    {
        if (_debuggerService is null) return;

        var filePath = _documentManager.ActiveDocument?.FilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        var activeContentId = _documentManager.ActiveDocument?.ContentId;
        if (string.IsNullOrEmpty(activeContentId)) return;

        if (!_contentCache.TryGetValue(activeContentId, out var ctrl)) return;

        var ce = GetCodeEditorControl(ctrl as WpfHexEditor.Editor.Core.IDocumentEditor
                                   ?? ctrl as object);
        if (ce is null) return;

        int line1 = ce.CursorLine + 1; // CursorLine is 0-based → convert to 1-based
        _ = _debuggerService.ToggleBreakpointAsync(filePath, line1);
    }

    /// <summary>Ctrl+F10 — Run to cursor (caret line in active CodeEditor).</summary>
    internal void OnRunToCursor()
    {
        if (_debuggerService is null || !_debuggerService.IsPaused) return;

        var filePath = _documentManager.ActiveDocument?.FilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        var activeContentId = _documentManager.ActiveDocument?.ContentId;
        if (string.IsNullOrEmpty(activeContentId)) return;
        if (!_contentCache.TryGetValue(activeContentId, out var ctrl)) return;

        var ce = GetCodeEditorControl(ctrl as WpfHexEditor.Editor.Core.IDocumentEditor ?? ctrl as object);
        if (ce is null) return;

        int line1 = ce.CursorLine + 1;
        _ = _debuggerService.RunToCursorAsync(filePath, line1);
    }

    /// <summary>Ctrl+Alt+P — Attach to process dialog.</summary>
    internal void OnAttachToProcess(object? sender = null, RoutedEventArgs? e = null)
    {
        if (_debuggerService is null)
        {
            _dialogService.Show(AppResources.App_Debug_AttachNotAvailable, AppResources.App_Debug_AttachTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Delegate to AttachToProcessDialog contributed by the Debugger plugin.
        // The plugin subscribes to AttachToProcessRequestedEvent and opens the dialog.
        _ideEventBus?.Publish(new AttachToProcessRequestedEvent());
    }

    // ── Launch helpers ────────────────────────────────────────────────────────

    private async Task LaunchDebugSessionAsync()
    {
        var startupProject = _solutionManager.CurrentSolution?.StartupProject;
        if (startupProject is null)
        {
            _dialogService.Show(AppResources.App_Debug_NoStartupProject, AppResources.App_Debug_StartTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Build startup project before debugging (fast — only the target project, not the full solution).
        if (_buildSystem is not null)
        {
            ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
            OutputLogger.ClearChannel(OutputLogger.SourceBuild);
            var buildResult = await _buildSystem.BuildProjectAsync(startupProject.Id);
            _buildErrorListAdapter?.SetDiagnostics(buildResult.Errors.Concat(buildResult.Warnings));
            if (!buildResult.IsSuccess)
            {
                _dialogService.Show(AppResources.App_Debug_BuildFailed, AppResources.App_Debug_StartTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // Derive output exe path using the active build configuration.
        var projDir   = Path.GetDirectoryName(startupProject.ProjectFilePath) ?? ".";
        var projName  = Path.GetFileNameWithoutExtension(startupProject.ProjectFilePath);
        var cfgName   = _configManager?.ActiveConfiguration.Name ?? "Debug";

        // Scan bin/{cfg}/ for the actual TFM directory (avoids hardcoding net8.0-windows).
        string? programPath = null;
        var binCfgDir = Path.Combine(projDir, "bin", cfgName);
        if (Directory.Exists(binCfgDir))
        {
            foreach (var tfmDir in Directory.GetDirectories(binCfgDir))
            {
                var candidate = Path.Combine(tfmDir, projName + ".exe");
                if (File.Exists(candidate)) { programPath = candidate; break; }
                candidate = Path.Combine(tfmDir, projName + ".dll");
                if (File.Exists(candidate)) { programPath = candidate; break; }
            }
        }
        // Fallback to the conventional path if scan found nothing.
        programPath ??= Path.Combine(projDir, "bin", cfgName, "net8.0-windows", projName + ".exe");

        if (!File.Exists(programPath))
        {
            _dialogService.Show(
                string.Format(AppResources.App_Debug_ExeNotFound, "\n", programPath),
                AppResources.App_Debug_StartTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var launchConfig = new DebugLaunchConfig
        {
            ProjectPath = startupProject.ProjectFilePath,
            ProgramPath = programPath,
            WorkDir     = projDir,
            StopAtEntry = AppSettingsService.Instance.Current.Debugger.StopAtEntry,
        };

        // Show output panel so the user sees debug output.
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);

        await _debuggerService!.LaunchAsync(launchConfig);
    }

    private async Task RestartDebugSessionAsync()
    {
        await _debuggerService!.StopSessionAsync();
        await LaunchDebugSessionAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Enumerates all CodeEditor controls currently open in the document host.</summary>
    private IEnumerable<WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor> GetAllCodeEditors()
    {
        foreach (var kv in _contentCache)
        {
            var ce = kv.Value switch
            {
                WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor direct   => direct,
                CodeEditorSplitHost host   => host.PrimaryEditor,
                _                          => null,
            };
            if (ce is not null) yield return ce;
        }
    }

    /// <summary>Returns the CodeEditor for any editor object (duck-typed).</summary>
    private static WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor? GetCodeEditorControl(object? editor) => editor switch
    {
        WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor ce    => ce,
        CodeEditorSplitHost h   => h.PrimaryEditor,
        _                       => null,
    };

    private bool IsEditorForFile(WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor ce, string filePath)
    {
        // Resolve the content-cache key for this CodeEditor (or its split-host parent).
        object? hostControl = ce.Parent is CodeEditorSplitHost h ? h : (object)ce;

        foreach (var kv in _contentCache)
        {
            if (!ReferenceEquals(kv.Value, hostControl)) continue;

            var doc = _documentManager.OpenDocuments
                          .FirstOrDefault(d => d.ContentId == kv.Key);
            return doc?.FilePath is not null &&
                   string.Equals(doc.FilePath, filePath, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    // ── Debug toolbar + status bar ────────────────────────────────────────────

    /// <summary>Shows/hides the debug toolbar and enables/disables its buttons appropriately.</summary>
    private void UpdateDebugToolbarState(bool isActive, bool isPaused)
    {
        if (DebugToolBar is null) return;
        DebugToolBar.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        if (!isActive) return;

        if (BtnDbgContinue is not null)  BtnDbgContinue.IsEnabled  = isPaused;
        if (BtnDbgPause    is not null)  BtnDbgPause.IsEnabled     = !isPaused;
        if (BtnDbgStepOver is not null)  BtnDbgStepOver.IsEnabled  = isPaused;
        if (BtnDbgStepInto is not null)  BtnDbgStepInto.IsEnabled  = isPaused;
        if (BtnDbgStepOut  is not null)  BtnDbgStepOut.IsEnabled   = isPaused;
    }

    private void UpdateDbgStatusBar(DebugSessionPausedEvent? e)
    {
        if (e is null)
        {
            _statusBarManager?.UpdateDebugStatus(null, visible: false);
            return;
        }
        var fileName = System.IO.Path.GetFileName(e.FilePath);
        _statusBarManager?.UpdateDebugStatus($"Paused at {fileName}:{e.Line}", visible: true);
    }

    // ── Toolbar button click handlers (DebugToolBar in MainWindow.xaml) ────────
    // Debug menu is now fully dynamic (DebugMenuOrganizer) — these remain for the toolbar buttons.

    private void OnDebugContinue(object sender, RoutedEventArgs e)     => _ = _debuggerService?.ContinueAsync();
    private void OnDebugPause(object sender, RoutedEventArgs e)        => _ = _debuggerService?.PauseAsync();
    private void OnDebugStop(object sender, RoutedEventArgs e)         => _ = _debuggerService?.StopSessionAsync();
    private void OnDebugStepOver(object sender, RoutedEventArgs e)     => _ = _debuggerService?.StepOverAsync();
    private void OnDebugStepInto(object sender, RoutedEventArgs e)     => _ = _debuggerService?.StepIntoAsync();
    private void OnDebugStepOut(object sender, RoutedEventArgs e)      => _ = _debuggerService?.StepOutAsync();

    // ── Solution → breakpoint lifecycle ─────────────────────────────────────

    private void OnSolutionChangedForBreakpoints(object? sender, SolutionChangedEventArgs e)
    {
        if (_debuggerService is not DebuggerServiceImpl impl) return;

        var solutionPath = e.Kind switch
        {
            SolutionChangeKind.Opened => e.Solution?.FilePath,
            SolutionChangeKind.Closed => null,
            _ => null, // Modified — no breakpoint action needed
        };

        // Only react to open/close transitions.
        if (e.Kind is SolutionChangeKind.Opened or SolutionChangeKind.Closed)
            impl.OnSolutionChanged(solutionPath);
    }

    // ── Shutdown ──────────────────────────────────────────────────────────────

    /// <summary>Unsubscribes debug event subscriptions. Called from ShutdownPluginSystemAsync.</summary>
    private void ShutdownDebugIntegration()
    {
        _solutionManager.SolutionChanged -= OnSolutionChangedForBreakpoints;

        if (_debugSubs is not null)
            foreach (var s in _debugSubs) s.Dispose();
        _debugSubs = null;

        if (_bpChangedHandler is not null && _debuggerService is not null)
            _debuggerService.BreakpointsChanged -= _bpChangedHandler;
        _bpChangedHandler  = null;
        _bpSourceAdapter   = null;
        _debugHoverAdapter = null;
    }

    // ── BreakpointSourceAdapter ───────────────────────────────────────────────

    /// <summary>
    /// Bridges the CodeEditor's IBreakpointSource to IDebuggerService.
    /// Implements <see cref="IBreakpointSource"/> without a compile-time dep on Core.Debugger.
    /// </summary>
    private sealed class BreakpointSourceAdapter : IBreakpointSource
    {
        private readonly WpfHexEditor.SDK.Contracts.Services.IDebuggerService _svc;

        public BreakpointSourceAdapter(WpfHexEditor.SDK.Contracts.Services.IDebuggerService svc)
            => _svc = svc;

        public bool HasBreakpoint(string filePath, int line) =>
            _svc.Breakpoints.Any(b =>
                string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase)
                && b.Line == line);

        public void Toggle(string filePath, int line) =>
            _ = _svc.ToggleBreakpointAsync(filePath, line);

        public BreakpointInfo? GetBreakpoint(string filePath, int line)
        {
            var bp = _svc.Breakpoints.FirstOrDefault(b =>
                string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase)
                && b.Line == line);
            return bp is null ? null : new BreakpointInfo(bp.Condition, bp.IsEnabled);
        }

        public void SetCondition(string filePath, int line, string? condition)
        {
            var bp = _svc.Breakpoints.FirstOrDefault(b =>
                string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase)
                && b.Line == line);
            if (bp is null) return;
            _ = _svc.UpdateBreakpointAsync(filePath, line, condition, bp.IsEnabled);
        }

        public void SetEnabled(string filePath, int line, bool enabled)
        {
            var bp = _svc.Breakpoints.FirstOrDefault(b =>
                string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase)
                && b.Line == line);
            if (bp is null) return;
            _ = _svc.UpdateBreakpointAsync(filePath, line, bp.Condition, enabled);
        }

        public void Delete(string filePath, int line) =>
            _ = _svc.DeleteBreakpointAsync(filePath, line);

        public IReadOnlyList<int> GetBreakpointLines(string filePath) =>
            _svc.Breakpoints
                .Where(b => string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                .Select(b => b.Line)
                .ToList();
    }

    // ── DebugHoverAdapter ─────────────────────────────────────────────────────

    /// <summary>
    /// Bridges the CodeEditor's IDebugHoverProvider to IDebuggerService.
    /// Evaluates hover expressions via DAP — no compile-time dep from CodeEditor to Core.Debugger.
    /// </summary>
    private sealed class DebugHoverAdapter : IDebugHoverProvider
    {
        private readonly WpfHexEditor.SDK.Contracts.Services.IDebuggerService _svc;
        private volatile bool _isPaused;

        public DebugHoverAdapter(WpfHexEditor.SDK.Contracts.Services.IDebuggerService svc)
            => _svc = svc;

        public bool IsSessionPaused => _isPaused;

        public void SetPaused(bool paused) => _isPaused = paused;

        public async Task<string?> EvaluateTokenAsync(string token, CancellationToken ct = default)
        {
            if (!_isPaused || string.IsNullOrWhiteSpace(token)) return null;
            try
            {
                return await _svc.EvaluateAsync(token);
            }
            catch
            {
                return null;
            }
        }
    }
}
