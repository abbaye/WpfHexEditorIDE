// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignCanvasStats.cs
// Description:
//     Lightweight snapshot of canvas performance statistics.
//     Produced by DesignCanvas after each RenderXaml() call and
//     consumed by PerformanceOverlayAdorner for display.
//
// Architecture Notes:
//     Pure data record — no WPF dependencies.
//     All timing values in milliseconds. Element scan runs off-thread.
// ==========================================================

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>
/// Snapshot of canvas rendering performance metrics.
/// </summary>
public sealed record DesignCanvasStats(
    double LastRenderMs,
    int    ElementCount,
    int    MaxDepth,
    double Fps
)
{
    /// <summary>Empty / initial stats.</summary>
    public static readonly DesignCanvasStats Empty = new(0, 0, 0, 0);
}
