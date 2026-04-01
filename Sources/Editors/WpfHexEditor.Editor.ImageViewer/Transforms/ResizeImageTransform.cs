// ==========================================================
// Project: WpfHexEditor.Editor.ImageViewer
// File: ResizeImageTransform.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Non-destructive resize transform. Resamples the source
//     BitmapSource to target dimensions using the selected algorithm.
//
// Architecture Notes:
//     Pattern: Strategy (IImageTransform)
//     Uses BitmapScalingMode to select the resampling algorithm.
//     RenderTargetBitmap is used to rasterise the scaled result.
//
// ==========================================================

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfHexEditor.Editor.ImageViewer.Transforms;

/// <summary>Resampling algorithm to use during resize.</summary>
public enum ResizeAlgorithm { NearestNeighbor, Bilinear, Bicubic }

/// <summary>Resamples an image to target pixel dimensions.</summary>
public sealed class ResizeImageTransform : IImageTransform
{
    public int TargetWidth  { get; }
    public int TargetHeight { get; }
    public ResizeAlgorithm Algorithm { get; }

    public ResizeImageTransform(int width, int height, ResizeAlgorithm algorithm = ResizeAlgorithm.Bilinear)
    {
        TargetWidth  = width;
        TargetHeight = height;
        Algorithm    = algorithm;
    }

    public string Name => $"Resize {TargetWidth}Ã—{TargetHeight}";

    public BitmapSource Apply(BitmapSource source)
    {
        var scalingMode = Algorithm switch
        {
            ResizeAlgorithm.NearestNeighbor => BitmapScalingMode.NearestNeighbor,
            ResizeAlgorithm.Bicubic         => BitmapScalingMode.HighQuality,
            _                               => BitmapScalingMode.Linear
        };

        double dpiX = source.DpiX > 0 ? source.DpiX : 96.0;
        double dpiY = source.DpiY > 0 ? source.DpiY : 96.0;

        var drawingVisual = new DrawingVisual();
        using (var ctx = drawingVisual.RenderOpen())
        {
            RenderOptions.SetBitmapScalingMode(drawingVisual, scalingMode);
            ctx.DrawImage(source, new Rect(0, 0, TargetWidth, TargetHeight));
        }

        var rtb = new RenderTargetBitmap(TargetWidth, TargetHeight, dpiX, dpiY, PixelFormats.Pbgra32);
        rtb.Render(drawingVisual);
        rtb.Freeze();
        return rtb;
    }

    public Dictionary<string, object> Serialize() =>
        new() { ["t"] = "resize", ["w"] = TargetWidth, ["h"] = TargetHeight, ["alg"] = (int)Algorithm };

    public static ResizeImageTransform? Deserialize(Dictionary<string, object> data)
    {
        if (!TryGetInt(data, "w", out int w) || !TryGetInt(data, "h", out int h)) return null;
        var alg = data.TryGetValue("alg", out var algRaw) &&
                  int.TryParse(algRaw?.ToString(), out int algInt)
                  ? (ResizeAlgorithm)algInt
                  : ResizeAlgorithm.Bilinear;
        return new ResizeImageTransform(w, h, alg);
    }

    private static bool TryGetInt(Dictionary<string, object> data, string key, out int value)
    {
        value = 0;
        return data.TryGetValue(key, out var raw) &&
               int.TryParse(raw?.ToString(), out value);
    }
}
