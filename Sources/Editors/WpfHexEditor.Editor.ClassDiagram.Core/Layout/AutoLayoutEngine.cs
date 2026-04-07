// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Layout/AutoLayoutEngine.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Layered auto-layout engine for class diagrams.  Builds an
//     inheritance-based BFS forest, assigns nodes to layers, computes
//     box sizes from member labels, and positions boxes in a grid with
//     inter-layer and intra-layer spacing.  Results are written directly
//     onto each ClassNode's X/Y/Width/Height properties.
//
// Architecture Notes:
//     Pattern: Strategy — the layout algorithm is encapsulated in a
//     single class so alternative algorithms (e.g. force-directed) can
//     be substituted without touching the caller.
//
//     Algorithm overview:
//       1. Build parent→children map from Inheritance relationships.
//       2. BFS from roots (nodes not referenced as a child) to assign layers.
//       3. Nodes unreachable by BFS (isolated, or non-inheritance edges only)
//          are placed in their own final layer.
//       4. Compute Width = max(MinBoxWidth, widest label + 2*BoxPaddingH).
//       5. Compute Height = HeaderHeight + MemberCount * MemberHeight + 2*BoxPaddingV.
//       6. Within each layer, nodes are placed left→right; the layer is
//          centred against the widest layer.
//       7. Canvas origin is offset by CanvasPadding.
//
//     No WPF or System.Drawing dependencies — uses only BCL maths.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Layout;

/// <summary>
/// Computes canvas positions and box sizes for all nodes in a
/// <see cref="DiagramDocument"/> using a BFS layer assignment algorithm.
/// </summary>
/// <summary>
/// Creates the appropriate <see cref="ILayoutStrategy"/> for a given <see cref="LayoutStrategyKind"/>.
/// </summary>
public static class LayoutStrategyFactory
{
    public static ILayoutStrategy Create(LayoutStrategyKind kind) => kind switch
    {
        LayoutStrategyKind.ForceDirected => new ForceDirectedLayout(),
        LayoutStrategyKind.Sugiyama      => new SugiyamaLayout(),
        _                                => new AutoLayoutEngine()
    };
}

public sealed class AutoLayoutEngine : ILayoutStrategy
{
    /// <summary>
    /// Assigns <see cref="ClassNode.X"/>, <see cref="ClassNode.Y"/>,
    /// <see cref="ClassNode.Width"/>, and <see cref="ClassNode.Height"/>
    /// for every node in <paramref name="doc"/>.
    /// </summary>
    /// <param name="doc">The document to lay out in place.</param>
    /// <param name="options">
    /// Spacing/sizing options, or <see langword="null"/> to use defaults.
    /// </param>
    public void Layout(DiagramDocument doc, LayoutOptions? options = null)
    {
        options ??= new LayoutOptions();

        if (doc.Classes.Count == 0)
            return;

        // Step 1 — Build inheritance map (parent id → child ids)
        var childrenOf = BuildChildrenMap(doc);
        var allChildIds = childrenOf.Values.SelectMany(v => v).ToHashSet(StringComparer.Ordinal);

        // Step 2 — BFS layer assignment starting from roots
        var roots = doc.Classes
            .Where(n => !allChildIds.Contains(n.Id))
            .ToList();

        var layerOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var queue = new Queue<(ClassNode node, int layer)>();

        foreach (var root in roots)
            queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var (node, layer) = queue.Dequeue();

            // Only advance a node to a deeper layer
            if (layerOf.TryGetValue(node.Id, out var existing) && existing >= layer)
                continue;

            layerOf[node.Id] = layer;

            if (childrenOf.TryGetValue(node.Id, out var children))
            {
                foreach (var childId in children)
                {
                    var childNode = doc.Classes.FirstOrDefault(
                        c => string.Equals(c.Id, childId, StringComparison.Ordinal));

                    if (childNode is not null)
                        queue.Enqueue((childNode, layer + 1));
                }
            }
        }

        // Step 3 — Assign isolated nodes (not reached by BFS) to a final layer
        var maxLayer = layerOf.Count > 0 ? layerOf.Values.Max() : 0;
        foreach (var node in doc.Classes)
        {
            if (!layerOf.ContainsKey(node.Id))
                layerOf[node.Id] = maxLayer + 1;
        }

        // Step 4 — Compute box sizes
        foreach (var node in doc.Classes)
        {
            var (w, h) = ComputeBoxSize(node, options);
            node.Width = w;
            node.Height = h;
        }

        // Step 5 — Group nodes by layer and compute layer widths
        var layers = layerOf
            .GroupBy(kv => kv.Value)
            .OrderBy(g => g.Key)
            .Select(g => g.Select(kv =>
                    doc.Classes.First(n => string.Equals(n.Id, kv.Key, StringComparison.Ordinal)))
                .ToList())
            .ToList();

        // Layer total width = sum of node widths + inter-node spacing
        var layerTotalWidths = layers
            .Select(layer => layer.Sum(n => n.Width) + (layer.Count - 1) * options.ColSpacing)
            .ToList();

        var maxLayerWidth = layerTotalWidths.Max();

        // Step 6 — Assign X/Y positions
        var currentY = options.CanvasPadding;

        for (var li = 0; li < layers.Count; li++)
        {
            var layer = layers[li];
            var layerWidth = layerTotalWidths[li];

            // Centre this layer relative to the widest layer
            var startX = options.CanvasPadding + (maxLayerWidth - layerWidth) / 2.0;
            var currentX = startX;

            // Find max height in the layer for next-row offset
            var maxHeight = layer.Max(n => n.Height);

            foreach (var node in layer)
            {
                node.X = currentX;
                node.Y = currentY;
                currentX += node.Width + options.ColSpacing;
            }

            currentY += maxHeight + options.RowSpacing;
        }
    }

    // -------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------

    private static Dictionary<string, List<string>> BuildChildrenMap(DiagramDocument doc)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var rel in doc.Relationships)
        {
            if (rel.Kind != RelationshipKind.Inheritance)
                continue;

            // Inheritance: source derives from target — target is the parent
            if (!map.ContainsKey(rel.TargetId))
                map[rel.TargetId] = [];

            map[rel.TargetId].Add(rel.SourceId);
        }

        return map;
    }

    private static (double Width, double Height) ComputeBoxSize(
        ClassNode node, LayoutOptions opts)
    {
        // Widest label determines box width
        var longestLabel = node.Members.Count > 0
            ? node.Members.Max(m => m.DisplayLabel.Length)
            : 0;

        // Approximate character width: 7px per character (monospace estimate)
        const double charWidth = 7.0;

        // Header label also contributes
        var headerLabelWidth = node.Name.Length * charWidth;
        var memberLabelWidth = longestLabel * charWidth;
        var contentWidth = Math.Max(headerLabelWidth, memberLabelWidth) + 2 * opts.BoxPaddingH;

        var width = Math.Max(opts.MinBoxWidth, contentWidth);
        var height = opts.HeaderHeight
                     + node.Members.Count * opts.MemberHeight
                     + 2 * opts.BoxPaddingV;

        return (width, height);
    }
}
