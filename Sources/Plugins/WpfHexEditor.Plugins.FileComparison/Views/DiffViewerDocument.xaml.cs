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

namespace WpfHexEditor.Plugins.FileComparison.Views;

public sealed partial class DiffViewerDocument : UserControl
{
    private const double RowHeight = 18.0;

    private DiffViewerViewModel? _vm;
    private bool                 _scrollSyncing;
    private bool                 _rulerDirty = true;

    // ── Constructor ──────────────────────────────────────────────────────────

    public DiffViewerDocument(DiffViewerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _vm         = vm;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DiffViewerViewModel.CurrentDiffIndex)
                                or nameof(DiffViewerViewModel.LeftRows))
            {
                ScrollToDiff(vm.CurrentDiffRowIndex);
            }
        };

        Loaded += (_, _) =>
        {
            UpdateStatusBar();
            PaintRuler();
        };
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

    // ── Overview ruler ────────────────────────────────────────────────────────

    private void OnRulerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _rulerDirty = true;
        PaintRuler();
    }

    private void OnRulerClicked(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;
        var totalRows = _vm.LeftRows.Count;
        if (totalRows == 0) return;

        var clickY   = e.GetPosition(OverviewRuler).Y;
        var fraction = clickY / OverviewRuler.ActualHeight;
        var rowIdx   = (int)(fraction * totalRows);
        var offset   = Math.Max(0, rowIdx) * RowHeight;
        LeftScroll.ScrollToVerticalOffset(offset);
    }

    private void PaintRuler()
    {
        OverviewRuler.Children.Clear();
        if (_vm is null) return;

        var rows      = _vm.LeftRows;
        var totalRows = rows.Count;
        if (totalRows == 0) return;

        var rulerH = OverviewRuler.ActualHeight;
        var rulerW = OverviewRuler.ActualWidth;
        if (rulerH <= 0 || rulerW <= 0) return;

        var modBrush  = TryBrush("DF_OverviewModifiedBrush")  ?? Brushes.Orange;
        var addBrush  = TryBrush("DF_OverviewAddedBrush")     ?? Brushes.Green;
        var remBrush  = TryBrush("DF_OverviewRemovedBrush")   ?? Brushes.Red;

        double rowH = rulerH / totalRows;
        double minH = Math.Max(rowH, 3.0);

        for (int i = 0; i < totalRows; i++)
        {
            var kind = rows[i].Kind;
            Brush? brush = kind switch
            {
                "Modified"     => modBrush,
                "InsertedRight"=> addBrush,
                "DeletedLeft"  => remBrush,
                _              => null
            };
            if (brush is null) continue;

            var rect = new Rectangle
            {
                Fill   = brush,
                Width  = rulerW,
                Height = minH
            };
            Canvas.SetTop(rect, i * rowH);
            OverviewRuler.Children.Add(rect);
        }

        _rulerDirty = false;
    }

    private Brush? TryBrush(string key)
        => TryFindResource(key) as Brush;

    // ── Navigation ────────────────────────────────────────────────────────────

    private void ScrollToDiff(int rowIdx)
    {
        if (rowIdx < 0) return;
        var offset = rowIdx * RowHeight;
        // Scroll so the diff block appears 3 rows from the top
        LeftScroll.ScrollToVerticalOffset(Math.Max(0, offset - RowHeight * 3));
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
}
