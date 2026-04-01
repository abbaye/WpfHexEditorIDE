// Project      : WpfHexEditorControl
// File         : ComparisonSettings.cs
// Description  : Persisted settings for the Compare Files feature.
// Architecture : Pure POCO — no WPF, no I/O. Serialised as "comparison": { ... } in settings.json.

namespace WpfHexEditor.Core.Options;

/// <summary>
/// A past comparison that can be re-opened from the history list or Command Palette.
/// </summary>
public sealed class ComparisonHistoryEntry
{
    public string   LeftPath  { get; set; } = string.Empty;
    public string   RightPath { get; set; } = string.Empty;
    public string   Mode      { get; set; } = "Auto";
    public DateTime LastUsed  { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// User preferences and comparison history for the Compare Files feature.
/// </summary>
public sealed class ComparisonSettings
{
    /// <summary>Default viewer mode: "SideBySide", "Inline", or "HexText".</summary>
    public string DefaultViewMode     { get; set; } = "SideBySide";

    /// <summary>Fold contiguous identical regions in text mode.</summary>
    public bool   FoldIdenticalRegions { get; set; } = true;

    /// <summary>Minimum count of identical lines to fold as a collapsed block.</summary>
    public int    FoldThreshold        { get; set; } = 5;

    /// <summary>Show the 14-pixel minimap on the right side of the diff viewer.</summary>
    public bool   ShowMinimap          { get; set; } = true;

    /// <summary>Compute character-level highlight within modified lines (≤ 500 chars).</summary>
    public bool   ShowCharLevelDiff    { get; set; } = true;

    /// <summary>Recent comparisons — newest first, capped at 20 entries.</summary>
    public List<ComparisonHistoryEntry> RecentComparisons { get; set; } = [];

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Prepends an entry and trims the list to 20 items.</summary>
    public void AddToHistory(string leftPath, string rightPath, string mode = "Auto")
    {
        RecentComparisons.RemoveAll(e =>
            string.Equals(e.LeftPath, leftPath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.RightPath, rightPath, StringComparison.OrdinalIgnoreCase));

        RecentComparisons.Insert(0, new ComparisonHistoryEntry
        {
            LeftPath = leftPath, RightPath = rightPath,
            Mode     = mode,    LastUsed  = DateTime.UtcNow
        });

        if (RecentComparisons.Count > 20)
            RecentComparisons.RemoveRange(20, RecentComparisons.Count - 20);
    }
}
