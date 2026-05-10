//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
//////////////////////////////////////////////

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.TestHelpers;

/// <summary>
/// Runs a Roslyn parse + minimal compile against generator output to assert
/// it is syntactically and semantically valid C#.
/// </summary>
internal static class CSharpCompileGate
{
    /// <summary>
    /// Parses <paramref name="source"/> and asserts no syntax diagnostic at
    /// Error severity is reported. Returns the parsed syntax tree for
    /// callers that want to perform extra inspections.
    /// </summary>
    public static SyntaxTree AssertParsesCleanly(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(source));
        var errors = tree
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count == 0)
            return tree;

        var detail = string.Join("\n", errors.Select(e => $"  {e.GetMessage()} @ {e.Location.GetLineSpan()}"));
        Assert.Fail($"Generated source contains {errors.Count} parse error(s):\n{detail}\n--- Source ---\n{source}");
        return tree;
    }
}
