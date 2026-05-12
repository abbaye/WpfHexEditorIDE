// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeFixes/FixerHelpers.cs
// Description: Shared helpers used by every IRoslynFixer and IMechanicalFixer
//              — line-hit predicate (1-based diagnostic line vs syntax span)
//              and the LspWorkspaceEdit boilerplate every fixer emits.
// ==========================================================

using Microsoft.CodeAnalysis;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Analysis.CodeFixes;

internal static class FixerHelpers
{
    /// <summary>True when the syntax node's source range contains <paramref name="diagLine1Based"/>.</summary>
    internal static bool OnLine(SyntaxNode node, int diagLine1Based)
    {
        var span = node.GetLocation().GetLineSpan();
        return diagLine1Based >= span.StartLinePosition.Line + 1
            && diagLine1Based <= span.EndLinePosition.Line + 1;
    }

    /// <summary>Wraps a single LspTextEdit on <paramref name="filePath"/> in an LspCodeAction.</summary>
    internal static LspCodeAction SingleFileEdit(string title, string filePath, LspTextEdit edit, string kind = "quickfix") => new()
    {
        Title = title,
        Kind  = kind,
        Edit  = new LspWorkspaceEdit
        {
            Changes = new Dictionary<string, IReadOnlyList<LspTextEdit>>(StringComparer.OrdinalIgnoreCase)
            {
                [filePath] = new[] { edit },
            },
        },
    };
}
