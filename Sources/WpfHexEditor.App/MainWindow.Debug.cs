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
using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.Editor.CodeEditor;
using WpfHexEditor.Events.IDEEvents;
using WpfHexEditor.Options;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // Shared breakpoint source adapter — injected into every CodeEditor gutter.
    private BreakpointSourceAdapter? _bpSourceAdapter;

    // IDisposable subscriptions for debug events (disposed on shutdown).
    private IDisposable? _debugPausedSub;
    private IDisposable? _debugResumedSub;
    private IDisposable? _debugEndedSub;

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Called from InitializePluginSystemAsync after _debuggerService is created.
    /// Wires the gutter adapter and subscribes to debug lifecycle events.
    /// </summary>
    private void InitDebugIntegration()
    {
        if (_debuggerService is null || _ideEventBus is null) return;

        _bpSourceAdapter = new BreakpointSourceAdapter(_debuggerService);

        // When breakpoints change, refresh all open CodeEditor gutters.
        _debuggerService.BreakpointsChanged += (_, _) =>
            Dispatcher.InvokeAsync(RefreshAllBreakpointGutters);

        // Pause → highlight execution line in matching CodeEditor.
        _debugPausedSub = _ideEventBus.Subscribe<DebugSessionPausedEvent>(e =>
            Dispatcher.InvokeAsync(() => OnDebugSessionPaused(e)));

        // Resume / End → clear execution lines everywhere.
        _debugResumedSub = _ideEventBus.Subscribe<DebugSessionResumedEvent>(_ =>
            Dispatcher.InvokeAsync(ClearAllExecutionLines));
        _debugEndedSub = _ideEventBus.Subscribe<DebugSessionEndedEvent>(_ =>
            Dispatcher.InvokeAsync(ClearAllExecutionLines));
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
            ce.InvalidateVisual();
        }
    }

    /// <summary>
    /// Wire the breakpoint adapter into a newly created CodeEditor control.
    /// Called by the content-creation pipeline so the gutter is ready immediately.
    /// </summary>
    internal void WireBreakpointSourceToEditor(WpfHexEditor.Editor.Core.IDocumentEditor editor)
    {
        if (_bpSourceAdapter is null) return;
        var ce = GetCodeEditorControl(editor);
        ce?.SetBreakpointSource(_bpSourceAdapter);
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

    /// <summary>Ctrl+Alt+P — Attach to process dialog.</summary>
    internal void OnAttachToProcess()
    {
        if (_debuggerService is null)
        {
            MessageBox.Show("Debugger service not available.", "Attach to Process",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Full AttachToProcessDialog is contributed by the Debugger plugin.
        // Fallback: prompt for PID via simple input.
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter the process ID (PID) to attach to:",
            "Attach to Process", "");

        if (int.TryParse(input, out var pid) && pid > 0)
            _ = _debuggerService.AttachAsync(pid);
    }

    // ── Launch helpers ────────────────────────────────────────────────────────

    private async Task LaunchDebugSessionAsync()
    {
        var startupProject = _solutionManager.CurrentSolution?.StartupProject;
        if (startupProject is null)
        {
            MessageBox.Show("No startup project is set. Please set a startup project before debugging.",
                "Start Debugging", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Derive output exe path using the active build configuration.
        var projDir   = Path.GetDirectoryName(startupProject.ProjectFilePath) ?? ".";
        var projName  = Path.GetFileNameWithoutExtension(startupProject.ProjectFilePath);
        var cfgName   = _configManager?.ActiveConfiguration.Name ?? "Debug";
        var programPath = Path.Combine(projDir, "bin", cfgName, "net8.0-windows", projName + ".exe");

        // Fall back to DLL for non-WinExe projects (dotnet run style).
        if (!File.Exists(programPath))
            programPath = Path.Combine(projDir, "bin", cfgName, "net8.0-windows", projName + ".dll");

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

    // ── Shutdown ──────────────────────────────────────────────────────────────

    /// <summary>Unsubscribes debug event subscriptions. Called from ShutdownPluginSystemAsync.</summary>
    private void ShutdownDebugIntegration()
    {
        _debugPausedSub?.Dispose();
        _debugResumedSub?.Dispose();
        _debugEndedSub?.Dispose();
        _debugPausedSub  = null;
        _debugResumedSub = null;
        _debugEndedSub   = null;
        _bpSourceAdapter = null;
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
                && b.Line == line
                && b.IsEnabled);

        public void Toggle(string filePath, int line) =>
            _ = _svc.ToggleBreakpointAsync(filePath, line);
    }
}
