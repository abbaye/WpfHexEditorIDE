// ==========================================================
// Project: WpfHexEditor.App
// File: Debug/DebugModule.cs
// Description:
//     Internal module that wires the Integrated Debugger into the IDE.
//     Owns 17 debug ViewModels and exposes their corresponding panels via
//     GetPanel(contentId) for the lazy MainWindow.BuildContentForItem switch.
//     Subscribes to IDE events (paused / output / open BP settings / …),
//     registers terminal commands, and registers the built-in debug
//     visualizers. The Debug menu items themselves live in
//     MainWindow.DebugMenu.cs (DebugMenuOrganizer) — this module no longer
//     touches IUIRegistry/RegisterPanel/RegisterMenuItem.
//
//     Replaces the former WpfHexEditor.Plugins.Debugger plugin (ADR-010).
// Architecture:
//     App layer — consumes the SDK contract types it actually needs
//     (IDebuggerService, IIDEEventBus, IDocumentHostService, …) but does
//     NOT register UI elements through the SDK plugin path. The SDK is a
//     communication contract for plugins; core modules dock their panels
//     through MainWindow's BuildContentForItem like SolutionExplorer does.
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.App.Debug.Commands;
using WpfHexEditor.App.Debug.Dialogs;
using WpfHexEditor.App.Debug.Panels;
using WpfHexEditor.App.Debug.ViewModels;
using WpfHexEditor.App.Debug.Properties;
using WpfHexEditor.App.Debug.Visualizers;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Debug;

internal sealed class DebugModule
{
    // ── Panel ContentIds (consumed by MainWindow.BuildContentForItem) ─────
    public const string ContentIdBreakpoints     = "panel-dbg-breakpoints";
    public const string ContentIdCallStack       = "panel-dbg-callstack";
    public const string ContentIdLocals          = "panel-dbg-locals";
    public const string ContentIdAutos           = "panel-dbg-autos";
    public const string ContentIdExceptions      = "panel-dbg-exceptions";
    public const string ContentIdImmediate       = "panel-dbg-immediate";
    public const string ContentIdModules         = "panel-dbg-modules";
    public const string ContentIdTasks           = "panel-dbg-tasks";
    public const string ContentIdDisassembly     = "panel-dbg-disassembly";
    public const string ContentIdMemory          = "panel-dbg-memory";
    public const string ContentIdRegisters       = "panel-dbg-registers";
    public const string ContentIdParallelWatch   = "panel-dbg-parallel-watch";
    public const string ContentIdWatch           = "panel-dbg-watch";
    public const string ContentIdConsole         = "panel-dbg-console";
    public const string ContentIdThreads         = "panel-dbg-threads";
    public const string ContentIdParallelStacks  = "panel-dbg-parallel-stacks";
    public const string ContentIdLaunchConfig    = "panel-dbg-launch-config";

    private IIDEHostContext?  _context;
    private IDebuggerService? _debugger;
    private readonly List<IDisposable> _subs = [];

    private BreakpointExplorerViewModel?     _bpVm;
    private CallStackPanelViewModel?         _csVm;
    private LocalsPanelViewModel?            _locVm;
    private AutosPanelViewModel?             _autosVm;
    private ExceptionSettingsPanelViewModel? _exceptionVm;
    private ImmediateWindowViewModel?        _immediateVm;
    private ModulesPanelViewModel?           _modulesVm;
    private TasksPanelViewModel?             _tasksVm;
    private DisassemblyPanelViewModel?       _disassemblyVm;
    private MemoryWindowViewModel?           _memoryVm;
    private RegistersPanelViewModel?         _registersVm;
    private ParallelWatchViewModel?          _parallelWatchVm;
    private WatchesPanelViewModel?           _watchVm;
    private DebugConsolePanelViewModel?      _consoleVm;
    private DebugSessionManagerViewModel?    _sessionMgrVm;
    private ThreadsPanelViewModel?           _threadsVm;
    private ParallelStacksPanelViewModel?    _parallelStacksVm;

