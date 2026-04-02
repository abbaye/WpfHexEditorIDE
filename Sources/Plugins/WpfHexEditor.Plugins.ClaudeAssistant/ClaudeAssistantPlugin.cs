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
using WpfHexEditor.Plugins.ClaudeAssistant.Panel.ConnectionManager;
using WpfHexEditor.Plugins.ClaudeAssistant.Providers.ClaudeCode;
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
    private bool _shownConnectionManagerOnce;

    public async Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // ── 0. Shared services ──────────────────────────────────────────────
        SafeGuard.SetLogger(msg => context.Output?.Error(msg));
        Panel.Messages.ChatCodeBlockCanvas.SyntaxColoringService = context.SyntaxColoring;

        // ── 1. Options ──────────────────────────────────────────────────────
        ClaudeAssistantOptions.Instance.Load();

        // ── 2. Connection monitor ───────────────────────────────────────────
        _connectionService = new ClaudeConnectionService();

        // ── 3. MCP server manager (created now, started lazily) ─────────────
        _mcpManager = new McpServerManager();

        // ── 4. Panel (empty — sessions restored lazily after init) ──────────
        _vm = new ClaudeAssistantPanelViewModel();
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
            openOptions: () => context.CommandRegistry?.Find("View.Options")?.Command.Execute(null),
            manageConnections: () => ShowConnectionManager());
        var titleBarUiId = context.UIRegistry.GenerateUIId(Id, "TitleBar", "Button");
        context.UIRegistry.RegisterTitleBarItem(titleBarUiId, Id, titleBarContributor);

        // ── 9. Connection status (titlebar only — no status bar item) ──────
        _connectionService.StatusChanged += (_, status) =>
        {
            _panel?.Dispatcher.InvokeAsync(() =>
            {
                // Auto-select claude-code if CLI is available and no API key configured
                if (status == ClaudeConnectionStatus.NotConfigured && !_shownConnectionManagerOnce)
                {
                    _shownConnectionManagerOnce = true;

                    if (ClaudeCodeModelProvider.FindClaudeExecutable() is not null && _vm?.ActiveTab is { } tab)
                    {
                        tab.SelectedProviderId = "claude-code";
                        tab.SelectedModelId = "sonnet";
                        context.Output?.Info("[ClaudeAssistant] Auto-selected Claude Code CLI (no API key needed)");
                    }
                    else
                    {
                        ShowConnectionManager();
                    }
                }
            });
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
            Id: "ClaudeAssistant.ManageConnections",
            Name: "Claude AI: Manage Connections",
            Category: "AI & Assistants",
            DefaultGesture: null,
            IconGlyph: "\uE8D7",
            Command: new RelayCommand(() => SafeGuard.Run(() => ShowConnectionManager()))));

        context.CommandRegistry?.Register(new SDK.Commands.SdkCommandDefinition(
            Id: "ClaudeAssistant.NewTab",
            Name: "Claude AI: New Conversation",
            Category: "AI & Assistants",
            DefaultGesture: "Ctrl+Shift+Alt+A",
            IconGlyph: "\uE710",
            Command: new RelayCommand(() => SafeGuard.Run(() => _vm?.CreateNewTabCommand.Execute(null)))));

        // ── Done (lightweight init) ─────────────────────────────────────────
        context.Output?.Info($"[ClaudeAssistant] Plugin initialized (v{Version}) — 4 providers");

        // ── Deferred heavy work (runs AFTER init measurement completes) ─────
        _ = _panel.Dispatcher.InvokeAsync(async () =>
        {
            _connectionService!.Start();
            try { await _mcpManager!.StartAllAsync(CancellationToken.None); }
            catch (Exception ex) { context.Output?.Warning($"[ClaudeAssistant] MCP startup: {ex.Message}"); }
            await PromptPresetsService.Instance.LoadAsync();
            await _vm!.RestoreSessionsAsync();
            context.Output?.Info($"[ClaudeAssistant] Deferred init done — {_mcpManager.GetAllTools().Count} MCP tools");
        }, System.Windows.Threading.DispatcherPriority.Background);
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
            manageConnections: () => ShowConnectionManager(),
            currentModel: currentModel,
            presets: PromptPresetsService.Instance.Presets);

        var owner = (_panel != null ? Window.GetWindow(_panel) : null)
                 ?? (anchor != null ? Window.GetWindow(anchor) : null)
                 ?? Application.Current.MainWindow;
        var paletteAnchor = _context?.UIRegistry.GetCommandPaletteAnchor();
        var palette = new ClaudeCommandPalette(entries, owner!, paletteAnchor);
        palette.Show();
    }

    private void ShowConnectionManager()
    {
        if (_vm?.ActiveTab?.Registry is not { } registry) return;

        var owner = (_panel != null ? Window.GetWindow(_panel) : null)
                 ?? Application.Current.MainWindow;
        var anchor = _context?.UIRegistry.GetCommandPaletteAnchor();
        var popup = new ConnectionManagerPopup(registry, owner, anchor);
        popup.Show();
    }

    private void ShowModelSwitcher()
    {
        if (_vm?.ActiveTab is not { } tab) return;

        var popup = new ModelSwitcherPopup(
            tab.Registry,
            tab.SelectedProviderId,
            tab.SelectedModelId,
            tab.ThinkingEnabled)
        {
            PlacementTarget = _panel,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Center
        };

        popup.SelectionCommitted += (_, _) => SafeGuard.Run(() =>
        {
            if (popup.SelectedProviderId is not null)
                tab.SelectedProviderId = popup.SelectedProviderId;
            if (popup.SelectedModelId is not null)
                tab.SelectedModelId = popup.SelectedModelId;
            tab.ThinkingEnabled = popup.ThinkingEnabled;
        });

        popup.Closed += (_, _) => SafeGuard.Run(() =>
        {
            tab.ThinkingEnabled = popup.ThinkingEnabled;
        });

        popup.IsOpen = true;
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
