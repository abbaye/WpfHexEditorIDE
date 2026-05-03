// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Editing/DocumentMutator.cs
// Description:
//     Single point of truth for all DocumentModel mutations.
//     Every mutation pushes to model.UndoEngine — no separate stack.
//     Fires BlockMutated event after each mutation so the HexPane
//     can update its change-highlight overlays.
// ==========================================================

using WpfHexEditor.Editor.Core.Undo;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Undo;
using System.Linq;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Editing;

/// <summary>
/// The kind of block mutation that occurred.
/// </summary>
public enum BlockMutationKind
{
    TextEdited,
    Deleted,
    Inserted,
    AttributeChanged
}

/// <summary>
/// Arguments for the <see cref="DocumentMutator.BlockMutated"/> event.
/// </summary>
public sealed class BlockMutatedArgs(DocumentBlock block, BlockMutationKind kind) : EventArgs
{
    public DocumentBlock     Block { get; } = block;
    public BlockMutationKind Kind  { get; } = kind;
}

/// <summary>
/// Performs all mutations on a <see cref="DocumentModel"/>, pushing undo entries
/// to the model's <see cref="WpfHexEditor.Editor.Core.Undo.UndoEngine"/>.
/// </summary>
public sealed class DocumentMutator(DocumentModel model)
{
    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised after every mutation. Used by the HexPane highlight manager.</summary>
    public event EventHandler<BlockMutatedArgs>? BlockMutated;

    // ── Text editing ─────────────────────────────────────────────────────────

    /// <summary>
    /// If the block stores text in run children (DOCX-style), collapses all children
    /// into <see cref="DocumentBlock.Text"/> and clears the children list.
    /// Called before any in-place text mutation so editing always operates on a flat string.
    /// Run-level formatting is dropped on first edit — run-aware editing is a future enhancement.
    /// </summary>
    public void EnsureFlatText(DocumentBlock block)
    {
        if (block.Children.Count == 0) return;
        block.Text = string.Concat(block.Children.Select(c => c.Text));
        block.Children.Clear();
        BlockMutated?.Invoke(this, new BlockMutatedArgs(block, BlockMutationKind.TextEdited));
        model.NotifyBlocksChanged();
    }

    /// <summary>
    /// Inserts <paramref name="text"/> at <paramref name="charOffset"/> in the block.
    /// Pushes a <see cref="TextEditUndoEntry"/> (auto-coalesces within 1 s).
    /// </summary>
    public void InsertText(DocumentBlock block, int charOffset, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (block.Children.Count > 0)
        {
            // Run-aware: find which run owns flatOffset and insert there, preserving formatting.
            int flatLen = block.Children.Sum(c => c.Text.Length);
            charOffset  = Math.Clamp(charOffset, 0, flatLen);
            var (run, localOff) = FindRunAtFlatOffset(block, charOffset);
            var oldRunText = run.Text;
            run.Text = run.Text.Insert(localOff, text);
            model.UndoEngine.Push(new TextEditUndoEntry { Block = run, OldText = oldRunText, NewText = run.Text });
            BlockMutated?.Invoke(this, new BlockMutatedArgs(block, BlockMutationKind.TextEdited));
            model.NotifyBlocksChanged();
            return;
        }

        charOffset = Math.Clamp(charOffset, 0, block.Text.Length);
        var oldText = block.Text;
        var newText = block.Text.Insert(charOffset, text);
        block.Text = newText;

        model.UndoEngine.Push(new TextEditUndoEntry { Block = block, OldText = oldText, NewText = newText });
        BlockMutated?.Invoke(this, new BlockMutatedArgs(block, BlockMutationKind.TextEdited));
        model.NotifyBlocksChanged();
    }

