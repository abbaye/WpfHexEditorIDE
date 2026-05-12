// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Refactoring/InlineMethodRefactoring.cs
// Description:
//     Inlines a method call. Caret must sit on a single-line method call
//     of the form `Identifier();`. The call is replaced by a placeholder
//     comment — full semantic inlining is deferred to Roslyn.
// ==========================================================

using System.Text.RegularExpressions;

namespace WpfHexEditor.Core.LSP.Refactoring;

/// <summary>Inlines a method call at the caret (heuristic).</summary>
public sealed class InlineMethodRefactoring : IRefactoring
{
    private static readonly Regex CallRegex = new(
        @"\b(?<name>[A-Za-z_]\w*)\s*\([^)]*\)\s*;",
        RegexOptions.Compiled);

    public string Name => "Inline Method";

    public bool CanApply(RefactoringContext context)
    {
        if (string.IsNullOrEmpty(context.DocumentText)) return false;
        return FindCallAtCaret(context) is not null;
    }

    public IReadOnlyList<TextEdit> Apply(RefactoringContext context)
    {
        var match = FindCallAtCaret(context);
        if (match is null) return [];

        var name = match.Groups["name"].Value;
        var placeholder = $"/* inlined call to {name}() — replace with the method body */";

        return
        [
            new TextEdit(context.FilePath, match.Index, match.Length, placeholder),
        ];
    }

    private static Match? FindCallAtCaret(RefactoringContext context)
    {
        var lineStart = LineStart(context.DocumentText, context.CaretOffset);
        var lineEnd   = LineEnd(context.DocumentText, context.CaretOffset);
        if (lineEnd <= lineStart) return null;

        var line = context.DocumentText[lineStart..lineEnd];
        var m = CallRegex.Match(line);
        if (!m.Success) return null;

        // Re-index to document coordinates.
        return CallRegex.Match(context.DocumentText, lineStart, lineEnd - lineStart);
    }

    private static int LineStart(string text, int offset)
    {
        int i = offset;
        while (i > 0 && text[i - 1] != '\n') i--;
        return i;
    }

    private static int LineEnd(string text, int offset)
    {
        int i = offset;
        while (i < text.Length && text[i] != '\n') i++;
        return i;
    }
}
