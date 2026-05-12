// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Controls/CyclicDepGraph.cs
// Description: Radial chord-style visualization for project cycles. Each
//              project participating in any cycle becomes a node on the
//              outer ring; each cycle edge becomes a Bézier chord between
//              two nodes. Highlights the worst cycles in red.
// Architecture Notes:
//     - FrameworkElement with cached frozen brushes/pens (no per-render alloc)
//     - Layout computed in OnRender from ItemsSource — re-runs only when the
//       Cycles dependency property changes (FrameworkPropertyMetadataOptions
//       .AffectsRender)
//     - Falls back to a placeholder "No cycles found" when ItemsSource empty
// ==========================================================

using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.UI.Controls;

public sealed class CyclicDepGraph : FrameworkElement
{
    public static readonly DependencyProperty CyclesProperty =
        DependencyProperty.Register(nameof(Cycles), typeof(IEnumerable), typeof(CyclicDepGraph),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? Cycles
    {
        get => (IEnumerable?)GetValue(CyclesProperty);
        set => SetValue(CyclesProperty, value);
    }

    private static readonly Typeface       Face       = new("Segoe UI");
    private static readonly SolidColorBrush NodeBrush  = Freeze(new SolidColorBrush(Color.FromRgb(0x42, 0x85, 0xF4)));
    private static readonly SolidColorBrush EdgeStroke = Freeze(new SolidColorBrush(Color.FromArgb(0xCC, 0xF4, 0x43, 0x36)));
    private static readonly Pen             EdgePen    = FreezePen(new Pen(EdgeStroke, 1.5) { LineJoin = PenLineJoin.Round });
    private static readonly Pen             NodePen    = FreezePen(new Pen(Brushes.Black, 0.5));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen             FreezePen(Pen p)         { p.Freeze();  return p; }

    public CyclicDepGraph()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding   = true;
        MinHeight = 220;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var cycles = ExtractCycles();
        var fg = TextElement.GetForeground(this) ?? SystemColors.ControlTextBrush;

        if (cycles.Count == 0)
        {
            var msg = new FormattedText("No project cycles detected.",
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, 12, fg, 1.0);
            dc.DrawText(msg, new Point((ActualWidth - msg.Width) / 2, (ActualHeight - msg.Height) / 2));
            return;
        }

        // Unique participating projects, deterministic order.
        var projects = cycles.SelectMany(c => c.Projects).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
        if (projects.Count == 0) return;

        var center  = new Point(ActualWidth / 2, ActualHeight / 2);
        double rad  = Math.Min(ActualWidth, ActualHeight) / 2 - 40;
        if (rad <= 0) return;

        var positions = new Dictionary<string, Point>(StringComparer.Ordinal);
        for (int i = 0; i < projects.Count; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI * 2 / projects.Count;
            positions[projects[i]] = new Point(center.X + rad * Math.Cos(a), center.Y + rad * Math.Sin(a));
        }

        // Draw chords for every consecutive pair within each cycle.
        foreach (var c in cycles)
        {
            var p = c.Projects;
            for (int i = 0; i < p.Count; i++)
            {
                var from = positions[p[i]];
                var to   = positions[p[(i + 1) % p.Count]];
                DrawChord(dc, from, to, center);
            }
        }

        // Draw nodes + labels on top of edges.
        foreach (var (name, pos) in positions)
        {
            dc.DrawEllipse(NodeBrush, NodePen, pos, 6, 6);
            var label = new FormattedText(ShortName(name), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Face, 10, fg, 1.0)
            { MaxTextWidth = 120, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };

            var labelPos = LabelPosition(center, pos, label);
            dc.DrawText(label, labelPos);
        }
    }

    private static void DrawChord(DrawingContext dc, Point from, Point to, Point center)
    {
        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            g.BeginFigure(from, false, false);
            g.BezierTo(center, center, to, true, false);
        }
        geom.Freeze();
        dc.DrawGeometry(null, EdgePen, geom);
    }

    private static Point LabelPosition(Point center, Point node, FormattedText t)
    {
        // Push the label outward from the center by a margin so it sits beyond the node dot.
        var dx = node.X - center.X;
        var dy = node.Y - center.Y;
        var len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        var ox = dx / len * 12;
        var oy = dy / len * 12;
        return new Point(node.X + ox - t.Width / 2, node.Y + oy - t.Height / 2);
    }

    private static string ShortName(string projectPath)
        => System.IO.Path.GetFileNameWithoutExtension(projectPath);

    private List<ProjectCycleInfo> ExtractCycles()
    {
        var list = new List<ProjectCycleInfo>();
        if (Cycles is null) return list;
        foreach (var item in Cycles)
            if (item is ProjectCycleInfo c) list.Add(c);
        return list;
    }
}
