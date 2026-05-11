// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Refactoring/RefactoringPreview.cs
// Description:
//     UI-agnostic data structure describing what a refactoring would do.
//     Groups TextEdits per file and computes a synthetic "old / new" line
//     pair for each edit so a host can render a diff DataGrid without
//     re-reading the file or running diff algorithms itself.
// ==========================================================

using System.IO;

namespace WpfHexEditor.Core.LSP.Refactoring;

/// <summary>A single visible diff row in a refactoring preview.</summary>
public sealed record RefactoringPreviewRow(
    string FilePath,
    int    LineNumber,        // 1-based line of the edit start
    string OldText,           // the line(s) being replaced (or "" for an insertion)
    string NewText,           // the replacement line(s)
    int    EditIndex);        // index into the original edit list

/// <summary>Builds <see cref="RefactoringPreviewRow"/> entries from a list of <see cref="TextEdit"/>.</summary>
public static class RefactoringPreviewBuilder
{
    /// <summary>
    /// Computes preview rows. Reads each impacted file from disk to extract
    /// the original line. When a file cannot be read (e.g. in-memory edit
    /// before save), OldText is left empty.
    /// </summary>
    public static IReadOnlyList<RefactoringPreviewRow> Build(IReadOnlyList<TextEdit> edits)
    {
        var rows = new List<RefactoringPreviewRow>(edits.Count);

        // Cache file contents to avoid re-reading on every edit.
        var fileCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < edits.Count; i++)
        {
            var e = edits[i];

            if (!fileCache.TryGetValue(e.FilePath, out var content))
            {
                try { content = File.Exists(e.FilePath) ? File.ReadAllText(e.FilePath) : null; }
                catch { content = null; }
                fileCache[e.FilePath] = content;
            }

            int line = 1;
            string oldText = "";
            if (content is not null && e.StartOffset <= content.Length)
            {
                line = 1 + CountLines(content, e.StartOffset);
                oldText = content.Substring(e.StartOffset, Math.Min(e.Length, content.Length - e.StartOffset));
            }

            rows.Add(new RefactoringPreviewRow(e.FilePath, line, oldText, e.NewText, i));
        }

        return rows;
    }

    private static int CountLines(string text, int upTo)
    {
        int n = 0;
        for (int i = 0; i < upTo && i < text.Length; i++)
            if (text[i] == '\n') n++;
        return n;
    }
}
