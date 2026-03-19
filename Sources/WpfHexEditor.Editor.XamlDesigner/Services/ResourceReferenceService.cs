// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ResourceReferenceService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Finds all usages of a specific resource key within the raw XAML text.
//     Returns a list of (line, column) locations for navigation.
//
// Architecture Notes:
//     Pure service — stateless text analysis.
//     Uses regex matching for {StaticResource Key} and {DynamicResource Key}.
// ==========================================================

using System.Text.RegularExpressions;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Finds resource key usages in XAML source text.
/// </summary>
public sealed class ResourceReferenceService
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all line numbers (1-based) where <paramref name="resourceKey"/>
    /// is referenced as StaticResource or DynamicResource in <paramref name="xaml"/>.
    /// </summary>
    public IReadOnlyList<ResourceUsage> FindUsages(string xaml, string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(xaml) || string.IsNullOrWhiteSpace(resourceKey))
            return Array.Empty<ResourceUsage>();

        var results = new List<ResourceUsage>();
        var pattern = $@"\{{(?:StaticResource|DynamicResource)\s+{Regex.Escape(resourceKey)}\}}";
        var regex   = new Regex(pattern, RegexOptions.Compiled);

        var lines = xaml.Split('\n');
        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];
            foreach (Match m in regex.Matches(line))
            {
                results.Add(new ResourceUsage(
                    Line:   lineIdx + 1,
                    Column: m.Index + 1,
                    Snippet: line.Trim()));
            }
        }

        return results;
    }

    /// <summary>
    /// Returns all resource keys referenced in <paramref name="xaml"/>.
    /// </summary>
    public IReadOnlyList<string> GetAllReferencedKeys(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return Array.Empty<string>();

        var keys    = new HashSet<string>(StringComparer.Ordinal);
        var pattern = @"\{(?:StaticResource|DynamicResource)\s+(\S+?)\}";
        var regex   = new Regex(pattern, RegexOptions.Compiled);

        foreach (Match m in regex.Matches(xaml))
            keys.Add(m.Groups[1].Value);

        return keys.OrderBy(k => k).ToList();
    }
}

/// <summary>Location of a single resource reference within a XAML file.</summary>
public sealed record ResourceUsage(int Line, int Column, string Snippet);