    // Panels are cached as singletons (lazy-built on first GetPanel(contentId)).
    // MainWindow.CreateContentForItem caches the same instance in _displayContent,
    // and DockControl's internal content cache reuses it across undock/redock by
    // detaching the panel from its previous parent before re-attaching — the
    // same way Solution Explorer (the prototypical core panel) survives
    // undock/redock with a single shared instance.
    private BreakpointExplorerPanel?     _bpPanel;
    private CallStackPanel?              _csPanel;
    private LocalsPanel?                 _locPanel;
    private AutosPanel?                  _autosPanel;
    private ExceptionSettingsPanel?      _exceptionPanel;
    private ImmediateWindowPanel?        _immediatePanel;
    private ModulesPanel?                _modulesPanel;
    private TasksPanel?                  _tasksPanel;
    private DisassemblyPanel?            _disassemblyPanel;
    private MemoryWindowPanel?           _memoryPanel;
    private RegistersPanel?              _registersPanel;
    private ParallelWatchPanel?          _parallelWatchPanel;
    private WatchesPanel?                _watchPanel;
    private DebugConsolePanel?           _consolePanel;
    private ThreadsPanel?                _threadsPanel;
    private ParallelStacksPanel?         _parallelStacksPanel;
    private LaunchConfigEditorPanel?     _launchConfigPanel;

    public bool IsEnabled => _debugger is not null;

