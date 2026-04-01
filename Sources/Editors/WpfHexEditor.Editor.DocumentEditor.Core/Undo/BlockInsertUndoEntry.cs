// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Undo/BlockInsertUndoEntry.cs
// Description:
//     Undo entry for inserting a new block into the document.
//     Undo removes the block; Redo re-inserts it at the same index.
// ==========================================================

using System.Diagnostics.CodeAnalysis;
using WpfHexEditor.Editor.Core.Undo;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Undo;

/// <summary>
/// Records a block insertion. Undo removes the block; Redo re-inserts it.
/// </summary>
public sealed class BlockInsertUndoEntry : IUndoEntry
{
    public required DocumentModel Model      { get; init; }
    public required DocumentBlock Block      { get; init; }
    public required int           BlockIndex { get; init; }

    public string   Description => $"Insert {Block.Kind}";
    public long     Revision    { get; set; }
    public DateTime Timestamp   { get; } = DateTime.UtcNow;

    public void Undo()
    {
        if (BlockIndex < Model.Blocks.Count && ReferenceEquals(Model.Blocks[BlockIndex], Block))
            Model.Blocks.RemoveAt(BlockIndex);
    }

    public void Redo()
    {
        var idx = Math.Min(BlockIndex, Model.Blocks.Count);
        Model.Blocks.Insert(idx, Block);
    }

    public bool TryMerge(IUndoEntry next, [NotNullWhen(true)] out IUndoEntry? merged)
    {
        merged = null;
        return false;
    }
}
