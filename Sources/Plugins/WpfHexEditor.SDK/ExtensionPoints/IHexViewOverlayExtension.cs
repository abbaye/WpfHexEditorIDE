// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/IHexViewOverlayExtension.cs
// Created: 2026-03-15
// Description:
//     Extension point contract for plugins that render overlays on the hex view.
//     Contributors provide highlight regions, annotations, and color bands.
// ==========================================================

namespace WpfHexEditor.SDK.ExtensionPoints;

/// <summary>
/// Extension point contract: hex view overlay.
/// Plugins implementing this contribute highlight regions and annotations
/// rendered directly on the hex editor view.
/// Register in manifest: <c>"extensions": { "HexViewOverlay": "MyPlugin.MyOverlayClass" }</c>
/// </summary>
public interface IHexViewOverlayExtension
{
    /// <summary>Display name for this overlay contributor (e.g. "PE Header Regions").</summary>
    string OverlayName { get; }

    /// <summary>
    /// Returns overlay regions for the visible byte range [<paramref name="offset"/>, <paramref name="offset"/> + <paramref name="length"/>].
    /// Called by the hex view renderer on each scroll / redraw.
    /// Implementations must be fast — avoid I/O on this hot path.
    /// </summary>
    IEnumerable<HexOverlayRegion> GetOverlays(byte[] visibleData, long offset, long length);
}

/// <summary>A colored/annotated region in the hex view.</summary>
public sealed record HexOverlayRegion(
    long StartOffset,
    long EndOffset,
    string Color,           // HTML hex color, e.g. "#FF6B6B"
    string? Tooltip = null,
    string? Label   = null);
