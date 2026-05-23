// ==========================================================
// Project: whfmt.Validate
// File: Commands/SweepCommand.cs
// Description: `whfmt sweep` — static-analysis sweep over the entire format catalog.
//              Runs WhfmtExpressionValidator (R10) on every .whfmt file found under
//              a directory, emits a CSV + console summary, and applies mechanical
//              backfill (prose-expression → _comment) when --fix is passed.
// Architecture: Command shell → SweepEngine (orchestration) → WhfmtExpressionValidator
//               (expression lint) + WhfmtProseCommentFixer (backfill).
// ==========================================================

using System.CommandLine;
using System.Text;
using WpfHexEditor.Core.Definitions.Models.Validation;
using WhfmtValidate.Engine;

namespace WhfmtValidate.Commands;

internal static class SweepCommand
{
    internal static Command Build()
    {
        var dirArg = new Argument<DirectoryInfo>(
            "catalog-dir",
            "Root directory containing .whfmt format definition files (searched recursively).");

        var outputOpt = new Option<string?>(
            ["--output", "-o"],
            "Write CSV report to this file path (appends .csv if omitted).");

        var fixOpt = new Option<bool>(
            ["--fix"],
            "Apply mechanical fixes: annotate prose-expression fields with a _comment sibling and neutralize the expression.");

        var failOnWarnOpt = new Option<bool>(
            ["--fail-on-warn"],
            "Exit with code 1 if any warnings are found (default: only errors trigger exit 1).");

        var quietOpt = new Option<bool>(
            ["--quiet", "-q"],
            "Suppress per-file progress; only print the final summary.");

        var cmd = new Command("sweep",
            "Run a static-analysis sweep over all .whfmt files in a catalog directory. " +
            "Reports R10 expression issues (undeclared identifiers, unknown functions, prose expressions). " +
            "Use --fix to automatically neutralize prose expressions for catalog cleanup.")
        {
            dirArg, outputOpt, fixOpt, failOnWarnOpt, quietOpt
        };

        cmd.SetHandler(async (dir, output, fix, failOnWarn, quiet) =>
        {
            if (!dir.Exists)
            {
                Console.Error.WriteLine($"ERR  Directory not found: {dir.FullName}");
                Environment.Exit(2);
                return;
            }

            string csvPath = ResolveCsvPath(output, dir);
            var results    = new List<SweepFileResult>();
            var files      = dir.EnumerateFiles("*.whfmt", SearchOption.AllDirectories).ToList();

            if (!quiet)
                Console.WriteLine($"  whfmt sweep — {files.Count} files in {dir.FullName}");

            int processed = 0;
            foreach (var file in files)
            {
                var result = ProcessFile(file, fix);
                results.Add(result);
                processed++;

                if (!quiet && result.IssueCount > 0)
                    Console.WriteLine($"  [{processed,4}/{files.Count}]  {result.IssueCount,3} issue(s)  {file.Name}");
            }

            // ── Summary ──────────────────────────────────────────────────────
            int totalErrors   = results.Sum(r => r.ErrorCount);
            int totalWarnings = results.Sum(r => r.WarningCount);
            int totalFixed    = results.Count(r => r.WasFixed);
            int filesClean    = results.Count(r => r.IssueCount == 0);

            Console.WriteLine();
            Console.WriteLine($"  ─── Sweep complete ────────────────────────────────");
            Console.WriteLine($"  Files scanned  : {files.Count}");
            Console.WriteLine($"  Files clean    : {filesClean}");
            Console.WriteLine($"  Total errors   : {totalErrors}");
            Console.WriteLine($"  Total warnings : {totalWarnings}");
            if (fix)
                Console.WriteLine($"  Files fixed    : {totalFixed}");
            Console.WriteLine();

            // ── CSV export ───────────────────────────────────────────────────
            await WriteCsvAsync(csvPath, results);
            Console.WriteLine($"  Report written : {csvPath}");
            Console.WriteLine();

            int exitCode = totalErrors > 0 ? 1 : (failOnWarn && totalWarnings > 0 ? 1 : 0);
            Environment.Exit(exitCode);
        },
        dirArg, outputOpt, fixOpt, failOnWarnOpt, quietOpt);

        return cmd;
    }

    // ── Core processing ──────────────────────────────────────────────────────

    private static SweepFileResult ProcessFile(FileInfo file, bool fix)
    {
        string content;
        try
        {
            content = File.ReadAllText(file.FullName, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            return new SweepFileResult(file.FullName, file.Name, [], false, ReadError: ex.Message);
        }

        var issues = WhfmtExpressionValidator.Validate(content);

        bool wasFixed = false;
        if (fix && issues.Count > 0)
        {
            var proseIssues = issues.Where(i => IsProseIssue(i)).ToList();
            if (proseIssues.Count > 0)
            {
                string? patched = WhfmtProseCommentFixer.TryApply(content, proseIssues);
                if (patched is not null && patched != content)
                {
                    File.WriteAllText(file.FullName, patched, Encoding.UTF8);
                    wasFixed = true;
                    // Re-validate after fix to update issue count
                    issues = WhfmtExpressionValidator.Validate(patched);
                }
            }
        }

        return new SweepFileResult(file.FullName, file.Name, issues, wasFixed);
    }

    private static bool IsProseIssue(WhfmtValidationIssue issue) =>
        issue.RuleId is "R10-000" or "R10-001" &&
        issue.Message.Contains("prose", StringComparison.OrdinalIgnoreCase);

    // ── CSV output ───────────────────────────────────────────────────────────

    private static async Task WriteCsvAsync(string path, List<SweepFileResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("file,fileName,ruleId,severity,path,message,wasFixed");

        foreach (var r in results)
        {
            if (r.ReadError is not null)
            {
                sb.AppendLine(CsvRow(r.FilePath, r.FileName, "READ-ERROR", "error", "", r.ReadError ?? "", false));
                continue;
            }

            if (r.Issues.Count == 0)
            {
                sb.AppendLine(CsvRow(r.FilePath, r.FileName, "", "ok", "", "", false));
                continue;
            }

            foreach (var issue in r.Issues)
                sb.AppendLine(CsvRow(r.FilePath, r.FileName, issue.RuleId, issue.Severity.ToString().ToLowerInvariant(), issue.Path ?? "", issue.Message, r.WasFixed));
        }

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
    }

    private static string CsvRow(string file, string fileName, string ruleId, string severity, string path, string message, bool wasFixed)
    {
        static string Q(string s) => $"\"{s.Replace("\"", "\"\"")}\"";
        return $"{Q(file)},{Q(fileName)},{Q(ruleId)},{Q(severity)},{Q(path)},{Q(message)},{(wasFixed ? "true" : "false")}";
    }

    private static string ResolveCsvPath(string? output, DirectoryInfo dir)
    {
        if (output is null)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
            return Path.Combine(dir.FullName, $"sweep-{timestamp}.csv");
        }
        return output.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? output : output + ".csv";
    }
}

// ── Result record ─────────────────────────────────────────────────────────────

internal sealed record SweepFileResult(
    string                     FilePath,
    string                     FileName,
    IReadOnlyList<WhfmtValidationIssue> Issues,
    bool                       WasFixed,
    string?                    ReadError = null)
{
    public int IssueCount   => Issues.Count;
    public int ErrorCount   => Issues.Count(i => i.Severity == WhfmtIssueSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == WhfmtIssueSeverity.Warning);
}
