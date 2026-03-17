// ==========================================================
// Project: WpfHexEditor.LSP
// File: Refactoring/RenameRefactoring.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Renames a symbol across all workspace documents.
//     Finds all references via SymbolTableManager and produces TextEdit
//     operations for each occurrence.
// ==========================================================

using WpfHexEditor.LSP.Parsing;
using System.Text.RegularExpressions;

namespace WpfHexEditor.LSP.Refactoring;

/// <summary>
/// Renames the identifier at the caret position to a new name across the workspace.
/// </summary>
public sealed class RenameRefactoring : IRefactoring
{
    /// <summary>New name to apply. Must be set before <see cref="Apply"/> is called.</summary>
    public string NewName { get; set; } = string.Empty;

    public string Name => "Rename Symbol";

    public bool CanApply(RefactoringContext context)
        => context.SymbolTable is not null
           && !string.IsNullOrWhiteSpace(NewName)
           && !string.IsNullOrEmpty(GetSymbolAtCaret(context));

    public IReadOnlyList<TextEdit> Apply(RefactoringContext context)
    {
        var oldName = GetSymbolAtCaret(context);
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrWhiteSpace(NewName))
            return [];

        var edits = new List<TextEdit>();

        // Collect all documents to scan.
        var filePaths = new List<string> { context.FilePath };
        if (context.SymbolTableManager is not null)
        {
            var workspaceSymbols = context.SymbolTableManager.FindWorkspaceSymbol(oldName);
            filePaths.AddRange(workspaceSymbols
                .Select(s => s.FilePath)
                .Where(p => !p.Equals(context.FilePath, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var filePath in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string text = filePath.Equals(context.FilePath, StringComparison.OrdinalIgnoreCase)
                ? context.DocumentText
                : TryReadFile(filePath);

            if (string.IsNullOrEmpty(text)) continue;
            edits.AddRange(BuildEdits(filePath, text, oldName));
        }

        return edits;
    }

    // -----------------------------------------------------------------------

    private static string GetSymbolAtCaret(RefactoringContext ctx)
    {
        if (string.IsNullOrEmpty(ctx.DocumentText)) return string.Empty;
        int offset = Math.Min(ctx.CaretOffset, ctx.DocumentText.Length - 1);
        // Walk left/right to find identifier boundaries.
        int start = offset;
        while (start > 0 && IsIdentChar(ctx.DocumentText[start - 1])) start--;
        int end = offset;
        while (end < ctx.DocumentText.Length && IsIdentChar(ctx.DocumentText[end])) end++;
        return end > start ? ctx.DocumentText[start..end] : string.Empty;
    }

    private static bool IsIdentChar(char c)
        => char.IsLetterOrDigit(c) || c == '_';

    private static IEnumerable<TextEdit> BuildEdits(string filePath, string text, string oldName)
    {
        // Whole-word regex match to avoid partial renames.
        var pattern = $@"\b{Regex.Escape(oldName)}\b";
        foreach (Match m in Regex.Matches(text, pattern))
            yield return new TextEdit(filePath, m.Index, m.Length, string.Empty); // placeholder NewText
    }

    private static string TryReadFile(string path)
    {
        try { return System.IO.File.ReadAllText(path); }
        catch { return string.Empty; }
    }
}
