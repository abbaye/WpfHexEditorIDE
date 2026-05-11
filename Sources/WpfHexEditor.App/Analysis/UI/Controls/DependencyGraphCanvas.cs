// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Controls/DependencyGraphCanvas.cs
// Description:
//     Lightweight WPF canvas that draws a directed dependency graph using
//     a stripped-down force-directed layout (Fruchterman-Reingold). Nodes
//     are rendered as round-rect badges; edges as straight arrows.
//     Click a node to raise NodeActivated; the host opens the file.
// Architecture Notes:
//     - Pure FrameworkElement, no template / no input bubbling beyond click.
//     - Layout runs once on data change (deterministic, no animation).
//     - Allocation-free OnRender except StreamGeometry creation on rebuild.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.App.Analysis.UI.Controls;

/// <summary>One node in the dependency graph.</summary>
public sealed class DependencyNode
{
    public required string Id    { get; init; }     // unique key (e.g. file path or fully-qualified class name)
    public required string Label { get; init; }     // display text
    public          string? Tag  { get; init; }     // arbitrary metadata (file path for navigation)
    public          int    Weight { get; init; } = 1;
}

/// <summary>A directed edge between two nodes (by Id).</summary>
public sealed record DependencyEdge(string FromId, string ToId);

public sealed class DependencyGraphCanvas : FrameworkElement
{
    public static readonly DependencyProperty NodesProperty =
        DependencyProperty.Register(nameof(Nodes), typeof(IList<DependencyNode>), typeof(DependencyGraphCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

    public static readonly DependencyProperty EdgesProperty =
        DependencyProperty.Register(nameof(Edges), typeof(IList<DependencyEdge>), typeof(DependencyGraphCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

    public IList<DependencyNode>? Nodes { get => (IList<DependencyNode>?)GetValue(NodesProperty); set => SetValue(NodesProperty, value); }
    public IList<DependencyEdge>? Edges { get => (IList<DependencyEdge>?)GetValue(EdgesProperty); set => SetValue(EdgesProperty, value); }

    public event EventHandler<DependencyNode>? NodeActivated;

    private readonly Dictionary<string, Point> _positions = new();

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DependencyGraphCanvas g) g._positions.Clear();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 400 : availableSize.Height);

    protected override void OnRender(DrawingContext dc)
    {
        var area = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(Brushes.Transparent, null, area);

        if (Nodes is null || Nodes.Count == 0) return;
        if (_positions.Count != Nodes.Count) RunLayout(area);

        DrawEdges(dc);
        DrawNodes(dc);
    }

    // ── Layout (Fruchterman-Reingold, deterministic seed) ────────────────────

    private void RunLayout(Rect area)
    {
        _positions.Clear();
        if (Nodes is null) return;

        var rng = new Random(42);
        foreach (var n in Nodes)
            _positions[n.Id] = new Point(
                area.X + rng.NextDouble() * area.Width,
                area.Y + rng.NextDouble() * area.Height);

        var k = Math.Sqrt(area.Width * area.Height / Math.Max(1, Nodes.Count));
        var temp = area.Width / 10;
        const int iterations = 80;

        var disp = new Dictionary<string, Vector>(Nodes.Count);

        for (int it = 0; it < iterations; it++)
        {
            foreach (var n in Nodes) disp[n.Id] = new Vector();

            // Repulsive forces.
            for (int i = 0; i < Nodes.Count; i++)
            for (int j = i + 1; j < Nodes.Count; j++)
            {
                var a = Nodes[i]; var b = Nodes[j];
                var delta = _positions[a.Id] - _positions[b.Id];
                var d     = Math.Max(0.01, delta.Length);
                var force = (k * k) / d;
                var dir   = delta / d;
                disp[a.Id] += dir * force;
                disp[b.Id] -= dir * force;
            }

            // Attractive forces along edges.
            if (Edges is not null)
                foreach (var e in Edges)
                {
                    if (!_positions.ContainsKey(e.FromId) || !_positions.ContainsKey(e.ToId)) continue;
                    var delta = _positions[e.FromId] - _positions[e.ToId];
                    var d     = Math.Max(0.01, delta.Length);
                    var force = (d * d) / k;
                    var dir   = delta / d;
                    disp[e.FromId] -= dir * force;
                    disp[e.ToId]   += dir * force;
                }

            // Apply with cooling temp.
            foreach (var n in Nodes)
            {
                var d  = disp[n.Id];
                var dl = Math.Max(0.01, d.Length);
                var move = d / dl * Math.Min(dl, temp);
                var p = _positions[n.Id] + move;
                p.X = Math.Clamp(p.X, area.X + 30, area.Right - 30);
                p.Y = Math.Clamp(p.Y, area.Y + 14, area.Bottom - 14);
                _positions[n.Id] = p;
            }
            temp *= 0.95;
        }
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    private static readonly Pen EdgePen = MakePen(Color.FromRgb(120, 120, 130), 1.0);
    private static readonly Brush NodeBg = new SolidColorBrush(Color.FromArgb(220, 245, 245, 250));
    private static readonly Pen NodeBorder = MakePen(Color.FromRgb(120, 120, 160), 1.0);
    private static readonly Typeface NodeFace = new("Segoe UI");

    private static Pen MakePen(Color c, double w)
    {
        var p = new Pen(new SolidColorBrush(c), w);
        p.Freeze();
        return p;
    }

    private void DrawEdges(DrawingContext dc)
    {
        if (Edges is null) return;
        foreach (var e in Edges)
        {
            if (!_positions.TryGetValue(e.FromId, out var p1)) continue;
            if (!_positions.TryGetValue(e.ToId,   out var p2)) continue;
            dc.DrawLine(EdgePen, p1, p2);

            // Arrowhead.
            var dir = p2 - p1; var len = dir.Length;
            if (len < 0.01) continue;
            var unit = dir / len;
            var perp = new Vector(-unit.Y, unit.X);
            var tip  = p2 - unit * 8;
            dc.DrawLine(EdgePen, tip + perp * 4, p2);
            dc.DrawLine(EdgePen, tip - perp * 4, p2);
        }
    }

    private void DrawNodes(DrawingContext dc)
    {
        if (Nodes is null) return;
        foreach (var n in Nodes)
        {
            if (!_positions.TryGetValue(n.Id, out var p)) continue;
            var ft = new FormattedText(n.Label,
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, NodeFace, 11,
                Brushes.Black, 1.0);
            var w = ft.Width + 12;
            var h = ft.Height + 6;
            var rect = new Rect(p.X - w / 2, p.Y - h / 2, w, h);
            dc.DrawRoundedRectangle(NodeBg, NodeBorder, rect, 4, 4);
            dc.DrawText(ft, new Point(rect.X + 6, rect.Y + 3));
        }
    }

    // ── Click → activate node ────────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (Nodes is null) return;
        foreach (var n in Nodes)
        {
            if (!_positions.TryGetValue(n.Id, out var c)) continue;
            // Approximate hit-test: 80px x 22px box centered on c.
            if (Math.Abs(pos.X - c.X) < 50 && Math.Abs(pos.Y - c.Y) < 12)
            {
                NodeActivated?.Invoke(this, n);
                return;
            }
        }
    }
}
