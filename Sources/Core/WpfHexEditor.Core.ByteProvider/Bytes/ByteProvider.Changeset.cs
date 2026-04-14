// ==========================================================
// Project: WpfHexEditor.Core
// File: ByteProvider.Changeset.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class of ByteProvider implementing the changeset snapshot feature.
//     Captures an immutable snapshot of all pending edits (modify/insert/delete)
//     grouped into contiguous runs for efficient serialization and persistence.
//
// Architecture Notes:
//     Partial class pattern — depends on EditsManager state from ByteProvider.cs.
//     O(e) complexity: iterates only the edit dictionaries, never reads the file.
//     Returns ChangesetSnapshot.Empty when the buffer is clean.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using WpfHexEditor.Core.Changesets;

namespace WpfHexEditor.Core.Bytes
{
    public sealed partial class ByteProvider
    {
        /// <summary>
        /// Captures an immutable snapshot of all pending edits (modify / insert / delete).
        /// O(e) — only iterates the edit dictionaries; never reads from the physical file.
        /// Returns <see cref="ChangesetSnapshot.Empty"/> when the buffer is clean.
        /// </summary>
        public ChangesetSnapshot GetChangesetSnapshot()
        {
            if (!_editsManager.HasChanges)
                return ChangesetSnapshot.Empty;

            // -- Modified: group consecutive bytes into runs ----------------
            var modifiedRanges = new List<ModifiedRange>();
            {
                long runStart = -1;
                long prevPos  = -2;
                var  runValues = new List<byte>();

                foreach (var kvp in _editsManager.GetAllModifiedBytes())
                {
                    if (kvp.Key == prevPos + 1)
                    {
                        runValues.Add(kvp.Value);
                    }
                    else
                    {
                        if (runValues.Count > 0)
                            modifiedRanges.Add(new ModifiedRange(runStart, runValues.ToArray()));
                        runStart = kvp.Key;
                        runValues.Clear();
                        runValues.Add(kvp.Value);
                    }
                    prevPos = kvp.Key;
                }

                if (runValues.Count > 0)
                    modifiedRanges.Add(new ModifiedRange(runStart, runValues.ToArray()));
            }

            // -- Inserted: collect bytes per physical position --------------
            var insertedBlocks = new List<InsertedBlock>();
            foreach (var kvp in _editsManager.GetInsertionPositionsWithCounts())
            {
                var list = _editsManager.GetInsertedBytesAt(kvp.Key);
                var ordered = list
                    .OrderBy(ib => ib.VirtualOffset)
                    .Select(ib => ib.Value)
                    .ToArray();
                if (ordered.Length > 0)
                    insertedBlocks.Add(new InsertedBlock(kvp.Key, ordered));
            }

            // -- Deleted: group consecutive positions into ranges -----------
            var deletedRanges = new List<DeletedRange>();
            {
                long rangeStart = -1;
                long prevDel    = -2;
                long rangeCount = 0;

                foreach (var pos in _editsManager.GetAllDeletedPositions())
                {
                    if (pos == prevDel + 1)
                    {
                        rangeCount++;
                    }
                    else
                    {
                        if (rangeCount > 0)
                            deletedRanges.Add(new DeletedRange(rangeStart, rangeCount));
                        rangeStart = pos;
                        rangeCount = 1;
                    }
                    prevDel = pos;
                }

                if (rangeCount > 0)
                    deletedRanges.Add(new DeletedRange(rangeStart, rangeCount));
            }

            return new ChangesetSnapshot(modifiedRanges, insertedBlocks, deletedRanges);
        }
    }
}
