// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/RoslynDiagnosticsCollector.cs
// Description: Extracts all compiler errors and warnings (CS*, IDE*) from a
//              Roslyn Compilation and translates them to AnalysisDiagnostic.
//              Stateless — safe for parallel use.
// ==========================================================

using Microsoft.CodeAnalysis;
using WpfHexEditor.App.Analysis.Models;
using RoslynSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;
using Severity = WpfHexEditor.App.Analysis.Models.DiagnosticSeverity;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class RoslynDiagnosticsCollector
{
    internal static IReadOnlyList<AnalysisDiagnostic> Collect(
        Compilation compilation, string projectName)
    {
        var diags = compilation.GetDiagnostics();
        var results = new List<AnalysisDiagnostic>(diags.Length);

        foreach (var d in diags)
        {
            if (d.Severity == RoslynSeverity.Hidden) continue;
            if (!d.Location.IsInSource) continue;

            var span = d.Location.GetLineSpan();
            results.Add(new AnalysisDiagnostic
            {
                Id          = d.Id,
                Severity    = MapSeverity(d.Severity),
                Message     = d.GetMessage(),
                FilePath    = span.Path,
                Line        = span.StartLinePosition.Line + 1,
                Column      = span.StartLinePosition.Character + 1,
                ProjectName = projectName,
                RuleSource  = "Roslyn",
            });
        }

        return results;
    }

    private static Severity MapSeverity(RoslynSeverity s) => s switch
    {
        RoslynSeverity.Error   => Severity.Error,
        RoslynSeverity.Warning => Severity.Warning,
        _                      => Severity.Info,
    };
}
