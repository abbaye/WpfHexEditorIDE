// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: DebuggerPlugin.cs
// Description:
//     Entry point for the Integrated Debugger plugin.
//     Registers 5 debug panels, contributes Debug menu, and
//     subscribes to IDE events to keep panels up-to-date.
// Architecture:
//     Plugin (isolated) → IDebuggerService (SDK) → DebuggerServiceImpl (App).
//     All UI marshalled to dispatcher thread.
// ==========================================================

using System;
using System.Linq;
using System.Windows;
using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Plugins.Debugger.Commands;
using WpfHexEditor.Plugins.Debugger.Dialogs;
using WpfHexEditor.Plugins.Debugger.Panels;
using WpfHexEditor.Plugins.Debugger.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.Debugger.Properties;
using WpfHexEditor.Plugins.Debugger.Visualizers;

namespace WpfHexEditor.Plugins.Debugger;

public sealed class DebuggerPlugin : IWpfHexEditorPluginV2
{
    // -- IWpfHexEditorPlugin identity -----------------------------------------
    public string             Id           => "WpfHexEditor.Plugins.Debugger";
    public string             Name         => "Integrated Debugger";
    public Version            Version      => new(1, 0, 0);
    public PluginCapabilities Capabilities => new() { RegisterMenus = true, WriteOutput = true, RegisterTerminalCommands = true };

    // -- IWpfHexEditorPluginV2 -------------------------------------------------
    public bool SupportsHotReload => false;
    public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;

    // -- State -----------------------------------------------------------------
    private IIDEHostContext?  _context;
    private IDebuggerService? _debugger;
    private readonly List<IDisposable> _subs = [];

    // Panel view-models
    private BreakpointExplorerViewModel?        _bpVm;
    private CallStackPanelViewModel?            _csVm;
    private LocalsPanelViewModel?               _locVm;
    private AutosPanelViewModel?                _autosVm;
    private ExceptionSettingsPanelViewModel?    _exceptionVm;
    private ImmediateWindowViewModel?           _immediateVm;
    private ModulesPanelViewModel?              _modulesVm;
    private TasksPanelViewModel?                _tasksVm;
    private DisassemblyPanelViewModel?          _disassemblyVm;
    private MemoryWindowViewModel?              _memoryVm;
    private RegistersPanelViewModel?            _registersVm;
    private ParallelWatchViewModel?             _parallelWatchVm;
    private WatchesPanelViewModel?        _watchVm;
    private DebugConsolePanelViewModel?   _consoleVm;
    private DebugSessionManagerViewModel? _sessionMgrVm;
    private ThreadsPanelViewModel?        _threadsVm;
    private ParallelStacksPanelViewModel? _parallelStacksVm;

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context  = context;
        _debugger = context.Debugger;

        if (_debugger is null)
        {
            context.Output.Write("Debugger", "IDebuggerService not available — Debugger plugin disabled.");
            return Task.CompletedTask;
        }

        // Create view-models
        _bpVm             = new BreakpointExplorerViewModel(_debugger, context);
        _csVm             = new CallStackPanelViewModel(_debugger, context);
        _locVm            = new LocalsPanelViewModel(_debugger);
        _autosVm          = new AutosPanelViewModel(_debugger);
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

