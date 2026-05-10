// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/CSharp/CSharpXmlDocBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Produces a SyntaxTriviaList containing a single-line
//     /// <summary> XML doc block. Implemented as raw trivia so the
//     emitted output matches what a hand-written doc comment looks
//     like — Roslyn's structured DocumentationCommentTriviaSyntax
//     formats poorly when re-serialized.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.CSharp;

internal static class CSharpXmlDocBuilder
{
    public static SyntaxTriviaList Build(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return [];

        var escaped = EscapeXml(summary.Trim());
        var text =
            "/// <summary>\n" +
            $"/// {escaped}\n" +
            "/// </summary>\n";

        return SyntaxFactory.ParseLeadingTrivia(text);
    }

    private static string EscapeXml(string input) => input
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
