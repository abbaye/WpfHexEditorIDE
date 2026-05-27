// ==========================================================
// Project: WpfHexEditor.Core.ProjectSystem
// File: Languages/EmbeddedRangeClassifier.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-05-26
// Description:
//     Identifies embedded-language zones inside a host document.
//     Given the text of a document and a list of EmbeddedLanguageZone
//     descriptors (open/close delimiters + target LanguageDefinition),
//     returns a sorted, non-overlapping list of resolved ranges.
//
//     Examples:
//       HTML  : <script …>…</script>  → javascript
//               <style …>…</style>    → css
//       PHP   : <?php … ?>            → php
//       Vue   : <template>…           → html
//       MD    : ```js … ```           → javascript
//
// Architecture Notes:
//     Pure static service — no state, no WPF dependency.
//     O(n × z) where n = text length and z = number of zone descriptors.
//     For typical HTML files (2–3 zones) this is effectively O(n).
//     Regex is compiled once per (open, close) pair via a private cache.
// ==========================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WpfHexEditor.Core.ProjectSystem.Languages;

/// <summary>
/// A resolved embedded-language region inside a host document.
/// </summary>
/// <param name="ContentStart">
///   Character offset of the first character <em>inside</em> the zone
///   (i.e., after the opening delimiter's closing <c>&gt;</c> or equivalent).
/// </param>
/// <param name="ContentEnd">
///   Character offset of the first character of the closing delimiter
///   (exclusive end of the embedded content).
/// </param>
/// <param name="Language">
///   Resolved <see cref="LanguageDefinition"/> for this zone.
///   Never <see langword="null"/> — ranges are only emitted when the zone
///   has a resolved language.
/// </param>
public readonly record struct EmbeddedRange(
    int                ContentStart,
    int                ContentEnd,
    LanguageDefinition Language);

/// <summary>
/// Classifies the embedded-language zones of a host document.
/// </summary>
public static class EmbeddedRangeClassifier
{
    // ── Compiled-regex cache ─────────────────────────────────────────────────
    // Key: (open prefix lowercase, close lowercase)
    // Value: pre-compiled Regex that captures the content between open and close.
    private static readonly ConcurrentDictionary<(string open, string close), Regex> _regexCache = new();

    /// <summary>
    /// Scans <paramref name="text"/> for all embedded-language zones declared in
    /// <paramref name="zones"/> and returns the resolved ranges sorted by
    /// <see cref="EmbeddedRange.ContentStart"/>.
    /// </summary>
    /// <param name="text">Full document text (may be multi-line).</param>
    /// <param name="zones">
    ///   Zone descriptors from <see cref="LanguageDefinition.EmbeddedLanguages"/>.
    ///   Zones whose <see cref="EmbeddedLanguageZone.ResolvedLanguage"/> is
    ///   <see langword="null"/> are silently skipped.
    /// </param>
    /// <returns>
    ///   Sorted list of non-overlapping <see cref="EmbeddedRange"/> values.
    ///   Empty when <paramref name="text"/> is empty or no zones match.
    /// </returns>
    public static IReadOnlyList<EmbeddedRange> ClassifyRanges(
        string                          text,
        IReadOnlyList<EmbeddedLanguageZone> zones)
    {
        if (string.IsNullOrEmpty(text) || zones.Count == 0)
            return [];

        var ranges = new List<EmbeddedRange>();

        foreach (var zone in zones)
        {
            if (zone.ResolvedLanguage is null) continue;

            var regex = GetOrBuildRegex(zone.Open, zone.Close);
            var matches = regex.Matches(text);

            foreach (Match m in matches)
            {
                // Group 1 captures the content between opening delimiter close
                // and the closing delimiter.
                var contentGroup = m.Groups["content"];
                if (!contentGroup.Success) continue;

                ranges.Add(new EmbeddedRange(
                    ContentStart: contentGroup.Index,
                    ContentEnd:   contentGroup.Index + contentGroup.Length,
                    Language:     zone.ResolvedLanguage));
            }
        }

        if (ranges.Count <= 1) return ranges;

        // Sort by start position and remove overlaps (first match wins).
        ranges.Sort((a, b) => a.ContentStart.CompareTo(b.ContentStart));
        return RemoveOverlaps(ranges);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns a cached (or newly compiled) Regex that matches the content
    /// between <paramref name="open"/> and <paramref name="close"/> delimiters.
    /// </summary>
    private static Regex GetOrBuildRegex(string open, string close)
    {
        var key = (open.ToLowerInvariant(), close.ToLowerInvariant());
        if (_regexCache.TryGetValue(key, out var cached)) return cached;

        // Build pattern:
        //   (?i)          — case-insensitive
        //   OPEN_PREFIX   — verbatim open prefix (e.g. <script)
        //   [^>]*>        — tolerate attributes until the first > closes the tag
        //                   EXCEPTION: if open already ends with > (e.g. "<?php"),
        //                   skip the [^>]*> part and use a plain suffix match.
        //   (?<content>   — named capture group for the embedded content
        //     [\s\S]*?    — non-greedy any-char including newlines
        //   )
        //   CLOSE         — verbatim close delimiter (e.g. </script>)

        // Use the attribute-tolerant [^>]*> suffix only for plain HTML/XML element tags
        // whose open prefix is of the form "<tagname" (starts with '<' followed immediately
        // by a letter or '/').  Processing instructions (<?php), declarations (<!DOCTYPE),
        // Markdown fences (```), and delimiters that already end with '>' are matched exactly.
        bool isHtmlElementTag = open.Length >= 2
            && open[0] == '<'
            && (char.IsLetter(open[1]) || open[1] == '/');
        bool openAlreadyClosed = open.TrimEnd().EndsWith(">", StringComparison.Ordinal);

        string openPattern = (isHtmlElementTag && !openAlreadyClosed)
            ? Regex.Escape(open) + @"[^>]*>"
            : Regex.Escape(open);

        string pattern = $@"{openPattern}(?<content>[\s\S]*?){Regex.Escape(close)}";

        var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        _regexCache[key] = regex;
        return regex;
    }

    /// <summary>
    /// Removes overlapping ranges from a pre-sorted list, keeping the first
    /// (lowest start) range when two ranges overlap.
    /// </summary>
    private static IReadOnlyList<EmbeddedRange> RemoveOverlaps(List<EmbeddedRange> sorted)
    {
        var result   = new List<EmbeddedRange>(sorted.Count);
        int lastEnd  = int.MinValue;

        foreach (var r in sorted)
        {
            if (r.ContentStart >= lastEnd)
            {
                result.Add(r);
                lastEnd = r.ContentEnd;
            }
        }

        return result;
    }
}
