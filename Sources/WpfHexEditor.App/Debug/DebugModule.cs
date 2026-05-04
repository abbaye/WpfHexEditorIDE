// ==========================================================
// Project: WpfHexEditor.App
// File: Debug/DebugModule.cs
// Description:
//     Internal module that wires the Integrated Debugger into the IDE.
//     Registers 17 debug panels, contributes Debug menu, exposes 5 terminal
//     commands, and subscribes to IDE events to keep panels up-to-date.
//
//     Replaces the former WpfHexEditor.Plugins.Debugger plugin (ADR-010).
//     Hosted directly by MainWindow.PluginSystem after DebuggerServiceImpl
//     is created and before the plugin loader runs.
// Architecture:
//     App layer — consumes IIDEHostContext like a plugin would, so the
//     module remains portable if it is ever re-extracted into a plugin.
// ==========================================================

using System;
using System.Collections.Generic;
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
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.App.Debug.Properties;
using WpfHexEditor.App.Debug.Visualizers;

namespace WpfHexEditor.App.Debug;

internal sealed class DebugModule
{
    private const string ModuleId = "WpfHexEditor.App.Debug";

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

    public void Initialize(IIDEHostContext context)
    {
        _context  = context;
        _debugger = context.Debugger;

        if (_debugger is null)
        {
            context.Output.Write("Debugger", "IDebuggerService not available — DebugModule disabled.");
            return;
        }

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

        _subs.Add(context.IDEEvents.Subscribe<DebugSessionPausedEvent>(OnPaused));
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionStartedEvent>(OnSessionStarted));
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionEndedEvent>(OnEnded));
        _subs.Add(context.IDEEvents.Subscribe<DebugOutputReceivedEvent>(OnOutput));
        _subs.Add(context.IDEEvents.Subscribe<OpenBreakpointSettingsRequestedEvent>(OnOpenBpSettings));
        _subs.Add(context.IDEEvents.Subscribe<AttachToProcessRequestedEvent>(OnAttachToProcessRequested));
        _subs.Add(context.IDEEvents.Subscribe<OpenTracepointDialogRequestedEvent>(OnOpenTracepointDialog));

        var ui = context.UIRegistry;

        // Wrap the 17 sequential RegisterPanel calls so the docking adapter
        // coalesces them into a single RebuildVisualTree at the end. Without
        // this, every RegisterPanel rebuilds the entire dock tree (incl. open
        // editors) — registering 17 panels in a row freezes the IDE for
        // several seconds at startup. Mirrors the SuspendRebuild() the plugin
        // host wraps around LoadAllAsync (MainWindow.PluginSystem:548).
        ui.BeginBulkRegistration();
        try
        {

        var bpPanel = new BreakpointExplorerPanel { DataContext = _bpVm };
        bpPanel.UIFactory = context.UIFactory;
        ui.RegisterPanel("panel-dbg-breakpoints", bpPanel, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_BreakpointsPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-callstack", new CallStackPanel { DataContext = _csVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_CallStackPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-locals", new LocalsPanel { DataContext = _locVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_LocalsPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-autos", new AutosPanel { DataContext = _autosVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_AutosPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-exceptions", new ExceptionSettingsPanel { DataContext = _exceptionVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ExceptionsPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-immediate", new ImmediateWindowPanel { DataContext = _immediateVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ImmediatePanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-modules", new ModulesPanel { DataContext = _modulesVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ModulesPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-tasks", new TasksPanel { DataContext = _tasksVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_TasksPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-disassembly", new DisassemblyPanel { DataContext = _disassemblyVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_DisassemblyPanelTitle, DefaultDockSide = "Center", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-memory", new MemoryWindowPanel { DataContext = _memoryVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_MemoryPanelTitle, DefaultDockSide = "Center", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-registers", new RegistersPanel { DataContext = _registersVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_RegistersPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-parallel-watch", new ParallelWatchPanel { DataContext = _parallelWatchVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ParallelWatchPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        ui.RegisterPanel("panel-dbg-watch", new WatchesPanel { DataContext = _watchVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_WatchPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        var consolePanel = new DebugConsolePanel { DataContext = _consoleVm };
        consolePanel.SetSessionManager(_sessionMgrVm);
        ui.RegisterPanel("panel-dbg-console", consolePanel, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ConsolePanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-threads", new ThreadsPanel { DataContext = _threadsVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ThreadsPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-parallel-stacks", new ParallelStacksPanel { DataContext = _parallelStacksVm }, ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_ParallelStacksPanelTitle, DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-launch-config",
            new LaunchConfigEditorPanel(context.DocumentHost, _debugger), ModuleId,
            new PanelDescriptor { Title = DebuggerResources.Debugger_LaunchConfigTitle, DefaultDockSide = "Bottom", DefaultAutoHide = true });

        }
        finally
        {
            ui.EndBulkRegistration();
        }

        ui.RegisterMenuItem($"{ModuleId}.Menu.Continue",   ModuleId, new MenuItemDescriptor { Header = DebuggerResources.Debugger_Menu_Continue, ParentPath = "Debug", GestureText = "F5",            Group = "Session",     IconGlyph = "", Command = new RelayCommand(_ => _ = _debugger?.ContinueAsync()) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.StepOver",   ModuleId, new MenuItemDescriptor { Header = DebuggerResources.Debugger_Menu_StepOver,  ParentPath = "Debug", GestureText = "F10",           Group = "Stepping",    IconGlyph = "", Command = new RelayCommand(_ => _ = _debugger?.StepOverAsync()) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.StepInto",   ModuleId, new MenuItemDescriptor { Header = DebuggerResources.Debugger_Menu_StepInto,  ParentPath = "Debug", GestureText = "F11",           Group = "Stepping",    IconGlyph = "", Command = new RelayCommand(_ => _ = _debugger?.StepIntoAsync()) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.StepOut",    ModuleId, new MenuItemDescriptor { Header = "Step Ou_t",              ParentPath = "Debug", GestureText = "Shift+F11",     Group = "Stepping",    IconGlyph = "", Command = new RelayCommand(_ => _ = _debugger?.StepOutAsync()) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.RunToCursor",      ModuleId, new MenuItemDescriptor { Header = "_Run to Cursor",          ParentPath = "Debug", GestureText = "Ctrl+F10",       Group = "Stepping",    IconGlyph = "", Command = new RelayCommand(_ => context.IDEEvents.Publish(new RunToCursorRequestedEvent())) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.SetNextStatement", ModuleId, new MenuItemDescriptor { Header = "Set _Next Statement",    ParentPath = "Debug", GestureText = "Ctrl+Shift+F10", Group = "Stepping",    IconGlyph = "", Command = new RelayCommand(_ => context.IDEEvents.Publish(new SetNextStatementRequestedEvent())) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ClearBps",       ModuleId, new MenuItemDescriptor { Header = "Delete _All Breakpoints", ParentPath = "Debug", GestureText = "Ctrl+Shift+F9", Group = "Breakpoints", IconGlyph = "", Command = new RelayCommand(_ => _ = _debugger?.ClearAllBreakpointsAsync()) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.AddTracepoint",  ModuleId, new MenuItemDescriptor { Header = "Add _Tracepoint…",  ParentPath = "Debug", Group = "Breakpoints", IconGlyph = "", Command = new RelayCommand(_ => context.IDEEvents.Publish(new AddTracepointRequestedEvent())) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.AttachProc", ModuleId, new MenuItemDescriptor { Header = "_Attach to Process…",    ParentPath = "Debug", GestureText = "Ctrl+Alt+P",    Group = "Session",     IconGlyph = "", Command = new RelayCommand(_ =>
        {
            if (_debugger is null) return;
            var pid = AttachToProcessDialog.Show(Application.Current.MainWindow);
            if (pid > 0) _ = _debugger.AttachAsync(pid);
        }) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowBps",    ModuleId, new MenuItemDescriptor { Header = "Show _Breakpoints",      ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-breakpoints")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowCs",     ModuleId, new MenuItemDescriptor { Header = "Show _Call Stack",       ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-callstack")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowLocals", ModuleId, new MenuItemDescriptor { Header = "Show _Locals",           ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-locals")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowAutos",  ModuleId, new MenuItemDescriptor { Header = "Show _Autos",            ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-autos")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowWatch",      ModuleId, new MenuItemDescriptor { Header = "Show _Watch",                ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-watch")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowExceptions", ModuleId, new MenuItemDescriptor { Header = "Show _Exception Settings",   ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-exceptions")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowThreads", ModuleId, new MenuItemDescriptor { Header = "Show _Threads",          ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-threads")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowParallelStacks", ModuleId, new MenuItemDescriptor { Header = "Show _Parallel Stacks",  ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-parallel-stacks")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowImmediate",      ModuleId, new MenuItemDescriptor { Header = "Show I_mmediate Window", ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-immediate")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowModules", ModuleId, new MenuItemDescriptor { Header = "Show _Modules",     ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-modules")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowTasks",       ModuleId, new MenuItemDescriptor { Header = "Show _Tasks",       ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-tasks")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowDisassembly", ModuleId, new MenuItemDescriptor { Header = "Show Disasse_mbly", ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-disassembly")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowMemory",    ModuleId, new MenuItemDescriptor { Header = "Show _Memory",    ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-memory")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowRegisters", ModuleId, new MenuItemDescriptor { Header = "Show _Registers", ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-registers")) });
        ui.RegisterMenuItem($"{ModuleId}.Menu.ShowParallelWatch", ModuleId, new MenuItemDescriptor { Header = "Show Parallel _Watch", ParentPath = "Debug", Group = "Panels", IconGlyph = "", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-parallel-watch")) });

        context.DebugVisualizers?.Register(new CollectionVisualizer());
        context.DebugVisualizers?.Register(new StringVisualizer());
        context.DebugVisualizers?.Register(new DateTimeVisualizer());

        context.Terminal.RegisterCommand(new DebugBpListCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugBpSetCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugBpClearCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugLocalsCommand(_debugger));
        context.Terminal.RegisterCommand(new DebugWatchCommand());
    }

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
            _sessionMgrVm?.AddSession(e.SessionId, System.IO.Path.GetFileName(e.ProjectPath), "csharp"));
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
