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

using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Plugins.Debugger.Panels;
using WpfHexEditor.Plugins.Debugger.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.Debugger;

public sealed class DebuggerPlugin : IWpfHexEditorPluginV2
{
    // -- IWpfHexEditorPlugin identity -----------------------------------------
    public string             Id           => "WpfHexEditor.Plugins.Debugger";
    public string             Name         => "Integrated Debugger";
    public Version            Version      => new(1, 0, 0);
    public PluginCapabilities Capabilities => new() { RegisterMenus = true, WriteOutput = true };

    // -- IWpfHexEditorPluginV2 -------------------------------------------------
    public bool SupportsHotReload => false;
    public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;

    // -- State -----------------------------------------------------------------
    private IIDEHostContext?  _context;
    private IDebuggerService? _debugger;
    private readonly List<IDisposable> _subs = [];

    // Panel view-models
    private BreakpointExplorerViewModel? _bpVm;
    private CallStackPanelViewModel?    _csVm;
    private LocalsPanelViewModel?       _locVm;
    private WatchesPanelViewModel?      _watchVm;
    private DebugConsolePanelViewModel? _consoleVm;

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
        _bpVm      = new BreakpointExplorerViewModel(_debugger, context);
        _csVm      = new CallStackPanelViewModel(_debugger, context);
        _locVm     = new LocalsPanelViewModel(_debugger);
        _watchVm   = new WatchesPanelViewModel(_debugger);
        _consoleVm = new DebugConsolePanelViewModel();

        // Wire IDE events — store tokens for disposal in ShutdownAsync
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionPausedEvent>(OnPaused));
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionEndedEvent>(OnEnded));
        _subs.Add(context.IDEEvents.Subscribe<DebugOutputReceivedEvent>(OnOutput));

        // Register panels
        var ui = context.UIRegistry;

        ui.RegisterPanel("panel-dbg-breakpoints", new BreakpointExplorerPanel { DataContext = _bpVm }, Id,
            new PanelDescriptor { Title = "Breakpoints", DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-callstack", new CallStackPanel { DataContext = _csVm }, Id,
            new PanelDescriptor { Title = "Call Stack", DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-locals", new LocalsPanel { DataContext = _locVm }, Id,
            new PanelDescriptor { Title = "Locals", DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-watch", new WatchesPanel { DataContext = _watchVm }, Id,
            new PanelDescriptor { Title = "Watch", DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-console", new DebugConsolePanel { DataContext = _consoleVm }, Id,
            new PanelDescriptor { Title = "Debug Console", DefaultDockSide = "Bottom", DefaultAutoHide = false });

        // Contribute Debug menu — one RegisterMenuItem call per item (no Children support in SDK)
        ui.RegisterMenuItem($"{Id}.Menu.Continue",   Id, new MenuItemDescriptor { Header = "_Continue",              ParentPath = "Debug", GestureText = "F5",            Group = "Session", Command = new RelayCommand(_ => _ = _debugger?.ContinueAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.StepOver",   Id, new MenuItemDescriptor { Header = "Step _Over",             ParentPath = "Debug", GestureText = "F10",           Group = "Step",    Command = new RelayCommand(_ => _ = _debugger?.StepOverAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.StepInto",   Id, new MenuItemDescriptor { Header = "Step _Into",             ParentPath = "Debug", GestureText = "F11",           Group = "Step",    Command = new RelayCommand(_ => _ = _debugger?.StepIntoAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.StepOut",    Id, new MenuItemDescriptor { Header = "Step Ou_t",              ParentPath = "Debug", GestureText = "Shift+F11",     Group = "Step",    Command = new RelayCommand(_ => _ = _debugger?.StepOutAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.ClearBps",   Id, new MenuItemDescriptor { Header = "Delete _All Breakpoints",ParentPath = "Debug", GestureText = "Ctrl+Shift+F9", Group = "Breakpoints", Command = new RelayCommand(_ => _ = _debugger?.ClearAllBreakpointsAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowBps",    Id, new MenuItemDescriptor { Header = "Show _Breakpoints",      ParentPath = "Debug", Group = "Panels",  Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-breakpoints")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowCs",     Id, new MenuItemDescriptor { Header = "Show _Call Stack",       ParentPath = "Debug", Group = "Panels",  Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-callstack")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowLocals", Id, new MenuItemDescriptor { Header = "Show _Locals",           ParentPath = "Debug", Group = "Panels",  Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-locals")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowWatch",  Id, new MenuItemDescriptor { Header = "Show _Watch",            ParentPath = "Debug", Group = "Panels",  Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-watch")) });

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
                var topFrame = frames[0];
                var locals   = await _debugger.GetVariablesAsync(0);
                _locVm?.SetVariables(locals);
                await _watchVm!.RefreshAsync(_debugger);
            }
        });
    }

    private void OnEnded(DebugSessionEndedEvent e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            _csVm?.SetFrames([]);
            _locVm?.SetVariables([]);
        });
    }

    private void OnOutput(DebugOutputReceivedEvent e) =>
        _consoleVm?.Append(e.Category, e.Output);

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var sub in _subs) sub.Dispose();
        _subs.Clear();
        return Task.CompletedTask;
    }
}
