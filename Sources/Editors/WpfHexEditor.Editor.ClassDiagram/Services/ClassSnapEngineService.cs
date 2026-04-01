// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Services/ClassSnapEngineService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Provides grid and element-edge snapping for the diagram canvas.
//     Used by ClassInteractionService during drag-move operations.
//
// Architecture Notes:
//     Pattern: Strategy — snapping algorithm is isolated and injectable.
//     SnapToGrid and SnapToElements are independent and additive:
//     grid snap is applied first, then element edge refinement.
//     Snap threshold for element edges is fixed at 8 logical pixels.
// ==========================================================

using System.Windows;

namespace WpfHexEditor.Editor.ClassDiagram.Services;

/// <summary>
/// Provides configurable grid and element-edge snapping for diagram drag operations.
/// </summary>
public sealed class ClassSnapEngineService
{
    private const double ElementSnapThreshold = 8.0;

    // ---------------------------------------------------------------------------
    // Configuration properties
    // ---------------------------------------------------------------------------

    /// <summary>When true, coordinates are rounded to the nearest <see cref="GridSize"/> multiple.</summary>
    public bool SnapToGrid { get; set; } = true;

    /// <summary>When true, coordinates are attracted to edges of nearby elements.</summary>
    public bool SnapToElements { get; set; } = true;

    /// <summary>Grid cell size in logical pixels. Default: 8.</summary>
    public double GridSize { get; set; } = 8.0;

    // ---------------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Computes the snapped position for a point given the current snap settings
    /// and an enumeration of other elements described as (x, y, width, height) tuples.
    /// </summary>
    /// <param name="x">Raw X coordinate to snap.</param>
    /// <param name="y">Raw Y coordinate to snap.</param>
    /// <param name="others">Bounding rectangles of peer elements on the canvas.</param>
    /// <returns>Snapped <see cref="Point"/>.</returns>
    public Point SnapPoint(double x, double y, IEnumerable<(double x, double y, double w, double h)> others)
    {
        double snappedX = x;
        double snappedY = y;

        if (SnapToGrid)
        {
            snappedX = SnapToGridValue(snappedX);
            snappedY = SnapToGridValue(snappedY);
        }

        if (SnapToElements)
        {
            (snappedX, snappedY) = SnapToElementEdges(snappedX, snappedY, others);
        }

        return new Point(snappedX, snappedY);
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private double SnapToGridValue(double value) =>
        Math.Round(value / GridSize) * GridSize;

    private static (double x, double y) SnapToElementEdges(
        double x,
        double y,
        IEnumerable<(double x, double y, double w, double h)> others)
    {
        double bestX = x;
        double bestY = y;
        double minDeltaX = ElementSnapThreshold;
        double minDeltaY = ElementSnapThreshold;

        foreach (var (ox, oy, ow, oh) in others)
        {
            // Candidate X edges: left and right
            TrySnapEdge(x, ox, ref bestX, ref minDeltaX);
            TrySnapEdge(x, ox + ow, ref bestX, ref minDeltaX);

            // Candidate Y edges: top and bottom
            TrySnapEdge(y, oy, ref bestY, ref minDeltaY);
            TrySnapEdge(y, oy + oh, ref bestY, ref minDeltaY);
        }

        return (bestX, bestY);
    }

    private static void TrySnapEdge(double raw, double candidate, ref double best, ref double minDelta)
    {
        double delta = Math.Abs(raw - candidate);
        if (delta < minDelta)
        {
            minDelta = delta;
            best = candidate;
        }
    }
}
