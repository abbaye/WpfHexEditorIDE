// ==========================================================
// Project: WpfHexEditor.Plugins.FileComparison
// File: Views/DiffViewerDocument.xaml.cs
// Description:
//     Code-behind for DiffViewerDocument — VS-style side-by-side diff viewer.
//     Handles synchronized scrolling, overview ruler painting, and keyboard shortcuts.
//
// Architecture Notes:
//     View-only: all data logic lives in DiffViewerViewModel.
//     Ruler is painted on demand when the layout changes or result loads.
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfHexEditor.Core.Diff.Models;
using WpfHexEditor.Plugins.FileComparison.ViewModels;
using BinaryByteKind = WpfHexEditor.Core.Diff.Models.BinaryByteKind;

namespace WpfHexEditor.Plugins.FileComparison.Views;

public sealed partial class DiffViewerDocument : UserControl
{
    private const double RowHeight = 18.0;

    private DiffViewerViewModel? _vm;
    private bool                 _scrollSyncing;
    private bool                 _rulerDirty    = true;
    private double               _statsPanelWidth = 240.0;

    // ── Constructor ──────────────────────────────────────────────────────────

    public DiffViewerDocument(DiffViewerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _vm         = vm;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DiffViewerViewModel.CurrentDiffIndex)
                                or nameof(DiffViewerViewModel.LeftRows)
                                or nameof(DiffViewerViewModel.BinaryHexRows))
            {
                ScrollToDiff(vm.CurrentDiffRowIndex);
            }
        };

        vm.StatsPanel.PropertyChanged += OnStatsPanelPropertyChanged;

        // Wire hover events from BinaryDiffCanvas
        BinaryDiffCanvas.FocusedSideChanged += (_, isLeft) => _vm!.IsLeftSideFocused = isLeft;
        BinaryDiffCanvas.HoverCellChanged   += OnHoverCellChanged;

        Loaded += (_, _) =>
        {
            WireRefreshTimeReporter();
            UpdateStatusBar();
            PaintRuler();
            // Sync initial column width with IsVisible default state.
            ApplyStatsPanelColumnWidth(vm.StatsPanel.IsVisible);
        };
    }

    private void OnStatsPanelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BinaryStatsPanelViewModel.IsVisible)) return;
        Dispatcher.InvokeAsync(() => ApplyStatsPanelColumnWidth(_vm!.StatsPanel.IsVisible));
    }

    private void ApplyStatsPanelColumnWidth(bool isVisible)
    {
        var col = OuterGrid.ColumnDefinitions[1];
        if (isVisible)
        {
            col.MinWidth = 180;
            col.MaxWidth = 400;
            col.Width    = new GridLength(_statsPanelWidth, GridUnitType.Pixel);
        }
        else
        {
            // Save current width so it can be restored on re-show.
            if (col.ActualWidth > 0) _statsPanelWidth = col.ActualWidth;
            col.MinWidth = 0;
            col.MaxWidth = double.PositiveInfinity;
            col.Width    = new GridLength(0, GridUnitType.Pixel);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Replaces the displayed result without opening a new tab.</summary>
    public void LoadResult(DiffEngineResult result)
    {
        _vm?.LoadResult(result);
        _rulerDirty = true;
        UpdateStatusBar();
        PaintRuler();
    }

    // ── Synchronized scrolling ────────────────────────────────────────────────

    private void OnLeftScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_scrollSyncing) return;
        _scrollSyncing = true;
        RightScroll.ScrollToVerticalOffset(e.VerticalOffset);
        _scrollSyncing = false;
    }

    private void OnRightScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_scrollSyncing) return;
        _scrollSyncing = true;
        LeftScroll.ScrollToVerticalOffset(e.VerticalOffset);
        _scrollSyncing = false;
        if (_rulerDirty) { PaintRuler(); _rulerDirty = false; }
    }

    private void OnBinaryHexScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_rulerDirty) { PaintRuler(); _rulerDirty = false; }
    }

    // ── Binary hex toolbar handlers ───────────────────────────────────────────

    private void OnContextLinesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        if (ContextLinesCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _vm.BinaryContextLines = tag == "-1" ? int.MaxValue : int.Parse(tag);
            _rulerDirty = true;
            PaintRuler();
        }
    }

    private void OnCopyHexClicked(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var sb = new System.Text.StringBuilder();
        foreach (var row in _vm.BinaryHexRows)
        {
            if (row.IsCollapsedContext)
            {
                sb.AppendLine($"--- {row.CollapsedRowCount} identical rows ---");
                continue;
            }
            sb.Append(row.LeftOffsetText);
            sb.Append("  ");
            foreach (var cell in row.LeftCells)
                sb.Append(cell.HexText + " ");
            sb.Append(" |");
            foreach (var cell in row.LeftCells)
                sb.Append(cell.AsciiChar);
            sb.Append("  ||  ");
            sb.Append(row.RightOffsetText);
            sb.Append("  ");
            foreach (var cell in row.RightCells)
                sb.Append(cell.HexText + " ");
            sb.Append(" |");
            foreach (var cell in row.RightCells)
                sb.Append(cell.AsciiChar);
            sb.AppendLine();
        }
        Clipboard.SetText(sb.ToString());
    }

    // ── Overview ruler ────────────────────────────────────────────────────────

    private void OnRulerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _rulerDirty = true;
        PaintRuler();
    }

    private void OnRulerClicked(object sender, MouseButtonEventArgs e)
    {
        OverviewRuler.CaptureMouse();
        ScrollToRulerFraction(e.GetPosition(OverviewRuler).Y);
    }

    private void OnRulerDragged(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        ScrollToRulerFraction(e.GetPosition(OverviewRuler).Y);
    }

    private void OnRulerReleased(object sender, MouseButtonEventArgs e)
    {
        OverviewRuler.ReleaseMouseCapture();
    }

    private void ScrollToRulerFraction(double y)
    {
        if (_vm is null) return;
        var fraction = Math.Clamp(y / OverviewRuler.ActualHeight, 0.0, 1.0);

        if (_vm.IsBinaryMode)
        {
            var totalRows = _vm.BinaryHexRows.Count;
            if (totalRows == 0) return;
            BinaryHexScroll.ScrollToVerticalOffset((int)(fraction * totalRows) * BinaryDiffCanvas.EffectiveRowH);
        }
        else
        {
            var totalRows = _vm.LeftRows.Count;
            if (totalRows == 0) return;
            LeftScroll.ScrollToVerticalOffset((int)(fraction * totalRows) * RowHeight);
        }
    }

    private void PaintRuler()
    {
        OverviewRuler.Children.Clear();
        if (_vm is null) return;

        var rulerH = OverviewRuler.ActualHeight;
        var rulerW = OverviewRuler.ActualWidth;
        if (rulerH <= 0 || rulerW <= 0) return;

        var modBrush = TryBrush("DF_OverviewModifiedBrush") ?? Brushes.Orange;
        var addBrush = TryBrush("DF_OverviewAddedBrush")    ?? Brushes.Green;
        var remBrush = TryBrush("DF_OverviewRemovedBrush")  ?? Brushes.Red;

        if (_vm.IsBinaryMode)
            PaintBinaryRuler(rulerH, rulerW, modBrush, addBrush, remBrush);
        else
            PaintTextRuler(rulerH, rulerW, modBrush, addBrush, remBrush);

        _rulerDirty = false;
    }

    private void PaintTextRuler(double rulerH, double rulerW,
        Brush modBrush, Brush addBrush, Brush remBrush)
    {
        var rows      = _vm!.LeftRows;
        var totalRows = rows.Count;
        if (totalRows == 0) return;

        double rowH = rulerH / totalRows;
        double minH = Math.Max(rowH, 3.0);

        for (int i = 0; i < totalRows; i++)
        {
            Brush? brush = rows[i].Kind switch
            {
                "Modified"      => modBrush,
                "InsertedRight" => addBrush,
                "DeletedLeft"   => remBrush,
                _               => null
            };
            if (brush is null) continue;

            var rect = new Rectangle { Fill = brush, Width = rulerW, Height = minH };
            Canvas.SetTop(rect, i * rowH);
            OverviewRuler.Children.Add(rect);
        }
    }

    private void PaintBinaryRuler(double rulerH, double rulerW,
        Brush modBrush, Brush addBrush, Brush remBrush)
    {
        var rows      = _vm!.BinaryHexRows;
        var totalRows = rows.Count;
        if (totalRows == 0) return;

        double rowH = rulerH / totalRows;
        double minH = Math.Max(rowH, 2.0);

        for (int i = 0; i < totalRows; i++)
        {
            var row = rows[i];
            if (!row.HasDiff) continue;

            // Pick brush by dominant kind in the row
            Brush brush = modBrush;
            foreach (var cell in row.LeftCells)
            {
                if (cell.Kind == BinaryByteKind.DeletedLeft)   { brush = remBrush; break; }
                if (cell.Kind == BinaryByteKind.InsertedRight) { brush = addBrush; break; }
            }
            foreach (var cell in row.RightCells)
            {
                if (cell.Kind == BinaryByteKind.InsertedRight) { brush = addBrush; break; }
            }

            var rect = new Rectangle { Fill = brush, Width = rulerW, Height = minH };
            Canvas.SetTop(rect, i * rowH);
            OverviewRuler.Children.Add(rect);
        }
    }

    private Brush? TryBrush(string key)
        => TryFindResource(key) as Brush;

    // ── Navigation ────────────────────────────────────────────────────────────

    private void ScrollToDiff(int rowIdx)
    {
        if (rowIdx < 0) return;
        if (_vm?.IsBinaryMode == true)
        {
            var rowH   = BinaryDiffCanvas.EffectiveRowH;
            var offset = rowIdx * rowH;
            BinaryHexScroll.ScrollToVerticalOffset(Math.Max(0, offset - rowH * 3));
        }
        else
        {
            var offset = rowIdx * RowHeight;
            LeftScroll.ScrollToVerticalOffset(Math.Max(0, offset - RowHeight * 3));
        }
    }

    // ── Keyboard shortcuts ───────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_vm is null) return;

        if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt)
        {
            if (e.Key == Key.Up)
            {
                _vm.PrevDiffCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                _vm.NextDiffCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    // ── Status bar ────────────────────────────────────────────────────────────

    private void UpdateStatusBar()
    {
        if (_vm is null) return;
        StatusBar.Text = $"{System.IO.Path.GetFileName(_vm.LeftPath)}  \u2194  {System.IO.Path.GetFileName(_vm.RightPath)}" +
                         $"   |   {_vm.TotalDiffCount} differences   |   {_vm.SimilarityText}";
    }

    private void OnHoverCellChanged(object? sender, Controls.BinaryDiffCanvas.HoverCellInfo info)
    {
        if (_vm is null) return;
        if (info.RowIndex < 0 || info.CellIndex < 0)
        {
            UpdateStatusBar();
            return;
        }
        var side = info.IsLeft ? "Left" : "Right";
        StatusBar.Text = $"{side}  |  Offset: 0x{info.Offset:X8}  |  Value: {info.HexText}" +
                         $"   |   {_vm.TotalDiffCount} differences   |   {_vm.SimilarityText}";
    }
}
