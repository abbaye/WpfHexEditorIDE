// ==========================================================
// Project: WpfHexEditor.Plugins.DataInspector
// File: DataInspectorPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Unified Data Inspector panel — ByteChart + byte interpretations.
//     Chart position is configurable (Left/Right/Top/Bottom) and persisted.
//
// Architecture Notes:
//     Pattern: Observer — driven by DataInspectorPlugin via SetContext/
//     OnHexEditorSelectionChanged.  Interpretation bindings delegate to
//     DataInspectorViewModel (MVVM). Chart and zoom state are managed
//     entirely in code-behind for performance.
//
//     Layout model:
//       RebuildLayout(ChartPosition) dynamically configures MainAreaGrid
//       ColumnDefinitions/RowDefinitions and Grid.Row/Col attached properties
//       on ChartContainer, ChartSplitter, and ListContainer.
//       Called on Loaded and when the user changes the Layout toolbar combo.
//
//     Zoom model:
//       _zoomLevel 1x  = all 256 bars fill the chart width
//       _zoomLevel Nx  = chart.Width = scrollViewer.Width * N  (scrollbar appears)
//       BarChartPanel.ZoomToRange narrows the rendered byte range
//       Drag rubber band → zoom to selected byte range
//       Mouse wheel → zoom in/out
//
//     Scope model:
//       ActiveView — ReadBytes(FirstVisible, viewport width)   instant, reactive on scroll
//       Selection  — GetSelectedBytes() / ReadBytes(sel range) async if > 1 MB
//       WholeFile  — ReadBytes(0, ≤4 MB)      async with progress overlay if > 1 MB
// ==========================================================

using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Plugins.DataInspector.Options;
using WpfHexEditor.Plugins.DataInspector.Properties;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.DataInspector.Views;

/// <summary>
/// Unified Data Inspector panel — ByteChart on the left, byte interpretations on the right.
/// Designed for bottom docking (wide, short layout).
/// </summary>
public partial class DataInspectorPanel : UserControl
{
    // ── Enums ────────────────────────────────────────────────────────────────

    private enum ChartMode { Frequency, Entropy }
    private enum DataScope { ActiveView, Selection, WholeFile }

    // ── Fields ───────────────────────────────────────────────────────────────

    private readonly DataInspectorViewModel _viewModel;

    private IIDEHostContext?         _context;
    private byte[]?                  _lastChartData;
    private ChartMode                _mode      = ChartMode.Frequency;
    private DataScope                _scope     = DataScope.Selection;

    // Zoom state
    private int    _viewStart  = 0;
    private int    _viewEnd    = 255;
    private double _zoomLevel  = 1.0;

    // Drag-to-zoom rubber band
    private bool   _isDragging;
    private double _dragStartX;

    // Async scope loading cancellation
    private CancellationTokenSource? _loadCts;

    // True once whole-file chart data has been loaded — prevents reloading on every SelectionChanged.
    // Reset to false on Clear(), scope switch to WholeFile, and manual Refresh.
    private bool _wholeFileChartLoaded;

    // Layout change suppression (prevents feedback loop when syncing the combo programmatically)
    private bool _suppressLayoutChange;

    // Coalescing flag: prevents dispatcher flooding during fast scrolling in ActiveView scope
    private bool _viewportRefreshPending;

    // Toolbar overflow manager
    private ToolbarOverflowManager _overflowManager = null!;

    // ── Constructor ──────────────────────────────────────────────────────────

    public DataInspectorPanel()
    {
        InitializeComponent();
        _viewModel  = new DataInspectorViewModel();
        DataContext = _viewModel;
        SetSeparatorsVisibility(Visibility.Collapsed);

        Loaded += (_, _) =>
        {
            // Wire overflow manager (groups ordered: index 0 = first to collapse)
            _overflowManager = new ToolbarOverflowManager(
                toolbarContainer:        ToolbarBorder,
                alwaysVisiblePanel:      ToolbarRightPanel,
                overflowButton:          ToolbarOverflowButton,
                overflowMenu:            OverflowContextMenu,
                groupsInCollapseOrder:   new FrameworkElement[]
                {
                    TbgLayout,   // [0] first to collapse
                    TbgAction,   // [1]
                    TbgZoom,     // [2]
                    TbgMode,     // [3]
                    TbgToggles,  // [4] last to collapse
                },
                leftFixedElements: new FrameworkElement[] { TbgScope });

            ApplyOptions();

            // Capture natural widths after the first full layout pass
            Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
        };
    }

