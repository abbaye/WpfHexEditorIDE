// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Undo/RunAttributeUndoEntry.cs
// Description:
//     Undo entry for run-level formatting attribute changes.
//     Stores before/after snapshots of affected blocks' Attributes dicts.
// ==========================================================

using System.Diagnostics.CodeAnalysis;
using WpfHexEditor.Editor.Core.Undo;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Undo;

/// <summary>
/// Snapshot of a single block's attribute dictionary (before or after a formatting change).
/// </summary>
public sealed record AttributeSnapshot(DocumentBlock Block, Dictionary<string, object> Attributes);

/// <summary>
/// Records a run-level attribute change (bold, italic, underline, fontSize, etc.).
/// Undo restores the before snapshots; Redo applies the after snapshots.
/// </summary>
public sealed class RunAttributeUndoEntry : IUndoEntry
{
    public required string                       AttributeName    { get; init; }
    public required IReadOnlyList<AttributeSnapshot> BeforeSnapshots { get; init; }
    public required IReadOnlyList<AttributeSnapshot> AfterSnapshots  { get; init; }

    public string   Description => $"Format {AttributeName}";
    public long     Revision    { get; set; }
    public DateTime Timestamp   { get; } = DateTime.UtcNow;

    public void Undo()
    {
        foreach (var snap in BeforeSnapshots)
            RestoreAttributes(snap);
    }

    public void Redo()
    {
        foreach (var snap in AfterSnapshots)
            RestoreAttributes(snap);
    }

    private static void RestoreAttributes(AttributeSnapshot snap)
    {
        snap.Block.Attributes.Clear();
        foreach (var kvp in snap.Attributes)
            snap.Block.Attributes[kvp.Key] = kvp.Value;
    }

    public bool TryMerge(IUndoEntry next, [NotNullWhen(true)] out IUndoEntry? merged)
    {
        // Only merge if same attribute changed on same set of blocks within 300ms
        if (next is RunAttributeUndoEntry n
            && n.AttributeName == AttributeName
            && (n.Timestamp - Timestamp).TotalMilliseconds < 300
            && n.BeforeSnapshots.Count == AfterSnapshots.Count)
        {
            merged = new RunAttributeUndoEntry
            {
                AttributeName    = AttributeName,
                BeforeSnapshots  = BeforeSnapshots,
                AfterSnapshots   = n.AfterSnapshots
            };
            return true;
        }
        merged = null;
        return false;
    }
}
