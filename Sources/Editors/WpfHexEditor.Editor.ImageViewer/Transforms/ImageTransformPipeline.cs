// ==========================================================
// Project: WpfHexEditor.Editor.ImageViewer
// File: ImageTransformPipeline.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Ordered pipeline of non-destructive image transforms.
//     Applies each IImageTransform in sequence and returns the
//     final BitmapSource. Supports JSON round-trip serialisation
//     for persistence via EditorConfigDto.Extra["Transforms"].
//
// Architecture Notes:
//     Pattern: Pipeline / Chain of Responsibility
//     Serialisation uses System.Text.Json (no external deps).
//     The pipeline is NOT thread-safe — must be accessed on the UI thread.
//
// ==========================================================

using System.Text.Json;
using System.Windows.Media.Imaging;

namespace WpfHexEditor.Editor.ImageViewer.Transforms;

/// <summary>
/// Ordered, mutable list of <see cref="IImageTransform"/> steps.
/// Call <see cref="Apply"/> to obtain the final composed <see cref="BitmapSource"/>.
/// </summary>
public sealed class ImageTransformPipeline
{
    private readonly List<IImageTransform> _steps = [];

    public IReadOnlyList<IImageTransform> Steps => _steps;
    public int Count => _steps.Count;

    public void Add(IImageTransform transform) => _steps.Add(transform);

    public void RemoveAt(int index) => _steps.RemoveAt(index);

    public void Clear() => _steps.Clear();

    /// <summary>
    /// Applies all transforms in order to <paramref name="source"/>.
    /// Returns <paramref name="source"/> unchanged when the pipeline is empty.
    /// </summary>
    public BitmapSource Apply(BitmapSource source)
    {
        var current = source;
        foreach (var step in _steps)
            current = step.Apply(current);
        return current;
    }

    // ------------------------------------------------------------------
    // JSON serialisation (EditorConfigDto.Extra["Transforms"])
    // ------------------------------------------------------------------

    /// <summary>Serialises the pipeline to a compact JSON string.</summary>
    public string ToJson()
    {
        var list = _steps.Select(s => s.Serialize()).ToList();
        return JsonSerializer.Serialize(list);
    }

    /// <summary>
    /// Deserialises a JSON string produced by <see cref="ToJson"/> and
    /// populates a new <see cref="ImageTransformPipeline"/>.
    /// Returns an empty pipeline on parse failure.
    /// </summary>
    public static ImageTransformPipeline FromJson(string json)
    {
        var pipeline = new ImageTransformPipeline();
        try
        {
            var list = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
            if (list is null) return pipeline;

            foreach (var raw in list)
            {
                // Convert JsonElement values to object for the Deserialize helpers
                var dict = raw.ToDictionary(
                    kv => kv.Key,
                    kv => (object)(kv.Value.ValueKind == JsonValueKind.Number
                        ? (object)kv.Value.GetDouble()
                        : kv.Value.GetString() ?? string.Empty));

                if (!dict.TryGetValue("t", out var typeRaw)) continue;
                var type = typeRaw?.ToString();

                IImageTransform? transform = type switch
                {
                    "rotate" => RotateImageTransform.Deserialize(dict),
                    "flip"   => FlipImageTransform.Deserialize(dict),
                    "crop"   => CropImageTransform.Deserialize(dict),
                    "resize" => ResizeImageTransform.Deserialize(dict),
                    _        => null
                };

                if (transform is not null) pipeline.Add(transform);
            }
        }
        catch
        {
            // Return empty pipeline rather than crashing on corrupt data
        }
        return pipeline;
    }
}
