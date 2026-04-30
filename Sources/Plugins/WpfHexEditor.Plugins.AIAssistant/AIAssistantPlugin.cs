// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AIAssistantPlugin.cs
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
using WpfHexEditor.Plugins.AIAssistant.Commands.Terminal;
using WpfHexEditor.Plugins.AIAssistant.Connection;
using WpfHexEditor.Plugins.AIAssistant.ContextMenu;
using WpfHexEditor.Plugins.AIAssistant.Mcp.Host;
using WpfHexEditor.Plugins.AIAssistant.Options;
using WpfHexEditor.Plugins.AIAssistant.Panel;
using WpfHexEditor.Plugins.AIAssistant.Panel.CommandPalette;
using WpfHexEditor.Plugins.AIAssistant.Panel.ModelSwitcher;
using WpfHexEditor.Plugins.AIAssistant.Presets;
using WpfHexEditor.Plugins.AIAssistant.TitleBar;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.Plugins.AIAssistant.Panel.AccountUsage;
using WpfHexEditor.Plugins.AIAssistant.Panel.ConnectionManager;
using WpfHexEditor.Plugins.AIAssistant.Providers.ClaudeCode;
using WpfHexEditor.Plugins.AIAssistant.Properties;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.AIAssistant;

public sealed class AIAssistantPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public string Id => "WpfHexEditor.Plugins.AIAssistant";
    public string Name => AIAssistantResources.AIAssistant_PluginName;
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
    private AIAssistantPanel? _panel;
    private AIAssistantPanelViewModel? _vm;
    private AIConnectionService? _connectionService;
    private McpServerManager? _mcpManager;
    private string? _panelUiId;
    private bool _shownConnectionManagerOnce;

    public async Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // ── 0. Shared services ──────────────────────────────────────────────
        SafeGuard.SetLogger(msg => context.Output?.Error(msg));
        Panel.Messages.ChatCodeBlockCanvas.SyntaxColoringService = context.SyntaxColoring;

        // ── 0b. Migrate legacy "Claude" settings dir → "AIAssistant" ────────
        var oldDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexEditor", "Claude");
        var newDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexEditor", "AIAssistant");
        if (System.IO.Directory.Exists(oldDir) && !System.IO.Directory.Exists(newDir))
            System.IO.Directory.Move(oldDir, newDir);

        // ── 1. Options ──────────────────────────────────────────────────────
        AIAssistantOptions.Instance.Load();

        // ── 2. Connection monitor ───────────────────────────────────────────
        _connectionService = new AIConnectionService();

        // ── 3. MCP server manager (created now, started lazily) ─────────────
        _mcpManager = new McpServerManager();

        // ── 4. Panel (empty — sessions restored lazily after init) ──────────
        _vm = new AIAssistantPanelViewModel();
        _panel = new AIAssistantPanel { DataContext = _vm };

        // ── 6. Register dockable panel ──────────────────────────────────────
        _panelUiId = context.UIRegistry.GenerateUIId(Id, "Panel", "AIAssistant");
        context.UIRegistry.RegisterPanel(_panelUiId, _panel, Id, new PanelDescriptor
        {
            Title = AIAssistantResources.AIAssistant_PluginName,
            DefaultDockSide = "Right",
            CanClose = true,
            PreferredWidth = 420,
            Category = "AI & Assistants"
        });

        // ── 7. View menu ────────────────────────────────────────────────────
        var menuUiId = context.UIRegistry.GenerateUIId(Id, "Menu", "Toggle");
        context.UIRegistry.RegisterMenuItem(menuUiId, Id, new MenuItemDescriptor
        {
            Header = "_AI Assistant",
            ParentPath = "View",
            GestureText = "Ctrl+Shift+A",
            IconGlyph = "\uE734",
            Command = new RelayCommand(() => context.UIRegistry.TogglePanel(_panelUiId!)),
            Category = "AI & Assistants"
        });

        // ── 8. Titlebar button ──────────────────────────────────────────────
        var titleBarContributor = new AITitleBarContributor(
            _connectionService,
            showCommandPalette: anchor => ShowCommandPalette(anchor),
            newTab: () => _vm?.CreateNewTabCommand.Execute(null),
            fixErrors: () => SendQuickAction("@selection @errors Fix the errors in this code."),
            openOptions: () => context.CommandRegistry?.Find("View.Options")?.Command.Execute(null),
            manageConnections: () => ShowConnectionManager(),
            accountUsage: () => ShowAccountUsage());
        var titleBarUiId = context.UIRegistry.GenerateUIId(Id, "TitleBar", "Button");
        context.UIRegistry.RegisterTitleBarItem(titleBarUiId, Id, titleBarContributor);

        // ── 9. Connection status (titlebar only — no status bar item) ──────
        _connectionService.StatusChanged += (_, status) =>
        {
            _panel?.Dispatcher.InvokeAsync(() =>
            {
                // Auto-select claude-code if CLI is available and no API key configured
                if (status == AIConnectionStatus.NotConfigured && !_shownConnectionManagerOnce)
                {
                    _shownConnectionManagerOnce = true;

                    if (ClaudeCodeModelProvider.FindClaudeExecutable() is not null && _vm?.ActiveTab is { } tab)
                    {
                        tab.SelectedProviderId = "claude-code";
                        tab.SelectedModelId = "sonnet";
                        context.Output?.Info("[AIAssistant] Auto-selected Claude Code CLI (no API key needed)");
                    }
                    else
                    {
                        ShowConnectionManager();
                    }
                }
            });
        };

        // ── 10. Terminal commands ───────────────────────────────────────────
        context.Terminal?.RegisterCommand(new AIAskCommand(() => _vm!));
        context.Terminal?.RegisterCommand(new AIExplainCommand(() => _vm!, context));
        context.Terminal?.RegisterCommand(new AIFixCommand(() => _vm!, context));
        context.Terminal?.RegisterCommand(new AINewTabCommand(() => _vm!));

        // ── 11. SolutionExplorer context menu ───────────────────────────────
        context.UIRegistry.RegisterContextMenuContributor(Id, new AISolutionExplorerContributor(() => _vm!));

        // ── 12. Command palette shortcut (Ctrl+Shift+A) ────────────────────
        context.CommandRegistry?.Register(new SDK.Commands.SdkCommandDefinition(
            Id: "AIAssistant.CommandPalette",
            Name: "AI: Command Palette",
            Category: "AI & Assistants",
            DefaultGesture: "Ctrl+Shift+A",
            IconGlyph: "\uE734",
            Command: new RelayCommand(() => SafeGuard.Run(() => ShowCommandPalette()))));

        context.CommandRegistry?.Register(new SDK.Commands.SdkCommandDefinition(
            Id: "AIAssistant.ManageConnections",
            Name: "AI: Manage Connections",
            Category: "AI & Assistants",
            DefaultGesture: null,
            IconGlyph: "\uE8D7",
            Command: new RelayCommand(() => SafeGuard.Run(() => ShowConnectionManager()))));

        context.CommandRegistry?.Register(new SDK.Commands.SdkCommandDefinition(
            Id: "AIAssistant.AccountUsage",
            Name: "AI: Account & Usage",
            Category: "AI & Assistants",
            DefaultGesture: null,
            IconGlyph: "\uE77B",
            Command: new RelayCommand(() => SafeGuard.Run(() => ShowAccountUsage()))));

        context.CommandRegistry?.Register(new SDK.Commands.SdkCommandDefinition(
            Id: "AIAssistant.NewTab",
            Name: "AI: New Conversation",
            Category: "AI & Assistants",
            DefaultGesture: "Ctrl+Shift+Alt+A",
            IconGlyph: "\uE710",
            Command: new RelayCommand(() => SafeGuard.Run(() => _vm?.CreateNewTabCommand.Execute(null)))));

        // ── Wire panel toolbar events ───────────────────────────────────────
        _panel.ShowModelSwitcherRequested += anchor => SafeGuard.Run(() => ShowModelSwitcher(anchor));
        _panel.ManageConnectionsRequested += () => SafeGuard.Run(ShowConnectionManager);
        _panel.OpenOptionsRequested += () => SafeGuard.Run(
            () => context.CommandRegistry?.Find("View.Options")?.Command.Execute(null));

        // ── Done (lightweight init) ─────────────────────────────────────────
        context.Output?.Info($"[AIAssistant] Plugin initialized (v{Version}) — 5 providers");

        // ── Deferred heavy work (runs AFTER init measurement completes) ─────
        _ = _panel.Dispatcher.InvokeAsync(async () =>
        {
            _connectionService!.Start();
            try { await _mcpManager!.StartAllAsync(CancellationToken.None); }
            catch (Exception ex) { context.Output?.Warning($"[AIAssistant] MCP startup: {ex.Message}"); }
            await PromptPresetsService.Instance.LoadAsync();
            await _vm!.RestoreSessionsAsync();

            // Warm up Claude CLI in background (reduces cold start on first message)
            if (ClaudeCodeModelProvider.FindClaudeExecutable() is { } cliPath)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo(cliPath, "-p --output-format json \"warmup\"")
                        { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, UseShellExecute = false };
                        using var p = System.Diagnostics.Process.Start(psi);
                        p?.WaitForExit(10000);
                    }
                    catch { /* warm-up is best-effort */ }
                });
            }

            context.Output?.Info($"[AIAssistant] Deferred init done — {_mcpManager.GetAllTools().Count} MCP tools");
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        // Unregister terminal commands
        _context?.Terminal?.UnregisterCommand("ai-ask");
        _context?.Terminal?.UnregisterCommand("ai-explain");
        _context?.Terminal?.UnregisterCommand("ai-fix");
        _context?.Terminal?.UnregisterCommand("ai-new-tab");

        // Save and cleanup
        _connectionService?.Dispose();
        if (_mcpManager is not null)
            await _mcpManager.DisposeAsync();
        if (_vm is not null)
            await _vm.SaveAllSessionsAsync();
        await PromptPresetsService.Instance.SaveAsync();
        AIAssistantOptions.Instance.Save();

        _context?.Output?.Info("[AIAssistant] Plugin shutdown.");
    }

    private void ShowCommandPalette(UIElement? anchor = null)
    {
        var currentModel = _vm?.ActiveTab is { } tab
            ? $"{tab.SelectedProviderId} / {tab.SelectedModelId}"
            : null;

        var entries = AICommandPalette.BuildDefaultCatalog(
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
        var palette = new AICommandPalette(entries, owner!, paletteAnchor);
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

    private void ShowAccountUsage()
    {
        var providerId = _vm?.ActiveTab?.SelectedProviderId ?? "anthropic";
        var owner = (_panel != null ? Window.GetWindow(_panel) : null)
                 ?? Application.Current.MainWindow;
        var anchor = _context?.UIRegistry.GetCommandPaletteAnchor();
        var popup = new AccountUsagePopup(providerId, owner, anchor);
        popup.Show();
    }

    private void ShowModelSwitcher(UIElement? anchor = null)
    {
        if (_vm?.ActiveTab is not { } tab) return;

        var popup = new ModelSwitcherPopup(
            tab.Registry,
            tab.SelectedProviderId,
            tab.SelectedModelId,
            tab.ThinkingEnabled)
        {
            PlacementTarget = anchor ?? _panel,
            Placement = anchor is not null
                ? System.Windows.Controls.Primitives.PlacementMode.Bottom
                : System.Windows.Controls.Primitives.PlacementMode.Center
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
    public FrameworkElement CreateOptionsPage() => new AIAssistantOptionsPage();
    public void SaveOptions() => AIAssistantOptions.Instance.Save();
    public void LoadOptions() => AIAssistantOptions.Instance.Load();
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
