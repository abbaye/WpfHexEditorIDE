//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : MainWindow.DebugMenu.cs
// Description  : Partial class that initialises the dynamic Debug menu system.
//                Registers built-in entries, wires the DebugMenuOrganizer to
//                the MenuAdapter's DebugItemsChanged event, and performs the
//                initial menu build.
// Architecture : Partial class of MainWindow (UI wiring layer).
//////////////////////////////////////////////

using WpfHexEditor.App.Services.DebugMenu;
using WpfHexEditor.Core.Commands;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    private DebugMenuOrganizer? _debugMenuOrganizer;

    /// <summary>
    /// Creates the <see cref="DebugMenuOrganizer"/>, registers all built-in Debug entries,
    /// subscribes to <see cref="Services.MenuAdapter.DebugItemsChanged"/>, and performs
    /// the first menu build.
    /// <para>
    /// Must be called after <c>_menuAdapter</c> is resolved from the service provider,
    /// and <strong>before</strong> plugins load (so that plugin contributions trigger
    /// <c>DebugItemsChanged → RebuildMenu</c>).
    /// </para>
    /// </summary>
    private void InitDebugMenuOrganizer()
    {
        if (_menuAdapter is null) return;

        _debugMenuOrganizer = new DebugMenuOrganizer(DebugMenu, _menuAdapter);

        RegisterBuiltInDebugEntries();

        _menuAdapter.DebugItemsChanged += OnDebugMenuItemsChanged;

        // Initial build (only built-in items; plugins will trigger rebuilds later).
        _debugMenuOrganizer.RebuildMenu();
    }

    /// <summary>
    /// Registers the hardcoded Debug menu items as <see cref="DebugMenuEntry"/> records.
    /// </summary>
    private void RegisterBuiltInDebugEntries()
    {
        if (_debugMenuOrganizer is null) return;

        // ── Session ─────────────────────────────────────────────────────────
        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.StartDebugging,
            Header:            "Start _Debugging",
            GestureText:       "F5",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnDebugStartOrContinue()),
            CommandParameter:  null,
            Group:             "Session",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_StartDebugging"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.StartWithoutDebugging,
            Header:            "Start _Without Debugging",
            GestureText:       "Ctrl+F5",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => _ = RunStartupProjectAsync()),
            CommandParameter:  null,
            Group:             "Session",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_StartWithoutDebugging"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.StopDebugging,
            Header:            "S_top Debugging",
            GestureText:       "Shift+F5",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => _ = _debuggerService?.StopSessionAsync()),
            CommandParameter:  null,
            Group:             "Session",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_StopDebugging"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.RestartDebugging,
            Header:            "_Restart",
            GestureText:       "Ctrl+Shift+F5",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnDebugRestart()),
            CommandParameter:  null,
            Group:             "Session",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_Restart"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.Continue,
            Header:            "_Continue",
            GestureText:       "F5",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => _ = _debuggerService?.ContinueAsync()),
            CommandParameter:  null,
            Group:             "Session",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_Continue"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.Pause,
            Header:            "_Pause",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => _ = _debuggerService?.PauseAsync()),
            CommandParameter:  null,
            Group:             "Session",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_Pause"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.AttachToProcess,
            Header:            "_Attach to Process…",
            GestureText:       "Ctrl+Alt+P",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnAttachToProcess()),
            CommandParameter:  null,
            Group:             "Session",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_AttachToProcess"));

        // ── Stepping ────────────────────────────────────────────────────────
        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.StepOver,
            Header:            "Step _Over",
            GestureText:       "F10",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => _ = _debuggerService?.StepOverAsync()),
            CommandParameter:  null,
            Group:             "Stepping",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_StepOver"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.StepInto,
            Header:            "Step _Into",
            GestureText:       "F11",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => _ = _debuggerService?.StepIntoAsync()),
            CommandParameter:  null,
            Group:             "Stepping",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_StepInto"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.StepOut,
            Header:            "Step O_ut",
            GestureText:       "Shift+F11",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => _ = _debuggerService?.StepOutAsync()),
            CommandParameter:  null,
            Group:             "Stepping",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_StepOut"));

        // ── Breakpoints ─────────────────────────────────────────────────────
        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.ToggleBreakpoint,
            Header:            "Toggle _Breakpoint",
            GestureText:       "F9",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnToggleBreakpoint()),
            CommandParameter:  null,
            Group:             "Breakpoints",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_ToggleBreakpoint"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.DeleteAllBreakpoints,
            Header:            "_Delete All Breakpoints",
            GestureText:       "Ctrl+Shift+F9",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => _ = _debuggerService?.ClearAllBreakpointsAsync()),
            CommandParameter:  null,
            Group:             "Breakpoints",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_DeleteAllBreakpoints"));

        // ── Panels ──────────────────────────────────────────────────────────
        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.ShowBreakpoints,
            Header:            "Show _Breakpoints",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => ShowOrCreatePanel("Breakpoints", "panel-dbg-breakpoints", DockDirection.Bottom)),
            CommandParameter:  null,
            Group:             "Panels",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_ShowBreakpoints"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.ShowCallStack,
            Header:            "Show _Call Stack",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => ShowOrCreatePanel("Call Stack", "panel-dbg-callstack", DockDirection.Bottom)),
            CommandParameter:  null,
            Group:             "Panels",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_ShowCallStack"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.ShowLocals,
            Header:            "Show _Locals",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => ShowOrCreatePanel("Locals", "panel-dbg-locals", DockDirection.Bottom)),
            CommandParameter:  null,
            Group:             "Panels",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_ShowLocals"));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                CommandIds.Debug.ShowWatch,
            Header:            "Show _Watch",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => ShowOrCreatePanel("Watch", "panel-dbg-watch", DockDirection.Bottom)),
            CommandParameter:  null,
            Group:             "Panels",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_DBG_ShowWatch"));

        // ── Additional panels (formerly registered by the Debugger plugin via IUIRegistry) ──
        RegisterShowPanelEntry("Debug.ShowAutos",          "Show _Autos",                  "Autos",           "panel-dbg-autos");
        RegisterShowPanelEntry("Debug.ShowExceptions",     "Show _Exception Settings",     "Exceptions",      "panel-dbg-exceptions");
        RegisterShowPanelEntry("Debug.ShowThreads",        "Show _Threads",                "Threads",         "panel-dbg-threads");
        RegisterShowPanelEntry("Debug.ShowParallelStacks", "Show _Parallel Stacks",        "Parallel Stacks", "panel-dbg-parallel-stacks");
        RegisterShowPanelEntry("Debug.ShowImmediate",      "Show I_mmediate Window",       "Immediate",       "panel-dbg-immediate");
        RegisterShowPanelEntry("Debug.ShowModules",        "Show _Modules",                "Modules",         "panel-dbg-modules");
        RegisterShowPanelEntry("Debug.ShowTasks",          "Show _Tasks",                  "Tasks",           "panel-dbg-tasks");
        RegisterShowPanelEntry("Debug.ShowDisassembly",    "Show Disasse_mbly",            "Disassembly",     "panel-dbg-disassembly");
        RegisterShowPanelEntry("Debug.ShowMemory",         "Show _Memory",                 "Memory",          "panel-dbg-memory");
        RegisterShowPanelEntry("Debug.ShowRegisters",      "Show _Registers",              "Registers",       "panel-dbg-registers");
        RegisterShowPanelEntry("Debug.ShowParallelWatch",  "Show Parallel _Watch",         "Parallel Watch",  "panel-dbg-parallel-watch");
        RegisterShowPanelEntry("Debug.ShowConsole",        "Show Debug Co_nsole",          "Debug Console",   "panel-dbg-console");
        RegisterShowPanelEntry("Debug.ShowLaunchConfig",   "Show _Launch Configuration",   "Launch Config",   "panel-dbg-launch-config");

        // Stepping additions: Run to Cursor + Set Next Statement (publish IDE events; consumed by editors).
        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                "Debug.RunToCursor",
            Header:            "_Run to Cursor",
            GestureText:       "Ctrl+F10",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => _ideEventBus?.Publish(new WpfHexEditor.Core.Events.IDEEvents.RunToCursorRequestedEvent())),
            CommandParameter:  null,
            Group:             "Stepping",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: null));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                "Debug.SetNextStatement",
            Header:            "Set _Next Statement",
            GestureText:       "Ctrl+Shift+F10",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => _ideEventBus?.Publish(new WpfHexEditor.Core.Events.IDEEvents.SetNextStatementRequestedEvent())),
            CommandParameter:  null,
            Group:             "Stepping",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: null));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                "Debug.AddTracepoint",
            Header:            "Add _Tracepoint…",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => _ideEventBus?.Publish(new WpfHexEditor.Core.Events.IDEEvents.AddTracepointRequestedEvent())),
            CommandParameter:  null,
            Group:             "Breakpoints",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: null));
    }

    /// <summary>Helper that registers a "Show X panel" Debug menu entry that uses
    /// <see cref="ShowOrCreatePanel"/> to dock the panel on first show.</summary>
    private void RegisterShowPanelEntry(string id, string header, string title, string contentId)
    {
        if (_debugMenuOrganizer is null) return;
        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:                id,
            Header:            header,
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => ShowOrCreatePanel(title, contentId, DockDirection.Bottom)),
            CommandParameter:  null,
            Group:             "Panels",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: null));
    }

    private void OnDebugMenuItemsChanged()
        => Dispatcher.InvokeAsync(() => _debugMenuOrganizer?.RebuildMenu());
}
