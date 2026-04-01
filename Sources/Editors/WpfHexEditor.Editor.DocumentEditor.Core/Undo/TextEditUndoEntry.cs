// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Undo/TextEditUndoEntry.cs
// Description:
//     Coalescible undo entry for text edits in a DocumentBlock.
//     Consecutive edits on the same block within 1 s are merged
//     into a single undo step.
// ==========================================================

using System.Diagnostics.CodeAnalysis;
using WpfHexEditor.Editor.Core.Undo;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Undo;

/// <summary>
/// Records a text change on a <see cref="DocumentBlock"/>.
/// Consecutive changes on the same block within 1 second coalesce.
/// </summary>
public sealed class TextEditUndoEntry : IUndoEntry
{
    public required DocumentBlock Block  { get; init; }
    public required string        OldText { get; init; }
    public required string        NewText { get; init; }

    public string   Description => $"Edit {Block.Kind}";
    public long     Revision    { get; set; }
    public DateTime Timestamp   { get; } = DateTime.UtcNow;

    // ──────────────────────────────── Undo / Redo ─────────────────────────────

    /// <summary>Reverts the block text to <see cref="OldText"/>.</summary>
    public void Undo() => Block.Text = OldText;

    /// <summary>Re-applies the change to <see cref="NewText"/>.</summary>
    public void Redo() => Block.Text = NewText;

    // ──────────────────────────────── Coalescence ─────────────────────────────

    /// <summary>
    /// Merges with <paramref name="next"/> if it edits the same block
    /// within the coalesce window (1 s).
    /// </summary>
    public bool TryMerge(IUndoEntry next, [NotNullWhen(true)] out IUndoEntry? merged)
    {
        if (next is TextEditUndoEntry n
            && ReferenceEquals(n.Block, Block)
            && (n.Timestamp - Timestamp).TotalSeconds < 1.0)
        {
            merged = new TextEditUndoEntry
            {
                Block   = Block,
                OldText = OldText,
                NewText = n.NewText
            };
            return true;
        }

        merged = null;
        return false;
    }
}
