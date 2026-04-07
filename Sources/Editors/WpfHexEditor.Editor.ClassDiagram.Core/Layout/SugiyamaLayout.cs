// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Layout/SugiyamaLayout.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Sugiyama-style layered graph drawing with barycenter crossing
//     minimisation. Produces clean top-down inheritance trees.
//
//     Algorithm:
//       1. Topological sort + layer assignment (longest-path).
//       2. Barycenter heuristic: sort nodes in each layer by median
//          X-position of their neighbours in the adjacent layer.
//       3. Coordinate assignment: horizontal centering per layer.
//
// Architecture Notes:
//     Implements ILayoutStrategy — pure BCL, no WPF deps.
//     The existing AutoLayoutEngine is superseded by this class when
//     LayoutStrategyKind.Sugiyama is selected.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Layout;

/// <summary>
/// Sugiyama layered layout with barycenter crossing minimisation.
/// </summary>
public sealed class SugiyamaLayout : ILayoutStrategy
{
    public void Layout(DiagramDocument doc, LayoutOptions? options = null)
    {
        options ??= new LayoutOptions();
        var nodes = doc.Classes;
        if (nodes.Count == 0) return;

        SizeNodes(nodes, options);

        // Build directed edge list (Inheritance + Realization only for layers)
        var edges = doc.Relationships
            .Where(r => r.Kind is RelationshipKind.Inheritance or RelationshipKind.Realization)
            .Select(r => (r.SourceId, r.TargetId))
            .ToList();

        // Longest-path layer assignment
        var layerOf = LongestPathLayers(nodes, edges);

        // Group by layer
        var layerGroups = layerOf
            .GroupBy(kv => kv.Value)
            .OrderBy(g => g.Key)
            .Select(g =>
                g.Select(kv => nodes.First(n => n.Id == kv.Key)).ToList())
            .ToList();

        // Barycenter passes (2 sweeps: top-down + bottom-up)
        var adjDown = BuildAdj(edges, forward: true);
        var adjUp   = BuildAdj(edges, forward: false);

        for (int pass = 0; pass < 3; pass++)
        {
            // Top-down sweep
            for (int li = 1; li < layerGroups.Count; li++)
                ReorderByBarycenter(layerGroups[li], layerGroups[li - 1], adjDown);

            // Bottom-up sweep
            for (int li = layerGroups.Count - 2; li >= 0; li--)
                ReorderByBarycenter(layerGroups[li], layerGroups[li + 1], adjUp);
        }

        // Coordinate assignment
        double currentY = options.CanvasPadding;
        double maxWidth  = 0;

        foreach (var layer in layerGroups)
        {
            double layerW = layer.Sum(n => n.Width) + (layer.Count - 1) * options.ColSpacing;
            maxWidth = Math.Max(maxWidth, layerW);
        }

        foreach (var layer in layerGroups)
        {
            double layerW  = layer.Sum(n => n.Width) + (layer.Count - 1) * options.ColSpacing;
            double startX  = options.CanvasPadding + (maxWidth - layerW) / 2.0;
            double maxH    = layer.Max(n => n.Height);
            double currentX = startX;

            foreach (var node in layer)
            {
                node.X = currentX;
                node.Y = currentY;
                currentX += node.Width + options.ColSpacing;
            }

            currentY += maxH + options.RowSpacing;
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

    private static Dictionary<string, int> LongestPathLayers(
        List<ClassNode> nodes, List<(string Src, string Tgt)> edges)
    {
        var layerOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var inDegree = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);

        foreach (var (src, tgt) in edges)
            if (inDegree.ContainsKey(src)) inDegree[src]++;

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        foreach (var id in queue) layerOf[id] = 0;

        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            foreach (var (src, tgt) in edges.Where(e => e.Tgt == id))
            {
                int next = layerOf[id] + 1;
                if (!layerOf.TryGetValue(src, out int cur) || cur < next)
                {
                    layerOf[src] = next;
                    queue.Enqueue(src);
                }
            }
        }

        // Assign isolated nodes to layer 0
        foreach (var n in nodes.Where(n => !layerOf.ContainsKey(n.Id)))
            layerOf[n.Id] = 0;

        return layerOf;
    }

    private static Dictionary<string, List<string>> BuildAdj(
        List<(string Src, string Tgt)> edges, bool forward)
    {
        var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (src, tgt) in edges)
        {
            string from = forward ? tgt : src;
            string to   = forward ? src : tgt;
            if (!adj.ContainsKey(from)) adj[from] = [];
            adj[from].Add(to);
        }
        return adj;
    }

    private static void ReorderByBarycenter(
        List<ClassNode> layer,
        List<ClassNode> refLayer,
        Dictionary<string, List<string>> adj)
    {
        var refX = refLayer
            .Select((n, i) => (n.Id, X: (double)i))
            .ToDictionary(t => t.Id, t => t.X, StringComparer.Ordinal);

        layer.Sort((a, b) =>
        {
            double bA = Barycenter(a.Id, adj, refX);
            double bB = Barycenter(b.Id, adj, refX);
            return bA.CompareTo(bB);
        });
    }

    private static double Barycenter(
        string id,
        Dictionary<string, List<string>> adj,
        Dictionary<string, double> refX)
    {
        if (!adj.TryGetValue(id, out var neighbors) || neighbors.Count == 0)
            return double.MaxValue;

        double sum = 0; int count = 0;
        foreach (var n in neighbors)
            if (refX.TryGetValue(n, out double x)) { sum += x; count++; }

        return count > 0 ? sum / count : double.MaxValue;
    }
}
