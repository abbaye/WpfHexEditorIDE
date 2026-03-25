// ==========================================================
// Project: WpfHexEditor.Plugins.FileComparison
// File: FileComparisonPlugin.cs
// Description:
//     Plugin entry point for the File Comparison feature.
//     - Registers the DiffHubPanel as a compact launcher panel (bottom dock).
//     - Subscribes to CompareCompleted → opens a DiffViewerDocument document tab.
//     - Deduplicates tabs by left+right path pair (reuses existing tab).
//     - Pre-fills File 1 with the active hex file for a one-click diff workflow.
//
// Architecture Notes:
//     Pattern: Observer — HexEditor.FileOpened drives SuggestFile1 (pre-fill only).
//     DiffViewerDocument tabs are keyed by "diff://{left}|{right}" for deduplication.
// ==========================================================

using System.IO;
using System.Windows.Threading;
using WpfHexEditor.Core.Diff.Models;
using WpfHexEditor.Core.Diff.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.FileComparison.Views;
using WpfHexEditor.Plugins.FileComparison.Services;
using WpfHexEditor.Plugins.FileComparison.ViewModels;

namespace WpfHexEditor.Plugins.FileComparison;

/// <summary>
/// Plugin entry point for the File Comparison feature.
/// The launcher panel stays docked at the bottom; comparison results open as
/// full document tabs (<see cref="DiffViewerDocument"/>).
/// </summary>
public sealed class FileComparisonPlugin : IWpfHexEditorPlugin
{
    private const string PanelUiId = "WpfHexEditor.Plugins.FileComparison.Panel.FileComparisonPanel";

    private IIDEHostContext?  _context;
    private DiffHubPanel?     _panel;

    // ── Tab deduplication ────────────────────────────────────────────────────
    private readonly Dictionary<string, DiffViewerDocument> _openViewers = new(StringComparer.OrdinalIgnoreCase);

    public string  Id      => "WpfHexEditor.Plugins.FileComparison";
    public string  Name    => "File Comparison";
    public Version Version => new(0, 3, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new DiffHubPanel();

        // Wire compare-completed → open document tab
        _panel.CompareCompleted += (_, result) => OpenDiffViewerTab(result);

        // Legacy "Open in Viewer" (used by DiffServiceAdapter / terminal diff-open):
        // simply run a fresh compare and the tab will open via CompareCompleted.

        context.UIRegistry.RegisterPanel(
            PanelUiId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Diff Hub",
                DefaultDockSide = "Bottom",
                DefaultAutoHide = true,
                CanClose        = true
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Diff Hub",
                ParentPath = "View",
                Group      = "FileTools",
                IconGlyph  = "\uE93D",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(PanelUiId))
            });

        context.UIRegistry.RegisterContextMenuContributor(Id,
            new SolutionExplorerCompareContributor(
                context,
                compareWithFile: (left, right) =>
                {
                    if (!string.IsNullOrEmpty(right))
                    {
                        _panel!.OpenFiles(left, right);
                        context.UIRegistry.ShowPanel(PanelUiId);
                    }
                    else
                    {
                        var cmd = context.CommandRegistry?.Find("View.CompareFiles")?.Command;
                        if (cmd is not null)
                            cmd.Execute(left);
                        else
                        {
                            _panel!.SuggestFile1(left);
                            context.UIRegistry.ShowPanel(PanelUiId);
                        }
                    }
                },
                compareWithActiveEditor: nodePath =>
                {
                    var activeDoc = context.DocumentHost?.Documents?.ActiveDocument?.FilePath;
                    if (!string.IsNullOrEmpty(activeDoc))
                        _panel!.OpenFiles(nodePath, activeDoc);
                    else
                        _panel!.SuggestFile1(nodePath);
                    context.UIRegistry.ShowPanel(PanelUiId);
                }));

        context.HexEditor.FileOpened += OnHexFileOpened;

        context.ExtensionRegistry.Register<IDiffService>(
            Id,
            new DiffServiceAdapter(
                new DiffEngine(),
                _panel,
                PanelUiId,
                () => context.UIRegistry.ShowPanel(PanelUiId)));

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
            _context.HexEditor.FileOpened -= OnHexFileOpened;

        _openViewers.Clear();
        _panel   = null;
        _context = null;
        return Task.CompletedTask;
    }

    // ── DiffViewerDocument tab management ────────────────────────────────────

    private void OpenDiffViewerTab(DiffEngineResult result)
    {
        if (_context is null) return;

        var uiId = $"diff://{result.LeftPath}|{result.RightPath}";

        if (_openViewers.TryGetValue(uiId, out var existing))
        {
            // Refresh the existing tab with the latest result
            existing.LoadResult(result);
            _context.UIRegistry.ShowPanel(uiId);
            return;
        }

        var vm  = new DiffViewerViewModel(result);
        var doc = new DiffViewerDocument(vm);

        var leftName  = Path.GetFileName(result.LeftPath);
        var rightName = Path.GetFileName(result.RightPath);

        _context.UIRegistry.RegisterDocumentTab(
            uiId,
            doc,
            Id,
            new DocumentDescriptor
            {
                Title    = $"{leftName} \u2194 {rightName}",
                ToolTip  = $"{result.LeftPath}  \u2194  {result.RightPath}",
                CanClose = true
            });

        _openViewers[uiId] = doc;
    }

    // ── Event handler ─────────────────────────────────────────────────────────

    private void OnHexFileOpened(object? sender, EventArgs e)
    {
        if (_panel is null || _context is null) return;
        var path = _context.HexEditor.CurrentFilePath;
        if (string.IsNullOrEmpty(path)) return;

        _panel.Dispatcher.BeginInvoke(
            () => _panel.SuggestFile1(path),
            DispatcherPriority.Background);
    }
}
