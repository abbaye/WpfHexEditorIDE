// ==========================================================
// Project: WpfHexEditor.Editor.ImageViewer
// File: CropImageTransform.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Non-destructive crop transform. Crops the source BitmapSource
//     to a given pixel rectangle using WPF CroppedBitmap.
//
// Architecture Notes:
//     Pattern: Strategy (IImageTransform)
//     CroppedBitmap is zero-copy when the source is already frozen.
//
// ==========================================================

using System.Windows;
using System.Windows.Media.Imaging;

namespace WpfHexEditor.Editor.ImageViewer.Transforms;

/// <summary>Crops an image to a pixel-coordinate rectangle.</summary>
public sealed class CropImageTransform : IImageTransform
{
    public Int32Rect Rect { get; }

    public CropImageTransform(Int32Rect rect) => Rect = rect;

    public string Name => $"Crop ({Rect.X},{Rect.Y}) {Rect.Width}Ã—{Rect.Height}";

    public BitmapSource Apply(BitmapSource source)
    {
        // Clamp rect to actual bitmap bounds to avoid ArgumentException
        int x = Math.Max(0, Rect.X);
        int y = Math.Max(0, Rect.Y);
        int w = Math.Min(Rect.Width,  source.PixelWidth  - x);
        int h = Math.Min(Rect.Height, source.PixelHeight - y);

        if (w <= 0 || h <= 0) return source;

        var cb = new CroppedBitmap(source, new Int32Rect(x, y, w, h));
        cb.Freeze();
        return cb;
    }

    public Dictionary<string, object> Serialize() =>
        new() { ["t"] = "crop", ["x"] = Rect.X, ["y"] = Rect.Y, ["w"] = Rect.Width, ["h"] = Rect.Height };

    public static CropImageTransform? Deserialize(Dictionary<string, object> data)
    {
        if (!TryGetInt(data, "x", out int x) || !TryGetInt(data, "y", out int y) ||
            !TryGetInt(data, "w", out int w) || !TryGetInt(data, "h", out int h))
            return null;
        return new CropImageTransform(new Int32Rect(x, y, w, h));
    }

    private static bool TryGetInt(Dictionary<string, object> data, string key, out int value)
    {
        value = 0;
        return data.TryGetValue(key, out var raw) &&
               int.TryParse(raw?.ToString(), out value);
    }
}
