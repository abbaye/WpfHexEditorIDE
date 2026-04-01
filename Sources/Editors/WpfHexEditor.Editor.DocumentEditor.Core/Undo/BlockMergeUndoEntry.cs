// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Undo/BlockMergeUndoEntry.cs
// Description:
//     Undo entry for merging two adjacent blocks into one.
//     Undo re-splits at the boundary; Redo re-merges.
// ==========================================================

using System.Diagnostics.CodeAnalysis;
using WpfHexEditor.Editor.Core.Undo;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Undo;

/// <summary>
/// Records a block merge (Backspace at start / Delete at end).
/// Undo re-splits; Redo re-merges.
/// </summary>
public sealed class BlockMergeUndoEntry : IUndoEntry
{
    public required DocumentModel Model      { get; init; }
    public required DocumentBlock First      { get; init; }
    public required DocumentBlock Second     { get; init; }
    public required int           BlockIndex { get; init; }
    public required string        FirstText  { get; init; }
    public required string        SecondText { get; init; }

    public string   Description => $"Merge {First.Kind}";
    public long     Revision    { get; set; }
    public DateTime Timestamp   { get; } = DateTime.UtcNow;

    public void Undo()
    {
        // Re-split: restore original texts, re-insert Second after First.
        First.Text  = FirstText;
        Second.Text = SecondText;
        var insertAt = Math.Min(BlockIndex + 1, Model.Blocks.Count);
        if (!Model.Blocks.Contains(Second))
            Model.Blocks.Insert(insertAt, Second);
    }

    public void Redo()
    {
        // Re-merge: combine texts into First, remove Second.
        First.Text = FirstText + SecondText;
        if (BlockIndex + 1 < Model.Blocks.Count &&
            ReferenceEquals(Model.Blocks[BlockIndex + 1], Second))
        {
            Model.Blocks.RemoveAt(BlockIndex + 1);
        }
    }

    public bool TryMerge(IUndoEntry next, [NotNullWhen(true)] out IUndoEntry? merged)
    {
        merged = null;
        return false;
    }
}
