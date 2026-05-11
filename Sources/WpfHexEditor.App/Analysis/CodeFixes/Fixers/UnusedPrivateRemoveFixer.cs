// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeFixes/Fixers/UnusedPrivateRemoveFixer.cs
// Description: WH0010 — remove a private MemberDeclarationSyntax (method,
//              property, field) at the diagnostic line. Refuses constructors
//              and partial methods because confirming "truly unused" needs
//              cross-partial reasoning.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Analysis.CodeFixes.Fixers;

internal sealed class UnusedPrivateRemoveFixer : IRoslynFixer
{
    public string RuleId => "WH0010";

    public LspCodeAction? TryBuild(AnalysisDiagnostic d, SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var member = root.DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .Where(m => m is MethodDeclarationSyntax or PropertyDeclarationSyntax or FieldDeclarationSyntax or EventFieldDeclarationSyntax)
            .FirstOrDefault(m => OnLine(m, d.Line));
        if (member is null) return null;

        // Refuse partial / abstract / virtual / override — caller analysis is shaky there
        if (HasModifier(member, SyntaxKind.PartialKeyword)
         || HasModifier(member, SyntaxKind.AbstractKeyword)
         || HasModifier(member, SyntaxKind.VirtualKeyword)
         || HasModifier(member, SyntaxKind.OverrideKeyword))
            return null;

        var span = member.GetLocation().GetLineSpan();
        var edit = new LspTextEdit
        {
            StartLine   = span.StartLinePosition.Line,
            StartColumn = 0,
            EndLine     = span.EndLinePosition.Line + 1,
            EndColumn   = 0,
            NewText     = string.Empty,
        };

        return new LspCodeAction
        {
            Title = AppResources.CodeAnalysis_Fix_WH0010_Title,
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

    private static bool HasModifier(MemberDeclarationSyntax m, SyntaxKind kind)
        => m.Modifiers.Any(mod => mod.IsKind(kind));

    private static bool OnLine(SyntaxNode node, int diagLine1Based)
    {
        var span = node.GetLocation().GetLineSpan();
        return diagLine1Based >= span.StartLinePosition.Line + 1
            && diagLine1Based <= span.EndLinePosition.Line + 1;
    }
}
