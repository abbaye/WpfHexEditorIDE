// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Refactoring/IntroduceVariableRefactoring.cs
// Description:
//     Replaces the selected expression with a new local variable and
//     introduces the variable declaration on the line above. Pragmatic
//     C#/VB-friendly implementation — heuristic indent + var keyword.
// ==========================================================

namespace WpfHexEditor.Core.LSP.Refactoring;

/// <summary>Introduces a local variable for the selected expression.</summary>
public sealed class IntroduceVariableRefactoring : IRefactoring
{
    /// <summary>Variable name to use. Defaults to "value".</summary>
    public string VariableName { get; set; } = "value";

    public string Name => "Introduce Variable";

    public bool CanApply(RefactoringContext context)
        => context.SelectionLength > 0
           && !string.IsNullOrWhiteSpace(context.SelectedText)
           && !context.SelectedText.Contains('\n');

    public IReadOnlyList<TextEdit> Apply(RefactoringContext context)
    {
        if (!CanApply(context)) return [];

        var indent = DetectIndent(context.DocumentText, context.SelectionStart);
        var lineStart = LineStart(context.DocumentText, context.SelectionStart);

        var declaration = $"{indent}var {VariableName} = {context.SelectedText.Trim()};\n";

        return
        [
            new TextEdit(context.FilePath, lineStart, 0, declaration),
            new TextEdit(context.FilePath, context.SelectionStart + declaration.Length, context.SelectionLength, VariableName),
        ];
    }

    private static int LineStart(string text, int offset)
    {
        int i = offset;
        while (i > 0 && text[i - 1] != '\n') i--;
        return i;
    }

    private static string DetectIndent(string text, int offset)
    {
        var start = LineStart(text, offset);
        int ws = start;
        while (ws < text.Length && (text[ws] == ' ' || text[ws] == '\t')) ws++;
        return text[start..ws];
    }
}
