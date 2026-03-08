// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: AssemblyExplorerPlugin.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Official plugin entry point for the .NET Assembly Explorer.
//     Implements IWpfHexEditorPlugin + IPluginWithOptions.
//     Registers the panel, 3 menu items, and 2 status bar items.
//     Wires HexEditor FileOpened / ActiveEditorChanged events for auto-analysis.
//
// Architecture Notes:
//     Pattern: Observer — subscribes to IHexEditorService events.
//     All UI constructed and registered on the calling thread (UI thread).
//     UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost on unload.
//
//     IDE menu integration:
//       View > "_Assembly Explorer"    (Panels group)
//       Tools > "_Analyze Assembly"    (AssemblyExplorer group, Ctrl+Shift+A)
//       Edit  > "Go to _Metadata Token…" (AssemblyExplorer group)
//
//     Status bar:
//       Right-aligned, order 20: "Assembly: {name} v{version}"  (or "No assembly loaded")
//       Right-aligned, order 21: "{typeCount} types | {methodCount} methods"
// ==========================================================

using System.Windows;
using WpfHexEditor.Plugins.AssemblyExplorer.Options;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;
using WpfHexEditor.Plugins.AssemblyExplorer.Views;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer;

/// <summary>
/// Entry point for the official Assembly Explorer plugin.
/// Discovers .NET / native PE metadata and displays a VS-Like explorer tree.
/// </summary>
public sealed class AssemblyExplorerPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string  Id      => "WpfHexEditor.Plugins.AssemblyExplorer";
    public string  Name    => "Assembly Explorer";
    public Version Version => new(0, 1, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true,
        AccessSettings   = true
    };

    // ── UI ID constants ───────────────────────────────────────────────────────

    private const string PanelUiId         = "WpfHexEditor.Plugins.AssemblyExplorer.Panel.Main";
    private const string StatusAssemblyId  = "WpfHexEditor.Plugins.AssemblyExplorer.StatusBar.Assembly";
    private const string StatusTypeCountId = "WpfHexEditor.Plugins.AssemblyExplorer.StatusBar.TypeCount";

    // ── State ─────────────────────────────────────────────────────────────────

    private AssemblyExplorerPanel? _panel;
    private IIDEHostContext?       _context;

    private StatusBarItemDescriptor? _sbAssembly;
    private StatusBarItemDescriptor? _sbTypeCount;
    private AssemblyExplorerOptionsPage? _optionsPage;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // Build internal services
        var offsetResolver  = new PeOffsetResolver();
        var analysisService = new AssemblyAnalysisService(offsetResolver);
        var decompiler      = new DecompilerService();

        // Build panel with all dependencies injected
        _panel = new AssemblyExplorerPanel(
            analysisService, offsetResolver, decompiler,
            context.HexEditor, context.Output, context.EventBus);

        _panel.SetContext(context);

        // Register the dockable panel (left-docked, VS-Like)
        context.UIRegistry.RegisterPanel(
            PanelUiId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Assembly Explorer",
                DefaultDockSide = "Left",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredWidth  = 280
            });

        // Register status bar items
        _sbAssembly = new StatusBarItemDescriptor
        {
            Text      = "No assembly loaded",
            Alignment = StatusBarAlignment.Right,
            Order     = 20,
            ToolTip   = "Assembly currently loaded in the Assembly Explorer"
        };
        _sbTypeCount = new StatusBarItemDescriptor
        {
            Text      = string.Empty,
            Alignment = StatusBarAlignment.Right,
            Order     = 21
        };
        context.UIRegistry.RegisterStatusBarItem(StatusAssemblyId,  Id, _sbAssembly);
        context.UIRegistry.RegisterStatusBarItem(StatusTypeCountId, Id, _sbTypeCount);

        // Register menu items
        RegisterMenuItems(context);

        // Subscribe to HexEditor events for auto-analysis
        context.HexEditor.FileOpened          += OnFileOpened;
        context.HexEditor.ActiveEditorChanged += OnActiveEditorChanged;

        // Wire ViewModel events → status bar + EventBus
        _panel.ViewModel.AssemblyLoaded += OnAssemblyLoaded;

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
        {
            _context.HexEditor.FileOpened          -= OnFileOpened;
            _context.HexEditor.ActiveEditorChanged -= OnActiveEditorChanged;
        }

        if (_panel?.ViewModel is not null)
            _panel.ViewModel.AssemblyLoaded -= OnAssemblyLoaded;

        _panel      = null;
        _context    = null;
        _optionsPage = null;

        return Task.CompletedTask;
    }

    // ── HexEditor event handlers ──────────────────────────────────────────────

    private void OnFileOpened(object? sender, EventArgs e)
    {
        if (!AssemblyExplorerOptions.Instance.AutoAnalyzeOnFileOpen) return;

        var path = _context?.HexEditor.CurrentFilePath;
        if (!string.IsNullOrEmpty(path))
            _ = _panel?.ViewModel.LoadAssemblyAsync(path);
    }

    private void OnActiveEditorChanged(object? sender, EventArgs e)
    {
        var path = _context?.HexEditor.CurrentFilePath;
        if (!string.IsNullOrEmpty(path) && _panel is not null)
            _ = _panel.ViewModel.LoadAssemblyAsync(path);
    }

    // ── Assembly loaded → status bar update ──────────────────────────────────

    private void OnAssemblyLoaded(
        object? sender,
        Events.AssemblyLoadedEvent evt)
    {
        if (_sbAssembly is not null)
            _sbAssembly.Text = evt.IsManaged
                ? $"Assembly: {evt.Name} v{evt.Version}"
                : $"Native PE: {evt.Name}";

        if (_sbTypeCount is not null)
            _sbTypeCount.Text = evt.IsManaged
                ? $"{evt.TypeCount} types | {evt.MethodCount} methods"
                : $"{_context?.HexEditor.FileSize:N0} bytes";
    }

    // ── Menu items ────────────────────────────────────────────────────────────

    private void RegisterMenuItems(IIDEHostContext context)
    {
        // View > Assembly Explorer
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.TogglePanel",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Assembly Explorer",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE8A5",
                ToolTip    = "Show or hide the Assembly Explorer panel",
                Command    = new RelayCommand(
                    _ => context.UIRegistry.TogglePanel(PanelUiId))
            });

        // Tools > Analyze Assembly  (Ctrl+Shift+A)
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.AnalyzeAssembly",
            Id,
            new MenuItemDescriptor
            {
                Header      = "_Analyze Assembly",
                ParentPath  = "Tools",
                Group       = "AssemblyExplorer",
                IconGlyph   = "\uE8F4",
                GestureText = "Ctrl+Shift+A",
                ToolTip     = "Analyze the currently open file in the Assembly Explorer",
                Command     = new RelayCommand(
                    _ =>
                    {
                        var path = context.HexEditor.CurrentFilePath;
                        if (!string.IsNullOrEmpty(path))
                            _ = _panel?.ViewModel.LoadAssemblyAsync(path);
                        context.UIRegistry.ShowPanel(PanelUiId);
                    },
                    _ => context.HexEditor.IsActive)
            });

        // Edit > Go to Metadata Token…
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.GoToToken",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Go to _Metadata Token\u2026",
                ParentPath = "Edit",
                Group      = "AssemblyExplorer",
                IconGlyph  = "\uE9D2",
                ToolTip    = "Navigate to a metadata token — coming in a future release",
                Command    = new RelayCommand(
                    _ => MessageBox.Show(
                        "Go to Metadata Token — Coming in a future release.",
                        "Assembly Explorer",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information))
            });
    }

    // ── IPluginWithOptions ────────────────────────────────────────────────────

    public FrameworkElement CreateOptionsPage()
    {
        _optionsPage = new AssemblyExplorerOptionsPage();
        _optionsPage.Load();
        return _optionsPage;
    }

    public void SaveOptions()
    {
        _optionsPage?.Save();
        _panel?.ApplyOptions();
    }

    public void LoadOptions()
    {
        AssemblyExplorerOptions.Invalidate();
        _optionsPage?.Load();
    }
}
