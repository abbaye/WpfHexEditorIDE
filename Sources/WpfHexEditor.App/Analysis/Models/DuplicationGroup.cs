// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/DuplicationGroup.cs
// Description: A clone group — identical (normalized) block found in ≥2 locations.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public sealed class DuplicationOccurrence
{
    public string FilePath   { get; init; } = string.Empty;
    public int    StartLine  { get; init; }
    public int    EndLine    { get; init; }
}

public sealed class DuplicationGroup
{
    public int    TokenCount   { get; init; }
    public int    LineCount    { get; init; }
    public IReadOnlyList<DuplicationOccurrence> Occurrences { get; init; } = [];
}
