// Project      : WpfHexEditorControl
// File         : ArchiveExplorerPlugin.cs
// Description  : Entry point for the Archive Explorer plugin.
//                Implements IWpfHexEditorPlugin + IPluginWithOptions.
//                Registers the dockable panel, 2 IDE menu items, and
//                a Solution Explorer context menu contributor.
//                Auto-shows the panel when a supported archive file is opened
//                (controlled by AutoShowOnArchiveOpen setting).
//
// Architecture : Observer pattern — subscribes to IHexEditorService.FileOpened.
//                Panel is left-docked, auto-hide.  Previews reuse existing
//                document editors via IDocumentHostService.
//
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6

using System.Windows;
using WpfHexEditor.Plugins.ArchiveExplorer.Options;
using WpfHexEditor.Plugins.ArchiveExplorer.Services;
using WpfHexEditor.Plugins.ArchiveExplorer.Views;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.ArchiveExplorer;

/// <summary>
/// Entry point for the Archive Explorer plugin.
/// Provides ZIP / 7z / RAR / TAR / GZip / BZip2 / XZ browsing directly inside the IDE.
/// </summary>
public sealed class ArchiveExplorerPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string  Id      => "WpfHexEditor.Plugins.ArchiveExplorer";
    public string  Name    => "Archive Explorer";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true,
        AccessSettings   = true
    };

    // ── UI IDs ────────────────────────────────────────────────────────────────

    private const string PanelUiId = "WpfHexEditor.Plugins.ArchiveExplorer.Panel.Main";

    // ── State ─────────────────────────────────────────────────────────────────

    private ArchiveExplorerPanel?       _panel;
    private IIDEHostContext?            _context;
    private ArchiveExplorerOptionsPage? _optionsPage;
    private readonly CancellationTokenSource _cts = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // Build panel with required services
        _panel = new ArchiveExplorerPanel(context.DocumentHost, context.Output);
        ApplyCurrentOptions();

        // Register dockable panel (left, auto-hide)
        context.UIRegistry.RegisterPanel(
            PanelUiId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Archive Explorer",
                DefaultDockSide = "Left",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 300
            });

        // Register IDE menu items
        RegisterMenuItems(context);

        // Register Solution Explorer contributor
        context.UIRegistry.RegisterContextMenuContributor(
            Id,
            new ArchiveSolutionExplorerContributor(context, this));

        // Subscribe to HexEditor file-open for auto-show (optional — plugin works standalone too)
        if (context.HexEditor is not null)
            context.HexEditor.FileOpened += OnFileOpened;

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _cts.Cancel();

        if (_context?.HexEditor is not null)
            _context.HexEditor.FileOpened -= OnFileOpened;

        _panel       = null;
        _context     = null;
        _optionsPage = null;

        return Task.CompletedTask;
    }

    // ── Internal: load archive (called by menu + contributor) ─────────────────

    internal async Task OpenArchiveAsync(string path)
    {
        if (_panel is null || _context is null) return;
        _context.UIRegistry.ShowPanel(PanelUiId);
        await _panel.LoadArchiveAsync(path, _cts.Token);
    }

    // ── HexEditor event handler ───────────────────────────────────────────────

    private void OnFileOpened(object? sender, EventArgs e)
    {
        if (!ArchiveExplorerOptions.Instance.AutoShowOnArchiveOpen) return;
        var path = _context?.HexEditor.CurrentFilePath;
        if (string.IsNullOrEmpty(path)) return;
        if (!ArchiveReaderFactory.IsSupported(path)) return;

        _ = OpenArchiveAsync(path);
    }

    // ── Menu items ────────────────────────────────────────────────────────────

    private void RegisterMenuItems(IIDEHostContext context)
    {
        // View > Archive Explorer (toggle panel)
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.TogglePanel",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Archive Explorer",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE7C3",
                ToolTip    = "Show or hide the Archive Explorer panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(PanelUiId))
            });

        // Tools > Open Archive… (Ctrl+Shift+R)
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.OpenArchive",
            Id,
            new MenuItemDescriptor
            {
                Header      = "Open _Archive\u2026",
                ParentPath  = "Tools",
                Group       = "ArchiveExplorer",
                IconGlyph   = "\uE7C3",
                GestureText = "Ctrl+Shift+R",
                ToolTip     = "Open an archive file in the Archive Explorer",
                Command     = new RelayCommand(_ => OnOpenArchiveDialog())
            });
    }

    private void OnOpenArchiveDialog()
    {
        var exts = string.Join(";", ArchiveReaderFactory.SupportedExtensions.Select(e => $"*{e}"));
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Open Archive",
            Filter = $"Archive files ({exts})|{exts}|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            _ = OpenArchiveAsync(dlg.FileName);
    }

    // ── IPluginWithOptions ────────────────────────────────────────────────────

    public FrameworkElement CreateOptionsPage()
    {
        _optionsPage = new ArchiveExplorerOptionsPage();
        _optionsPage.Load();
        return _optionsPage;
    }

    public void SaveOptions()
    {
        _optionsPage?.Save();
        ApplyCurrentOptions();
    }

    public void LoadOptions()
    {
        ArchiveExplorerOptions.Invalidate();
        _optionsPage?.Load();
    }

    public string GetOptionsCategory()     => "Archive Explorer";
    public string GetOptionsCategoryIcon() => "\uE7C3";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ApplyCurrentOptions()
    {
        if (_panel is null) return;
        var opts = ArchiveExplorerOptions.Instance;
        _panel.ApplyOptions(
            opts.ShowCompressionRatio,
            opts.ShowFormatBadge,
            opts.MaxFormatDetectionSizeKb,
            opts.PreviewMaxSizeKb);
    }
}

// ─── Solution Explorer contributor ────────────────────────────────────────────

/// <summary>
/// Contributes "Open in Archive Explorer" to the Solution Explorer context menu
/// for any supported archive file node.
/// </summary>
internal sealed class ArchiveSolutionExplorerContributor : ISolutionExplorerContextMenuContributor
{
    private readonly IIDEHostContext       _context;
    private readonly ArchiveExplorerPlugin _plugin;

    public ArchiveSolutionExplorerContributor(IIDEHostContext context, ArchiveExplorerPlugin plugin)
    {
        _context = context;
        _plugin  = plugin;
    }

    public IReadOnlyList<SolutionContextMenuItem> GetContextMenuItems(string nodeKind, string? nodePath)
    {
        if (nodeKind != "File" || string.IsNullOrEmpty(nodePath)) return [];
        if (!ArchiveReaderFactory.IsSupported(nodePath))           return [];

        return
        [
            SolutionContextMenuItem.Item(
                "Open in Archive Explorer",
                new RelayCommand(_ => _ = _plugin.OpenArchiveAsync(nodePath!)),
                iconGlyph: "\uE7C3")
        ];
    }
}
