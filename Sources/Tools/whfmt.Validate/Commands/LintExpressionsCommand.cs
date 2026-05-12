// ==========================================================
// Project: whfmt.Validate
// File: Commands/LintExpressionsCommand.cs
// Description: `whfmt lint-expressions` — validates the expression-bearing fields
//              inside one or more .whfmt files against the WhfmtExpressionValidator
//              from WpfHexEditor.Core.Definitions.Models.Validation. Emits one
//              JSON-line per issue so the whfmt-guard PowerShell skill can parse it.
// ==========================================================

using System.CommandLine;
using System.Text.Json;
using WpfHexEditor.Core.Definitions.Models.Validation;

namespace WhfmtValidate.Commands;

internal static class LintExpressionsCommand
{
    internal static Command Build()
    {
        var filesArg = new Argument<FileInfo[]>("files", ".whfmt files to lint.")
        {
            Arity = ArgumentArity.OneOrMore
        };

        var jsonOpt = new Option<bool>(
            ["--json"],
            "Emit one JSON object per line (one per issue) for tooling consumption.");

        var cmd = new Command("lint-expressions",
            "Statically validate expression-bearing fields (assertions[].expression, blocks[].expression/condition, " +
            "forensic.suspiciousPatterns[].condition) inside .whfmt files. Reports parse errors, undeclared identifiers, " +
            "and unknown function calls.")
        {
            filesArg, jsonOpt
        };

        cmd.SetHandler((files, json) =>
        {
            int issueCount = 0;
            foreach (var file in files)
            {
                if (!file.Exists)
                {
                    Console.Error.WriteLine($"ERR  not-found  {file.FullName}");
                    continue;
                }

                var content = File.ReadAllText(file.FullName);
                var issues  = WhfmtExpressionValidator.Validate(content);
                foreach (var issue in issues)
                {
                    issueCount++;
                    if (json)
                    {
                        var payload = new
                        {
                            file       = file.FullName,
                            ruleId     = issue.RuleId,
                            severity   = issue.Severity.ToString(),
                            path       = issue.Path,
                            message    = issue.Message,
                            source     = issue.Source,
                            position   = issue.Position,
                        };
                        Console.WriteLine(JsonSerializer.Serialize(payload));
                    }
                    else
                    {
                        Console.WriteLine($"{issue.Severity,-7} {issue.RuleId}  {file.Name} {issue.Path}: {issue.Message}");
                    }
                }
            }
            return Task.FromResult(issueCount == 0 ? 0 : 1);
        }, filesArg, jsonOpt);

        return cmd;
    }
}
