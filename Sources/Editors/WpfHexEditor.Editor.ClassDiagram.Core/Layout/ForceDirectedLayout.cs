// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Layout/ForceDirectedLayout.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Force-directed spring simulation layout (Fruchterman-Reingold).
//     Repulsion: O(n²) naive (sufficient for diagrams up to ~200 nodes).
//     Attraction along edges. Simulated annealing cooling schedule.
//
// Architecture Notes:
//     Implements ILayoutStrategy — pure BCL, no WPF or threading deps.
//     Runs synchronously; callers wrap in Task.Run if needed.
//     Results written directly onto ClassNode.X/Y in-place.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Layout;

/// <summary>
/// Fruchterman-Reingold force-directed layout with simulated annealing cooling.
/// </summary>
public sealed class ForceDirectedLayout : ILayoutStrategy
{
    public void Layout(DiagramDocument doc, LayoutOptions? options = null)
    {
        options ??= new LayoutOptions();
        var nodes = doc.Classes;
        if (nodes.Count == 0) return;

        // Size all nodes first
        SizeNodes(nodes, options);

        // Seed initial positions if all at origin
        if (nodes.All(n => n.X == 0 && n.Y == 0))
            SeedCircle(nodes, options);

        int    iterations   = options.ForceIterations;
        double spring       = options.SpringLength;
        double k            = options.RepulsionK;
        double temperature  = options.InitialTemperature;
        double cooling      = temperature / (iterations + 1);

        // Displacement accumulators
        var dx = new double[nodes.Count];
        var dy = new double[nodes.Count];

        // Build adjacency set for attraction
        var edges = BuildEdgeSet(doc);

        for (int iter = 0; iter < iterations; iter++)
        {
            Array.Clear(dx, 0, dx.Length);
            Array.Clear(dy, 0, dy.Length);

            // Repulsion between all pairs
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    double ddx = nodes[i].X - nodes[j].X;
                    double ddy = nodes[i].Y - nodes[j].Y;
                    double dist = Math.Sqrt(ddx * ddx + ddy * ddy);
                    if (dist < 1e-6) { dist = 1e-6; ddx = 1; ddy = 0; }

                    double force = k / (dist * dist);
                    dx[i] += ddx / dist * force;
                    dy[i] += ddy / dist * force;
                    dx[j] -= ddx / dist * force;
                    dy[j] -= ddy / dist * force;
                }
            }

            // Attraction along edges
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (i == j) continue;
                    if (!edges.Contains((nodes[i].Id, nodes[j].Id))) continue;

                    double ddx = nodes[j].X - nodes[i].X;
                    double ddy = nodes[j].Y - nodes[i].Y;
                    double dist = Math.Sqrt(ddx * ddx + ddy * ddy);
                    if (dist < 1e-6) continue;

                    double force = (dist - spring) / spring;
                    dx[i] += ddx / dist * force * 8;
                    dy[i] += ddy / dist * force * 8;
                }
            }

            // Apply displacements with temperature cap
            for (int i = 0; i < nodes.Count; i++)
            {
                double disp = Math.Sqrt(dx[i] * dx[i] + dy[i] * dy[i]);
                if (disp < 1e-6) continue;

                double scale = Math.Min(disp, temperature) / disp;
                nodes[i].X = Math.Max(options.CanvasPadding, nodes[i].X + dx[i] * scale);
                nodes[i].Y = Math.Max(options.CanvasPadding, nodes[i].Y + dy[i] * scale);
            }

            temperature -= cooling;
            if (temperature < 0.5) break;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void SizeNodes(List<ClassNode> nodes, LayoutOptions opts)
    {
        foreach (var n in nodes)
        {
            double w = Math.Max(opts.MinBoxWidth,
                Math.Max(n.Name.Length, n.Members.Count > 0 ? n.Members.Max(m => m.DisplayLabel.Length) : 0) * 7.5
                + opts.BoxPaddingH * 2);
            n.Width  = w;
            n.Height = opts.HeaderHeight + n.Members.Count * opts.MemberHeight + opts.BoxPaddingV * 2;
        }
    }

    private static void SeedCircle(List<ClassNode> nodes, LayoutOptions opts)
    {
        double radius = Math.Max(200, nodes.Count * 60);
        double step   = 2 * Math.PI / nodes.Count;
        double cx     = opts.CanvasPadding + radius;
        double cy     = opts.CanvasPadding + radius;

        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].X = cx + radius * Math.Cos(i * step);
            nodes[i].Y = cy + radius * Math.Sin(i * step);
        }
    }

    private static HashSet<(string, string)> BuildEdgeSet(DiagramDocument doc)
    {
        var set = new HashSet<(string, string)>();
        foreach (var r in doc.Relationships)
        {
            set.Add((r.SourceId, r.TargetId));
            set.Add((r.TargetId, r.SourceId));
        }
        return set;
    }
}
