// ==========================================================
// Project: WpfHexEditor.Editor.ImageViewer
// File: RotateImageTransform.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Non-destructive rotation transform. Supports 90, 180, 270
//     degrees clockwise via WPF TransformedBitmap + RotateTransform.
//
// Architecture Notes:
//     Pattern: Strategy (IImageTransform)
//     Uses WPF native TransformedBitmap — no pixel copy, GPU path.
//
// ==========================================================

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfHexEditor.Editor.ImageViewer.Transforms;

/// <summary>Rotates an image by a multiple of 90 degrees clockwise.</summary>
public sealed class RotateImageTransform : IImageTransform
{
    /// <summary>Rotation angle in degrees. Must be 90, 180, or 270.</summary>
    public double Angle { get; }

    public RotateImageTransform(double angle) => Angle = angle;

    public string Name => $"Rotate {Angle}°";

    public BitmapSource Apply(BitmapSource source)
    {
        var tb = new TransformedBitmap(source, new RotateTransform(Angle));
        tb.Freeze();
        return tb;
    }

    public Dictionary<string, object> Serialize() =>
        new() { ["t"] = "rotate", ["a"] = Angle };

    public static RotateImageTransform? Deserialize(Dictionary<string, object> data)
    {
        if (!data.TryGetValue("a", out var raw)) return null;
        return double.TryParse(raw?.ToString(), out var angle)
            ? new RotateImageTransform(angle)
            : null;
    }
}
