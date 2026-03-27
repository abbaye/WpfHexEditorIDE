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
            Id:               CommandIds.Debug.StartDebugging,
            Header:           "Start _Debugging",
            GestureText:      "F5",
            IconGlyph:        "\uE768",
            Command:          new RelayCommand(_ => OnDebugStartOrContinue()),
            CommandParameter: null,
            Group:            "Session",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.StartWithoutDebugging,
            Header:           "Start _Without Debugging",
            GestureText:      "Ctrl+F5",
            IconGlyph:        "\uEDB5",
            Command:          new RelayCommand(_ => _ = RunStartupProjectAsync()),
            CommandParameter: null,
            Group:            "Session",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.StopDebugging,
            Header:           "S_top Debugging",
            GestureText:      "Shift+F5",
            IconGlyph:        "\uE71A",
            Command:          new RelayCommand(_ => _ = _debuggerService?.StopSessionAsync()),
            CommandParameter: null,
            Group:            "Session",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.RestartDebugging,
            Header:           "_Restart",
            GestureText:      "Ctrl+Shift+F5",
            IconGlyph:        "\uE72C",
            Command:          new RelayCommand(_ => OnDebugRestart()),
            CommandParameter: null,
            Group:            "Session",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.Continue,
            Header:           "_Continue",
            GestureText:      "F5",
            IconGlyph:        "\uE768",
            Command:          new RelayCommand(_ => _ = _debuggerService?.ContinueAsync()),
            CommandParameter: null,
            Group:            "Session",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.Pause,
            Header:           "_Pause",
            GestureText:      null,
            IconGlyph:        "\uE769",
            Command:          new RelayCommand(_ => _ = _debuggerService?.PauseAsync()),
            CommandParameter: null,
            Group:            "Session",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.AttachToProcess,
            Header:           "_Attach to Process\u2026",
            GestureText:      "Ctrl+Alt+P",
            IconGlyph:        "\uE71B",
            Command:          new RelayCommand(_ => OnAttachToProcess()),
            CommandParameter: null,
            Group:            "Session",
            ToolTip:          null,
            IsBuiltIn:        true));

        // ── Stepping ────────────────────────────────────────────────────────
        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.StepOver,
            Header:           "Step _Over",
            GestureText:      "F10",
            IconGlyph:        "\uE7EE",
            Command:          new RelayCommand(_ => _ = _debuggerService?.StepOverAsync()),
            CommandParameter: null,
            Group:            "Stepping",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.StepInto,
            Header:           "Step _Into",
            GestureText:      "F11",
            IconGlyph:        "\uE70D",
            Command:          new RelayCommand(_ => _ = _debuggerService?.StepIntoAsync()),
            CommandParameter: null,
            Group:            "Stepping",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.StepOut,
            Header:           "Step O_ut",
            GestureText:      "Shift+F11",
            IconGlyph:        "\uE70E",
            Command:          new RelayCommand(_ => _ = _debuggerService?.StepOutAsync()),
            CommandParameter: null,
            Group:            "Stepping",
            ToolTip:          null,
            IsBuiltIn:        true));

        // ── Breakpoints ─────────────────────────────────────────────────────
        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.ToggleBreakpoint,
            Header:           "Toggle _Breakpoint",
            GestureText:      "F9",
            IconGlyph:        "\uE7C1",
            Command:          new RelayCommand(_ => OnToggleBreakpoint()),
            CommandParameter: null,
            Group:            "Breakpoints",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.DeleteAllBreakpoints,
            Header:           "_Delete All Breakpoints",
            GestureText:      "Ctrl+Shift+F9",
            IconGlyph:        "\uE74D",
            Command:          new RelayCommand(_ => _ = _debuggerService?.ClearAllBreakpointsAsync()),
            CommandParameter: null,
            Group:            "Breakpoints",
            ToolTip:          null,
            IsBuiltIn:        true));

        // ── Panels ──────────────────────────────────────────────────────────
        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.ShowBreakpoints,
            Header:           "Show _Breakpoints",
            GestureText:      null,
            IconGlyph:        "\uEBE8",
            Command:          new RelayCommand(_ => ShowOrCreatePanel("Breakpoints", "panel-dbg-breakpoints", DockDirection.Bottom)),
            CommandParameter: null,
            Group:            "Panels",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.ShowCallStack,
            Header:           "Show _Call Stack",
            GestureText:      null,
            IconGlyph:        "\uE81E",
            Command:          new RelayCommand(_ => ShowOrCreatePanel("Call Stack", "panel-dbg-callstack", DockDirection.Bottom)),
            CommandParameter: null,
            Group:            "Panels",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.ShowLocals,
            Header:           "Show _Locals",
            GestureText:      null,
            IconGlyph:        "\uE943",
            Command:          new RelayCommand(_ => ShowOrCreatePanel("Locals", "panel-dbg-locals", DockDirection.Bottom)),
            CommandParameter: null,
            Group:            "Panels",
            ToolTip:          null,
            IsBuiltIn:        true));

        _debugMenuOrganizer.RegisterBuiltInEntry(new DebugMenuEntry(
            Id:               CommandIds.Debug.ShowWatch,
            Header:           "Show _Watch",
            GestureText:      null,
            IconGlyph:        "\uE7B3",
            Command:          new RelayCommand(_ => ShowOrCreatePanel("Watch", "panel-dbg-watch", DockDirection.Bottom)),
            CommandParameter: null,
            Group:            "Panels",
            ToolTip:          null,
            IsBuiltIn:        true));
    }

    private void OnDebugMenuItemsChanged()
        => Dispatcher.InvokeAsync(() => _debugMenuOrganizer?.RebuildMenu());
}
