// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Models/GridDefinitionModel.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     Immutable snapshot of a single Grid ColumnDefinition or RowDefinition
//     captured from the live rendered element at the time of adorner refresh.
//     Carries both the raw XAML string (e.g. "*", "Auto", "2*", "120") and
//     the actual rendered pixel size / offset needed for guide line placement.
// ==========================================================

using System.Globalization;

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>Size category of a Grid column or row definition.</summary>
public enum GridSizeType { Fixed, Auto, Star }

/// <summary>
/// Immutable snapshot of one Grid column or row definition with
/// rendered geometry data attached.
/// </summary>
public sealed record GridDefinitionModel(
    int          Index,
    bool         IsColumn,
    string       RawValue,
    GridSizeType SizeType,
    double       StarFactor,    // 1.0 for "*", 2.0 for "2*"
    double       FixedPixels,   // for Fixed only
    double       ActualPixels,  // rendered size after layout
    double       OffsetPixels)  // distance from Grid origin to this def's start
{
    /// <summary>Short label shown in the guide handle badge (e.g. "*", "Auto", "120").</summary>
    public string DisplayLabel => SizeType switch
    {
        GridSizeType.Auto  => "Auto",
        GridSizeType.Star  => StarFactor == 1.0 ? "*" : $"{StarFactor:G}*",
        GridSizeType.Fixed => $"{FixedPixels:G}",
        _                  => RawValue
    };

    /// <summary>Pixel position of this definition's END boundary.</summary>
    public double EndOffsetPixels => OffsetPixels + ActualPixels;

    /// <summary>Pixel position of this definition's CENTER (for handle placement).</summary>
    public double CenterOffsetPixels => OffsetPixels + ActualPixels / 2.0;

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Constructs a <see cref="GridDefinitionModel"/> from a raw XAML Width/Height string
    /// and the rendered pixel measurements from the live Grid element.
    /// </summary>
    public static GridDefinitionModel Parse(
        int    index,
        bool   isColumn,
        string rawValue,
        double actualPixels,
        double offsetPixels)
    {
        var (type, star, px) = ParseRaw(rawValue);
        return new GridDefinitionModel(index, isColumn, rawValue,
                                       type, star, px, actualPixels, offsetPixels);
    }

    // ── Raw value parser ──────────────────────────────────────────────────────

    private static (GridSizeType Type, double Star, double Px) ParseRaw(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) ||
            raw.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return (GridSizeType.Auto, 1.0, 0.0);

        if (raw.EndsWith('*'))
        {
            var prefix = raw[..^1];
            double factor = string.IsNullOrEmpty(prefix)
                ? 1.0
                : double.TryParse(prefix, NumberStyles.Float,
                                  CultureInfo.InvariantCulture, out var f) ? f : 1.0;
            return (GridSizeType.Star, factor, 0.0);
        }

        if (double.TryParse(raw, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var px))
            return (GridSizeType.Fixed, 1.0, px);

        // Fallback: treat as Auto.
        return (GridSizeType.Auto, 1.0, 0.0);
    }
}
