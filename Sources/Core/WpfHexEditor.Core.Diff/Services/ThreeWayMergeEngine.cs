// Project      : WpfHexEditorControl
// File         : Services/ThreeWayMergeEngine.cs
// Description  : 3-way line merge: base + ours + theirs → ThreeWayMergeResult.
// Architecture : Stateless service. Uses two Myers diffs (base→ours, base→theirs)
//                then merges edit scripts. Non-conflicting single-side changes are
//                auto-accepted; regions where both sides diverged → MergeConflict.

using WpfHexEditor.Core.Diff.Algorithms;
using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Services;

/// <summary>
/// Performs a 3-way line-level merge of base, ours, and theirs text.
/// </summary>
public sealed class ThreeWayMergeEngine
{
    private readonly MyersDiffAlgorithm _myers = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <param name="baseLines">Common ancestor lines.</param>
    /// <param name="oursLines">Left/ours revision lines.</param>
    /// <param name="theirsLines">Right/theirs revision lines.</param>
    public ThreeWayMergeResult Merge(string[] baseLines, string[] oursLines, string[] theirsLines)
    {
        var oursEdits   = ComputeLinearEdits(baseLines, oursLines);
        var theirsEdits = ComputeLinearEdits(baseLines, theirsLines);

        return BuildMerge(baseLines, oursEdits, theirsEdits);
    }

    // ── Linear edit computation ───────────────────────────────────────────────

    // Returns a list of (baseStart, baseLen, replacementLines) tuples — each
    // represents one contiguous edit chunk in the diff.
    private List<EditChunk> ComputeLinearEdits(string[] from, string[] to)
    {
        var result = _myers.ComputeLines(from, to);
        var chunks = new List<EditChunk>();

        int baseIdx   = 0;
        int targetIdx = 0;
        int i         = 0;
        var lines     = result.Lines;

        while (i < lines.Count)
        {
            var line = lines[i];
            if (line.Kind == TextLineKind.Equal)
            {
                baseIdx++;
                targetIdx++;
                i++;
                continue;
            }

            int chunkBaseStart = baseIdx;
            int removedCount   = 0;
            var added          = new List<string>();

            while (i < lines.Count && lines[i].Kind != TextLineKind.Equal)
            {
                var ln = lines[i];
                if (ln.Kind is TextLineKind.DeletedLeft or TextLineKind.Modified)
                {
                    removedCount++;
                    baseIdx++;
                }
                if (ln.Kind is TextLineKind.InsertedRight or TextLineKind.Modified)
                {
                    added.Add(to[targetIdx]);
                    targetIdx++;
                }
                i++;
            }

            chunks.Add(new EditChunk(chunkBaseStart, removedCount, added));
        }

        return chunks;
    }

    // ── Merge engine ──────────────────────────────────────────────────────────

