// Project      : WpfHexEditorControl
// File         : Models/MergeConflict.cs
// Description  : Model for a 3-way merge conflict region.
// Architecture : Pure model — no WPF, no I/O.

namespace WpfHexEditor.Core.Diff.Models;

/// <summary>How a merge conflict was resolved.</summary>
public enum ConflictResolution
{
    /// <summary>User has not yet chosen a side.</summary>
    Unresolved,
    /// <summary>Accepted the "ours" version.</summary>
    AcceptOurs,
    /// <summary>Accepted the "theirs" version.</summary>
    AcceptTheirs,
    /// <summary>Accepted both versions (ours first, then theirs).</summary>
    AcceptBoth,
}

/// <summary>A region where both sides diverged from the base.</summary>
public sealed class MergeConflict
{
    /// <summary>1-based index into <see cref="ThreeWayMergeResult.Lines"/> where the conflict begins.</summary>
    public int Index { get; init; }

    /// <summary>Lines from the base file in this region.</summary>
    public IReadOnlyList<string> BaseLines  { get; init; } = [];

    /// <summary>Lines from "ours" (left) in this region.</summary>
    public IReadOnlyList<string> OursLines  { get; init; } = [];

    /// <summary>Lines from "theirs" (right) in this region.</summary>
    public IReadOnlyList<string> TheirsLines { get; init; } = [];

    /// <summary>Resolution chosen by the user.</summary>
    public ConflictResolution Resolution { get; set; } = ConflictResolution.Unresolved;
}

/// <summary>Classification of a line in the 3-way merge view.</summary>
public enum MergeLineKind
{
    /// <summary>Unchanged in all three versions.</summary>
    Equal,
    /// <summary>Changed only in "ours" — auto-accepted from ours.</summary>
    AcceptedOurs,
    /// <summary>Changed only in "theirs" — auto-accepted from theirs.</summary>
    AcceptedTheirs,
    /// <summary>Part of an unresolved conflict region.</summary>
    ConflictOurs,
    /// <summary>Part of an unresolved conflict region (theirs side).</summary>
    ConflictTheirs,
    /// <summary>Part of a resolved conflict (kept).</summary>
    Resolved,
}

/// <summary>A single output line in the 3-way merge view.</summary>
public sealed class MergeLine
{
    public MergeLineKind Kind           { get; init; }
    public string        Content        { get; init; } = string.Empty;
    public int?          BaseLineNumber  { get; init; }
    public int?          OursLineNumber  { get; init; }
    public int?          TheirsLineNumber { get; init; }

    /// <summary>Index of the owning <see cref="MergeConflict"/> in <see cref="ThreeWayMergeResult.Conflicts"/>, or -1.</summary>
    public int ConflictIndex { get; init; } = -1;
}

/// <summary>Complete result of a 3-way text merge.</summary>
public sealed class ThreeWayMergeResult
{
    /// <summary>Merged output lines (auto-resolved + conflict placeholders).</summary>
    public IReadOnlyList<MergeLine>    Lines     { get; init; } = [];

    /// <summary>Detected conflict regions (may be empty when merge is clean).</summary>
    public IReadOnlyList<MergeConflict> Conflicts { get; init; } = [];

    /// <summary>True when all conflicts have been resolved.</summary>
    public bool IsFullyResolved => Conflicts.All(c => c.Resolution != ConflictResolution.Unresolved);

    /// <summary>Number of conflicts that are still unresolved.</summary>
    public int UnresolvedCount => Conflicts.Count(c => c.Resolution == ConflictResolution.Unresolved);

    /// <summary>Builds the final merged text from resolved lines.</summary>
    public string BuildOutput(string lineEnding = "\n")
    {
        var sb = new System.Text.StringBuilder();
        foreach (var line in Lines)
        {
            if (line.Kind is MergeLineKind.ConflictOurs or MergeLineKind.ConflictTheirs)
                continue; // unresolved — skip
            sb.Append(line.Content);
            sb.Append(lineEnding);
        }
        return sb.ToString();
    }
}