    /// <summary>
    /// Deletes <paramref name="length"/> characters starting at <paramref name="charOffset"/>.
    /// Pushes a <see cref="TextEditUndoEntry"/>.
    /// </summary>
    public void DeleteText(DocumentBlock block, int charOffset, int length)
    {
        if (length <= 0 || charOffset < 0) return;

        if (block.Children.Count > 0)
        {
            // Run-aware: if the range fits entirely within one run, delete there.
            // If it spans multiple runs, fall back to flatten (cross-run delete loses formatting).
            int flatLen = block.Children.Sum(c => c.Text.Length);
            charOffset  = Math.Clamp(charOffset, 0, flatLen);
            length      = Math.Min(length, flatLen - charOffset);
            if (length <= 0) return;

            int pos = 0;
            foreach (var run in block.Children)
            {
                int end = pos + run.Text.Length;
                if (charOffset >= pos && charOffset + length <= end)
                {
                    // Fully within this run
                    int localStart = charOffset - pos;
                    var oldRunText = run.Text;
                    run.Text = run.Text.Remove(localStart, length);
                    model.UndoEngine.Push(new TextEditUndoEntry { Block = run, OldText = oldRunText, NewText = run.Text });
                    BlockMutated?.Invoke(this, new BlockMutatedArgs(block, BlockMutationKind.TextEdited));
                    model.NotifyBlocksChanged();
                    return;
                }
                pos = end;
            }
            // Cross-run: flatten then fall through to block.Text path
            EnsureFlatText(block);
            length = Math.Min(length, block.Text.Length - charOffset);
            if (length <= 0) return;
        }

        length = Math.Min(length, block.Text.Length - charOffset);
        if (length <= 0) return;
        var oldText = block.Text;
        var newText = block.Text.Remove(charOffset, length);
        block.Text = newText;

        model.UndoEngine.Push(new TextEditUndoEntry { Block = block, OldText = oldText, NewText = newText });
        BlockMutated?.Invoke(this, new BlockMutatedArgs(block, BlockMutationKind.TextEdited));
        model.NotifyBlocksChanged();
    }

    /// <summary>
    /// Replaces the entire text of a block (used by table cell commit).
    /// </summary>
    public void SetText(DocumentBlock block, string text)
    {
        if (block.Text == text) return;
        var oldText = block.Text;
        block.Text = text;

        model.UndoEngine.Push(new TextEditUndoEntry { Block = block, OldText = oldText, NewText = text });
        BlockMutated?.Invoke(this, new BlockMutatedArgs(block, BlockMutationKind.TextEdited));
        model.NotifyBlocksChanged();
    }

    // ── Block structure ───────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a new empty paragraph after the block at <paramref name="blockIndex"/>.
    /// </summary>
    public DocumentBlock InsertParagraphAfter(int blockIndex)
    {
        var newBlock = DocumentBlockFactory.NewParagraph();
        var insertAt = Math.Min(blockIndex + 1, model.Blocks.Count);
        model.Blocks.Insert(insertAt, newBlock);

        model.UndoEngine.Push(new BlockInsertUndoEntry
        {
            Model      = model,
            Block      = newBlock,
            BlockIndex = insertAt
        });

        BlockMutated?.Invoke(this, new BlockMutatedArgs(newBlock, BlockMutationKind.Inserted));
        model.NotifyBlocksChanged();
        return newBlock;
    }

    /// <summary>Deletes the block at the specified index.</summary>
    public void DeleteBlock(int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= model.Blocks.Count) return;
        var block = model.Blocks[blockIndex];
        model.Blocks.RemoveAt(blockIndex);

        model.UndoEngine.Push(new BlockDeleteUndoEntry
        {
            Model      = model,
            Block      = block,
            BlockIndex = blockIndex
        });

