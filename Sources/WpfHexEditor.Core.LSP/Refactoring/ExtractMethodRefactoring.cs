// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Refactoring/ExtractMethodRefactoring.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Extracts the current selection into a new method/function stub.
//     Produces two TextEdit operations:
//       1. Replace the selection with a call-site placeholder.
//       2. Insert the extracted method body below the current function.
// ==========================================================

namespace WpfHexEditor.Core.LSP.Refactoring;

/// <summary>
/// Extracts the selected text into a new method and replaces the selection
/// with a call to that method.
/// </summary>
public sealed class ExtractMethodRefactoring : IRefactoring
{
    /// <summary>Name to assign to the extracted method. Must be set before Apply().</summary>
    public string MethodName { get; set; } = "ExtractedMethod";

    public string Name => "Extract Method";

    public bool CanApply(RefactoringContext context)
        => context.SelectionLength > 0
           && !string.IsNullOrWhiteSpace(context.SelectedText);

    public IReadOnlyList<TextEdit> Apply(RefactoringContext context)
    {
        if (!CanApply(context)) return [];

        var selected = context.SelectedText;
        var indent   = DetectIndent(context.DocumentText, context.SelectionStart);

        // Build the extracted method stub.
        var methodBody = $"\n{indent}private void {MethodName}()\n{indent}{{\n"
                       + IndentLines(selected, indent + "    ")
                       + $"\n{indent}}}\n";

        // Insert point: after the end of the document or after the current block.
        int insertPoint = FindInsertPoint(context.DocumentText, context.SelectionStart + context.SelectionLength);

        return
        [
            // Replace selection with a call.
            new TextEdit(context.FilePath, context.SelectionStart, context.SelectionLength, $"{MethodName}();"),
            // Insert the new method body.
            new TextEdit(context.FilePath, insertPoint, 0, methodBody),
        ];
    }

    // -----------------------------------------------------------------------

    private static string DetectIndent(string text, int offset)
    {
        // Find the start of the line containing offset.
        int lineStart = offset;
        while (lineStart > 0 && text[lineStart - 1] != '\n') lineStart--;
        int ws = lineStart;
        while (ws < text.Length && (text[ws] == ' ' || text[ws] == '\t')) ws++;
        return text[lineStart..ws];
    }

    private static string IndentLines(string text, string indent)
        => string.Join("\n", text.Split('\n').Select(l => indent + l.TrimStart()));

    private static int FindInsertPoint(string text, int fromOffset)
    {
        // Skip to end of current statement (next blank line or end of text).
        int i = fromOffset;
        while (i < text.Length)
        {
            if (i + 1 < text.Length && text[i] == '\n' && text[i + 1] == '\n')
                return i + 1;
            i++;
        }
        return text.Length;
    }
}
