// ==========================================================
// Project: WpfHexEditor.Editor.ImageViewer
// File: FlipImageTransform.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Non-destructive flip transform. Supports horizontal and
//     vertical mirroring via WPF TransformedBitmap + ScaleTransform.
//
// Architecture Notes:
//     Pattern: Strategy (IImageTransform)
//     ScaleTransform(-1,1) = Horizontal, ScaleTransform(1,-1) = Vertical.
//
// ==========================================================

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfHexEditor.Editor.ImageViewer.Transforms;

/// <summary>Flip axis: horizontal (left-right mirror) or vertical (top-bottom mirror).</summary>
public enum FlipAxis { Horizontal, Vertical }

/// <summary>Flips an image along a horizontal or vertical axis.</summary>
public sealed class FlipImageTransform : IImageTransform
{
    public FlipAxis Axis { get; }

    public FlipImageTransform(FlipAxis axis) => Axis = axis;

    public string Name => Axis == FlipAxis.Horizontal ? "Flip Horizontal" : "Flip Vertical";

    public BitmapSource Apply(BitmapSource source)
    {
        var scale = Axis == FlipAxis.Horizontal
            ? new ScaleTransform(-1, 1, source.PixelWidth  / 2.0, source.PixelHeight / 2.0)
            : new ScaleTransform( 1, -1, source.PixelWidth / 2.0, source.PixelHeight / 2.0);

        var tb = new TransformedBitmap(source, scale);
        tb.Freeze();
        return tb;
    }

    public Dictionary<string, object> Serialize() =>
        new() { ["t"] = "flip", ["ax"] = Axis == FlipAxis.Horizontal ? "H" : "V" };

    public static FlipImageTransform? Deserialize(Dictionary<string, object> data)
    {
        if (!data.TryGetValue("ax", out var raw)) return null;
        var axis = raw?.ToString() == "H" ? FlipAxis.Horizontal : FlipAxis.Vertical;
        return new FlipImageTransform(axis);
    }
}
