// ==========================================================
// Project: whfmt.Validate
// File: Commands/ValidateCommand.cs
// Description: `whfmt validate` — validates one or more files against whfmt definitions.
// ==========================================================

using System.CommandLine;
using WhfmtValidate.Engine;
using WhfmtValidate.Reporting;

namespace WhfmtValidate.Commands;

internal static class ValidateCommand
{
    internal static Command Build()
    {
        var filesArg = new Argument<FileSystemInfo[]>("files", "File(s) or directory to validate.")
        {
            Arity = ArgumentArity.OneOrMore
        };

        var formatOpt = new Option<string?>(
            ["--format", "-f"],
            "Force a specific format name or extension (skips auto-detection).");

        var reportOpt = new Option<string>(
            ["--report", "-r"],
            () => "text",
            "Output report format: text (default), json, html.");

        var outputOpt = new Option<string?>(
            ["--output", "-o"],
            "Write report to this file path instead of stdout.");

        var recursiveOpt = new Option<bool>(
            ["--recursive", "-R"],
            "Recursively validate all files in a directory.");

        var failFastOpt = new Option<bool>(
            ["--fail-fast"],
            "Exit immediately on first validation error.");

        var quietOpt = new Option<bool>(
            ["--quiet", "-q"],
            "Suppress output; only set exit code (0=valid, 1=invalid, 2=error).");

        var cmd = new Command("validate", "Validate binary files against whfmt format definitions.")
        {
            filesArg, formatOpt, reportOpt, outputOpt, recursiveOpt, failFastOpt, quietOpt
        };

        cmd.SetHandler(async (files, format, report, output, recursive, failFast, quiet) =>
        {
            var engine = new ValidationEngine();
            var reports = new List<ValidationReport>();
            int exitCode = 0;

            var paths = CollectPaths(files, recursive);

            foreach (var path in paths)
            {
                var r = engine.Validate(path, format);
                reports.Add(r);

                if (!quiet)
                {
                    string rendered = report.ToLowerInvariant() switch
                    {
                        "json" => ReportRenderer.RenderJson(r),
                        "html" => ReportRenderer.RenderHtml(r),
                        _      => ReportRenderer.RenderText(r)
                    };

                    if (output is not null)
                        await File.AppendAllTextAsync(output, rendered);
                    else
                        Console.Write(rendered);
                }

                if (!r.IsValid) exitCode = 1;
                if (r.FileNotFound) exitCode = 2;
                if (failFast && exitCode != 0) break;
            }

            // For JSON/HTML with output file and multi-file batch, write combined report
            if (output is not null && reports.Count > 1 && report.ToLowerInvariant() != "text")
            {
                string combined = report.ToLowerInvariant() == "json"
                    ? RenderCombinedJson(reports)
                    : RenderCombinedHtml(reports);
                await File.WriteAllTextAsync(output, combined);
            }

            Environment.Exit(exitCode);
        },
        filesArg, formatOpt, reportOpt, outputOpt, recursiveOpt, failFastOpt, quietOpt);

        return cmd;
    }

    private static IEnumerable<string> CollectPaths(FileSystemInfo[] items, bool recursive)
    {
        foreach (var item in items)
        {
            if (item is FileInfo fi)
            {
                yield return fi.FullName;
            }
            else if (item is DirectoryInfo di && di.Exists)
            {
                var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var f in di.EnumerateFiles("*", option))
                    yield return f.FullName;
            }
        }
    }

    private static string RenderCombinedJson(List<ValidationReport> reports)
    {
        var summary = new
        {
            total   = reports.Count,
            valid   = reports.Count(r => r.IsValid),
            invalid = reports.Count(r => !r.IsValid),
            files   = reports.Select(r => new
            {
                file     = r.FilePath,
                format   = r.FormatName,
                isValid  = r.IsValid,
                errors   = r.ErrorCount,
                warnings = r.WarningCount
            })
        };
        return System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static string RenderCombinedHtml(List<ValidationReport> reports)
    {
        // Simple summary table for multi-file HTML
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><title>whfmt validate — Batch Report</title>");
        sb.AppendLine("<style>body{font-family:system-ui;background:#0d1117;color:#c9d1d9;padding:2rem}table{width:100%;border-collapse:collapse}th,td{padding:.5rem .8rem;border:1px solid #30363d;text-align:left}th{background:#161b22}tr.invalid td{color:#f85149}tr.valid td{color:#3fb950}</style></head><body>");
        sb.AppendLine($"<h1>whfmt validate — Batch Report</h1><p>{reports.Count} files — {reports.Count(r => r.IsValid)} valid, {reports.Count(r => !r.IsValid)} invalid</p>");
        sb.AppendLine("<table><tr><th>File</th><th>Format</th><th>Valid</th><th>Errors</th><th>Warnings</th></tr>");
        foreach (var r in reports)
        {
            string cls = r.IsValid ? "valid" : "invalid";
            sb.AppendLine($"<tr class=\"{cls}\"><td>{r.FileName}</td><td>{r.FormatName}</td><td>{(r.IsValid ? "✓" : "✗")}</td><td>{r.ErrorCount}</td><td>{r.WarningCount}</td></tr>");
        }
        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }
}
