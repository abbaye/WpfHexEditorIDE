// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeAssistantPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Main plugin entry point. Registers panel, menus, commands, titlebar icon, MCP servers.
// ==========================================================
using System.Windows;
using WpfHexEditor.Plugins.ClaudeAssistant.Connection;
using WpfHexEditor.Plugins.ClaudeAssistant.Options;
using WpfHexEditor.Plugins.ClaudeAssistant.Panel;
using WpfHexEditor.Plugins.ClaudeAssistant.TitleBar;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.ClaudeAssistant;

public sealed class ClaudeAssistantPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public string Id => "WpfHexEditor.Plugins.ClaudeAssistant";
    public string Name => "Claude AI Assistant";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor = true,
        AccessCodeEditor = true,
        AccessFileSystem = true,
        AccessNetwork = true,
        AccessSettings = true,
        RegisterMenus = true,
        WriteOutput = true,
        WriteTerminal = true,
        RegisterTerminalCommands = true
    };

    private IIDEHostContext? _context;
    private ClaudeAssistantPanel? _panel;
    private ClaudeAssistantPanelViewModel? _vm;
    private ClaudeConnectionService? _connectionService;
    private string? _panelUiId;

    public async Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // Load options (DPAPI-encrypted API keys)
        ClaudeAssistantOptions.Instance.Load();

        // Start connection monitor (Phase 0.5)
        _connectionService = new ClaudeConnectionService();
        _connectionService.Start();

        // Create panel and restore saved sessions
        _vm = new ClaudeAssistantPanelViewModel();
        await _vm.RestoreSessionsAsync();
        _panel = new ClaudeAssistantPanel { DataContext = _vm };

        // Register dockable panel
        _panelUiId = context.UIRegistry.GenerateUIId(Id, "Panel", "ClaudeAssistant");
        context.UIRegistry.RegisterPanel(_panelUiId, _panel, Id, new PanelDescriptor
        {
            Title = "Claude AI Assistant",
            DefaultDockSide = "Right",
            CanClose = true,
            PreferredWidth = 420,
            Category = "AI & Assistants"
        });

        // Register View menu item
        var menuUiId = context.UIRegistry.GenerateUIId(Id, "Menu", "Toggle");
        context.UIRegistry.RegisterMenuItem(menuUiId, Id, new MenuItemDescriptor
        {
            Header = "_Claude AI Assistant",
            ParentPath = "View",
            GestureText = "Ctrl+Shift+A",
            IconGlyph = "\uE99A",
            Command = new RelayCommand(() => context.UIRegistry.TogglePanel(_panelUiId!)),
            Category = "AI & Assistants"
        });

        // Register titlebar button (Phase 0.5)
        // NOTE: ITitleBarContributor.RegisterTitleBarItem not yet in IUIRegistry —
        // the contributor is created but will be wired once IUIRegistry is extended.
        var _titleBarContributor = new ClaudeTitleBarContributor(
            _connectionService,
            () => context.UIRegistry.TogglePanel(_panelUiId!));

        // Register status bar item
        var statusUiId = context.UIRegistry.GenerateUIId(Id, "StatusBar", "Connection");
        context.UIRegistry.RegisterStatusBarItem(statusUiId, Id, new StatusBarItemDescriptor
        {
            Text = "Claude · Connecting...",
            Alignment = StatusBarAlignment.Right,
            ToolTip = "Claude AI Assistant connection status",
            Order = 50
        });

        // Update status bar on connection change
        _connectionService.StatusChanged += (_, status) =>
        {
            var opts = ClaudeAssistantOptions.Instance;
            var text = status switch
            {
                ClaudeConnectionStatus.Connected => $"Claude · {opts.DefaultModelId} · Connected",
                ClaudeConnectionStatus.Connecting => "Claude · Connecting...",
                ClaudeConnectionStatus.NotConfigured => "Claude · No API key",
                ClaudeConnectionStatus.RateLimited => "Claude · Rate limited",
                ClaudeConnectionStatus.Error => "Claude · Error",
                ClaudeConnectionStatus.Offline => "Claude · Offline",
                _ => "Claude"
            };
            _panel?.Dispatcher.InvokeAsync(() =>
            {
                // TODO: update the registered status bar item text
                _vm!.StatusText = text;
            });
        };

        context.Output?.Info($"[ClaudeAssistant] Plugin initialized (v{Version})");
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        _connectionService?.Dispose();
        if (_vm is not null)
            await _vm.SaveAllSessionsAsync();
        ClaudeAssistantOptions.Instance.Save();
        _context?.Output?.Info("[ClaudeAssistant] Plugin shutdown.");
    }

    // IPluginWithOptions
    public FrameworkElement CreateOptionsPage() => new ClaudeAssistantOptionsPage();
    public void SaveOptions() => ClaudeAssistantOptions.Instance.Save();
    public void LoadOptions() => ClaudeAssistantOptions.Instance.Load();
    public string GetOptionsCategory() => "AI & Assistants";
    public string GetOptionsCategoryIcon() => "\uE99A";

    /// <summary>Minimal relay command for menu items.</summary>
    private sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