    private static ThreeWayMergeResult BuildMerge(
        string[]         baseLines,
        List<EditChunk>  oursEdits,
        List<EditChunk>  theirsEdits)
    {

        var outputLines = new List<MergeLine>();
        var conflicts   = new List<MergeConflict>();

        int basePos  = 0;
        int oi       = 0; // index into oursEdits
        int ti       = 0; // index into theirsEdits
        int oursPos  = 0; // current line index in oursLines
        int theirsPos = 0;

        // Re-build ours/theirs position cursors from chunk offsets.
        // Each EditChunk already carries baseStart; we need the corresponding
        // target start.  Recompute it by walking the base→target mapping.
        var oursTargetStart   = ComputeTargetStarts(oursEdits);
        var theirsTargetStart = ComputeTargetStarts(theirsEdits);

        // Advance base pointer, flushing equal lines as Equal MergeLines.
        void FlushEqual(int upTo)
        {
            while (basePos < upTo && basePos < baseLines.Length)
            {
                outputLines.Add(new MergeLine
                {
                    Kind            = MergeLineKind.Equal,
                    Content         = baseLines[basePos],
                    BaseLineNumber  = basePos + 1,
                    OursLineNumber  = oursPos + 1,
                    TheirsLineNumber = theirsPos + 1,
                });
                basePos++;
                oursPos++;
                theirsPos++;
            }
        }

        while (oi < oursEdits.Count || ti < theirsEdits.Count || basePos < baseLines.Length)
        {
            int nextOurs   = oi < oursEdits.Count   ? oursEdits[oi].BaseStart   : int.MaxValue;
            int nextTheirs = ti < theirsEdits.Count ? theirsEdits[ti].BaseStart : int.MaxValue;

            if (nextOurs == int.MaxValue && nextTheirs == int.MaxValue)
            {
                // Remaining base lines are equal.
                FlushEqual(baseLines.Length);
                break;
            }

            int nextBase = Math.Min(nextOurs, nextTheirs);
            FlushEqual(nextBase);

            bool hasOurs   = oi < oursEdits.Count   && oursEdits[oi].BaseStart   == basePos;
            bool hasTheirs = ti < theirsEdits.Count && theirsEdits[ti].BaseStart == basePos;

            if (hasOurs && hasTheirs)
            {
                var oc = oursEdits[oi];
                var tc = theirsEdits[ti];
                int baseEnd = Math.Max(basePos + oc.BaseLen, basePos + tc.BaseLen);

                // Both sides changed — conflict or identical change.
                if (oc.Added.SequenceEqual(tc.Added))
                {
                    // Same replacement on both sides — auto-accept (no conflict).
                    int oursTgt   = oursTargetStart[oi];
                    int theirsTgt = theirsTargetStart[ti];

                    for (int li = 0; li < oc.Added.Count; li++)
                    {
                        outputLines.Add(new MergeLine
                        {
                            Kind             = MergeLineKind.AcceptedOurs,
                            Content          = oc.Added[li],
                            OursLineNumber   = oursTgt + li + 1,
                            TheirsLineNumber = theirsTgt + li + 1,
                        });
                    }
                    oursPos   = oursTgt   + oc.Added.Count;
                    theirsPos = theirsTgt + tc.Added.Count;
                }
                else
                {
                    // True conflict.
                    int conflictIndex = conflicts.Count;
                    int oursTgt   = oursTargetStart[oi];
                    int theirsTgt = theirsTargetStart[ti];

                    var conflict = new MergeConflict
                    {
                        Index       = outputLines.Count,
                        BaseLines   = baseLines[basePos..Math.Min(baseEnd, baseLines.Length)],
                        OursLines   = oc.Added,
                        TheirsLines = tc.Added,
                    };
                    conflicts.Add(conflict);

                    for (int li = 0; li < oc.Added.Count; li++)
                        outputLines.Add(new MergeLine
                        {
                            Kind           = MergeLineKind.ConflictOurs,
                            Content        = oc.Added[li],
                            OursLineNumber = oursTgt + li + 1,
                            ConflictIndex  = conflictIndex,
                        });

                    for (int li = 0; li < tc.Added.Count; li++)
                        outputLines.Add(new MergeLine
                        {
                            Kind             = MergeLineKind.ConflictTheirs,
                            Content          = tc.Added[li],
                            TheirsLineNumber = theirsTgt + li + 1,
                            ConflictIndex    = conflictIndex,
                        });

                    oursPos   = oursTgt   + oc.Added.Count;
                    theirsPos = theirsTgt + tc.Added.Count;
                }

                basePos = baseEnd;
                oi++;
                ti++;
            }
            else if (hasOurs)
            {
                var oc = oursEdits[oi];
                int oursTgt = oursTargetStart[oi];

                for (int li = 0; li < oc.Added.Count; li++)
                    outputLines.Add(new MergeLine
                    {
                        Kind           = MergeLineKind.AcceptedOurs,
                        Content        = oc.Added[li],
                        OursLineNumber = oursTgt + li + 1,
                    });

                basePos   += oc.BaseLen;
                oursPos    = oursTgt + oc.Added.Count;
                theirsPos += oc.BaseLen;
                oi++;
            }
            else // hasTheirs
            {
                var tc = theirsEdits[ti];
                int theirsTgt = theirsTargetStart[ti];

                for (int li = 0; li < tc.Added.Count; li++)
                    outputLines.Add(new MergeLine
                    {
                        Kind             = MergeLineKind.AcceptedTheirs,
                        Content          = tc.Added[li],
                        TheirsLineNumber = theirsTgt + li + 1,
                    });

                basePos   += tc.BaseLen;
                oursPos   += tc.BaseLen;
                theirsPos  = theirsTgt + tc.Added.Count;
                ti++;
            }
        }

        return new ThreeWayMergeResult
        {
            Lines     = outputLines,
            Conflicts = conflicts,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Recomputes the first target-line index for each chunk, walking base→target.
    private static int[] ComputeTargetStarts(List<EditChunk> chunks)
    {
        var starts  = new int[chunks.Count];
        int basePos = 0;
        int tgtPos  = 0;

        for (int i = 0; i < chunks.Count; i++)
        {
            var c = chunks[i];
            tgtPos   += c.BaseStart - basePos; // equal lines
            starts[i] = tgtPos;
            basePos   = c.BaseStart + c.BaseLen;
            tgtPos   += c.Added.Count;
        }

        return starts;
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed record EditChunk(int BaseStart, int BaseLen, IReadOnlyList<string> Added);
}
