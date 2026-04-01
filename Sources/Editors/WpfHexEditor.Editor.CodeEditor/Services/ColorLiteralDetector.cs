// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/ColorLiteralDetector.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Detects color literal tokens within a single line of source text using
//     the regex patterns from LanguageDefinition.ColorLiteralPatterns (whfmt-driven).
//     Returns a list of matches with the parsed Color for swatch rendering.
//
//     Supported syntaxes (defined per-language in .whfmt colorLiteralPatterns):
//       #AARRGGBB / #RRGGBB / #RGB  — hex literals (WPF ARGB order for 8-digit)
//       rgb(r,g,b)                  — RGB function
//       Colors.ColorName            — WPF named colors via reflection
//
// Architecture Notes:
//     Stateless — all methods are static.
//     Pattern matching is done entirely by the pre-compiled Regex list from LanguageDefinition.
//     Color parsing is done internally; no WPF API call needed for hex parsing.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>A single detected color literal on a line.</summary>
internal sealed record ColorLiteralMatch(
    /// <summary>0-based start column of the matched token.</summary>
    int StartColumn,
    /// <summary>0-based exclusive end column of the matched token.</summary>
    int EndColumn,
    /// <summary>Parsed color represented by the literal.</summary>
    Color Color);

/// <summary>
/// Detects color literals in source lines using whfmt-defined patterns.
/// </summary>
internal static class ColorLiteralDetector
{
    /// <summary>
    /// Scans <paramref name="line"/> for all color literals matching any pattern in
    /// <paramref name="patterns"/>. Returns an empty list when <paramref name="patterns"/> is null.
    /// </summary>
    public static List<ColorLiteralMatch> Detect(
        string line,
        IReadOnlyList<Regex>? patterns)
    {
        if (patterns is null || patterns.Count == 0 || string.IsNullOrEmpty(line))
            return [];

        var results = new List<ColorLiteralMatch>();

        foreach (var regex in patterns)
        {
            foreach (Match m in regex.Matches(line))
            {
                if (!m.Success) continue;
                if (!TryParseColor(m.Value, out var color)) continue;
                results.Add(new ColorLiteralMatch(m.Index, m.Index + m.Length, color));
            }
        }

        // Sort by start column and de-duplicate overlapping matches (keep longest).
        results.Sort((a, b) => a.StartColumn.CompareTo(b.StartColumn));
        return Deduplicate(results);
    }

    // -- Color parsers -----------------------------------------------------------

    /// <summary>
    /// Tries to parse a string value as a color.
    /// Supports #AARRGGBB, #RRGGBB, #RGB, rgb(r,g,b), and Colors.Name.
    /// </summary>
    private static bool TryParseColor(string value, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrEmpty(value)) return false;

        // Hex literals: #AARRGGBB (8), #RRGGBB (6), #RGB (3)
        if (value.StartsWith('#'))
        {
            string hex = value[1..];
            switch (hex.Length)
            {
                case 8: // #AARRGGBB
                    if (TryParseHex8(hex, out color)) return true;
                    break;
                case 6: // #RRGGBB
                    if (TryParseHex6(hex, out color)) return true;
                    break;
                case 3: // #RGB
                    if (TryParseHex3(hex, out color)) return true;
                    break;
            }
            return false;
        }

        // rgb(r,g,b) function
        if (value.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(')'))
            return TryParseRgb(value, out color);

        // Colors.Name — WPF named color via reflection
        if (value.StartsWith("Colors.", StringComparison.OrdinalIgnoreCase))
        {
            string name = value["Colors.".Length..];
            return TryParseNamedColor(name, out color);
        }

        // Bare name (e.g. "red" used as a CSS color keyword)
        return TryParseNamedColor(value, out color);
    }

    private static bool TryParseHex8(string hex, out Color color)
    {
        color = Colors.Transparent;
        if (!uint.TryParse(hex, NumberStyles.HexNumber, null, out uint v)) return false;
        byte a = (byte)((v >> 24) & 0xFF);
        byte r = (byte)((v >> 16) & 0xFF);
        byte g = (byte)((v >>  8) & 0xFF);
        byte b = (byte)( v        & 0xFF);
        color = Color.FromArgb(a, r, g, b);
        return true;
    }

    private static bool TryParseHex6(string hex, out Color color)
    {
        color = Colors.Transparent;
        if (!uint.TryParse(hex, NumberStyles.HexNumber, null, out uint v)) return false;
        byte r = (byte)((v >> 16) & 0xFF);
        byte g = (byte)((v >>  8) & 0xFF);
        byte b = (byte)( v        & 0xFF);
        color = Color.FromRgb(r, g, b);
        return true;
    }

    private static bool TryParseHex3(string hex, out Color color)
    {
        color = Colors.Transparent;
        if (hex.Length != 3) return false;
        if (!int.TryParse(hex[0..1], NumberStyles.HexNumber, null, out int r1)) return false;
        if (!int.TryParse(hex[1..2], NumberStyles.HexNumber, null, out int g1)) return false;
        if (!int.TryParse(hex[2..3], NumberStyles.HexNumber, null, out int b1)) return false;
        color = Color.FromRgb((byte)(r1 * 17), (byte)(g1 * 17), (byte)(b1 * 17));
        return true;
    }

    private static bool TryParseRgb(string value, out Color color)
    {
        color = Colors.Transparent;
        // value = "rgb(r,g,b)"
        int open  = value.IndexOf('(');
        int close = value.LastIndexOf(')');
        if (open < 0 || close < 0 || close <= open) return false;

        var parts = value[(open + 1)..close].Split(',');
        if (parts.Length != 3) return false;

        if (!byte.TryParse(parts[0].Trim(), out byte r)) return false;
        if (!byte.TryParse(parts[1].Trim(), out byte g)) return false;
        if (!byte.TryParse(parts[2].Trim(), out byte b)) return false;

        color = Color.FromRgb(r, g, b);
        return true;
    }

    private static bool TryParseNamedColor(string name, out Color color)
    {
        color = Colors.Transparent;
        try
        {
            var prop = typeof(Colors).GetProperty(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop?.GetValue(null) is Color c) { color = c; return true; }
        }
        catch { /* reflection failure */ }
        return false;
    }

    // -- Helpers -----------------------------------------------------------------

    private static List<ColorLiteralMatch> Deduplicate(List<ColorLiteralMatch> sorted)
    {
        if (sorted.Count <= 1) return sorted;
        var result = new List<ColorLiteralMatch>(sorted.Count) { sorted[0] };
        for (int i = 1; i < sorted.Count; i++)
        {
            var prev = result[result.Count - 1];
            var cur  = sorted[i];
            // Overlapping: keep the one with the later end (longer match wins).
            if (cur.StartColumn < prev.EndColumn)
            {
                if (cur.EndColumn > prev.EndColumn)
                    result[result.Count - 1] = cur;
            }
            else
            {
                result.Add(cur);
            }
        }
        return result;
    }
}
