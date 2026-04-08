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
    private BreakpointExplorerViewModel?  _bpVm;
    private CallStackPanelViewModel?      _csVm;
    private LocalsPanelViewModel?         _locVm;
    private WatchesPanelViewModel?        _watchVm;
    private DebugConsolePanelViewModel?   _consoleVm;
    private DebugSessionManagerViewModel? _sessionMgrVm;

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
        _bpVm         = new BreakpointExplorerViewModel(_debugger, context);
        _csVm         = new CallStackPanelViewModel(_debugger, context);
        _locVm        = new LocalsPanelViewModel(_debugger);
        _watchVm      = new WatchesPanelViewModel(_debugger);
        _consoleVm    = new DebugConsolePanelViewModel();
        _sessionMgrVm = new DebugSessionManagerViewModel();

        // Wire IDE events — store tokens for disposal in ShutdownAsync
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionPausedEvent>(OnPaused));
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionStartedEvent>(OnSessionStarted));
        _subs.Add(context.IDEEvents.Subscribe<DebugSessionEndedEvent>(OnEnded));
        _subs.Add(context.IDEEvents.Subscribe<DebugOutputReceivedEvent>(OnOutput));
        _subs.Add(context.IDEEvents.Subscribe<OpenBreakpointSettingsRequestedEvent>(OnOpenBpSettings));

        // Register panels
        var ui = context.UIRegistry;

        var bpPanel = new BreakpointExplorerPanel { DataContext = _bpVm };
        bpPanel.UIFactory = context.UIFactory;
        ui.RegisterPanel("panel-dbg-breakpoints", bpPanel, Id,
            new PanelDescriptor { Title = "Breakpoints", DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-callstack", new CallStackPanel { DataContext = _csVm }, Id,
            new PanelDescriptor { Title = "Call Stack", DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-locals", new LocalsPanel { DataContext = _locVm }, Id,
            new PanelDescriptor { Title = "Locals", DefaultDockSide = "Bottom", DefaultAutoHide = false });

        ui.RegisterPanel("panel-dbg-watch", new WatchesPanel { DataContext = _watchVm }, Id,
            new PanelDescriptor { Title = "Watch", DefaultDockSide = "Bottom", DefaultAutoHide = false });

        var consolePanel = new DebugConsolePanel { DataContext = _consoleVm };
        consolePanel.SetSessionManager(_sessionMgrVm);
        ui.RegisterPanel("panel-dbg-console", consolePanel, Id,
            new PanelDescriptor { Title = "Debug Console", DefaultDockSide = "Bottom", DefaultAutoHide = false });

        // Launch configuration editor panel
        ui.RegisterPanel("panel-dbg-launch-config",
            new Panels.LaunchConfigEditorPanel(context.DocumentHost, _debugger), Id,
            new PanelDescriptor { Title = "Launch Config (.whdbg)", DefaultDockSide = "Bottom", DefaultAutoHide = true });

        // Contribute Debug menu — one RegisterMenuItem call per item (no Children support in SDK)
        // Icons included for Command Palette; DebugMenuOrganizer deduplicates against built-in entries.
        ui.RegisterMenuItem($"{Id}.Menu.Continue",   Id, new MenuItemDescriptor { Header = "_Continue",              ParentPath = "Debug", GestureText = "F5",            Group = "Session",     IconGlyph = "\uE768", Command = new RelayCommand(_ => _ = _debugger?.ContinueAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.StepOver",   Id, new MenuItemDescriptor { Header = "Step _Over",             ParentPath = "Debug", GestureText = "F10",           Group = "Stepping",    IconGlyph = "\uE7EE", Command = new RelayCommand(_ => _ = _debugger?.StepOverAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.StepInto",   Id, new MenuItemDescriptor { Header = "Step _Into",             ParentPath = "Debug", GestureText = "F11",           Group = "Stepping",    IconGlyph = "\uE70D", Command = new RelayCommand(_ => _ = _debugger?.StepIntoAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.StepOut",    Id, new MenuItemDescriptor { Header = "Step Ou_t",              ParentPath = "Debug", GestureText = "Shift+F11",     Group = "Stepping",    IconGlyph = "\uE70E", Command = new RelayCommand(_ => _ = _debugger?.StepOutAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.ClearBps",   Id, new MenuItemDescriptor { Header = "Delete _All Breakpoints",ParentPath = "Debug", GestureText = "Ctrl+Shift+F9", Group = "Breakpoints", IconGlyph = "\uE74D", Command = new RelayCommand(_ => _ = _debugger?.ClearAllBreakpointsAsync()) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowBps",    Id, new MenuItemDescriptor { Header = "Show _Breakpoints",      ParentPath = "Debug", Group = "Panels", IconGlyph = "\uEBE8", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-breakpoints")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowCs",     Id, new MenuItemDescriptor { Header = "Show _Call Stack",       ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE81E", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-callstack")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowLocals", Id, new MenuItemDescriptor { Header = "Show _Locals",           ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE943", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-locals")) });
        ui.RegisterMenuItem($"{Id}.Menu.ShowWatch",  Id, new MenuItemDescriptor { Header = "Show _Watch",            ParentPath = "Debug", Group = "Panels", IconGlyph = "\uE7B3", Command = new RelayCommand(_ => ui.ShowPanel("panel-dbg-watch")) });

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
                var topFrame = frames[0];
                var locals   = await _debugger.GetVariablesAsync(0);
                _locVm?.SetVariables(locals);
                await _watchVm!.RefreshAsync(_debugger);
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
