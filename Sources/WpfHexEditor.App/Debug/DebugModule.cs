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

    // Lazy-built panels (instantiated on first GetPanel(contentId) call from
    // MainWindow.BuildContentForItem — never up front).
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
    /// Returns the panel for a Debug ContentId, building it lazily on the
    /// first call. Used by MainWindow.BuildContentForItem when the docking
    /// layout asks for the panel's content. Returns null when called for an
    /// unknown ContentId or when the module is disabled (no IDebuggerService).
    /// </summary>
    public UIElement? GetPanel(string contentId)
    {
        if (_debugger is null || _context is null) return null;

        return contentId switch
        {
            ContentIdBreakpoints    => _bpPanel ??= BuildBreakpointsPanel(),
            ContentIdCallStack      => _csPanel ??= new CallStackPanel { DataContext = _csVm },
            ContentIdLocals         => _locPanel ??= new LocalsPanel { DataContext = _locVm },
            ContentIdAutos          => _autosPanel ??= new AutosPanel { DataContext = _autosVm },
            ContentIdExceptions     => _exceptionPanel ??= new ExceptionSettingsPanel { DataContext = _exceptionVm },
            ContentIdImmediate      => _immediatePanel ??= new ImmediateWindowPanel { DataContext = _immediateVm },
            ContentIdModules        => _modulesPanel ??= new ModulesPanel { DataContext = _modulesVm },
            ContentIdTasks          => _tasksPanel ??= new TasksPanel { DataContext = _tasksVm },
            ContentIdDisassembly    => _disassemblyPanel ??= new DisassemblyPanel { DataContext = _disassemblyVm },
            ContentIdMemory         => _memoryPanel ??= new MemoryWindowPanel { DataContext = _memoryVm },
            ContentIdRegisters      => _registersPanel ??= new RegistersPanel { DataContext = _registersVm },
            ContentIdParallelWatch  => _parallelWatchPanel ??= new ParallelWatchPanel { DataContext = _parallelWatchVm },
            ContentIdWatch          => _watchPanel ??= new WatchesPanel { DataContext = _watchVm },
            ContentIdConsole        => _consolePanel ??= BuildConsolePanel(),
            ContentIdThreads        => _threadsPanel ??= new ThreadsPanel { DataContext = _threadsVm },
            ContentIdParallelStacks => _parallelStacksPanel ??= new ParallelStacksPanel { DataContext = _parallelStacksVm },
            ContentIdLaunchConfig   => _launchConfigPanel ??= new LaunchConfigEditorPanel(_context.DocumentHost, _debugger),
            _ => null
        };
    }

    private BreakpointExplorerPanel BuildBreakpointsPanel()
    {
        var p = new BreakpointExplorerPanel { DataContext = _bpVm };
        p.UIFactory = _context!.UIFactory;
        return p;
    }

    private DebugConsolePanel BuildConsolePanel()
    {
        var p = new DebugConsolePanel { DataContext = _consoleVm };
        p.SetSessionManager(_sessionMgrVm);
        return p;
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
