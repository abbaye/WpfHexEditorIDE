// Project      : WpfHexEditorControl
// File         : Views/ArchiveExplorerPanel.xaml.cs
// Description  : Code-behind for the Archive Explorer dockable panel.
//                Initializes ToolbarOverflowManager (3 collapsible groups),
//                wires drag-drop from TreeView, and handles ViewModel events.
// Architecture : Thin code-behind; all logic in ArchiveExplorerViewModel.
//                ToolbarOverflowManager group collapse order (index 0 = first):
//                  TbgExtract → TbgActions → TbgFilter (last to collapse)
//
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Plugins.ArchiveExplorer.Services;
using WpfHexEditor.Plugins.ArchiveExplorer.ViewModels;
using WpfHexEditor.Plugins.ArchiveExplorer.Views.Dialogs;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.ArchiveExplorer.Views;

/// <summary>
/// VS-style dockable panel for the Archive Explorer plugin.
/// </summary>
public partial class ArchiveExplorerPanel : UserControl
{
    private ToolbarOverflowManager? _overflowManager;
    private Point                   _dragStartPoint;
    private bool                    _isDragging;

    public ArchiveExplorerViewModel ViewModel { get; }

    // ── Constructor ────────────────────────────────────────────────────────
    public ArchiveExplorerPanel(
        IDocumentHostService documentHost,
        IOutputService       output)
    {
        InitializeComponent();
        ViewModel   = new ArchiveExplorerViewModel();
        DataContext = ViewModel;

        var preview = new PreviewService(documentHost);
        var extract = new ExtractService(output);
        ViewModel.SetServices(preview, extract);

        ViewModel.PropertiesRequested += OnPropertiesRequested;
    }

    // ── Public API (called by plugin entry point) ──────────────────────────

    /// <summary>Loads an archive into the panel asynchronously.</summary>
    public async Task LoadArchiveAsync(string archivePath, CancellationToken ct = default)
    {
        if (string.Equals(ViewModel.CurrentArchivePath, archivePath, StringComparison.OrdinalIgnoreCase))
            return; // already loaded
        await ViewModel.LoadArchiveAsync(archivePath, ct);
    }

    /// <summary>Applies settings from the options page.</summary>
    public void ApplyOptions(bool showRatio, bool showBadge, int maxBadgeKb, int previewMaxKb)
    {
        ViewModel.ShowCompressionRatio  = showRatio;
        ViewModel.ShowFormatBadge       = showBadge;
        ViewModel.MaxFormatDetectionKb  = maxBadgeKb;
        ViewModel.PreviewMaxSizeKb      = previewMaxKb;
    }

    // ── Loaded / Overflow ──────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _overflowManager = new ToolbarOverflowManager(
            toolbarContainer:      ToolbarBorder,
            alwaysVisiblePanel:    ToolbarRightPanel,
            overflowButton:        ToolbarOverflowButton,
            overflowMenu:          (ContextMenu)Resources["OverflowContextMenu"],
            groupsInCollapseOrder: [TbgExtract, TbgActions, TbgFilter]);

        Dispatcher.InvokeAsync(
            _overflowManager.CaptureNaturalWidths,
            DispatcherPriority.Loaded);
    }

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
            _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        var menu = (ContextMenu)Resources["OverflowContextMenu"];
        menu.PlacementTarget = ToolbarOverflowButton;
        menu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen          = true;
        _overflowManager?.SyncMenuVisibility();
    }

    // ── Tree events ────────────────────────────────────────────────────────

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ArchiveNodeViewModel vm)
            ViewModel.SelectedNode = vm;
    }

    // ── Drag-Drop ──────────────────────────────────────────────────────────

    private async void OnTreeMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (ViewModel.SelectedNode is not { IsFolder: false } node) return;
        if (node.Node.Entry is null) return;

        var pos   = e.GetPosition(ArchiveTree);
        var delta = pos - _dragStartPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (_isDragging) return;
        _isDragging = true;

        // Capture UI-thread values before the await suspends.
        var entry       = node.Node.Entry;
        var nodeName    = node.Name;
        var archivePath = ViewModel.CurrentArchivePath;
        var tempRoot    = Path.Combine(Path.GetTempPath(), "WpfHexEditor", "ArchiveExplorer", "drag");
        var destPath    = Path.Combine(tempRoot, nodeName);

        try
        {
            if (!File.Exists(destPath) && archivePath is not null)
            {
                // Extract on thread pool — no SynchronizationContext inside Task.Run,
                // so ConfigureAwait(false) inside ExtractEntryAsync works without deadlock.
                // The await here (without ConfigureAwait(false)) resumes on the UI thread.
                await Task.Run(async () =>
                {
                    Directory.CreateDirectory(tempRoot);
                    using var reader = Services.ArchiveReaderFactory.CreateReader(archivePath);
                    if (reader is not null)
                        await reader.ExtractEntryAsync(entry, destPath).ConfigureAwait(false);
                });
            }

            // Back on UI thread — DoDragDrop must be called here.
            if (File.Exists(destPath))
            {
                var data = new DataObject(DataFormats.FileDrop, new[] { destPath });
                DragDrop.DoDragDrop(ArchiveTree, data, DragDropEffects.Copy);
            }
        }
        finally { _isDragging = false; }
    }

    // ── Panel-level drop (open archive by dropping onto the panel) ────────────

    private void OnPanelDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length == 1 && ArchiveReaderFactory.IsSupported(files[0]))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPanelDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (files?.Length != 1) return;
        var path = files[0];
        if (!ArchiveReaderFactory.IsSupported(path)) return;
        _ = LoadArchiveAsync(path);
    }

    // ── Properties dialog ──────────────────────────────────────────────────

    private void OnPropertiesRequested(object? sender, ArchiveNodeViewModel vm)
    {
        var dlg = new ArchivePropertiesDialog(vm.Node) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
    }
}
