// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Refactoring/ExtractClassRefactoring.cs
// Description:
//     Extracts the current selection into a new top-level class declaration
//     appended at the end of the document. The original selection is
//     replaced by a reference comment for the user to integrate by hand.
//     Pragmatic implementation — heavy semantic moves remain a Roslyn-only
//     task and are deferred to a Roslyn-based refactoring provider.
// ==========================================================

namespace WpfHexEditor.Core.LSP.Refactoring;

/// <summary>Extracts the current selection into a new class declaration.</summary>
public sealed class ExtractClassRefactoring : IRefactoring
{
    /// <summary>Name to assign to the extracted class. Defaults to "ExtractedClass".</summary>
    public string ClassName { get; set; } = "ExtractedClass";

    public string Name => "Extract Class";

    public bool CanApply(RefactoringContext context)
        => context.SelectionLength > 0
           && !string.IsNullOrWhiteSpace(context.SelectedText);

    public IReadOnlyList<TextEdit> Apply(RefactoringContext context)
    {
        if (!CanApply(context)) return [];

        var selected = context.SelectedText;
        var indent   = DetectBaseIndent(context.DocumentText, context.SelectionStart);
        var body     = IndentLines(selected, indent + "    ");

        var classBlock = $"\n\n{indent}// Extracted from selection — review and integrate as needed.\n"
                       + $"{indent}public class {ClassName}\n"
                       + $"{indent}{{\n"
                       + $"{body}\n"
                       + $"{indent}}}\n";

        return
        [
            new TextEdit(context.FilePath, context.SelectionStart, context.SelectionLength,
                $"// see {ClassName} below for the extracted members"),
            new TextEdit(context.FilePath, context.DocumentText.Length, 0, classBlock),
        ];
    }

    private static string DetectBaseIndent(string text, int offset)
    {
        int lineStart = offset;
        while (lineStart > 0 && text[lineStart - 1] != '\n') lineStart--;
        int ws = lineStart;
        while (ws < text.Length && (text[ws] == ' ' || text[ws] == '\t')) ws++;
        return text[lineStart..ws];
    }

    private static string IndentLines(string text, string indent)
        => string.Join("\n", text.Split('\n').Select(l => indent + l.TrimStart()));
}
