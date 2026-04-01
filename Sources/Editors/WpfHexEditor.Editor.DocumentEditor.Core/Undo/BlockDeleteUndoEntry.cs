// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Undo/BlockDeleteUndoEntry.cs
// Description:
//     Undo entry for deleting a block from the document.
//     Undo re-inserts the block; Redo removes it again.
// ==========================================================

using System.Diagnostics.CodeAnalysis;
using WpfHexEditor.Editor.Core.Undo;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Undo;

/// <summary>
/// Records a block deletion. Undo re-inserts; Redo removes again.
/// </summary>
public sealed class BlockDeleteUndoEntry : IUndoEntry
{
    public required DocumentModel Model      { get; init; }
    public required DocumentBlock Block      { get; init; }
    public required int           BlockIndex { get; init; }

    public string   Description => $"Delete {Block.Kind}";
    public long     Revision    { get; set; }
    public DateTime Timestamp   { get; } = DateTime.UtcNow;

    public void Undo()
    {
        var idx = Math.Min(BlockIndex, Model.Blocks.Count);
        Model.Blocks.Insert(idx, Block);
    }

    public void Redo()
    {
        if (BlockIndex < Model.Blocks.Count && ReferenceEquals(Model.Blocks[BlockIndex], Block))
            Model.Blocks.RemoveAt(BlockIndex);
    }

    public bool TryMerge(IUndoEntry next, [NotNullWhen(true)] out IUndoEntry? merged)
    {
        merged = null;
        return false;
    }
}