        BlockMutated?.Invoke(this, new BlockMutatedArgs(block, BlockMutationKind.Deleted));
        model.NotifyBlocksChanged();
    }

    /// <summary>
    /// Splits the block at <paramref name="blockIndex"/> at <paramref name="charOffset"/>,
    /// producing two blocks. Returns the (first, second) pair.
    /// </summary>
    public (DocumentBlock First, DocumentBlock Second) SplitBlock(int blockIndex, int charOffset)
    {
        var block = model.Blocks[blockIndex];

        // Run-aware split: preserve per-run formatting when block has children.
        if (block.Children.Count > 0)
        {
            int flatLen = block.Children.Sum(c => c.Text.Length);
            charOffset  = Math.Clamp(charOffset, 0, flatLen);

            // Snapshot original children for undo.
            var originalChildren = block.Children.ToList();

            // Distribute children: runs before split → firstChildren, runs after → secondChildren.
            var firstChildren  = new List<DocumentBlock>();
            var secondChildren = new List<DocumentBlock>();
            int pos = 0;
            bool splitDone = false;
            foreach (var run in originalChildren)
            {
                int end = pos + run.Text.Length;
                if (splitDone)
                {
                    secondChildren.Add(run);
                }
                else if (charOffset >= end)
                {
                    firstChildren.Add(run);
                }
                else
                {
                    // Split falls inside this run.
                    int localOff = charOffset - pos;
                    if (localOff > 0)
                        firstChildren.Add(DocumentBlockFactory.CloneWithText(run, run.Text[..localOff]));
                    if (localOff < run.Text.Length)
                        secondChildren.Add(DocumentBlockFactory.CloneWithText(run, run.Text[localOff..]));
                    splitDone = true;
                }
                pos = end;
            }

            // Apply first children to existing block.
            block.Children.Clear();
            foreach (var c in firstChildren) block.Children.Add(c);
            block.Text = string.Concat(firstChildren.Select(c => c.Text));

            // Build second block with second children.
            var second = DocumentBlockFactory.CloneWithText(block, string.Concat(secondChildren.Select(c => c.Text)));
            foreach (var c in secondChildren) second.Children.Add(c);
            model.Blocks.Insert(blockIndex + 1, second);

            model.UndoEngine.Push(new BlockSplitUndoEntry
            {
                Model          = model,
                First          = block,
                Second         = second,
                BlockIndex     = blockIndex,
                FirstText      = block.Text,
                SecondText     = second.Text,
                FirstChildren  = firstChildren,
                SecondChildren = secondChildren
            });

            BlockMutated?.Invoke(this, new BlockMutatedArgs(block, BlockMutationKind.TextEdited));
            model.NotifyBlocksChanged();
            return (block, second);
        }

        // Plain-text split path (no children).
        charOffset = Math.Clamp(charOffset, 0, block.Text.Length);
        var firstText  = block.Text[..charOffset];
        var secondText = block.Text[charOffset..];

        block.Text = firstText;
        var secondPlain = DocumentBlockFactory.CloneWithText(block, secondText);
        model.Blocks.Insert(blockIndex + 1, secondPlain);

        model.UndoEngine.Push(new BlockSplitUndoEntry
        {
            Model      = model,
            First      = block,
            Second     = secondPlain,
            BlockIndex = blockIndex,
            FirstText  = firstText,
            SecondText = secondText
        });

        BlockMutated?.Invoke(this, new BlockMutatedArgs(block, BlockMutationKind.TextEdited));
        model.NotifyBlocksChanged();
        return (block, secondPlain);
    }

    /// <summary>
    /// Merges the block at <paramref name="blockIndex"/> with the next block.
    /// Returns the surviving (first) block.
    /// </summary>
    public DocumentBlock MergeWithNext(int blockIndex)
    {
        if (blockIndex < 0 || blockIndex + 1 >= model.Blocks.Count)
            return model.Blocks[blockIndex];

        var first      = model.Blocks[blockIndex];
        var second     = model.Blocks[blockIndex + 1];
        EnsureFlatText(first);
        EnsureFlatText(second);
        var firstText  = first.Text;
        var secondText = second.Text;

        first.Text = firstText + secondText;
        model.Blocks.RemoveAt(blockIndex + 1);

        model.UndoEngine.Push(new BlockMergeUndoEntry
        {
            Model      = model,
            First      = first,
            Second     = second,
            BlockIndex = blockIndex,
            FirstText  = firstText,
            SecondText = secondText
        });

        BlockMutated?.Invoke(this, new BlockMutatedArgs(first, BlockMutationKind.TextEdited));
        model.NotifyBlocksChanged();
        return first;
    }

    // ── Run-level formatting ──────────────────────────────────────────────────

    /// <summary>
    /// Applies a formatting attribute to the run children of <paramref name="para"/>
    /// that overlap [<paramref name="fromChar"/>, <paramref name="toChar"/>).
    /// For non-run blocks, applies directly to the block itself.
    /// </summary>
    public void ApplyRunAttribute(
        DocumentBlock para, int fromChar, int toChar,
        string attribute, object value)
    {
        var beforeSnapshots = SnapshotChildren(para);
        ApplyAttributeToRange(para, fromChar, toChar, attribute, value);
        var afterSnapshots  = SnapshotChildren(para);

        model.UndoEngine.Push(new RunAttributeUndoEntry
        {
            AttributeName    = attribute,
            BeforeSnapshots  = beforeSnapshots,
            AfterSnapshots   = afterSnapshots
        });

        BlockMutated?.Invoke(this, new BlockMutatedArgs(para, BlockMutationKind.AttributeChanged));
        model.NotifyBlocksChanged();
    }

    /// <summary>Removes a formatting attribute from runs in the specified char range.</summary>
    public void RemoveRunAttribute(
        DocumentBlock para, int fromChar, int toChar, string attribute)
    {
        var beforeSnapshots = SnapshotChildren(para);
        RemoveAttributeFromRange(para, fromChar, toChar, attribute);
        var afterSnapshots  = SnapshotChildren(para);

        model.UndoEngine.Push(new RunAttributeUndoEntry
        {
            AttributeName    = attribute,
            BeforeSnapshots  = beforeSnapshots,
            AfterSnapshots   = afterSnapshots
        });

        BlockMutated?.Invoke(this, new BlockMutatedArgs(para, BlockMutationKind.AttributeChanged));
        model.NotifyBlocksChanged();
    }

    // ── Block-level attributes ────────────────────────────────────────────────

    /// <summary>
    /// Sets a block-level attribute (style, align, listStyle, indent, etc.).
    /// </summary>
    public void SetBlockAttribute(DocumentBlock block, string attribute, object? value)
    {
        var before = TakeAttributeSnapshot(block);
        if (value is null)
            block.Attributes.Remove(attribute);
        else
            block.Attributes[attribute] = value;
        var after = TakeAttributeSnapshot(block);

        model.UndoEngine.Push(new RunAttributeUndoEntry
        {
            AttributeName    = attribute,
            BeforeSnapshots  = [before],
            AfterSnapshots   = [after]
        });

        BlockMutated?.Invoke(this, new BlockMutatedArgs(block, BlockMutationKind.AttributeChanged));
        model.NotifyBlocksChanged();
    }

    // ── Undo / Redo application ───────────────────────────────────────────────

    /// <summary>
    /// Undoes the last pushed entry. Returns true when an entry was applied.
    /// </summary>
    public bool TryUndo()
    {
        var entry = model.UndoEngine.TryUndo();
        if (entry is null) return false;
        ApplyEntry(entry, isUndo: true);
        model.NotifyBlocksChanged();
        return true;
    }

    /// <summary>
    /// Redoes the next entry in the redo stack. Returns true when applied.
    /// </summary>
    public bool TryRedo()
    {
        var entry = model.UndoEngine.TryRedo();
        if (entry is null) return false;
        ApplyEntry(entry, isUndo: false);
        model.NotifyBlocksChanged();
        return true;
    }

    private static void ApplyEntry(IUndoEntry entry, bool isUndo)
    {
        switch (entry)
        {
            case TextEditUndoEntry     te: if (isUndo) te.Undo(); else te.Redo(); break;
            case BlockInsertUndoEntry  bi: if (isUndo) bi.Undo(); else bi.Redo(); break;
            case BlockDeleteUndoEntry  bd: if (isUndo) bd.Undo(); else bd.Redo(); break;
            case BlockSplitUndoEntry   bs: if (isUndo) bs.Undo(); else bs.Redo(); break;
            case BlockMergeUndoEntry   bm: if (isUndo) bm.Undo(); else bm.Redo(); break;
            case RunAttributeUndoEntry ra: if (isUndo) ra.Undo(); else ra.Redo(); break;
            case WpfHexEditor.Editor.Core.Undo.CompositeUndoEntry comp:
                ApplyComposite(comp, isUndo); break;
        }
    }

    private static void ApplyComposite(
        WpfHexEditor.Editor.Core.Undo.CompositeUndoEntry comp, bool isUndo)
    {
        var children = comp.Children;
        if (isUndo)
        {
            for (var i = children.Count - 1; i >= 0; i--)
                ApplyEntry(children[i], isUndo: true);
        }
        else
        {
            foreach (var e in children) ApplyEntry(e, isUndo: false);
        }
    }

    // ── Run-aware helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Given a flat <paramref name="flatOffset"/> into <paramref name="para"/>'s children,
    /// returns the child run that owns that offset and the local offset within that run.
    /// </summary>
    private static (DocumentBlock Run, int LocalOffset) FindRunAtFlatOffset(
        DocumentBlock para, int flatOffset)
    {
        int pos = 0;
        foreach (var run in para.Children)
        {
            int end = pos + run.Text.Length;
            if (flatOffset <= end)
                return (run, flatOffset - pos);
            pos = end;
        }
        // Past all runs: clamp to end of last run
        var last = para.Children[^1];
        return (last, last.Text.Length);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void ApplyAttributeToRange(
        DocumentBlock para, int fromChar, int toChar,
        string attribute, object value)
    {
        if (para.Children.Count == 0)
        {
            SplitFlatBlockIntoRuns(para, fromChar, toChar);
            // Fall through to the run-loop below
        }
        else
        {
            // Split any existing runs that straddle fromChar or toChar so that
            // the attribute is applied only to the exact selected characters.
            SplitRunsAtBoundaries(para, fromChar, toChar);
        }

        var cursor = 0;
        foreach (var run in para.Children)
        {
            var runLen = run.Text.Length;
            var runEnd = cursor + runLen;
            if (runEnd > fromChar && cursor < toChar)
                run.Attributes[attribute] = value;
            cursor = runEnd;
        }
    }

    private static void RemoveAttributeFromRange(
        DocumentBlock para, int fromChar, int toChar, string attribute)
    {
        if (para.Children.Count == 0)
        {
            SplitFlatBlockIntoRuns(para, fromChar, toChar);
        }
        else
        {
            SplitRunsAtBoundaries(para, fromChar, toChar);
        }

        var cursor = 0;
        foreach (var run in para.Children)
        {
            var runLen = run.Text.Length;
            var runEnd = cursor + runLen;
            if (runEnd > fromChar && cursor < toChar)
                run.Attributes.Remove(attribute);
            cursor = runEnd;
        }
    }

    /// <summary>
    /// Splits any runs in <paramref name="para"/> that straddle <paramref name="fromChar"/>
    /// or <paramref name="toChar"/> so that formatting can be applied to the exact range.
    /// Runs that fall entirely before/inside/after the range are left intact.
    /// </summary>
    private static void SplitRunsAtBoundaries(DocumentBlock para, int fromChar, int toChar)
    {
        // We may need to split at two boundaries: fromChar and toChar.
        // Process fromChar first (lower offset), then toChar.
        foreach (var boundary in new[] { fromChar, toChar })
        {
            int cursor = 0;
            for (int i = 0; i < para.Children.Count; i++)
            {
                var run    = para.Children[i];
                int runEnd = cursor + run.Text.Length;

                if (boundary > cursor && boundary < runEnd)
                {
                    // Split this run at the boundary
                    int localOff = boundary - cursor;
                    var head = new DocumentBlock { Kind = run.Kind, Text = run.Text[..localOff] };
                    var tail = new DocumentBlock { Kind = run.Kind, Text = run.Text[localOff..] };
                    foreach (var kv in run.Attributes)
                    {
                        head.Attributes[kv.Key] = kv.Value;
                        tail.Attributes[kv.Key] = kv.Value;
                    }
                    para.Children.RemoveAt(i);
                    para.Children.Insert(i,     head);
                    para.Children.Insert(i + 1, tail);
                    break; // list mutated — restart outer loop for next boundary
                }

                cursor = runEnd;
                if (cursor >= boundary) break; // boundary is between runs — nothing to split
            }
        }
    }

    /// <summary>
    /// Converts a flat (no-children) block into 1–3 run children that cover
    /// [0, fromChar), [fromChar, toChar), [toChar, end).  Empty segments are skipped.
    /// The block's own Text and Attributes are cleared after splitting.
    /// </summary>
    private static void SplitFlatBlockIntoRuns(DocumentBlock para, int fromChar, int toChar)
    {
        var text = para.Text ?? string.Empty;
        int len  = text.Length;
        fromChar = Math.Clamp(fromChar, 0, len);
        toChar   = Math.Clamp(toChar,   0, len);

        // Copy block-level attributes to propagate to all runs
        var baseAttrs = new Dictionary<string, object>(para.Attributes);

        void AddRun(string seg)
        {
            var run = new DocumentBlock { Kind = "run", Text = seg };
            foreach (var kv in baseAttrs) run.Attributes[kv.Key] = kv.Value;
            para.Children.Add(run);
        }

        if (fromChar > 0)             AddRun(text[..fromChar]);
        if (fromChar < toChar)        AddRun(text[fromChar..toChar]);
        if (toChar   < len)           AddRun(text[toChar..]);

        // The flat text on the block itself is now represented by the runs
        para.Text = string.Empty;
        para.Attributes.Clear();
    }

    private static IReadOnlyList<AttributeSnapshot> SnapshotChildren(DocumentBlock para)
    {
        if (para.Children.Count == 0)
            return [TakeAttributeSnapshot(para)];
        return para.Children.Select(TakeAttributeSnapshot).ToList();
    }

    private static AttributeSnapshot TakeAttributeSnapshot(DocumentBlock block) =>
        new(block, new Dictionary<string, object>(block.Attributes));
}
