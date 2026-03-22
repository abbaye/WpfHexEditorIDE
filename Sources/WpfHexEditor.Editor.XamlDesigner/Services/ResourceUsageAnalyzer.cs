// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ResourceUsageAnalyzer.cs
// Author: Derek Tremblay
// Created: 2026-03-19
// Description:
//     Scans a XAML source string and counts usages of each resource
//     key via {StaticResource key} and {DynamicResource key} patterns.
//     Also reports line-number information for each matched usage.
//
// Architecture Notes:
//     Pure service — no WPF dependencies, no state.
//     Strategy: regex-based scan covering all four markup-extension
//     forms (positional and ResourceKey= named forms) for both
//     StaticResource and DynamicResource.
// ==========================================================

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Scans XAML source text and counts how many times each resource key
/// is referenced via <c>{StaticResource}</c> or <c>{DynamicResource}</c>.
/// </summary>
public sealed class ResourceUsageAnalyzer
{
    // ── Regex patterns ────────────────────────────────────────────────────────

    // Matches: {StaticResource Foo}  {StaticResource  Foo }
    private static readonly Regex PositionalPattern = new(
        @"\{(?:StaticResource|DynamicResource)\s+([\w\.]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Matches: {StaticResource ResourceKey=Foo}  {DynamicResource ResourceKey=Foo }
    private static readonly Regex NamedKeyPattern = new(
        @"\{(?:StaticResource|DynamicResource)\s+ResourceKey\s*=\s*([\w\.]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans <paramref name="xamlSource"/> and returns a dictionary mapping
    /// each resource key to the total number of times it is referenced.
    /// </summary>
    /// <param name="xamlSource">Raw XAML string to scan.</param>
    /// <returns>Key → usage-count map (empty if no references found).</returns>
    public IReadOnlyDictionary<string, int> AnalyzeUsages(string xamlSource)
    {
        if (string.IsNullOrEmpty(xamlSource))
            return new Dictionary<string, int>(0);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        AccumulateMatches(xamlSource, PositionalPattern, counts);
        AccumulateMatches(xamlSource, NamedKeyPattern, counts);

        return counts;
    }

    /// <summary>
    /// Returns the number of times <paramref name="key"/> is referenced
    /// in <paramref name="xamlSource"/>.
    /// </summary>
    public int GetUsageCount(string xamlSource, string key)
    {
        if (string.IsNullOrEmpty(xamlSource) || string.IsNullOrEmpty(key))
            return 0;

        var usages = AnalyzeUsages(xamlSource);
        return usages.TryGetValue(key, out int count) ? count : 0;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void AccumulateMatches(
        string source,
        Regex pattern,
        Dictionary<string, int> accumulator)
    {
        foreach (Match match in pattern.Matches(source))
        {
            string key = match.Groups[1].Value;
            accumulator[key] = accumulator.TryGetValue(key, out int existing)
                ? existing + 1
                : 1;
        }
    }

    /// <summary>
    /// Returns the 1-based line number of <paramref name="matchIndex"/>
    /// within <paramref name="xamlSource"/>.
    /// </summary>
    private static int GetLineNumber(string xamlSource, int matchIndex)
        => xamlSource.AsSpan(0, matchIndex).Count('\n') + 1;
}
