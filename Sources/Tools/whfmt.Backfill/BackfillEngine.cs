// ==========================================================
// Project: whfmt.Backfill
// File: BackfillEngine.cs
// Description: Orchestrates parse → infer → append for one or many .whfmt files.
// Architecture: Single-responsibility class; produces a BackfillReport per file.
// ==========================================================

using WhfmtBackfill.Emit;
using WhfmtBackfill.Inferrers;
using WhfmtBackfill.Parsing;

namespace WhfmtBackfill;

/// <summary>Per-file outcome of a backfill operation.</summary>
public sealed record BackfillFileResult(
    string  Path,
    string  FormatId,
    string  Category,
    bool    AddedDiff,
    bool    AddedRepair,
    bool    AddedFuzz,
    bool    Skipped,
    string? SkipReason,
    string? Error);

/// <summary>Aggregate session report.</summary>
public sealed class BackfillReport
{
    public List<BackfillFileResult> Files { get; } = [];
    public int TotalProcessed     => Files.Count;
    public int FilesModified      => Files.Count(f => !f.Skipped && f.Error is null && (f.AddedDiff || f.AddedRepair || f.AddedFuzz));
    public int FilesSkipped       => Files.Count(f => f.Skipped);
    public int FilesErrored       => Files.Count(f => f.Error is not null);
    public int DiffBlocksAdded    => Files.Count(f => f.AddedDiff);
    public int RepairBlocksAdded  => Files.Count(f => f.AddedRepair);
    public int FuzzBlocksAdded    => Files.Count(f => f.AddedFuzz);
}

/// <summary>Orchestrates the full backfill pipeline.</summary>
public sealed class BackfillEngine
{
    private readonly bool _dryRun;

    public BackfillEngine(bool dryRun) { _dryRun = dryRun; }

    /// <summary>Process a single .whfmt file. Returns the result without throwing.</summary>
    public BackfillFileResult ProcessFile(string path)
    {
        try
        {
            string original = File.ReadAllText(path);
            var summary = WhfmtParser.Parse(original);

            // Idempotency: if all four blocks already exist, skip
            if (summary.HasDiffBlock && summary.HasRepairBlock && summary.HasFuzzBlock)
            {
                return new BackfillFileResult(path, summary.FormatId, summary.Category,
                    false, false, false, Skipped: true, SkipReason: "all blocks already present", Error: null);
            }

            var fragments = new List<string>();
            bool addedDiff = false, addedRepair = false, addedFuzz = false;

            if (!summary.HasDiffBlock)
            {
                var diff = DiffInferrer.Infer(summary);
                if (diff is not null)
                {
                    fragments.Add(JsonBlockEmitter.EmitDiff(diff));
                    addedDiff = true;
                }
            }

            if (!summary.HasRepairBlock)
            {
                var rules = RepairInferrer.Infer(summary);
                if (rules.Count > 0)
                {
                    fragments.Add(JsonBlockEmitter.EmitRepair(rules));
                    addedRepair = true;
                }
            }

            if (!summary.HasFuzzBlock)
            {
                var fuzz = FuzzInferrer.Infer(summary);
                if (fuzz is not null)
                {
                    fragments.Add(JsonBlockEmitter.EmitFuzz(fuzz));
                    addedFuzz = true;
                }
            }

            if (fragments.Count == 0)
            {
                return new BackfillFileResult(path, summary.FormatId, summary.Category,
                    false, false, false, Skipped: true, SkipReason: "no inferable blocks", Error: null);
            }

            string updated = WhfmtAppender.Append(original, fragments);
            if (!_dryRun)
                File.WriteAllText(path, updated);

            return new BackfillFileResult(path, summary.FormatId, summary.Category,
                addedDiff, addedRepair, addedFuzz, Skipped: false, SkipReason: null, Error: null);
        }
        catch (Exception ex)
        {
            return new BackfillFileResult(path, "?", "?", false, false, false, Skipped: false, SkipReason: null, Error: ex.Message);
        }
    }

    /// <summary>Process every .whfmt file under <paramref name="root"/>.</summary>
    public BackfillReport ProcessDirectory(string root)
    {
        var report = new BackfillReport();
        var files  = Directory.EnumerateFiles(root, "*.whfmt", SearchOption.AllDirectories).OrderBy(p => p);
        foreach (var file in files)
            report.Files.Add(ProcessFile(file));
        return report;
    }
}
