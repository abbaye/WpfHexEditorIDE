// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeFixes/Fixers/UnusedLocalRemoveFixer.cs
// Description: WH0013 — remove a LocalDeclarationStatement whose initializer
//              is side-effect-free (literal / identifier / cast). Refuses
//              when the initializer is an invocation (could be effectful).
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Analysis.CodeFixes.Fixers;

internal sealed class UnusedLocalRemoveFixer : IRoslynFixer
{
    public string RuleId => "WH0013";

    public LspCodeAction? TryBuild(AnalysisDiagnostic d, SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var local = root.DescendantNodes()
            .OfType<LocalDeclarationStatementSyntax>()
            .FirstOrDefault(l => FixerHelpers.OnLine(l, d.Line));
        if (local is null) return null;

        // Multi-declarator (var a = 1, b = 2;) — out of scope for v1
        if (local.Declaration.Variables.Count != 1) return null;

        var v = local.Declaration.Variables[0];
        var init = v.Initializer?.Value;
        // Refuse anything that may have side effects.
        if (init is InvocationExpressionSyntax
                 or AwaitExpressionSyntax
                 or ObjectCreationExpressionSyntax
                 or ImplicitObjectCreationExpressionSyntax
                 or ConditionalAccessExpressionSyntax)
            return null;

        var span = local.GetLocation().GetLineSpan();
        var edit = new LspTextEdit
        {
            StartLine   = span.StartLinePosition.Line,
            StartColumn = 0,
            EndLine     = span.EndLinePosition.Line + 1,
            EndColumn   = 0,
            NewText     = string.Empty,
        };

        return FixerHelpers.SingleFileEdit(AppResources.CodeAnalysis_Fix_WH0013_Title, d.FilePath, edit);
    }
}
