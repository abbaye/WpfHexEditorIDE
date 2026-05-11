// ==========================================================
// Project: WpfHexEditor.App
// File: Refactoring/RefactoringOrchestrator.cs
// Description:
//     Bridges the CodeEditor Refactor menu to the Core.LSP refactoring
//     engine. Builds the RefactoringContext from the active editor, picks
//     the right IRefactoring by kind, shows the preview dialog, and
//     applies the edits if the user confirms.
// ==========================================================

using System.Diagnostics;
using System.IO;
using System.Windows;
using WpfHexEditor.Core.LSP.Refactoring;
using WpfHexEditor.Editor.CodeEditor.Controls;

namespace WpfHexEditor.App.Refactoring;

/// <summary>Builds and applies refactorings invoked from the CodeEditor.</summary>
public sealed class RefactoringOrchestrator
{
    private readonly RefactoringEngine _engine = new(
    [
        new RenameRefactoring(),
        new ExtractMethodRefactoring(),
        new ExtractClassRefactoring(),
        new IntroduceVariableRefactoring(),
        new InlineMethodRefactoring(),
    ]);

    /// <summary>Subscribes the orchestrator to the CodeEditor's Refactor menu events.</summary>
    public void Attach(WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor editor)
    {
        editor.RefactoringMenuRequested += (s, e) => Handle(editor, e);
    }

    private void Handle(WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor editor, RefactoringMenuRequestedEventArgs e)
    {
        var context = new RefactoringContext
        {
            DocumentText    = e.DocumentText,
            FilePath        = e.FilePath,
            CaretOffset     = e.CaretOffset,
            SelectionStart  = e.SelectionStart > 0 ? e.SelectionStart : e.CaretOffset,
            SelectionLength = e.SelectionLength,
        };

        IRefactoring? refactoring = e.Kind switch
        {
            RefactoringKind.Rename            => new RenameRefactoring { NewName = ExtractWordAtCaret(context) + "_renamed" },
            RefactoringKind.ExtractMethod     => new ExtractMethodRefactoring(),
            RefactoringKind.ExtractClass      => new ExtractClassRefactoring(),
            RefactoringKind.IntroduceVariable => new IntroduceVariableRefactoring(),
            RefactoringKind.InlineMethod      => new InlineMethodRefactoring(),
            _                                 => null,
        };
        if (refactoring is null || !refactoring.CanApply(context)) return;

        var edits = refactoring.Apply(context);
        if (edits.Count == 0) return;

        var rows = RefactoringPreviewBuilder.Build(edits);
        var dlg  = new RefactoringPreviewDialog(rows)
        {
            Owner = Window.GetWindow(editor),
        };
        if (dlg.ShowDialog() != true) return;

        ApplyEdits(edits, e);
    }

    private static string ExtractWordAtCaret(RefactoringContext context)
    {
        var t = context.DocumentText;
        int s = context.CaretOffset;
        if (s >= t.Length) s = t.Length - 1;
        if (s < 0) return "";
        while (s > 0 && (char.IsLetterOrDigit(t[s - 1]) || t[s - 1] == '_')) s--;
        int e = s;
        while (e < t.Length && (char.IsLetterOrDigit(t[e]) || t[e] == '_')) e++;
        return e > s ? t[s..e] : "";
    }

    private static void ApplyEdits(IReadOnlyList<TextEdit> edits, RefactoringMenuRequestedEventArgs activeArgs)
    {
        foreach (var group in edits.GroupBy(e => e.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderByDescending(e => e.StartOffset).ToList();
            var isActiveFile = string.Equals(group.Key, activeArgs.FilePath, StringComparison.OrdinalIgnoreCase);
            var seed = isActiveFile ? activeArgs.DocumentText : SafeRead(group.Key);
            if (seed is null) continue;

            var sb = ApplyOrdered(seed, ordered);
            try { File.WriteAllText(group.Key, sb); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Refactor] write failed: {group.Key} — {ex.Message}"); }
        }
    }

    private static string? SafeRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Refactor] read failed: {path} — {ex.Message}"); return null; }
    }

    private static string ApplyOrdered(string seed, IEnumerable<TextEdit> orderedDescending)
    {
        var sb = new System.Text.StringBuilder(seed);
        foreach (var ed in orderedDescending)
        {
            int start = Math.Clamp(ed.StartOffset, 0, sb.Length);
            int len   = Math.Clamp(ed.Length, 0, sb.Length - start);
            sb.Remove(start, len);
            sb.Insert(start, ed.NewText);
        }
        return sb.ToString();
    }
}
