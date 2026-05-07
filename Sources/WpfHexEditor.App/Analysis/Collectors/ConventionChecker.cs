// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/ConventionChecker.cs
// Description: Checks naming conventions (PascalCase, camelCase, _prefix),
//              file/class name mismatch, and TODO/FIXME/HACK markers.
//              Stateless — safe for parallel use.
// ==========================================================

using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.App.Analysis.Models;
using Severity = WpfHexEditor.App.Analysis.Models.DiagnosticSeverity;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class ConventionChecker
{
    private static readonly Regex TodoPattern =
        new(@"\b(TODO|FIXME|HACK)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] PrivateFieldPrefixes = ["_", "s_", "k_"];

    internal static IReadOnlyList<AnalysisDiagnostic> Check(
        SyntaxTree tree, string projectName, CodeAnalysisOptions options)
    {
        var root     = tree.GetRoot();
        var text     = tree.GetText();
        var filePath = tree.FilePath;
        var results  = new List<AnalysisDiagnostic>();

        // WH0031 — File/class name mismatch
        if (options.IsRuleEnabled("WH0031"))
        {
            var primaryType = root.DescendantNodes().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            if (primaryType is not null)
            {
                var fileName  = Path.GetFileNameWithoutExtension(filePath);
                var className = primaryType.Identifier.Text;
                if (!string.Equals(fileName, className, StringComparison.Ordinal))
                    results.Add(Diag("WH0031", Severity.Warning,
                        $"File name '{fileName}' does not match primary type '{className}'.",
                        filePath, primaryType.GetLocation(), projectName));
            }
        }

        // WH0030 — Naming conventions
        if (options.IsRuleEnabled("WH0030"))
        {
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (!IsPascalCase(method.Identifier.Text))
                    results.Add(Diag("WH0030", Severity.Info,
                        $"Method '{method.Identifier.Text}' should be PascalCase.",
                        filePath, method.Identifier.GetLocation(), projectName));
            }

            foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    var name = variable.Identifier.Text;
                    bool isPrivate = !field.Modifiers.Any(m =>
                        m.IsKind(SyntaxKind.PublicKeyword)    ||
                        m.IsKind(SyntaxKind.ProtectedKeyword) ||
                        m.IsKind(SyntaxKind.InternalKeyword));

                    if (isPrivate && !PrivateFieldPrefixes.Any(name.StartsWith))
                        results.Add(Diag("WH0030", Severity.Info,
                            $"Private field '{name}' should start with '_'.",
                            filePath, variable.Identifier.GetLocation(), projectName));
                }
            }
        }

        // WH0032 — TODO/FIXME/HACK markers
        if (options.IsRuleEnabled("WH0032"))
        {
            int lineNum = 1;
            foreach (var line in text.Lines)
            {
                var lineText = line.ToString();
                var match    = TodoPattern.Match(lineText);
                if (match.Success)
                    results.Add(Diag("WH0032", Severity.Info,
                        $"{match.Value} marker found.",
                        filePath, null, projectName, lineNum));
                lineNum++;
            }
        }

        return results;
    }

    private static bool IsPascalCase(string name)
        => name.Length > 0 && char.IsUpper(name[0]);

    private static AnalysisDiagnostic Diag(
        string id, Severity severity, string message,
        string filePath, Location? location, string project, int line = -1)
    {
        int ln = line >= 0 ? line : (location?.GetLineSpan().StartLinePosition.Line + 1 ?? -1);
        int col = location?.GetLineSpan().StartLinePosition.Character + 1 ?? -1;
        return new AnalysisDiagnostic
        {
            Id          = id,
            Severity    = severity,
            Message     = message,
            FilePath    = filePath,
            Line        = ln,
            Column      = col,
            ProjectName = project,
            RuleSource  = "Quality",
        };
    }
}