        // Wire IDE events — store tokens for disposal in ShutdownAsync
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionPausedEvent>(OnPaused));
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionStartedEvent>(OnSessionStarted));
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionEndedEvent>(OnEnded));
        _subs.Add(context.IDEEvents.Subscribe<DebugOutputReceivedEvent>(OnOutput));
        _subs.Add(context.IDEEvents.Subscribe<OpenBreakpointSettingsRequestedEvent>(OnOpenBpSettings));
        _subs.Add(context.IDEEvents.Subscribe<AttachToProcessRequestedEvent>(OnAttachToProcessRequested));
        _subs.Add(context.IDEEvents.Subscribe<OpenTracepointDialogRequestedEvent>(OnOpenTracepointDialog));

        // Register panels
        var ui = context.UIRegistry;

        var bpPanel = new BreakpointExplorerPanel { DataContext = _bpVm };
        bpPanel.UIFactory = context.UIFactory;
        ui.RegisterPanel("panel-dbg-breakpoints", bpPanel, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_BreakpointsPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-callstack", new CallStackPanel { DataContext = _csVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_CallStackPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-locals", new LocalsPanel { DataContext = _locVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_LocalsPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-autos", new AutosPanel { DataContext = _autosVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_AutosPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-exceptions", new ExceptionSettingsPanel { DataContext = _exceptionVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ExceptionsPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-immediate", new ImmediateWindowPanel { DataContext = _immediateVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ImmediatePanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-modules", new ModulesPanel { DataContext = _modulesVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ModulesPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-tasks", new TasksPanel { DataContext = _tasksVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_TasksPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-disassembly", new DisassemblyPanel { DataContext = _disassemblyVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_DisassemblyPanelTitle, DefaultDockSide = "Center", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-memory", new MemoryWindowPanel { DataContext = _memoryVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_MemoryPanelTitle, DefaultDockSide = "Center", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-registers", new RegistersPanel { DataContext = _registersVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_RegistersPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-parallel-watch", new ParallelWatchPanel { DataContext = _parallelWatchVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ParallelWatchPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-watch", new WatchesPanel { DataContext = _watchVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_WatchPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        var consolePanel = new DebugConsolePanel { DataContext = _consoleVm };
        consolePanel.SetSessionManager(_sessionMgrVm);
        ui.RegisterPanel("panel-dbg-console", consolePanel, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ConsolePanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-threads", new ThreadsPanel { DataContext = _threadsVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ThreadsPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-parallel-stacks", new ParallelStacksPanel { DataContext = _parallelStacksVm }, Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ParallelStacksPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        // Launch configuration editor panel
        ui.RegisterPanel("panel-dbg-launch-config",
            new Panels.LaunchConfigEditorPanel(context.DocumentHost, _debugger), Id,
            new PanelDescriptor { Title = DebuggerResources.Debugger_LaunchConfigTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        // Contribute Debug menu — one RegisterMenuItem call per item (no Children support in SDK)
        // Icons included for Command Palette; DebugMenuOrganizer deduplicates against built-in entries.
        ui.RegisterMenuItem($"{Id}.Menu.Continue",   Id, new MenuItemDescriptor { Header = DebuggerResources.Debugger_Menu_Continue, ParentPath = "Debug", GestureText = "F5",            Group = "Session",     IconGlyph = "\uE768", Command = new RelayCommand(_ => _ = _debugger?.ContinueAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.StepOver",   Id, new MenuItemDescriptor { Header = DebuggerResources.Debugger_Menu_StepOver,  ParentPath = "Debug", GestureText = "F10",           Group = "Stepping",    IconGlyph = "\uE7EE", Command = new RelayCommand(_ => _ = _debugger?.StepOverAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.StepInto",   Id, new MenuItemDescriptor { Header = DebuggerResources.Debugger_Menu_StepInto,  ParentPath = "Debug", GestureText = "F11",           Group = "Stepping",    IconGlyph = "\uE70D", Command = new RelayCommand(_ => _ = _debugger?.StepIntoAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.StepOut",    Id, new MenuItemDescriptor { Header = "Step Ou_t",              ParentPath = "Debug", GestureText = "Shift+F11",     Group = "Stepping",    IconGlyph = "\uE70E", Command = new RelayCommand(_ => _ = _debugger?.StepOutAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.RunToCursor",      Id, new MenuItemDescriptor { Header = "_Run to Cursor",          ParentPath = "Debug", GestureText = "Ctrl+F10",       Group = "Stepping",    IconGlyph = "\uE7FC", Command = new RelayCommand(_ => context.IDEEvents.Publish(new WpfHexEditor.Core.Events.IDEEvents.RunToCursorRequestedEvent())) });
        ui.RegisterMenuItem($"{Id}.Menu.SetNextStatement", Id, new MenuItemDescriptor { Header = "Set _Next Statement",    ParentPath = "Debug", GestureText = "Ctrl+Shift+F10", Group = "Stepping",    IconGlyph = "\uE72C", Command = new RelayCommand(_ => context.IDEEvents.Publish(new WpfHexEditor.Core.Events.IDEEvents.SetNextStatementRequestedEvent())) });
        ui.RegisterMenuItem($"{Id}.Menu.ClearBps",       Id, new MenuItemDescriptor { Header = "Delete _All Breakpoints", ParentPath = "Debug", GestureText = "Ctrl+Shift+F9", Group = "Breakpoints", IconGlyph = "\uE74D", Command = new RelayCommand(_ => _ = _debugger?.ClearAllBreakpointsAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.AddTracepoint",  Id, new MenuItemDescriptor { Header = "Add _Tracepoint\u2026",  ParentPath = "Debug", Group = "Breakpoints", IconGlyph = "\uE7C1", Command = new RelayCommand(_ => context.IDEEvents.Publish(new WpfHexEditor.Core.Events.IDEEvents.AddTracepointRequestedEvent())) });
        ui.RegisterMenuItem($"{Id}.Menu.AttachProc", Id, new MenuItemDescriptor { Header = "_Attach to Process\u2026",    ParentPath = "Debug", GestureText = "Ctrl+Alt+P",    Group = "Session",     IconGlyph = "\uE77B", Command = new RelayCommand(_ =>
        {
            if (_debugger is null) return;
            var pid = Dialogs.AttachToProcessDialog.Show(Application.Current.MainWindow);
            if (pid > 0) _ = _debugger.AttachAsync(pid);
        }) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowBps",    Id, new MenuItemDescriptor { Header = "Show _Breakpoints",      ParentPath = "Debug", Group = "Panels", IconGlyph = "\uEBE8", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-breakpoints")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowCs",     Id, new MenuItemDescriptor { Header = "Show _Call Stack",       ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE81E", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-callstack")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowLocals", Id, new MenuItemDescriptor { Header = "Show _Locals",           ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE943", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-locals")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowAutos",  Id, new MenuItemDescriptor { Header = "Show _Autos",            ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE943", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-autos")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowWatch",      Id, new MenuItemDescriptor { Header = "Show _Watch",                ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE7B3", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-watch")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowExceptions", Id, new MenuItemDescriptor { Header = "Show _Exception Settings",   ParentPath = "Debug", Group = "Panels", IconGlyph = "\uEA39", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-exceptions")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowThreads", Id, new MenuItemDescriptor { Header = "Show _Threads",          ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE734", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-threads")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowParallelStacks", Id, new MenuItemDescriptor { Header = "Show _Parallel Stacks",  ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE81E", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-parallel-stacks")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowImmediate",      Id, new MenuItemDescriptor { Header = "Show I_mmediate Window", ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE756", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-immediate")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowModules", Id, new MenuItemDescriptor { Header = "Show _Modules",     ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE8F4", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-modules")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowTasks",       Id, new MenuItemDescriptor { Header = "Show _Tasks",       ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE945", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-tasks")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowDisassembly", Id, new MenuItemDescriptor { Header = "Show Disasse_mbly", ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE8F2", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-disassembly")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowMemory",    Id, new MenuItemDescriptor { Header = "Show _Memory",    ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE190", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-memory")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowRegisters", Id, new MenuItemDescriptor { Header = "Show _Registers", ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE8D0", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-registers")) });

        ui.RegisterMenuItem($"{Id}.Menu.ShowParallelWatch", Id, new MenuItemDescriptor { Header = "Show Parallel _Watch", ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-parallel-watch")) });

        // Register built-in debug visualizers.
        context.DebugVisualizers?.Register(new CollectionVisualizer());
        context.DebugVisualizers?.Register(new StringVisualizer());
        context.DebugVisualizers?.Register(new DateTimeVisualizer());

        // Register terminal commands.
        context.Terminal.RegisterCommand(new DebugBpListCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugBpSetCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugBpClearCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugLocalsCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugWatchCommand());

        return Task.CompletedTask;
    }

    private void OnPaused(DebugSessionPausedEvent e)
    {
        _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            if (_debugger is null) return;
            var frames = await _debugger.GetCallStackAsync();
            _csVm?.SetFrames(frames);

            if (frames.Count > 0)
            {
                var locals = await _debugger.GetVariablesAsync(0);
                _locVm?.SetVariables(locals);
                _autosVm?.SetVariables(locals);
                await _watchVm!.RefreshAsync(_debugger);
            }

            await _threadsVm!.RefreshAsync();
            await _parallelStacksVm!.RefreshAsync();
            await (_modulesVm?.RefreshAsync() ?? Task.CompletedTask);
            await (_tasksVm?.RefreshAsync()   ?? Task.CompletedTask);

            await (_registersVm?.RefreshAsync()     ?? Task.CompletedTask);
            await (_parallelWatchVm?.RefreshAsync() ?? Task.CompletedTask);

            // Refresh disassembly at current IP if the panel VM is initialized
            if (_disassemblyVm is not null && frames.Count > 0)
            {
                var ipRef = frames[0].InstructionPointerReference;
                if (!string.IsNullOrEmpty(ipRef))
                    await _disassemblyVm.RefreshAtCurrentIPAsync(ipRef);
            }
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
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            _sessionMgrVm?.AddSession(e.SessionId, System.IO.Path.GetFileName(e.ProjectPath), "csharp"));
    }

    private void OnEnded(DebugSessionEndedEvent e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
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
            var pid = Dialogs.AttachToProcessDialog.Show(Application.Current.MainWindow);
            if (pid > 0) _ = _debugger.AttachAsync(pid);
        });
    }

    private void OnOutput(DebugOutputReceivedEvent e) =>
        _consoleVm?.Append(e.Category, e.Output);

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var sub in _subs) sub.Dispose();
        _subs.Clear();

        _context?.Terminal.UnregisterCommand("debug-bp-list");
        _context?.Terminal.UnregisterCommand("debug-bp-set");
        _context?.Terminal.UnregisterCommand("debug-bp-clear");
        _context?.Terminal.UnregisterCommand("debug-locals");
        _context?.Terminal.UnregisterCommand("debug-watch");

        return Task.CompletedTask;
    }
}
