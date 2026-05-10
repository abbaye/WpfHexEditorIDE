// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Controls/Treemap.cs
// Description: Squarified treemap (Bruls/Huijsing/van Wijk) of files —
//              rectangle area is proportional to LOC, fill colour is keyed
//              to the per-file Score. Click raises ItemActivated with the
//              underlying FileMetrics.
// Architecture Notes:
//     OnRender draws rectangles; arrange-time tile layout is recomputed
//     whenever Items changes. Hit-testing is point-in-rect on MouseLeftButtonDown.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.UI.Controls;

public sealed class TreemapContextMenuEventArgs : EventArgs
{
    public FileMetrics File { get; }
    public TreemapContextMenuEventArgs(FileMetrics file) => File = file;
}

public sealed class Treemap : Control
{
    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IReadOnlyList<FileMetrics>), typeof(Treemap),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsChanged));

    public IReadOnlyList<FileMetrics>? Items
    {
        get => (IReadOnlyList<FileMetrics>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public event EventHandler<FileMetrics>?                  ItemActivated;
    public event EventHandler<TreemapContextMenuEventArgs>?  ContextMenuRequested;

    private List<(Rect bounds, FileMetrics file)> _tiles = [];
    private bool _hotspotMode;

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Treemap)d).Recompute();

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Recompute();
    }

    private void Recompute()
    {
        _tiles.Clear();
        if (Items is null || Items.Count == 0 || ActualWidth < 4 || ActualHeight < 4)
        {
            InvalidateVisual();
            return;
        }

        var sorted = Items
            .Where(f => f.TotalLines > 0)
            .OrderByDescending(f => f.TotalLines)
            .ToList();
        if (sorted.Count == 0) { InvalidateVisual(); return; }

        double total = sorted.Sum(f => (double)f.TotalLines);
        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        Squarify(sorted, total, rect, _tiles);
        InvalidateVisual();
    }

    // ── Squarified algorithm ──────────────────────────────────────────────────

    private static void Squarify(IReadOnlyList<FileMetrics> children, double totalValue,
        Rect bounds, List<(Rect, FileMetrics)> output)
    {
        if (children.Count == 0 || bounds.Width <= 0 || bounds.Height <= 0) return;

        // Project values to current bounds area
        double area = bounds.Width * bounds.Height;
        var sizes   = children.Select(c => c.TotalLines / totalValue * area).ToList();

        var row = new List<int>();
        double rowSum = 0;
        double shortSide = Math.Min(bounds.Width, bounds.Height);
        var remaining = bounds;

        for (int i = 0; i < sizes.Count; i++)
        {
            // Try adding sizes[i] to the row; if worst aspect ratio worsens, flush row
            double withNew = Worst(row.Select(j => sizes[j]).Append(sizes[i]).ToList(),
                                    rowSum + sizes[i], shortSide);
            double current = row.Count == 0 ? double.PositiveInfinity
                                            : Worst(row.Select(j => sizes[j]).ToList(), rowSum, shortSide);

            if (row.Count == 0 || withNew <= current)
            {
                row.Add(i);
                rowSum += sizes[i];
            }
            else
            {
                remaining = LayoutRow(row.Select(j => (sizes[j], children[j])).ToList(), rowSum, remaining, output);
                if (remaining.IsEmpty || remaining.Width <= 0 || remaining.Height <= 0) return;
                row.Clear();
                rowSum = 0;
                shortSide = Math.Min(remaining.Width, remaining.Height);
                row.Add(i);
                rowSum = sizes[i];
            }
        }
        if (row.Count > 0 && !remaining.IsEmpty && remaining.Width > 0 && remaining.Height > 0)
            LayoutRow(row.Select(j => (sizes[j], children[j])).ToList(), rowSum, remaining, output);
    }

    private static double Worst(List<double> row, double sum, double shortSide)
    {
        if (row.Count == 0 || sum <= 0) return double.PositiveInfinity;
        double s2 = shortSide * shortSide;
        double sum2 = sum * sum;
        double rmax = row.Max();
        double rmin = row.Min();
        return Math.Max(s2 * rmax / sum2, sum2 / (s2 * rmin));
    }

    private static Rect LayoutRow(List<(double size, FileMetrics file)> row, double rowSum,
        Rect bounds, List<(Rect, FileMetrics)> output)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || rowSum <= 0) return Rect.Empty;

        bool horizontal = bounds.Width >= bounds.Height;
        double along    = horizontal ? bounds.Height : bounds.Width;
        if (along <= 0) return Rect.Empty;

        // Clamp thickness so we never request a width/height that exceeds the bounds —
        // floating-point error can otherwise drive `bounds.Width - thickness` negative
        // and crash `new Rect(...)` with "Width/Height cannot be negative".
        double thickness = rowSum / along;
        double maxThickness = horizontal ? bounds.Width : bounds.Height;
        if (thickness > maxThickness) thickness = maxThickness;

        double remainingExtent = along;
        double cursor = horizontal ? bounds.Top : bounds.Left;

        foreach (var (size, file) in row)
        {
            double extent = thickness > 0 ? size / thickness : 0;
            if (extent > remainingExtent) extent = remainingExtent;
            if (extent < 0) extent = 0;

            Rect tile = horizontal
                ? new Rect(bounds.Left, cursor, thickness, extent)
                : new Rect(cursor, bounds.Top, extent, thickness);
            output.Add((tile, file));
            cursor += extent;
            remainingExtent -= extent;
        }

        double leftover = Math.Max(0, (horizontal ? bounds.Width : bounds.Height) - thickness);
        return horizontal
            ? new Rect(bounds.Left + thickness, bounds.Top, leftover, bounds.Height)
            : new Rect(bounds.Left, bounds.Top + thickness, bounds.Width, leftover);
    }

    // ── Render & hit-test ─────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        var labelBr = (Foreground as SolidColorBrush) ?? Brushes.White;
        var typeface = new Typeface("Segoe UI");

        foreach (var (rect, file) in _tiles)
        {
            if (rect.Width <= 1 || rect.Height <= 1) continue;
            var brush = _hotspotMode && !file.IsHotspot
                ? new SolidColorBrush(Color.FromArgb(80, 60, 60, 60))
                : ScoreToBrush(file.Score);
            var pen   = new Pen(Brushes.Black, 0.5);
            dc.DrawRectangle(brush, pen, rect);

            if (rect.Width > 60 && rect.Height > 22)
            {
                var ft = new FormattedText(file.FileName, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, 10, labelBr, 1.0)
                {
                    MaxTextWidth  = Math.Max(0, rect.Width - 4),
                    MaxLineCount  = 1,
                    Trimming      = TextTrimming.CharacterEllipsis,
                };
                dc.DrawText(ft, new Point(rect.Left + 2, rect.Top + 2));
            }
        }
    }

    private static Brush ScoreToBrush(int score) => score switch
    {
        >= 85 => new SolidColorBrush(Color.FromRgb( 67, 160,  71)),  // green
        >= 70 => new SolidColorBrush(Color.FromRgb(158, 157,  36)),  // olive
        >= 55 => new SolidColorBrush(Color.FromRgb(245, 124,   0)),  // orange
        >= 40 => new SolidColorBrush(Color.FromRgb(229,  57,  53)),  // red-orange
        _     => new SolidColorBrush(Color.FromRgb(176,  35,  30)),  // dark red
    };

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        foreach (var (rect, file) in _tiles)
        {
            if (rect.Contains(p))
            {
                ItemActivated?.Invoke(this, file);
                e.Handled = true;
                return;
            }
        }
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        foreach (var (rect, file) in _tiles)
        {
            if (!rect.Contains(p)) continue;
            ContextMenuRequested?.Invoke(this, new TreemapContextMenuEventArgs(file));
            e.Handled = true;
            return;
        }
    }

    public void ToggleHotspotMode()
    {
        _hotspotMode = !_hotspotMode;
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        foreach (var (rect, file) in _tiles)
        {
            if (!rect.Contains(p)) continue;
            Cursor = Cursors.Hand;
            ToolTip = BuildTooltip(file);
            return;
        }
        Cursor  = Cursors.Arrow;
        ToolTip = null;
    }

    private static string BuildTooltip(FileMetrics f) =>
        $"""
         {f.FileName}
         Project   : {f.ProjectName}
         Score     : {f.Score}/100
         LOC       : {f.TotalLines:N0}  ({f.CodeLines:N0} code · {f.CommentLines:N0} comments)
         Max CC    : {f.MaxCyclomaticComplexity}
         Max Cog   : {f.MaxCognitiveComplexity}
         MI        : {f.MaintainabilityIndex:F0}
         Methods   : {f.MethodCount}
         {(f.IsHotspot ? "🔥 hotspot — frequent changes + low score\n" : "")}Click to open file
         """;
}
