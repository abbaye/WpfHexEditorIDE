// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: Bridge/SynalysisToBackgroundBlockBridge.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Converts SynalysisColorRegion DTOs (from the Core interpreter) into
//     CustomBackgroundBlock instances consumed by the HexEditor overlay system.
//
// Architecture Notes:
//     Pattern: Adapter / Bridge
//     All generated blocks carry the "synalysis:" prefix in their Description
//     so that ClearCustomBackgroundBlockByTag("synalysis:") can selectively
//     remove them without touching user-created or WHFMT-generated blocks.
//     Theme: no WPF theme resource used here — colours come from the grammar.
// ==========================================================

using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Core.SynalysisGrammar;

namespace WpfHexEditor.Plugins.SynalysisGrammar.Bridge;

/// <summary>
/// Converts <see cref="SynalysisColorRegion"/> list into
/// <see cref="CustomBackgroundBlock"/> list for hex-view overlay rendering.
/// </summary>
public static class SynalysisToBackgroundBlockBridge
{
    private const string TagPrefix = "synalysis:";

    /// <summary>
    /// Converts a collection of colour regions into CustomBackgroundBlock instances.
    /// Returns an empty list when <paramref name="regions"/> is null or empty.
    /// </summary>
    public static IReadOnlyList<CustomBackgroundBlock> Convert(
        IReadOnlyList<SynalysisColorRegion>? regions)
    {
        if (regions is null || regions.Count == 0)
            return [];

        var result = new List<CustomBackgroundBlock>(regions.Count);

        foreach (var region in regions)
        {
            if (region is null) continue;
            var brush = ParseBrush(region.Color);
            if (brush is null) continue;

            result.Add(new CustomBackgroundBlock(
                start:       region.Offset,
                length:      region.Length,
                color:       brush,
                description: TagPrefix + region.Description,
                opacity:     region.Opacity));
        }

        return result;
    }

    // -- Helpers -----------------------------------------------------------

    private static SolidColorBrush? ParseBrush(string colorHex)
    {
        if (string.IsNullOrEmpty(colorHex)) return null;

        try
        {
            // Grammar colours are 6-char hex without leading '#'.
            var hex = colorHex.StartsWith('#') ? colorHex : "#" + colorHex;
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch
        {
            return null;
        }
    }
}
