// ==========================================================
// Project: WpfHexEditor.Plugins.DataInspector
// File: DataInspectorPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Unified bottom panel — ByteChart (left) + byte interpretations (right).
//     Optimised for bottom-dock layout: wide and short (design 1000x180).
//
// Architecture Notes:
//     Pattern: Observer — driven by DataInspectorPlugin via SetContext/
//     OnHexEditorSelectionChanged.  Interpretation bindings delegate to
//     DataInspectorViewModel (MVVM). Chart and zoom state are managed
//     entirely in code-behind for performance.
//
//     Zoom model:
//       _zoomLevel 1x  = all 256 bars fill the chart width
//       _zoomLevel Nx  = chart.Width = scrollViewer.Width * N  (scrollbar appears)
//       BarChartPanel.ZoomToRange narrows the rendered byte range
//       Drag rubber band → zoom to selected byte range
//       Mouse wheel → zoom in/out
//
//     Scope model:
//       Selection  — GetSelectedBytes()       instant, no overlay
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
using WpfHexEditor.HexEditor.ViewModels;
using WpfHexEditor.Plugins.DataInspector.Options;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.DataInspector.Views;

/// <summary>
/// Unified Data Inspector panel — ByteChart on the left, byte interpretations on the right.
/// Designed for bottom docking (wide, short layout).
/// </summary>
public partial class DataInspectorPanel : UserControl
{
    // ── Enums ────────────────────────────────────────────────────────────────

    private enum ChartMode { Frequency, Entropy }
    private enum DataScope { Selection, WholeFile }

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

    // ── Constructor ──────────────────────────────────────────────────────────

    public DataInspectorPanel()
    {
        InitializeComponent();
        _viewModel  = new DataInspectorViewModel();
        DataContext = _viewModel;
        SetSeparatorsVisibility(Visibility.Collapsed);

        // Apply persisted options once the visual tree is ready.
        Loaded += (_, _) => ApplyOptions();
    }

    // ── Options ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads <see cref="DataInspectorOptions"/> and applies them to the panel.
    /// Called on <see cref="Loaded"/> and by <see cref="DataInspectorPlugin.SaveOptions"/> after save.
    /// </summary>
    public void ApplyOptions()
    {
        var opts = DataInspectorOptions.Instance;
        SetChartVisible(opts.ShowByteChart);
        RefreshTheme();
    }

    /// <summary>Shows or hides the chart column and its splitter.</summary>
    private void SetChartVisible(bool visible)
    {
        ChartContainer.Visibility  = visible ? Visibility.Visible : Visibility.Collapsed;
        ChartSplitter.Visibility   = visible ? Visibility.Visible : Visibility.Collapsed;
        ChartColumnDef.Width       = visible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SplitterColumnDef.Width    = visible ? new GridLength(5) : new GridLength(0);
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

    private void RefreshFromEditor()
    {
        if (_context == null) return;

        switch (_scope)
        {
            case DataScope.Selection:
                var bytes = _context.HexEditor.GetSelectedBytes();
                UpdateChartData(bytes);
                UpdateInterpretations(bytes);
                break;

            case DataScope.WholeFile:
                // Interpretations always follow the active selection.
                UpdateInterpretations(_context.HexEditor.GetSelectedBytes());
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
            if (!ct.IsCancellationRequested) UpdateChartData(data);
            return;
        }

        // Large file: show non-blocking async overlay
        ShowProgressOverlay($"Analyzing file ({readLen / 1024 / 1024} MB sample)…");
        try
        {
            var ctx = _context;
            var data = await Task.Run(() => ctx.HexEditor.ReadBytes(0, readLen), ct);
            if (!ct.IsCancellationRequested) UpdateChartData(data);
        }
        catch (OperationCanceledException) { /* scope changed, discard result */ }
        finally { HideProgressOverlay(); }
    }

    // ── Scope ────────────────────────────────────────────────────────────────

    private void OnScopeChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = ScopeComboBox.SelectedItem as ComboBoxItem;
        _scope = item?.Content?.ToString() == "Whole file"
            ? DataScope.WholeFile
            : DataScope.Selection;

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
        ModeLabel.Text                = "Frequency";
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
        ModeLabel.Text                = "Entropy";
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
        TotalBytesText.Text = "No data";
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
    }
}
