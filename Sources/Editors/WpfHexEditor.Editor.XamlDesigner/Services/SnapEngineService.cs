// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: SnapEngineService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Calculates snapped positions during drag-move and resize operations.
//     Supports grid-based snapping and edge-alignment snapping to sibling
//     elements visible on the design canvas.
//
// Architecture Notes:
//     Pure service — stateless calculations, no WPF rendering dependency.
//     Strategy pattern: snap targets (grid, siblings, canvas center) are
//     evaluated in priority order; closest within threshold wins.
// ==========================================================

using System.Windows;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Calculates snap-corrected positions for design-surface interactions.
/// </summary>
public sealed class SnapEngineService
{
    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>Grid cell size in device-independent pixels. Default: 8.</summary>
    public int GridSize { get; set; } = 8;

    /// <summary>Enables snapping to the virtual grid.</summary>
    public bool SnapToGrid { get; set; } = true;

    /// <summary>Enables edge-alignment snapping to sibling element boundaries.</summary>
    public bool SnapToElements { get; set; } = true;

    /// <summary>Maximum distance (dp) at which snapping activates.</summary>
    public double SnapThreshold { get; set; } = 6.0;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the snapped version of <paramref name="rawPoint"/>, given the
    /// bounding rectangles of sibling elements and the canvas bounds.
    /// </summary>
    /// <param name="rawPoint">Unsnapped position in canvas coordinates.</param>
    /// <param name="dragSize">Size of the element being dragged.</param>
    /// <param name="siblings">Bounding rects of peer elements (canvas-relative).</param>
    /// <param name="canvasBounds">Full bounds of the design canvas.</param>
    /// <param name="activeGuides">
    /// Receives the set of guide lines that should be drawn by SnapGuideOverlay.
    /// </param>
    public Point Snap(
        Point rawPoint,
        Size dragSize,
        IEnumerable<Rect> siblings,
        Rect canvasBounds,
        out IReadOnlyList<SnapGuide> activeGuides)
    {
        double x = rawPoint.X;
        double y = rawPoint.Y;
        var    guides = new List<SnapGuide>();

        if (SnapToGrid)
        {
            x = SnapToGridValue(x);
            y = SnapToGridValue(y);
        }

        if (SnapToElements)
        {
            double r  = x + dragSize.Width;
            double b  = y + dragSize.Height;
            double cx = x + dragSize.Width  / 2.0;
            double cy = y + dragSize.Height / 2.0;

            // Collect candidate snap X values: sibling left/center/right edges + canvas edges.
            var xCandidates = new List<(double value, double guideX, bool isCenter)>();
            var yCandidates = new List<(double value, double guideY, bool isCenter)>();

            AddEdgeCandidates(siblings, xCandidates, yCandidates);
            xCandidates.Add((canvasBounds.Left,  canvasBounds.Left,  false));
            xCandidates.Add((canvasBounds.Right, canvasBounds.Right, false));
            yCandidates.Add((canvasBounds.Top,    canvasBounds.Top,   false));
            yCandidates.Add((canvasBounds.Bottom, canvasBounds.Bottom,false));

            // Find best X snap.
            double bestXDist = SnapThreshold;
            foreach (var (val, guideX, isCenter) in xCandidates)
            {
                foreach (var edge in new[] { x, r, cx })
                {
                    double dist = Math.Abs(edge - val);
                    if (dist < bestXDist)
                    {
                        bestXDist = dist;
                        double offset = edge == r  ? val - dragSize.Width
                                       : edge == cx ? val - dragSize.Width / 2.0
                                       : val;
                        x = offset;
                        guides.RemoveAll(g => g.IsVertical);
                        // Distance label: gap between dragged element edge and snap target (skip center snaps).
                        string? label = !isCenter && dist > 0.5 ? $"{Math.Round(dist)}px" : null;
                        guides.Add(new SnapGuide(IsVertical: true, Position: guideX, DistanceLabel: label));
                    }
                }
            }

            // Find best Y snap.
            double bestYDist = SnapThreshold;
            foreach (var (val, guideY, isCenter) in yCandidates)
            {
                foreach (var edge in new[] { y, b, cy })
                {
                    double dist = Math.Abs(edge - val);
                    if (dist < bestYDist)
                    {
                        bestYDist = dist;
                        double offset = edge == b  ? val - dragSize.Height
                                       : edge == cy ? val - dragSize.Height / 2.0
                                       : val;
                        y = offset;
                        guides.RemoveAll(g => !g.IsVertical);
                        string? label = !isCenter && dist > 0.5 ? $"{Math.Round(dist)}px" : null;
                        guides.Add(new SnapGuide(IsVertical: false, Position: guideY, DistanceLabel: label));
                    }
                }
            }
        }

        activeGuides = guides;
        return new Point(x, y);
    }

    /// <summary>Snaps a scalar value to the configured grid.</summary>
    public double SnapToGridValue(double value)
    {
        if (GridSize <= 0) return value;
        return Math.Round(value / GridSize) * GridSize;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void AddEdgeCandidates(
        IEnumerable<Rect> siblings,
        List<(double, double, bool)> xList,
        List<(double, double, bool)> yList)
    {
        foreach (var r in siblings)
        {
            xList.Add((r.Left,                     r.Left,                     false));
            xList.Add((r.Right,                    r.Right,                    false));
            xList.Add((r.Left + r.Width  / 2.0,    r.Left + r.Width  / 2.0,   true));
            yList.Add((r.Top,                      r.Top,                      false));
            yList.Add((r.Bottom,                   r.Bottom,                   false));
            yList.Add((r.Top  + r.Height / 2.0,    r.Top  + r.Height / 2.0,   true));
        }
    }
}

/// <summary>A single snap guide line to be rendered by <see cref="Controls.SnapGuideOverlay"/>.</summary>
/// <param name="IsVertical">True for a vertical line, false for a horizontal line.</param>
/// <param name="Position">Canvas-relative coordinate of the guide (X if vertical, Y if horizontal).</param>
/// <param name="DistanceLabel">
/// Optional label shown on the guide (e.g. "24px") when snapping to a sibling edge.
/// Null means no label is drawn.
/// </param>
public sealed record SnapGuide(bool IsVertical, double Position, string? DistanceLabel = null);
