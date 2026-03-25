// ==========================================================
// Project: WpfHexEditor.App
// File: Services/CommandPaletteService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Description:
//     Catalog and fuzzy-search engine for the Command Palette.
//     Supports 3-tier scoring (prefix/substring/subsequence) with
//     MatchIndices output for character-level highlight, per-entry
//     execution history (frequency + recency boost), and context boost.
//
// Architecture Notes:
//     Pure service — no WPF dependency. Consumed by CommandPaletteWindow.
//     Settings are injected at query time via CommandPaletteSettings so
//     changes made in the Options page take effect immediately.
// ==========================================================

using WpfHexEditor.App.Models;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Manages the full command catalog and provides fuzzy-filtered results
/// for the Command Palette, including frequency boosting and context awareness.
/// </summary>
public sealed class CommandPaletteService
{
    private readonly List<CommandPaletteEntry> _allEntries;
    private string? _activeCategory;

    public CommandPaletteService(IEnumerable<CommandPaletteEntry> entries)
    {
        _allEntries = entries
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name,     StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>All entries sorted by Category then Name.</summary>
    public IReadOnlyList<CommandPaletteEntry> AllEntries => _allEntries;

    /// <summary>
    /// Sets the editor category of the currently active document (e.g. "Build")
    /// so matching commands receive a context boost.
    /// </summary>
    public void SetActiveCategory(string? editorCategory) => _activeCategory = editorCategory;

    // ── Execution history ─────────────────────────────────────────────────────

    /// <summary>
    /// Records that <paramref name="entryName"/> was executed.
    /// Persists to <paramref name="settings"/> immediately.
    /// </summary>
    public void RecordExecution(string entryName, CommandPaletteSettings settings)
    {
        if (settings.CommandHistory.TryGetValue(entryName, out var rec))
        {
            settings.CommandHistory[entryName] = new CommandExecutionRecord
            {
                Count   = rec.Count + 1,
                LastUtc = DateTime.UtcNow,
            };
        }
        else
        {
            settings.CommandHistory[entryName] = new CommandExecutionRecord
            {
                Count   = 1,
                LastUtc = DateTime.UtcNow,
            };
        }
    }

    // ── Query entry-point ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns up to <see cref="CommandPaletteSettings.MaxResults"/> matching entries.
    /// When <paramref name="query"/> is empty and
    /// <see cref="CommandPaletteSettings.ShowRecentCommands"/> is true, the most
    /// recently executed commands are prepended.
    /// </summary>
    public IReadOnlyList<CommandPaletteEntry> Filter(string query, CommandPaletteSettings settings)
    {
        var q = query.Trim();

        if (string.IsNullOrEmpty(q))
            return BuildEmptyQueryResults(settings);

        var scored = new List<(CommandPaletteEntry Entry, int Score, int[] Indices)>(_allEntries.Count);

        foreach (var entry in _allEntries)
        {
            var (score, indices) = ScoreEntry(entry.Name, q);
            if (score < 0)
            {
                // Fallback: try scoring against Category (no highlight indices in that case)
                (score, _) = ScoreEntry(entry.Category, q);
            }
            if (score < 0) continue;

            score += FrequencyBoost(entry.Name, settings);
            score += ContextBoost(entry, settings);

            scored.Add((entry, score, indices));
        }

        return scored
            .OrderByDescending(x => x.Score)
            .Take(settings.MaxResults)
            .Select(x => x.Entry with { MatchIndices = x.Indices })
            .ToList();
    }

    // ── Empty query — recent commands first ───────────────────────────────────

    private IReadOnlyList<CommandPaletteEntry> BuildEmptyQueryResults(CommandPaletteSettings settings)
    {
        if (!settings.ShowRecentCommands || settings.CommandHistory.Count == 0)
            return _allEntries;

        var recentNames = settings.CommandHistory
            .OrderByDescending(kv => kv.Value.LastUtc)
            .Take(settings.RecentCommandsCount)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var recents = _allEntries
            .Where(e => recentNames.Contains(e.Name))
            .OrderByDescending(e => settings.CommandHistory.TryGetValue(e.Name, out var r) ? r.LastUtc : DateTime.MinValue)
            .Select(e => e with { IsRecent = true })
            .ToList();

        var rest = _allEntries
            .Where(e => !recentNames.Contains(e.Name))
            .ToList();

        return recents.Concat(rest).ToList();
    }

    // ── Boosts ────────────────────────────────────────────────────────────────

    private int FrequencyBoost(string name, CommandPaletteSettings s)
    {
        if (!s.FrequencyBoostEnabled) return 0;
        if (!s.CommandHistory.TryGetValue(name, out var h)) return 0;
        var age = DateTime.UtcNow - h.LastUtc;
        return age.TotalMinutes < 5  ? 300 :
               age.TotalHours   < 24 ? 150 :
               age.TotalDays    < 7  ?  50 : 0;
    }

    private int ContextBoost(CommandPaletteEntry e, CommandPaletteSettings s)
        => s.ContextBoostEnabled && !string.IsNullOrEmpty(_activeCategory)
           && string.Equals(e.Category, _activeCategory, StringComparison.OrdinalIgnoreCase)
           ? 200 : 0;

    // ── Scoring ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns (score, matchIndices) for a single name/needle pair.
    /// score = -1 means no match; matchIndices is the set of positions in name
    /// that contributed to the match (used for character-level highlight).
    /// </summary>
    private static (int Score, int[] Indices) ScoreEntry(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack)) return (-1, Array.Empty<int>());

        // Tier 1: prefix match
        if (haystack.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
        {
            var indices = Enumerable.Range(0, needle.Length).ToArray();
            return (1000 - haystack.Length, indices);
        }

        // Tier 2: substring match
        var idx = haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var indices = Enumerable.Range(idx, needle.Length).ToArray();
            return (500 - idx, indices);
        }

        // Tier 3: scattered subsequence
        var (gaps, seqIndices) = MatchSubsequence(haystack, needle);
        if (gaps >= 0)
            return (100 - gaps, seqIndices);

        return (-1, Array.Empty<int>());
    }

    /// <summary>
    /// Returns (totalGaps, matchedPositions) if needle is a subsequence of haystack,
    /// or (-1, empty) otherwise.
    /// </summary>
    private static (int Gaps, int[] Indices) MatchSubsequence(string haystack, string needle)
    {
        var matchedPositions = new int[needle.Length];
        var hi   = 0;
        var ni   = 0;
        var gaps = 0;
        var last = -1;

        while (ni < needle.Length && hi < haystack.Length)
        {
            if (char.ToUpperInvariant(haystack[hi]) == char.ToUpperInvariant(needle[ni]))
            {
                if (last >= 0) gaps += hi - last - 1;
                matchedPositions[ni] = hi;
                last = hi;
                ni++;
            }
            hi++;
        }

        return ni == needle.Length
            ? (gaps, matchedPositions)
            : (-1, Array.Empty<int>());
    }
}