    /// <summary>
    /// Light-weight initialisation. Wires IDE event subscriptions, terminal
    /// commands, and debug visualizers. Does NOT instantiate any panel —
    /// panels are built lazily by GetPanel(contentId) when MainWindow's
    /// docking layout asks for the corresponding ContentId.
    /// </summary>
    public void Initialize(IIDEHostContext context)
    {
        _context  = context;
        _debugger = context.Debugger;

        if (_debugger is null)
        {
            context.Output.Write("Debugger", "IDebuggerService not available — DebugModule disabled.");
            return;
        }

        // ViewModels are cheap (no XAML); allocate up-front so event handlers
        // can push state into them even before any panel is materialised.
        _bpVm             = new BreakpointExplorerViewModel(_debugger, context);
        _csVm             = new CallStackPanelViewModel(_debugger, context);
        _locVm            = new LocalsPanelViewModel(_debugger, context.IDEEvents);
        _autosVm          = new AutosPanelViewModel(_debugger, context.IDEEvents);
        _exceptionVm      = new ExceptionSettingsPanelViewModel(_debugger);
        _immediateVm      = new ImmediateWindowViewModel(_debugger);
        _modulesVm        = new ModulesPanelViewModel(_debugger);
        _tasksVm          = new TasksPanelViewModel(_debugger);
        _disassemblyVm    = new DisassemblyPanelViewModel(_debugger);
        _memoryVm         = new MemoryWindowViewModel(_debugger);
        _registersVm      = new RegistersPanelViewModel(_debugger);
        _parallelWatchVm  = new ParallelWatchViewModel(_debugger);
        _watchVm          = new WatchesPanelViewModel(_debugger);
        _consoleVm        = new DebugConsolePanelViewModel();
        _sessionMgrVm     = new DebugSessionManagerViewModel();
        _threadsVm        = new ThreadsPanelViewModel(_debugger);
        _parallelStacksVm = new ParallelStacksPanelViewModel(_debugger);

        _subs.Add(context.IDEEvents.Subscribe<DebugSessionPausedEvent>(OnPaused));
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionStartedEvent>(OnSessionStarted));
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionEndedEvent>(OnEnded));
        _subs.Add(context.IDEEvents.Subscribe<DebugOutputReceivedEvent>(OnOutput));
        _subs.Add(context.IDEEvents.Subscribe<OpenBreakpointSettingsRequestedEvent>(OnOpenBpSettings));
        _subs.Add(context.IDEEvents.Subscribe<AttachToProcessRequestedEvent>(OnAttachToProcessRequested));
        _subs.Add(context.IDEEvents.Subscribe<OpenTracepointDialogRequestedEvent>(OnOpenTracepointDialog));
        _subs.Add(context.IDEEvents.Subscribe<AddWatchRequestedEvent>(e =>
            Application.Current?.Dispatcher.Invoke(() => _watchVm?.AddWatch(e.Expression))));
        _subs.Add(context.IDEEvents.Subscribe<GoToSourceRequestedEvent>(e =>
            Application.Current?.Dispatcher.Invoke(() => context.DocumentHost.OpenDocument(e.FilePath))));

        // Built-in debug visualizers — registered on the SDK extensibility
        // registry so plugins can also add visualizers.
        context.DebugVisualizers?.Register(new CollectionVisualizer());
        context.DebugVisualizers?.Register(new StringVisualizer());
        context.DebugVisualizers?.Register(new DateTimeVisualizer());

        // Terminal commands (consumed by user via the integrated terminal).
        context.Terminal.RegisterCommand(new DebugBpListCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugBpSetCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugBpClearCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugLocalsCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugWatchCommand());
    }

    /// <summary>
    /// Builds the panel shell for a Debug ContentId immediately, with no DataContext.
    /// Safe to call before Initialize — returns null only for panels that require
    /// constructor args (ContentIdLaunchConfig). DataContext is wired by
    /// WireDataContexts() after Initialize completes.
    /// Matches the Solution Explorer pattern: panel construction is decoupled from
    /// service availability so the docking layout can cache the real instance from
    /// the first BuildContentForItem call.
    /// </summary>
    public UIElement? GetOrBuildPanelShell(string contentId)
    {
        return contentId switch
        {
            ContentIdBreakpoints    => _bpPanel    ??= new BreakpointExplorerPanel(),
            ContentIdCallStack      => _csPanel    ??= new CallStackPanel(),
            ContentIdLocals         => _locPanel   ??= new LocalsPanel(),
            ContentIdAutos          => _autosPanel ??= new AutosPanel(),
            ContentIdExceptions     => _exceptionPanel    ??= new ExceptionSettingsPanel(),
            ContentIdImmediate      => _immediatePanel    ??= new ImmediateWindowPanel(),
            ContentIdModules        => _modulesPanel      ??= new ModulesPanel(),
            ContentIdTasks          => _tasksPanel        ??= new TasksPanel(),
            ContentIdDisassembly    => _disassemblyPanel  ??= new DisassemblyPanel(),
            ContentIdMemory         => _memoryPanel       ??= new MemoryWindowPanel(),
            ContentIdRegisters      => _registersPanel    ??= new RegistersPanel(),
            ContentIdParallelWatch  => _parallelWatchPanel ??= new ParallelWatchPanel(),
            ContentIdWatch          => _watchPanel        ??= new WatchesPanel(),
            ContentIdConsole        => _consolePanel      ??= new DebugConsolePanel(),
            ContentIdThreads        => _threadsPanel      ??= new ThreadsPanel(),
            ContentIdParallelStacks => _parallelStacksPanel ??= new ParallelStacksPanel(),
            // LaunchConfigEditorPanel requires constructor args (IDocumentHostService, IDebuggerService).
            // It falls back to the deferred path in MainWindow via GetPanel after Initialize.
            _ => null
        };
    }

    /// <summary>
    /// Builds panels that require constructor args (LaunchConfigEditorPanel).
    /// Called from MainWindow after Initialize() for ContentIds not handled by GetOrBuildPanelShell.
    /// </summary>
    public UIElement? GetPanel(string contentId)
    {
        if (_debugger is null || _context is null) return null;
        return contentId == ContentIdLaunchConfig
            ? _launchConfigPanel ??= new LaunchConfigEditorPanel(_context.DocumentHost, _debugger)
            : null;
    }

    /// <summary>
    /// Wires DataContext on all pre-built panel shells. If MainWindow built shells before
    /// Initialize() was called (layout restore race), those shells are adopted here via
    /// the <paramref name="externalShells"/> dictionary keyed by ContentId.
    /// Follows the _pendingTerminalPanel pattern from MainWindow.PluginSystem.cs.
    /// </summary>
    public void WireDataContexts(IReadOnlyDictionary<string, System.Windows.FrameworkElement>? externalShells = null)
    {
        if (_debugger is null || _context is null) return;

        // Adopt any panel shells pre-built by MainWindow before this module was initialized.
        if (externalShells is not null)
        {
            if (externalShells.TryGetValue(ContentIdBreakpoints,   out var s) && s is BreakpointExplorerPanel bp)     _bpPanel    ??= bp;
            if (externalShells.TryGetValue(ContentIdCallStack,     out s)     && s is CallStackPanel cs)               _csPanel    ??= cs;
            if (externalShells.TryGetValue(ContentIdLocals,        out s)     && s is LocalsPanel loc)                 _locPanel   ??= loc;
            if (externalShells.TryGetValue(ContentIdAutos,         out s)     && s is AutosPanel autos)                _autosPanel ??= autos;
            if (externalShells.TryGetValue(ContentIdExceptions,    out s)     && s is ExceptionSettingsPanel exc)      _exceptionPanel    ??= exc;
            if (externalShells.TryGetValue(ContentIdImmediate,     out s)     && s is ImmediateWindowPanel imm)        _immediatePanel    ??= imm;
            if (externalShells.TryGetValue(ContentIdModules,       out s)     && s is ModulesPanel mod)                _modulesPanel      ??= mod;
            if (externalShells.TryGetValue(ContentIdTasks,         out s)     && s is TasksPanel tsk)                  _tasksPanel        ??= tsk;
            if (externalShells.TryGetValue(ContentIdDisassembly,   out s)     && s is DisassemblyPanel dis)            _disassemblyPanel  ??= dis;
            if (externalShells.TryGetValue(ContentIdMemory,        out s)     && s is MemoryWindowPanel mem)           _memoryPanel       ??= mem;
            if (externalShells.TryGetValue(ContentIdRegisters,     out s)     && s is RegistersPanel reg)              _registersPanel    ??= reg;
            if (externalShells.TryGetValue(ContentIdParallelWatch, out s)     && s is ParallelWatchPanel pw)           _parallelWatchPanel ??= pw;
            if (externalShells.TryGetValue(ContentIdWatch,         out s)     && s is WatchesPanel wch)                _watchPanel        ??= wch;
            if (externalShells.TryGetValue(ContentIdConsole,       out s)     && s is DebugConsolePanel con)           _consolePanel      ??= con;
            if (externalShells.TryGetValue(ContentIdThreads,       out s)     && s is ThreadsPanel thr)                _threadsPanel      ??= thr;
            if (externalShells.TryGetValue(ContentIdParallelStacks,out s)     && s is ParallelStacksPanel pst)         _parallelStacksPanel ??= pst;
        }

        if (_bpPanel    is not null) { _bpPanel.DataContext    = _bpVm;    _bpPanel.UIFactory = _context.UIFactory; }
        if (_csPanel    is not null)   _csPanel.DataContext    = _csVm;
        if (_locPanel   is not null)   _locPanel.DataContext   = _locVm;
        if (_autosPanel is not null)   _autosPanel.DataContext = _autosVm;
        if (_exceptionPanel   is not null) _exceptionPanel.DataContext   = _exceptionVm;
        if (_immediatePanel   is not null) _immediatePanel.DataContext   = _immediateVm;
        if (_modulesPanel     is not null) _modulesPanel.DataContext     = _modulesVm;
        if (_tasksPanel       is not null) _tasksPanel.DataContext       = _tasksVm;
        if (_disassemblyPanel is not null) _disassemblyPanel.DataContext = _disassemblyVm;
        if (_memoryPanel      is not null) _memoryPanel.DataContext      = _memoryVm;
        if (_registersPanel   is not null) _registersPanel.DataContext   = _registersVm;
        if (_parallelWatchPanel is not null) _parallelWatchPanel.DataContext = _parallelWatchVm;
        if (_watchPanel       is not null) _watchPanel.DataContext       = _watchVm;
        if (_consolePanel     is not null) { _consolePanel.DataContext = _consoleVm; _consolePanel.SetSessionManager(_sessionMgrVm); }
        if (_threadsPanel     is not null) _threadsPanel.DataContext     = _threadsVm;
        if (_parallelStacksPanel is not null) _parallelStacksPanel.DataContext = _parallelStacksVm;
        // _launchConfigPanel: built on-demand in GetPanel (requires constructor args).
    }

    // ── IDE event handlers ────────────────────────────────────────────────

    private void OnPaused(DebugSessionPausedEvent e)
    {
        _ = Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            if (_debugger is null) return;
            var frames = await _debugger.GetCallStackAsync();
            _csVm?.SetFrames(frames);

            if (frames.Count == 0) return;

            var locals = await _debugger.GetVariablesAsync(0);
            _locVm?.SetVariables(locals);
            _autosVm?.SetVariables(locals);

            // Independent refreshes — fan out to minimize pause-to-UI latency.
            await Task.WhenAll(
                _watchVm!.RefreshAsync(_debugger),
                _threadsVm!.RefreshAsync(),
                _parallelStacksVm!.RefreshAsync(),
                _modulesVm?.RefreshAsync()       ?? Task.CompletedTask,
                _tasksVm?.RefreshAsync()         ?? Task.CompletedTask,
                _registersVm?.RefreshAsync()     ?? Task.CompletedTask,
                _parallelWatchVm?.RefreshAsync() ?? Task.CompletedTask);

            var ipRef = frames[0].InstructionPointerReference;
            if (_disassemblyVm is not null && !string.IsNullOrEmpty(ipRef))
                await _disassemblyVm.RefreshAtCurrentIPAsync(ipRef);
        });
    }

    private void OnOpenBpSettings(OpenBreakpointSettingsRequestedEvent e)
    {
        if (_debugger is null) return;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            var bp = _debugger.Breakpoints.FirstOrDefault(
                b => string.Equals(b.FilePath, e.FilePath, StringComparison.OrdinalIgnoreCase)
                     && b.Line == e.Line);
            if (bp is null) return;

            var loc = new BreakpointLocation
            {
                FilePath          = bp.FilePath,
                Line              = bp.Line,
                Condition         = bp.Condition ?? string.Empty,
                IsEnabled         = bp.IsEnabled,
                ConditionKind     = bp.ConditionKind,
                ConditionMode     = bp.ConditionMode,
                HitCountOp        = bp.HitCountOp,
                HitCountTarget    = bp.HitCountTarget,
                FilterExpr        = bp.FilterExpr,
                HasAction         = bp.HasAction,
                LogMessage        = bp.LogMessage,
                ContinueExecution = bp.ContinueExecution,
                DisableOnceHit    = bp.DisableOnceHit,
                DependsOnBpKey    = bp.DependsOnBpKey,
            };

            var allLocs = _debugger.Breakpoints.Select(b => new BreakpointLocation
            {
                FilePath  = b.FilePath,
                Line      = b.Line,
                Condition = b.Condition ?? string.Empty,
                IsEnabled = b.IsEnabled,
            }).ToList();

            var result = BreakpointConditionDialog.Show(Application.Current.MainWindow, loc, allLocs);
            if (result is not null)
                _ = _debugger.UpdateBreakpointSettingsAsync(e.FilePath, e.Line, result);
        });
    }

    private void OnSessionStarted(DebugSessionStartedEvent e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
            _sessionMgrVm?.AddSession(e.SessionId, Path.GetFileName(e.ProjectPath), "csharp"));
    }

    private void OnEnded(DebugSessionEndedEvent e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            _sessionMgrVm?.RemoveSession(e.SessionId);
            _csVm?.SetFrames([]);
            _locVm?.SetVariables([]);
            _autosVm?.SetVariables([]);
            _threadsVm?.Clear();
            _parallelStacksVm?.Clear();
            _modulesVm?.Clear();
            _tasksVm?.Clear();
            _disassemblyVm?.Clear();
            _memoryVm?.Clear();
            _registersVm?.Clear();
            _parallelWatchVm?.Clear();
        });
    }

    private void OnOpenTracepointDialog(OpenTracepointDialogRequestedEvent e)
    {
        if (_debugger is null) return;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            var logMessage = QuickTracepointDialog.Show(Application.Current.MainWindow, e.FilePath, e.Line);
            if (string.IsNullOrEmpty(logMessage)) return;

            _ = _debugger.ToggleBreakpointAsync(e.FilePath, e.Line);
            _ = _debugger.UpdateBreakpointSettingsAsync(e.FilePath, e.Line, new BreakpointSettings(
                ConditionKind:     BpConditionKind.None,
                ConditionExpr:     null,
                ConditionMode:     BpConditionMode.IsTrue,
                HitCountOp:        BpHitCountOp.Equal,
                HitCountTarget:    1,
                FilterExpr:        null,
                HasAction:         true,
                LogMessage:        logMessage,
                ContinueExecution: true,
                DisableOnceHit:    false,
                DependsOnBpKey:    null));
        });
    }

    private void OnAttachToProcessRequested(AttachToProcessRequestedEvent e)
    {
        if (_debugger is null) return;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var pid = AttachToProcessDialog.Show(Application.Current.MainWindow);
            if (pid > 0) _ = _debugger.AttachAsync(pid);
        });
    }

    private void OnOutput(DebugOutputReceivedEvent e) =>
        _consoleVm?.Append(e.Category, e.Output);

    public void Shutdown()
    {
        foreach (var sub in _subs) sub.Dispose();
        _subs.Clear();

        _context?.Terminal.UnregisterCommand("debug-bp-list");
        _context?.Terminal.UnregisterCommand("debug-bp-set");
        _context?.Terminal.UnregisterCommand("debug-bp-clear");
        _context?.Terminal.UnregisterCommand("debug-locals");
        _context?.Terminal.UnregisterCommand("debug-watch");
    }
}
