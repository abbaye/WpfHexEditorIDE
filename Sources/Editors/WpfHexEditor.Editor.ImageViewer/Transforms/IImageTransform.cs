// ==========================================================
// Project: WpfHexEditor.Editor.ImageViewer
// File: IImageTransform.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Contract for a single non-destructive image transform step.
//     Each implementation applies one atomic operation (rotate, flip,
//     crop, resize) to a frozen BitmapSource and returns a new frozen
//     BitmapSource. The pipeline is serialised to JSON for persistence
//     via EditorConfigDto.Extra["Transforms"].
//
// Architecture Notes:
//     Pattern: Strategy — each transform is an interchangeable step
//     in an ImageTransformPipeline. Transforms are stateless value
//     objects and must never mutate the input BitmapSource.
//
// ==========================================================

using System.Windows.Media.Imaging;

namespace WpfHexEditor.Editor.ImageViewer.Transforms;

/// <summary>
/// Contract for a single non-destructive image transform step.
/// </summary>
public interface IImageTransform
{
    /// <summary>Human-readable name shown in the status bar dropdown.</summary>
    string Name { get; }

    /// <summary>
    /// Applies this transform to <paramref name="source"/> and returns
    /// a new frozen <see cref="BitmapSource"/>. The input is never mutated.
    /// </summary>
    BitmapSource Apply(BitmapSource source);

    /// <summary>Serialises this transform to a compact JSON-compatible dictionary.</summary>
    Dictionary<string, object> Serialize();
}
