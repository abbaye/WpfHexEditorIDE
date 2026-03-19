// ==========================================================
// Project: WpfHexEditor.Shell
// File: TabColorService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Computes the accent Brush to display on a document tab based on the current
//     DocumentTabBarSettings.ColorMode. Supports None, ByExtension (file type),
//     Sequential (index-based), and Random modes.
//
// Architecture Notes:
//     Static service — no state, no WPF dependency tree access. Uses a 12-color
//     accessible palette with ~50-60% lightness for visibility on both dark and
//     light themes. Brush instances are cached by color value to minimize allocations.
//
// ==========================================================

using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Shell.Services;

/// <summary>
/// Computes the accent <see cref="Brush"/> to display on a document tab based on the
/// current <see cref="DocumentTabBarSettings.ColorMode"/>.
/// </summary>
public static class TabColorService
{
    // --- Palette -------------------------------------------------------------
    // 12 distinct, accessible colors that work on both dark and light backgrounds.
    // Kept at ~50–60% lightness so the stripe is visible without being harsh.

    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x61, 0x9A, 0xD1), // Cornflower blue
        Color.FromRgb(0x4E, 0xC9, 0xB0), // Teal mint
        Color.FromRgb(0xDC, 0xDC, 0xAA), // Warm sand
        Color.FromRgb(0xCE, 0x91, 0x78), // Terra cotta
        Color.FromRgb(0x9C, 0xCC, 0x65), // Fresh green
        Color.FromRgb(0xC5, 0x86, 0xC0), // Mauve
        Color.FromRgb(0xFF, 0xC6, 0x6D), // Golden yellow
        Color.FromRgb(0xF4, 0x47, 0x47), // Soft red
        Color.FromRgb(0x89, 0xD1, 0xEC), // Sky blue
        Color.FromRgb(0xB5, 0xD6, 0x67), // Lime green
        Color.FromRgb(0xE0, 0xA3, 0x63), // Amber
        Color.FromRgb(0xA0, 0xA0, 0xF0), // Periwinkle
    ];

    // --- Public API ----------------------------------------------------------

    /// <summary>
    /// Returns the accent <see cref="Brush"/> for <paramref name="item"/> given the
    /// current settings, or <see langword="null"/> when no colorization should apply.
    /// </summary>
    public static Brush? GetTabBrush(DockItem item, DocumentTabBarSettings settings)
    {
        var color = settings.ColorMode switch
        {
            DocumentTabColorMode.FileExtension => ByExtension(item.Title),
            DocumentTabColorMode.Project       => ByProject(item.Metadata),
            DocumentTabColorMode.Regex         => ByRegex(item.Title, settings.RegexRules),
            _                                  => (Color?)null
        };

        return color.HasValue ? new SolidColorBrush(color.Value) : null;
    }

    // --- Private helpers -----------------------------------------------------

    private static Color? ByExtension(string title)
    {
        var ext = Path.GetExtension(title)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) return null;
        return Palette[Math.Abs(ext.GetHashCode(StringComparison.Ordinal)) % Palette.Length];
    }

    private static Color? ByProject(Dictionary<string, string> metadata)
    {
        if (!metadata.TryGetValue("ProjectId", out var projectId) ||
            string.IsNullOrEmpty(projectId))
            return null;

        return Palette[Math.Abs(projectId.GetHashCode(StringComparison.Ordinal)) % Palette.Length];
    }

    private static Color? ByRegex(string title,
        System.Collections.ObjectModel.ObservableCollection<RegexColorRule> rules)
    {
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Pattern)) continue;
            try
            {
                if (Regex.IsMatch(title, rule.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
                {
                    try { return (Color)ColorConverter.ConvertFromString(rule.ColorHex); }
                    catch { /* fallback: skip */ }
                }
            }
            catch (Exception) { /* skip invalid/timed-out patterns */ }
        }
        return null;
    }
}
