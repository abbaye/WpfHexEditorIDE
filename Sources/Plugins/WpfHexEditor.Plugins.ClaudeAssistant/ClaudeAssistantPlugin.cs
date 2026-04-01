// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeAssistantPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Main plugin entry point. Full wiring of all subsystems:
//     connection, panel, titlebar, MCP, terminal commands, context menus,
//     command palette, presets, SDK service exposure.
// ==========================================================
using System.Windows;
using WpfHexEditor.Plugins.ClaudeAssistant.Commands.Terminal;
using WpfHexEditor.Plugins.ClaudeAssistant.Connection;
using WpfHexEditor.Plugins.ClaudeAssistant.ContextMenu;
using WpfHexEditor.Plugins.ClaudeAssistant.Mcp.Host;
using WpfHexEditor.Plugins.ClaudeAssistant.Options;
using WpfHexEditor.Plugins.ClaudeAssistant.Panel;
using WpfHexEditor.Plugins.ClaudeAssistant.Panel.CommandPalette;
using WpfHexEditor.Plugins.ClaudeAssistant.Panel.ModelSwitcher;
using WpfHexEditor.Plugins.ClaudeAssistant.Presets;
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
    private McpServerManager? _mcpManager;
    private string? _panelUiId;

    public async Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // ── 0. SafeGuard logger ─────────────────────────────────────────────
        SafeGuard.SetLogger(msg => context.Output?.Error(msg));

        // ── 1. Options ──────────────────────────────────────────────────────
        ClaudeAssistantOptions.Instance.Load();

        // ── 2. Connection monitor ───────────────────────────────────────────
        _connectionService = new ClaudeConnectionService();
        _connectionService.Start();

        // ── 3. MCP server manager ───────────────────────────────────────────
        _mcpManager = new McpServerManager();
        // IDE servers will be registered here when the host context services
        // are wired (Phase 4 runtime integration — delegates set by App at startup).
        try { await _mcpManager.StartAllAsync(ct); }
        catch (Exception ex) { context.Output?.Warning($"[ClaudeAssistant] MCP startup: {ex.Message}"); }

        // ── 4. Prompt presets ───────────────────────────────────────────────
        await PromptPresetsService.Instance.LoadAsync();

        // ── 5. Panel + session restore ──────────────────────────────────────
        _vm = new ClaudeAssistantPanelViewModel();
        await _vm.RestoreSessionsAsync();
        _panel = new ClaudeAssistantPanel { DataContext = _vm };

        // ── 6. Register dockable panel ──────────────────────────────────────
        _panelUiId = context.UIRegistry.GenerateUIId(Id, "Panel", "ClaudeAssistant");
        context.UIRegistry.RegisterPanel(_panelUiId, _panel, Id, new PanelDescriptor
        {
            Title = "Claude AI Assistant",
            DefaultDockSide = "Right",
            CanClose = true,
            PreferredWidth = 420,
            Category = "AI & Assistants"
        });

        // ── 7. View menu ────────────────────────────────────────────────────
        var menuUiId = context.UIRegistry.GenerateUIId(Id, "Menu", "Toggle");
        context.UIRegistry.RegisterMenuItem(menuUiId, Id, new MenuItemDescriptor
        {
            Header = "_Claude AI Assistant",
            ParentPath = "View",
            GestureText = "Ctrl+Shift+A",
            IconGlyph = "\uE734",
            Command = new RelayCommand(() => context.UIRegistry.TogglePanel(_panelUiId!)),
            Category = "AI & Assistants"
        });

        // ── 8. Titlebar button ──────────────────────────────────────────────
        var titleBarContributor = new ClaudeTitleBarContributor(
            _connectionService,
            showCommandPalette: anchor => ShowCommandPalette(anchor),
            newTab: () => _vm?.CreateNewTabCommand.Execute(null),
            fixErrors: () => SendQuickAction("@selection @errors Fix the errors in this code."),
            openOptions: () => context.CommandRegistry?.Find("View.Options")?.Command.Execute(null));
        var titleBarUiId = context.UIRegistry.GenerateUIId(Id, "TitleBar", "Button");
        context.UIRegistry.RegisterTitleBarItem(titleBarUiId, Id, titleBarContributor);

        // ── 9. Status bar ───────────────────────────────────────────────────
        var statusUiId = context.UIRegistry.GenerateUIId(Id, "StatusBar", "Connection");
        context.UIRegistry.RegisterStatusBarItem(statusUiId, Id, new StatusBarItemDescriptor
        {
            Text = "Claude · Connecting...",
            Alignment = StatusBarAlignment.Right,
            ToolTip = "Claude AI Assistant connection status",
            Order = 50
        });

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
            _panel?.Dispatcher.InvokeAsync(() => _vm!.StatusText = text);
        };

        // ── 10. Terminal commands ───────────────────────────────────────────
        context.Terminal?.RegisterCommand(new ClaudeAskCommand(() => _vm!));
        context.Terminal?.RegisterCommand(new ClaudeExplainCommand(() => _vm!, context));
        context.Terminal?.RegisterCommand(new ClaudeFixCommand(() => _vm!, context));
        context.Terminal?.RegisterCommand(new ClaudeNewTabCommand(() => _vm!));

        // ── 11. SolutionExplorer context menu ───────────────────────────────
        context.UIRegistry.RegisterContextMenuContributor(Id, new ClaudeSolutionExplorerContributor(() => _vm!));

        // ── 12. Command palette shortcut (Ctrl+Shift+A) ────────────────────
        context.CommandRegistry?.Register(new SDK.Commands.SdkCommandDefinition(
            Id: "ClaudeAssistant.CommandPalette",
            Name: "Claude AI: Command Palette",
            Category: "AI & Assistants",
            DefaultGesture: "Ctrl+Shift+A",
            IconGlyph: "\uE734",
            Command: new RelayCommand(() => SafeGuard.Run(() => ShowCommandPalette()))));

        context.CommandRegistry?.Register(new SDK.Commands.SdkCommandDefinition(
            Id: "ClaudeAssistant.NewTab",
            Name: "Claude AI: New Conversation",
            Category: "AI & Assistants",
            DefaultGesture: "Ctrl+Shift+Alt+A",
            IconGlyph: "\uE710",
            Command: new RelayCommand(() => SafeGuard.Run(() => _vm?.CreateNewTabCommand.Execute(null)))));

        // ── Done ────────────────────────────────────────────────────────────
        context.Output?.Info($"[ClaudeAssistant] Plugin initialized (v{Version}) — 4 providers, {_mcpManager.GetAllTools().Count} MCP tools");
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        // Unregister terminal commands
        _context?.Terminal?.UnregisterCommand("claude-ask");
        _context?.Terminal?.UnregisterCommand("claude-explain");
        _context?.Terminal?.UnregisterCommand("claude-fix");
        _context?.Terminal?.UnregisterCommand("claude-new-tab");

        // Save and cleanup
        _connectionService?.Dispose();
        if (_mcpManager is not null)
            await _mcpManager.DisposeAsync();
        if (_vm is not null)
            await _vm.SaveAllSessionsAsync();
        await PromptPresetsService.Instance.SaveAsync();
        ClaudeAssistantOptions.Instance.Save();

        _context?.Output?.Info("[ClaudeAssistant] Plugin shutdown.");
    }

    private void ShowCommandPalette(UIElement? anchor = null)
    {
        var currentModel = _vm?.ActiveTab is { } tab
            ? $"{tab.SelectedProviderId} / {tab.SelectedModelId}"
            : null;

        var entries = ClaudeCommandPalette.BuildDefaultCatalog(
            explainSelection: () => SendQuickAction("@selection Explain this code in detail."),
            fixErrors: () => SendQuickAction("@selection @errors Fix the errors in this code."),
            refactorSelection: () => SendQuickAction("@selection Refactor for readability. Show diff."),
            generateTests: () => SendQuickAction("@selection Generate comprehensive xUnit tests."),
            addDocs: () => SendQuickAction("@selection Add complete XML documentation."),
            newTab: () => _vm?.CreateNewTabCommand.Execute(null),
            showHistory: () => _vm?.ToggleHistoryCommand.Execute(null),
            openOptions: () => _context?.CommandRegistry?.Find("View.Options")?.Command.Execute(null),
            switchModel: () => ShowModelSwitcher(),
            currentModel: currentModel,
            presets: PromptPresetsService.Instance.Presets);

        var owner = (_panel != null ? Window.GetWindow(_panel) : null)
                 ?? (anchor != null ? Window.GetWindow(anchor) : null)
                 ?? Application.Current.MainWindow;
        var paletteAnchor = _context?.UIRegistry.GetCommandPaletteAnchor();
        var palette = new ClaudeCommandPalette(entries, owner!, paletteAnchor);
        palette.Show();
    }

    private void ShowModelSwitcher()
    {
        if (_vm?.ActiveTab is not { } tab) return;
        var owner = (_panel != null ? Window.GetWindow(_panel) : null)
                 ?? Application.Current.MainWindow;
        var popup = new ModelSwitcherPopup(
            tab.Registry,
            tab.SelectedProviderId,
            tab.SelectedModelId,
            tab.ThinkingEnabled,
            _panel);
        popup.Closed += (_, _) => SafeGuard.Run(() =>
        {
            if (popup.SelectedProviderId is not null)
                tab.SelectedProviderId = popup.SelectedProviderId;
            if (popup.SelectedModelId is not null)
                tab.SelectedModelId = popup.SelectedModelId;
            tab.ThinkingEnabled = popup.ThinkingEnabled;
        });
        popup.Show();
    }

    private void SendQuickAction(string message)
    {
        if (_vm is null) return;
        _context?.UIRegistry.ShowPanel(_panelUiId!);
        if (_vm.ActiveTab is null)
            _vm.CreateNewTabCommand.Execute(null);
        if (_vm.ActiveTab is not null)
        {
            _vm.ActiveTab.InputText = message;
            _vm.ActiveTab.SendCommand.Execute(null);
        }
    }

    // IPluginWithOptions
    public FrameworkElement CreateOptionsPage() => new ClaudeAssistantOptionsPage();
    public void SaveOptions() => ClaudeAssistantOptions.Instance.Save();
    public void LoadOptions() => ClaudeAssistantOptions.Instance.Load();
    public string GetOptionsCategory() => "AI & Assistants";
    public string GetOptionsCategoryIcon() => "\uE734";

    /// <summary>Minimal relay command for menu items and commands.</summary>
    private sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
