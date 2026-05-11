// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeFixes/Fixers/AsyncVoidToTaskFixer.cs
// Description: WH0061 — replace `async void` with `async Task` on the
//              MethodDeclarationSyntax at the diagnostic line. Refuses when
//              the signature looks like a WPF/WinForms event handler.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Analysis.CodeFixes.Fixers;

internal sealed class AsyncVoidToTaskFixer : IRoslynFixer
{
    public string RuleId => "WH0061";

    public LspCodeAction? TryBuild(AnalysisDiagnostic d, SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => OnLine(m, d.Line));
        if (method is null) return null;

        // Must be async + void
        if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword)) return null;
        if (method.ReturnType is not PredefinedTypeSyntax pt || !pt.Keyword.IsKind(SyntaxKind.VoidKeyword)) return null;

        // Refuse event handler signatures (sender, EventArgs|args)
        if (IsEventHandlerSignature(method)) return null;

        var voidSpan = pt.Keyword.GetLocation().GetLineSpan();
        var edit = new LspTextEdit
        {
            StartLine   = voidSpan.StartLinePosition.Line,
            StartColumn = voidSpan.StartLinePosition.Character,
            EndLine     = voidSpan.EndLinePosition.Line,
            EndColumn   = voidSpan.EndLinePosition.Character,
            NewText     = "Task",
        };

        return new LspCodeAction
        {
            Title = AppResources.CodeAnalysis_Fix_WH0061_Title,
            Kind  = "quickfix",
            Edit  = new LspWorkspaceEdit
            {
                Changes = new Dictionary<string, IReadOnlyList<LspTextEdit>>(StringComparer.OrdinalIgnoreCase)
                {
                    [d.FilePath] = new[] { edit },
                },
            },
        };
    }

    private static bool OnLine(SyntaxNode node, int diagLine1Based)
    {
        var span = node.GetLocation().GetLineSpan();
        int from = span.StartLinePosition.Line + 1;
        int to   = span.EndLinePosition.Line + 1;
        return diagLine1Based >= from && diagLine1Based <= to;
    }

    private static bool IsEventHandlerSignature(MethodDeclarationSyntax m)
    {
        var ps = m.ParameterList.Parameters;
        if (ps.Count != 2) return false;
        var t0 = ps[0].Type?.ToString();
        var t1 = ps[1].Type?.ToString();
        return string.Equals(t0, "object", StringComparison.Ordinal)
            && (t1?.EndsWith("EventArgs", StringComparison.Ordinal) == true
             || t1?.EndsWith("Args",      StringComparison.Ordinal) == true);
    }
}
