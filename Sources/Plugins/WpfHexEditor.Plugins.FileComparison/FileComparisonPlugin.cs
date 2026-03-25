// ==========================================================
// Project: WpfHexEditor.Plugins.FileComparison
// File: FileComparisonPlugin.cs
// Description:
//     Plugin entry point for the File Comparison feature.
//     - DiffHubPanel is lazy: registered as a document tab on first demand (not at startup).
//     - "Compare with Another File…" uses a native OpenFileDialog (CommandRegistry is
//       unreachable from plugin ALC — DIM returns null).
//     - CompareCompleted → OpenDiffViewerTab; DiffHub panel stays hidden during normal compare.
//     - Deduplicates viewer tabs by left+right path pair (reuses existing tab).
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
/// DiffHub is opened lazily as a document tab (not a docked panel).
/// Comparison results always open as <see cref="DiffViewerDocument"/> tabs in the main area.
/// </summary>
public sealed class FileComparisonPlugin : IWpfHexEditorPlugin
{
    private const string PanelUiId = "WpfHexEditor.Plugins.FileComparison.Panel.FileComparisonPanel";

    private IIDEHostContext?  _context;
    private DiffHubPanel?     _panel;
    private bool              _diffHubDocumentOpen;

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

        // Lazy helper — registers DiffHub as a document tab on first call,
        // then activates it via ShowPanel on subsequent calls.
        void ShowDiffHubDocument()
        {
            if (!_diffHubDocumentOpen)
            {
                context.UIRegistry.RegisterDocumentTab(
                    PanelUiId,
                    _panel!,
                    Id,
                    new DocumentDescriptor
                    {
                        Title    = "Diff Hub",
                        CanClose = true
                    });
                _diffHubDocumentOpen = true;
            }
            else
            {
                context.UIRegistry.ShowPanel(PanelUiId);
            }
        }

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Diff Hub",
                ParentPath = "View",
                Group      = "FileTools",
                IconGlyph  = "\uE93D",
                Command    = new RelayCommand(_ => ShowDiffHubDocument())
            });

        context.UIRegistry.RegisterContextMenuContributor(Id,
            new SolutionExplorerCompareContributor(
                context,
                compareWithFile: async (left, right) =>
                {
                    if (!string.IsNullOrEmpty(right))
                    {
                        // Both paths known (git extraction etc.) → compare directly.
                        // CompareCompleted fires → OpenDiffViewerTab opens the viewer tab.
                        _panel!.OpenFiles(left, right);
                        return;
                    }

                    // Show VS Code-style in-IDE file picker.
                    // GetSolutionFilePaths() covers both WH (.whsln) and VS (.sln) solutions —
                    // VsSolutionLoader injects VS solutions into ISolutionManager.CurrentSolution.
                    var solutionFiles = context.SolutionExplorer.GetSolutionFilePaths();
                    var openFiles     = context.SolutionExplorer.GetOpenFilePaths();
                    var slnPath       = context.SolutionExplorer.ActiveSolutionPath;
                    var owner         = System.Windows.Application.Current.MainWindow;

                    var picked = await CompareFilePickerPopup.ShowAsync(owner, left, solutionFiles, openFiles, slnPath);
                    if (!string.IsNullOrEmpty(picked))
                        _panel!.OpenFiles(left, picked);
                    // CompareCompleted → OpenDiffViewerTab (DiffHub panel stays hidden)
                },
                compareWithActiveEditor: nodePath =>
                {
                    var activeDoc = context.DocumentHost?.Documents?.ActiveDocument?.FilePath;
                    if (!string.IsNullOrEmpty(activeDoc))
                    {
                        // Both files known → compare directly; viewer tab opens via CompareCompleted.
                        _panel!.OpenFiles(nodePath, activeDoc);
                    }
                    else
                    {
                        // No active editor — pre-fill File 1 and open the hub so user can pick File 2.
                        _panel!.SuggestFile1(nodePath);
                        ShowDiffHubDocument();
                    }
                }));

        context.HexEditor.FileOpened += OnHexFileOpened;

        context.ExtensionRegistry.Register<IDiffService>(
            Id,
            new DiffServiceAdapter(
                new DiffEngine(),
                _panel,
                PanelUiId,
                () => ShowDiffHubDocument()));

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
            _context.HexEditor.FileOpened -= OnHexFileOpened;

        _openViewers.Clear();
        _diffHubDocumentOpen = false;
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
