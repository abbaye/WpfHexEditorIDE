// ==========================================================
// Project: whfmt.Backfill
// File: Program.cs
// Description: CLI entry — `whfmt-backfill --input <dir> [--dry-run] [--single <file>]`.
// ==========================================================

using System.CommandLine;
using WhfmtBackfill;

var inputOpt   = new Option<string?>(["--input", "-i"], "Root directory containing .whfmt files (recursive).");
var singleOpt  = new Option<string?>(["--single", "-s"], "Single .whfmt file to process (overrides --input).");
var dryRunOpt  = new Option<bool>   (["--dry-run"],     "Compute changes without writing to disk.");
var verboseOpt = new Option<bool>   (["--verbose", "-v"], "Print per-file outcome.");

var root = new RootCommand("whfmt.Backfill — infer diff/repair/fuzz blocks for .whfmt format definitions.")
{
    inputOpt, singleOpt, dryRunOpt, verboseOpt,
};

root.SetHandler((string? input, string? single, bool dryRun, bool verbose) =>
{
    if (string.IsNullOrEmpty(input) && string.IsNullOrEmpty(single))
    {
        Console.Error.WriteLine("Error: --input or --single is required.");
        Environment.Exit(2);
        return;
    }

    var engine = new BackfillEngine(dryRun);

    if (!string.IsNullOrEmpty(single))
    {
        if (!File.Exists(single))
        {
            Console.Error.WriteLine($"Error: file not found: {single}");
            Environment.Exit(2);
            return;
        }
        var result = engine.ProcessFile(single);
        PrintFile(result);
        return;
    }

    if (!Directory.Exists(input))
    {
        Console.Error.WriteLine($"Error: directory not found: {input}");
        Environment.Exit(2);
        return;
    }

    var report = engine.ProcessDirectory(input!);

    if (verbose)
    {
        foreach (var f in report.Files) PrintFile(f);
        Console.WriteLine();
    }

    Console.WriteLine($"  whfmt.Backfill — {(dryRun ? "DRY-RUN" : "WRITE")} mode");
    Console.WriteLine($"  Total processed   : {report.TotalProcessed}");
    Console.WriteLine($"  Files modified    : {report.FilesModified}");
    Console.WriteLine($"  Files skipped     : {report.FilesSkipped}");
    Console.WriteLine($"  Files errored     : {report.FilesErrored}");
    Console.WriteLine($"  diff   blocks added: {report.DiffBlocksAdded}");
    Console.WriteLine($"  repair blocks added: {report.RepairBlocksAdded}");
    Console.WriteLine($"  fuzz   blocks added: {report.FuzzBlocksAdded}");

    if (report.FilesErrored > 0)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"  {report.FilesErrored} file(s) errored:");
        foreach (var f in report.Files.Where(f => f.Error is not null))
            Console.Error.WriteLine($"    {f.Path}: {f.Error}");
        Environment.Exit(1);
    }
}, inputOpt, singleOpt, dryRunOpt, verboseOpt);

return await root.InvokeAsync(args);

static void PrintFile(BackfillFileResult f)
{
    if (f.Error is not null)        { Console.WriteLine($"  ✗ {f.Path}: {f.Error}"); return; }
    if (f.Skipped)                  { Console.WriteLine($"  - {f.Path}: skipped ({f.SkipReason})"); return; }
    var added = new List<string>();
    if (f.AddedDiff)   added.Add("diff");
    if (f.AddedRepair) added.Add("repair");
    if (f.AddedFuzz)   added.Add("fuzz");
    Console.WriteLine($"  ✓ {f.Path}: +{string.Join(",", added)}");
}