    // ── Options ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads <see cref="DataInspectorOptions"/> and applies them to the panel.
    /// Called on <see cref="Loaded"/> and by <see cref="DataInspectorPlugin.SaveOptions"/> after save.
    /// </summary>
    public void ApplyOptions()
    {
        var opts = DataInspectorOptions.Instance;
        SyncChartPositionCombo(opts.ChartPosition);
        SetChartVisible(opts.ShowByteChart);
        RefreshTheme();
    }

    /// <summary>Shows or hides the chart and its splitter, rebuilding the layout accordingly.</summary>
    private void SetChartVisible(bool visible)
    {
        ChartContainer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ChartSplitter.Visibility  = visible ? Visibility.Visible : Visibility.Collapsed;

        if (visible)
            RebuildLayout(DataInspectorOptions.Instance.ChartPosition);
        else
            RebuildLayoutListOnly();
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds <see cref="MainAreaGrid"/> definitions and child positions for the given
    /// chart position.  Must be called whenever <see cref="ChartPosition"/> changes or on Loaded.
    /// </summary>
    private void RebuildLayout(ChartPosition position)
    {
        MainAreaGrid.RowDefinitions.Clear();
        MainAreaGrid.ColumnDefinitions.Clear();

        switch (position)
        {
            case ChartPosition.Left:
                MainAreaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 120 });
                MainAreaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
                MainAreaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280), MinWidth = 160 });

                Grid.SetRow(ChartContainer, 0); Grid.SetColumn(ChartContainer, 0);
                Grid.SetRow(ChartSplitter,  0); Grid.SetColumn(ChartSplitter,  1);
                Grid.SetRow(ListContainer,  0); Grid.SetColumn(ListContainer,  2);

                ChartSplitter.Width           = 5;
                ChartSplitter.Height          = double.NaN;
                ChartSplitter.ResizeDirection = GridResizeDirection.Columns;
                ChartSplitter.Cursor          = Cursors.SizeWE;
                break;

            case ChartPosition.Right:
                MainAreaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280), MinWidth = 160 });
                MainAreaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
                MainAreaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 120 });

                Grid.SetRow(ListContainer,  0); Grid.SetColumn(ListContainer,  0);
                Grid.SetRow(ChartSplitter,  0); Grid.SetColumn(ChartSplitter,  1);
                Grid.SetRow(ChartContainer, 0); Grid.SetColumn(ChartContainer, 2);

                ChartSplitter.Width           = 5;
                ChartSplitter.Height          = double.NaN;
                ChartSplitter.ResizeDirection = GridResizeDirection.Columns;
                ChartSplitter.Cursor          = Cursors.SizeWE;
                break;

            case ChartPosition.Top:
                MainAreaGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 50 });
                MainAreaGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
                MainAreaGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 60 });

                Grid.SetRow(ChartContainer, 0); Grid.SetColumn(ChartContainer, 0);
                Grid.SetRow(ChartSplitter,  1); Grid.SetColumn(ChartSplitter,  0);
                Grid.SetRow(ListContainer,  2); Grid.SetColumn(ListContainer,  0);

                ChartSplitter.Width           = double.NaN;
                ChartSplitter.Height          = 5;
                ChartSplitter.ResizeDirection = GridResizeDirection.Rows;
                ChartSplitter.Cursor          = Cursors.SizeNS;
                break;

            case ChartPosition.Bottom:
                MainAreaGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 60 });
                MainAreaGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
                MainAreaGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 50 });

                Grid.SetRow(ListContainer,  0); Grid.SetColumn(ListContainer,  0);
                Grid.SetRow(ChartSplitter,  1); Grid.SetColumn(ChartSplitter,  0);
                Grid.SetRow(ChartContainer, 2); Grid.SetColumn(ChartContainer, 0);

                ChartSplitter.Width           = double.NaN;
                ChartSplitter.Height          = 5;
                ChartSplitter.ResizeDirection = GridResizeDirection.Rows;
                ChartSplitter.Cursor          = Cursors.SizeNS;
                break;
        }
    }

    /// <summary>
    /// Configures <see cref="MainAreaGrid"/> so that <see cref="ListContainer"/> fills the entire
    /// area when the chart is hidden.
    /// </summary>
    private void RebuildLayoutListOnly()
    {
        MainAreaGrid.RowDefinitions.Clear();
        MainAreaGrid.ColumnDefinitions.Clear();
        Grid.SetRow(ListContainer, 0);
        Grid.SetColumn(ListContainer, 0);
    }

    /// <summary>
    /// Syncs <see cref="ChartPositionCombo"/> to reflect <paramref name="pos"/> without
    /// triggering <see cref="OnChartPositionChanged"/>.
    /// </summary>
    private void SyncChartPositionCombo(ChartPosition pos)
    {
        _suppressLayoutChange = true;
        foreach (ComboBoxItem item in ChartPositionCombo.Items)
        {
            if (item.Tag?.ToString() == pos.ToString())
            {
                ChartPositionCombo.SelectedItem = item;
                break;
            }
        }
        _suppressLayoutChange = false;
    }

    /// <summary>
    /// Fires when the user picks a new chart position from the toolbar combo.
    /// Saves the choice and rebuilds the layout immediately.
    /// </summary>
    private void OnChartPositionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLayoutChange) return;
        if (ChartPositionCombo.SelectedItem is not ComboBoxItem item) return;
        if (!Enum.TryParse<ChartPosition>(item.Tag?.ToString(), out var pos)) return;

        DataInspectorOptions.Instance.ChartPosition = pos;
        DataInspectorOptions.Instance.Save();

        if (ChartContainer.Visibility == Visibility.Visible)
            RebuildLayout(pos);
    }

    // ── Public API (called by DataInspectorPlugin) ───────────────────────────

    /// <summary>
    /// Stores the IDE host context for scope-aware data reads (WholeFile mode).
    /// Must be called before the first <see cref="OnHexEditorSelectionChanged"/>.
    /// </summary>
    public void SetContext(IIDEHostContext context) => _context = context;

    /// <summary>
    /// Called by the plugin when <see cref="IHexEditorService.SelectionChanged"/> fires.
    /// Skipped when <see cref="DataInspectorOptions.AutoRefresh"/> is false (manual refresh only).
    /// </summary>
    public void OnHexEditorSelectionChanged()
    {
        if (!DataInspectorOptions.Instance.AutoRefresh) return;
        RefreshFromEditor();
    }

    /// <summary>
    /// Called by the plugin when <see cref="IHexEditorService.ViewportScrolled"/> fires.
    /// Only triggers a refresh when the "Active view" scope is active.
    /// Coalesced via a bool flag to avoid flooding the dispatcher at high scroll rates.
    /// </summary>
    public void OnViewportScrolled()
    {
        if (_scope != DataScope.ActiveView) return;
        if (_viewportRefreshPending) return;

        _viewportRefreshPending = true;
        Dispatcher.InvokeAsync(() =>
        {
            _viewportRefreshPending = false;
            if (_scope == DataScope.ActiveView && _context != null)
                RefreshFromEditor();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void RefreshFromEditor()
    {
        if (_context == null) return;

        switch (_scope)
        {
            case DataScope.ActiveView:
                var fileSize  = _context.HexEditor.FileSize;
                var firstByte = _context.HexEditor.FirstVisibleByteOffset;
                var lastByte  = Math.Min(_context.HexEditor.LastVisibleByteOffset, fileSize);
                // Guard: nothing to display if viewport is past the file (e.g. empty or tiny file)
                if (firstByte >= fileSize) break;
                var viewLen = (int)Math.Max(0, lastByte - firstByte);
                if (viewLen > 0)
                {
                    var viewBytes = _context.HexEditor.ReadBytes(firstByte, viewLen);
                    if (viewBytes.Length > 0)
                        UpdateChartData(viewBytes);
                }
                // Interpretations always follow cursor/selection.
                UpdateInterpretations(_context.HexEditor.GetSelectedBytes());
                UpdateScopeStatus($"Active view: {viewLen:N0} bytes");
                break;

            case DataScope.Selection:
                var selBytes = _context.HexEditor.GetSelectedBytes();
                if (selBytes.Length == 0)
                {
                    ShowEmptyState(DataInspectorResources.DataInspector_EmptyState);
                    return;
                }
                // Large selection (> 1 MB): async path with overlay.
                // Small selection or caret-only: use in-memory data directly (no stream I/O).
                if (_context.HexEditor.SelectionLength > 1L * 1024 * 1024)
                {
                    _ = LoadSelectionAsync();
                }
                else
                {
                    UpdateChartData(selBytes);
                    UpdateInterpretations(selBytes);
                    var selLen = _context.HexEditor.SelectionLength;
                    UpdateScopeStatus(selLen > 0
                        ? $"Selection: {selLen:N0} bytes"
                        : $"Cursor: {selBytes.Length} bytes");
                }
                break;

            case DataScope.WholeFile:
                // Interpretations always follow the active selection/cursor.
                UpdateInterpretations(_context.HexEditor.GetSelectedBytes());
                // Chart is static (whole file content): only load once per file.
                // Reloads are triggered by scope switch, file open, or manual Refresh.
                if (!_wholeFileChartLoaded)
                    _ = LoadWholeFileAsync();
                break;
        }
    }

    /// <summary>
    /// Clears both chart and interpretation pane (called on FileOpened / file close).
    /// </summary>
    public void Clear()
    {
        _loadCts?.Cancel();
        _wholeFileChartLoaded = false;
        _lastChartData = null;

        Chart.Clear();
        EntropyCanvas.Children.Clear();
        ClearFooterStats();
        _viewModel.UpdateBytes(null);

        ResetZoomState();
        HideProgressOverlay();
    }

    // ── Internal data flow ───────────────────────────────────────────────────

    private void UpdateChartData(byte[]? data)
    {
        _lastChartData = data;

        if (_mode == ChartMode.Frequency)
        {
            Chart.UpdateData(data ?? Array.Empty<byte>());
            RefreshFooterStats();
        }
        else
        {
            RedrawEntropyCanvas();
        }
    }

    private void UpdateInterpretations(byte[]? bytes)
        => _viewModel.UpdateBytes(bytes ?? Array.Empty<byte>());

    // ── Async WholeFile loading ───────────────────────────────────────────────

    private async Task LoadWholeFileAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        if (_context == null) return;
        var fileSize = _context.HexEditor.FileSize;
        if (fileSize <= 0) { Clear(); return; }

        const long MaxInstant = 1L * 1024 * 1024;  // 1 MB threshold for overlay
        const int  MaxSample  = 4  * 1024 * 1024;  // 4 MB stratified sample cap

        var readLen = (int)Math.Min(fileSize, MaxSample);

        if (fileSize <= MaxInstant)
        {
            var data = _context.HexEditor.ReadBytes(0, readLen);
            if (!ct.IsCancellationRequested)
            {
                _wholeFileChartLoaded = true;
                UpdateChartData(data);
            }
            return;
        }

        // Large file: show non-blocking async overlay
        ShowProgressOverlay($"Analyzing file ({readLen / 1024 / 1024} MB sample)…");
        try
        {
            var ctx = _context;
            var data = await Task.Run(() => ctx.HexEditor.ReadBytes(0, readLen), ct);
            if (!ct.IsCancellationRequested)
            {
                _wholeFileChartLoaded = true;
                UpdateChartData(data);
            }
        }
        catch (OperationCanceledException) { /* scope changed, discard result */ }
        finally { HideProgressOverlay(); }
    }

    // ── Async Selection loading ───────────────────────────────────────────────

    private async Task LoadSelectionAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        if (_context == null) return;
        var selLength = _context.HexEditor.SelectionLength;
        if (selLength <= 0) { Clear(); return; }

        const long MaxInstant = 1L * 1024 * 1024;  // 1 MB threshold for async
        const int  MaxSample  = 4  * 1024 * 1024;  // 4 MB cap for large selections

        UpdateScopeStatus(selLength > MaxSample
            ? $"Selection: {selLength:N0} bytes (showing {MaxSample / 1024 / 1024} MB sample)"
            : $"Selection: {selLength:N0} bytes");

        // Small selection (≤ 1 MB): use in-memory GetSelectedBytes() — no stream I/O,
        // safe to call on every SelectionChanged without UI thread stalls.
        if (selLength <= MaxInstant)
        {
            var data = _context.HexEditor.GetSelectedBytes();
            if (!ct.IsCancellationRequested)
            {
                UpdateChartData(data);
                UpdateInterpretations(data);
            }
            return;
        }

        // Large selection (> 1 MB): async read with progress overlay + sample cap.
        var selStart = _context.HexEditor.SelectionStart;
        var readLen  = (int)Math.Min(selLength, MaxSample);

        ShowProgressOverlay($"Reading selection ({readLen / 1024 / 1024} MB sample)…");
        try
        {
            var ctx   = _context;
            var start = selStart;
            var data  = await Task.Run(() => ctx.HexEditor.ReadBytes(start, readLen), ct);
            if (!ct.IsCancellationRequested)
            {
                UpdateChartData(data);
                UpdateInterpretations(data);
            }
        }
        catch (OperationCanceledException) { /* scope changed, discard */ }
        finally { HideProgressOverlay(); }
    }

    // ── Scope ────────────────────────────────────────────────────────────────

    private void OnScopeChanged(object sender, SelectionChangedEventArgs e)
    {
        var item   = ScopeComboBox.SelectedItem as ComboBoxItem;
        var label  = item?.Content?.ToString() ?? string.Empty;

        _scope = label switch
        {
            _ when label == DataInspectorResources.DataInspector_Scope_ActiveView => DataScope.ActiveView,
            _ when label == DataInspectorResources.DataInspector_Scope_WholeFile  => DataScope.WholeFile,
            _                                                                      => DataScope.Selection
        };

        // Switching to WholeFile forces a chart reload (new file context or explicit scope switch).
        if (_scope == DataScope.WholeFile)
            _wholeFileChartLoaded = false;

        // Scope change always triggers a refresh regardless of AutoRefresh setting.
        if (_context != null) RefreshFromEditor();
    }

    // ── Chart mode ───────────────────────────────────────────────────────────

    private void OnModeButtonClick(object sender, RoutedEventArgs e)
    {
        if (ModeButton.ContextMenu is { } cm)
        {
            cm.PlacementTarget = ModeButton;
            cm.Placement       = PlacementMode.Bottom;
            cm.IsOpen          = true;
        }
    }

    private void OnModeFrequency(object sender, RoutedEventArgs e)
    {
        _mode = ChartMode.Frequency;
        ChartScrollViewer.Visibility  = Visibility.Visible;
        EntropyCanvas.Visibility      = Visibility.Collapsed;
        ModeLabel.Text                = DataInspectorResources.DataInspector_Mode_Frequency;
        ModeFrequencyItem.IsChecked   = true;
        ModeEntropyItem.IsChecked     = false;

        if (_lastChartData != null)
        {
            Chart.UpdateData(_lastChartData);
            RefreshFooterStats();
        }
    }

    private void OnModeEntropy(object sender, RoutedEventArgs e)
    {
        _mode = ChartMode.Entropy;
        ChartScrollViewer.Visibility  = Visibility.Collapsed;
        EntropyCanvas.Visibility      = Visibility.Visible;
        ModeLabel.Text                = DataInspectorResources.DataInspector_Mode_Entropy;
        ModeFrequencyItem.IsChecked   = false;
        ModeEntropyItem.IsChecked     = true;

        RedrawEntropyCanvas();
    }

    // ── Toolbar toggles ──────────────────────────────────────────────────────

    private void OnToggleLabels(object sender, RoutedEventArgs e)
    { if (Chart is not null) Chart.ShowAxisLabels = ToggleLabels.IsChecked == true; }

    private void OnToggleGrid(object sender, RoutedEventArgs e)
    { if (Chart is not null) Chart.ShowGridLines = ToggleGrid.IsChecked == true; }

    private void OnToggleStats(object sender, RoutedEventArgs e)
    { if (Chart is not null) Chart.ShowStatistics = ToggleStats.IsChecked == true; }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        // Manual refresh always runs regardless of AutoRefresh setting.
        // For WholeFile scope: force chart reload (reset flag so LoadWholeFileAsync runs again).
        if (_scope == DataScope.WholeFile)
            _wholeFileChartLoaded = false;
        if (_context != null) RefreshFromEditor();
    }

    private void OnCopyChart(object sender, RoutedEventArgs e)
    {
        FrameworkElement source = _mode == ChartMode.Frequency
            ? (FrameworkElement)Chart
            : EntropyCanvas;

        if (source.ActualWidth <= 0 || source.ActualHeight <= 0) return;

        var rtb = new RenderTargetBitmap(
            (int)source.ActualWidth, (int)source.ActualHeight,
            96, 96, PixelFormats.Pbgra32);
        rtb.Render(source);
        Clipboard.SetImage(rtb);
    }

    // ── Zoom ─────────────────────────────────────────────────────────────────

    private void OnZoomIn(object sender, RoutedEventArgs e)    => ApplyZoom(_zoomLevel * 2);
    private void OnZoomOut(object sender, RoutedEventArgs e)   => ApplyZoom(_zoomLevel / 2);
    private void OnZoomReset(object sender, RoutedEventArgs e) => ApplyZoom(1.0);

    private void ApplyZoom(double newZoom)
    {
        _zoomLevel = Math.Clamp(newZoom, 1.0, 16.0);

        var rangeSize = (int)Math.Ceiling(256.0 / _zoomLevel);
        var center    = (_viewStart + _viewEnd) / 2;
        _viewStart    = Math.Max(0,   center - rangeSize / 2);
        _viewEnd      = Math.Min(255, _viewStart + rangeSize - 1);

        Chart.ZoomToRange(_viewStart, _viewEnd);
        SyncChartWidth();
        UpdateZoomStatusBar();
    }

    private void SyncChartWidth()
    {
        // Stretch BarChartPanel so the ScrollViewer reveals the horizontal scrollbar at zoom > 1x.
        if (ChartScrollViewer.ActualWidth > 0)
            Chart.Width = _zoomLevel <= 1.0
                ? double.NaN
                : ChartScrollViewer.ActualWidth * _zoomLevel;
    }

    private void OnChartScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
        => SyncChartWidth();

    private void ResetZoomState()
    {
        _viewStart = 0;
        _viewEnd   = 255;
        _zoomLevel = 1.0;
        Chart.ZoomReset();
        Chart.Width = double.NaN;
        UpdateZoomStatusBar();
    }

    private void UpdateZoomStatusBar()
    {
        bool zoomed = _zoomLevel > 1.0;
        SepZoom.Visibility       = zoomed ? Visibility.Visible : Visibility.Collapsed;
        ZoomRangeText.Visibility = zoomed ? Visibility.Visible : Visibility.Collapsed;
        if (zoomed)
            ZoomRangeText.Text = $"Zoom {_zoomLevel:F0}x  [0x{_viewStart:X2}–0x{_viewEnd:X2}]";
    }

    // ── Chart context menu ───────────────────────────────────────────────────

    /// <summary>
    /// Manually opens the chart context menu from BarChartPanel or EntropyCanvas right-click.
    /// Required because custom FrameworkElement + ScrollViewer does not propagate right-click
    /// to the parent Grid's ContextMenu automatically.
    /// </summary>
    private void OnChartAreaRightClick(object sender, MouseButtonEventArgs e)
    {
        OnChartContextMenuOpening(this, new RoutedEventArgs());
        ChartContainer.ContextMenu!.PlacementTarget = (FrameworkElement)sender;
        ChartContainer.ContextMenu!.Placement       = PlacementMode.MousePoint;
        ChartContainer.ContextMenu!.IsOpen          = true;
        e.Handled = true;
    }

    /// <summary>
    /// Syncs IsChecked states from live toolbar/mode state before the menu is shown.
    /// </summary>
    private void OnChartContextMenuOpening(object sender, RoutedEventArgs e)
    {
        CtxToggleLabels.IsChecked  = ToggleLabels.IsChecked == true;
        CtxToggleGrid.IsChecked    = ToggleGrid.IsChecked   == true;
        CtxToggleStats.IsChecked   = ToggleStats.IsChecked  == true;
        CtxModeFrequency.IsChecked = _mode == ChartMode.Frequency;
        CtxModeEntropy.IsChecked   = _mode == ChartMode.Entropy;
    }

    private void OnCtxToggleLabels(object sender, RoutedEventArgs e)
    {
        ToggleLabels.IsChecked = CtxToggleLabels.IsChecked;
        Chart.ShowAxisLabels   = CtxToggleLabels.IsChecked == true;
    }

    private void OnCtxToggleGrid(object sender, RoutedEventArgs e)
    {
        ToggleGrid.IsChecked = CtxToggleGrid.IsChecked;
        Chart.ShowGridLines  = CtxToggleGrid.IsChecked == true;
    }

    private void OnCtxToggleStats(object sender, RoutedEventArgs e)
    {
        ToggleStats.IsChecked = CtxToggleStats.IsChecked;
        Chart.ShowStatistics  = CtxToggleStats.IsChecked == true;
    }

    // ── Mouse wheel zoom ─────────────────────────────────────────────────────

    private void OnChartMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (e.Delta > 0) ApplyZoom(_zoomLevel * 2);
        else             ApplyZoom(_zoomLevel / 2);
    }

    // ── Drag-to-zoom rubber band ──────────────────────────────────────────────

    private void OnChartMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_mode != ChartMode.Frequency) return;

        _isDragging = true;
        _dragStartX = e.GetPosition(Chart).X;
        Chart.CaptureMouse();
        e.Handled = true;
    }

    private void OnChartMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentX = e.GetPosition(Chart).X;
        var minX     = Math.Min(_dragStartX, currentX);
        var maxX     = Math.Max(_dragStartX, currentX);

        ZoomRubberBandCanvas.Children.Clear();
        if (maxX - minX < 3) return;

        var band = new Rectangle
        {
            Width           = maxX - minX,
            Height          = ZoomRubberBandCanvas.ActualHeight,
            Fill            = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x78, 0xD4)),
            Stroke          = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(band, minX);
        Canvas.SetTop(band, 0);
        ZoomRubberBandCanvas.Children.Add(band);
    }

    private void OnChartMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;
        Chart.ReleaseMouseCapture();
        ZoomRubberBandCanvas.Children.Clear();

        var endX  = e.GetPosition(Chart).X;
        var minX  = Math.Min(_dragStartX, endX);
        var maxX  = Math.Max(_dragStartX, endX);

        // Ignore accidental single click (< 5px drag)
        if (maxX - minX < 5) return;

        var viewCount     = _viewEnd - _viewStart + 1;
        var pixelsPerByte = Chart.ActualWidth / viewCount;
        if (pixelsPerByte <= 0) return;

        var newStart = _viewStart + (int)(minX / pixelsPerByte);
        var newEnd   = _viewStart + (int)(maxX / pixelsPerByte);
        newStart = Math.Max(0,   newStart);
        newEnd   = Math.Min(255, newEnd);

        if (newEnd <= newStart) return;

        _viewStart = newStart;
        _viewEnd   = newEnd;
        _zoomLevel = 256.0 / (_viewEnd - _viewStart + 1);

        Chart.ZoomToRange(_viewStart, _viewEnd);
        SyncChartWidth();
        UpdateZoomStatusBar();

        e.Handled = true;
    }

    // ── Entropy canvas ────────────────────────────────────────────────────────

    private void OnEntropyCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_mode == ChartMode.Entropy && _lastChartData != null)
            RedrawEntropyCanvas();
    }

    private void RedrawEntropyCanvas()
    {
        EntropyCanvas.Children.Clear();
        if (_lastChartData is null || _lastChartData.Length == 0) return;

        const int WindowSize = 512;
        var h = EntropyCanvas.ActualHeight;
        if (h <= 0) h = 80;

        var blocks = Math.Max(1, (_lastChartData.Length + WindowSize - 1) / WindowSize);
        var barW   = EntropyCanvas.ActualWidth / blocks;

        for (int b = 0; b < blocks; b++)
        {
            var start = b * WindowSize;
            var len   = Math.Min(WindowSize, _lastChartData.Length - start);

            var freq = new long[256];
            for (int i = start; i < start + len; i++) freq[_lastChartData[i]]++;

            double ent = 0;
            for (int i = 0; i < 256; i++)
            {
                if (freq[i] <= 0) continue;
                var p = freq[i] / (double)len;
                ent -= p * Math.Log(p, 2);
            }

            var barH = (ent / 8.0) * (h - 4);
            var rect = new Rectangle
            {
                Width  = Math.Max(1, barW - 0.5),
                Height = Math.Max(1, barH),
                Fill   = EntropyBarBrush(ent)
            };
            Canvas.SetLeft(rect, b * barW);
            Canvas.SetBottom(rect, 2);
            EntropyCanvas.Children.Add(rect);
        }
    }

    private static Brush EntropyBarBrush(double entropy)
    {
        var t = entropy / 8.0;
        return new SolidColorBrush(
            Color.FromRgb((byte)(t * 220), (byte)((1.0 - t) * 180), 60)) { Opacity = 0.9 };
    }

    // ── Footer statistics ─────────────────────────────────────────────────────

    private void RefreshFooterStats()
    {
        var entropy = Chart.CalculateEntropy();
        EntropyText.Text = $"Entropy: {entropy:F3} bits";

        long maxFreq    = 0;
        byte mostCommon = 0;
        for (int i = 0; i < 256; i++)
        {
            var freq = Chart.GetFrequency((byte)i);
            if (freq > maxFreq) { maxFreq = freq; mostCommon = (byte)i; }
        }
        MostCommonText.Text = $"Most common: 0x{mostCommon:X2} ({Chart.GetPercentage(mostCommon):F1}%)";
        NullPctText.Text    = $"Null: {Chart.GetPercentage(0):F1}%";

        double asciiPct = 0;
        for (int i = 0x20; i <= 0x7E; i++) asciiPct += Chart.GetPercentage((byte)i);
        AsciiPctText.Text   = $"ASCII: {asciiPct:F1}%";

        TotalBytesText.Text = $"{Chart.TotalBytes:N0} bytes";

        SetSeparatorsVisibility(Visibility.Visible);
    }

    private void ClearFooterStats()
    {
        EntropyText.Text    = string.Empty;
        MostCommonText.Text = string.Empty;
        NullPctText.Text    = string.Empty;
        AsciiPctText.Text   = string.Empty;
        TotalBytesText.Text = DataInspectorResources.DataInspector_Footer_NoData;
        SetSeparatorsVisibility(Visibility.Collapsed);
    }

    private void SetSeparatorsVisibility(Visibility v)
    {
        SepEntropy.Visibility    = v;
        SepMostCommon.Visibility = v;
        SepNull.Visibility       = v;
        SepAscii.Visibility      = v;
    }

    // ── Progress overlay ──────────────────────────────────────────────────────

    private void ShowProgressOverlay(string message)
    {
        ProgressLabel.Text         = message;
        ProgressOverlay.Visibility = Visibility.Visible;
    }

    private void HideProgressOverlay()
        => ProgressOverlay.Visibility = Visibility.Collapsed;

    // ── Empty state + scope status ────────────────────────────────────────────

    /// <summary>
    /// Shows an empty-state message in the status bar and clears chart/interpretations.
    /// Used by the Selection scope when no bytes are selected.
    /// </summary>
    private void ShowEmptyState(string message)
    {
        _loadCts?.Cancel();
        Chart.Clear();
        EntropyCanvas.Children.Clear();
        _viewModel.UpdateBytes(null);
        UpdateScopeStatus(message);
    }

    /// <summary>Updates <see cref="TotalBytesText"/> with an arbitrary scope description.</summary>
    private void UpdateScopeStatus(string message)
        => TotalBytesText.Text = message;

    // ── Theme refresh ─────────────────────────────────────────────────────────

    /// <summary>
    /// Re-applies theme colors to <see cref="BarChartPanel"/> after a theme change.
    /// Called by <see cref="DataInspectorPlugin"/> on theme switch notification.
    /// </summary>
    public void RefreshTheme()
    {
        var res = Application.Current?.Resources;
        if (res is null) return;

        if (res["Panel_ToolbarButtonActiveBrush"] is SolidColorBrush accent)
            Chart.BarColor = accent.Color;

        if (res["PFP_PanelBackgroundBrush"] is SolidColorBrush bg)
            Chart.BackgroundColor = bg.Color;
        else if (res["DockBackgroundBrush"] is SolidColorBrush dockBg)
            Chart.BackgroundColor = dockBg.Color;

        if (res["DockMenuForegroundBrush"] is SolidColorBrush fg)
            Chart.TextColor = fg.Color;

        if (res["DockBorderBrush"] is SolidColorBrush border)
            Chart.GridLineColor = border.Color;

        Chart.InvalidateVisual();

        if (_mode == ChartMode.Entropy && _lastChartData != null)
            RedrawEntropyCanvas();

        // Invalidate cached widths — icons/fonts may have changed size with the theme
        _overflowManager?.InvalidateWidths();
    }

    // ── Toolbar overflow ──────────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        OverflowContextMenu.PlacementTarget = ToolbarOverflowButton;
        OverflowContextMenu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        OverflowContextMenu.IsOpen          = true;
    }

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        // Sync toggle IsChecked states
        OvfToggleLabels.IsChecked  = ToggleLabels.IsChecked == true;
        OvfToggleGrid.IsChecked    = ToggleGrid.IsChecked   == true;
        OvfToggleStats.IsChecked   = ToggleStats.IsChecked  == true;
        OvfModeFrequency.IsChecked = _mode == ChartMode.Frequency;
        OvfModeEntropy.IsChecked   = _mode == ChartMode.Entropy;

        // Sync layout position checked state
        var pos = DataInspectorOptions.Instance.ChartPosition.ToString();
        OvfLayoutLeft.IsChecked   = pos == "Left";
        OvfLayoutRight.IsChecked  = pos == "Right";
        OvfLayoutTop.IsChecked    = pos == "Top";
        OvfLayoutBottom.IsChecked = pos == "Bottom";

        _overflowManager?.SyncMenuVisibility();
    }

    private void OnOverflowLayoutClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi &&
            Enum.TryParse<ChartPosition>(mi.Tag?.ToString(), out var pos))
        {
            DataInspectorOptions.Instance.ChartPosition = pos;
            DataInspectorOptions.Instance.Save();
            SyncChartPositionCombo(pos);
            if (ChartContainer.Visibility == Visibility.Visible)
                RebuildLayout(pos);
        }
    }
}
