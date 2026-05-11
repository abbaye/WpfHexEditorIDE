// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Services/DiagramAlignmentService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Phase 3C — multi-select align/distribute operations on a set of
//     ClassNodes. Each operation captures before/after positions and
//     produces a single SingleClassDiagramUndoEntry that restores the
//     starting layout atomically.
//
// Architecture Notes:
//     Stateless service — instance methods read the supplied node list
//     and never retain references. Each method returns a single undo
//     entry the caller pushes into ClassDiagramUndoManager.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Services;

/// <summary>How to align a set of nodes along an edge or centre.</summary>
public enum AlignmentKind
{
    Left, Right, Top, Bottom, HorizontalCentre, VerticalCentre
}

/// <summary>How to distribute a set of nodes along an axis.</summary>
public enum DistributionKind
{
    Horizontal, Vertical
}

/// <summary>
/// Stateless service providing the eight multi-select layout operations
/// the toolbar can invoke: 6 alignments, 2 distributions, equalize sizes.
/// All methods return an <see cref="IClassDiagramUndoEntry"/> the caller
/// pushes onto the undo manager.
/// </summary>
public static class DiagramAlignmentService
{
    /// <summary>Aligns nodes along the chosen edge/centre.</summary>
    public static IClassDiagramUndoEntry Align(
        IReadOnlyList<ClassNode> nodes, AlignmentKind kind, Action onApply)
    {
        var before = Snapshot(nodes);

        switch (kind)
        {
            case AlignmentKind.Left:
            {
                double x = nodes.Min(n => n.X);
                foreach (var n in nodes) n.X = x;
                break;
            }
            case AlignmentKind.Right:
            {
                double x = nodes.Max(n => n.X + n.Width);
                foreach (var n in nodes) n.X = x - n.Width;
                break;
            }
            case AlignmentKind.Top:
            {
                double y = nodes.Min(n => n.Y);
                foreach (var n in nodes) n.Y = y;
                break;
            }
            case AlignmentKind.Bottom:
            {
                double y = nodes.Max(n => n.Y + n.Height);
                foreach (var n in nodes) n.Y = y - n.Height;
                break;
            }
            case AlignmentKind.HorizontalCentre:
            {
                double cx = nodes.Average(n => n.X + n.Width / 2.0);
                foreach (var n in nodes) n.X = cx - n.Width / 2.0;
                break;
            }
            case AlignmentKind.VerticalCentre:
            {
                double cy = nodes.Average(n => n.Y + n.Height / 2.0);
                foreach (var n in nodes) n.Y = cy - n.Height / 2.0;
                break;
            }
        }
        onApply();
        return BuildUndo(nodes, before, $"Align {kind}", onApply);
    }

    /// <summary>Distributes nodes evenly along the horizontal or vertical axis.</summary>
    public static IClassDiagramUndoEntry Distribute(
        IReadOnlyList<ClassNode> nodes, DistributionKind kind, Action onApply)
    {
        var before = Snapshot(nodes);
        if (nodes.Count < 3) return BuildUndo(nodes, before, $"Distribute {kind}", onApply);

        if (kind == DistributionKind.Horizontal)
        {
            var sorted = nodes.OrderBy(n => n.X).ToList();
            double left = sorted[0].X;
            double right = sorted[^1].X;
            double step = (right - left) / (sorted.Count - 1);
            for (int i = 1; i < sorted.Count - 1; i++)
                sorted[i].X = left + step * i;
        }
        else
        {
            var sorted = nodes.OrderBy(n => n.Y).ToList();
            double top = sorted[0].Y;
            double bottom = sorted[^1].Y;
            double step = (bottom - top) / (sorted.Count - 1);
            for (int i = 1; i < sorted.Count - 1; i++)
                sorted[i].Y = top + step * i;
        }
        onApply();
        return BuildUndo(nodes, before, $"Distribute {kind}", onApply);
    }

    /// <summary>Resizes every node to the dimensions of the first one in the set.</summary>
    public static IClassDiagramUndoEntry EqualizeSize(
        IReadOnlyList<ClassNode> nodes, Action onApply)
    {
        var before = Snapshot(nodes);
        if (nodes.Count < 2) return BuildUndo(nodes, before, "Equalize size", onApply);

        double w = nodes[0].Width;
        double h = nodes[0].Height;
        foreach (var n in nodes) { n.Width = w; n.Height = h; }
        onApply();
        return BuildUndo(nodes, before, "Equalize size", onApply);
    }

    // ── Snapshot + undo helpers ──────────────────────────────────────────────

    private static Dictionary<string, (double X, double Y, double W, double H)>
        Snapshot(IEnumerable<ClassNode> nodes) =>
            nodes.ToDictionary(n => n.Id, n => (n.X, n.Y, n.Width, n.Height));

    private static IClassDiagramUndoEntry BuildUndo(
        IReadOnlyList<ClassNode> nodes,
        Dictionary<string, (double X, double Y, double W, double H)> before,
        string description,
        Action onApply)
    {
        // Capture after-state right now so Redo restores it without recomputing.
        var after = Snapshot(nodes);

        return new SingleClassDiagramUndoEntry(
            Description: description,
            UndoAction: () =>
            {
                foreach (var n in nodes)
                    if (before.TryGetValue(n.Id, out var s))
                    { n.X = s.X; n.Y = s.Y; n.Width = s.W; n.Height = s.H; }
                onApply();
            },
            RedoAction: () =>
            {
                foreach (var n in nodes)
                    if (after.TryGetValue(n.Id, out var s))
                    { n.X = s.X; n.Y = s.Y; n.Width = s.W; n.Height = s.H; }
                onApply();
            });
    }
}
