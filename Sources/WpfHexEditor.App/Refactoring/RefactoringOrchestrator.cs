// ==========================================================
// Project: WpfHexEditor.App
// File: Refactoring/RefactoringOrchestrator.cs
// Description:
//     Bridges the CodeEditor Refactor menu to the Core.LSP refactoring
//     engine: builds a RefactoringContext from the active editor, picks
//     the right IRefactoring by kind, shows the preview dialog, and
//     applies the edits if the user confirms.
// ==========================================================

using System.IO;
using System.Text;
using System.Windows;
using WpfHexEditor.Core.LSP.Refactoring;
using WpfHexEditor.Editor.CodeEditor.Controls;

namespace WpfHexEditor.App.Refactoring;

/// <summary>Builds and applies refactorings invoked from the CodeEditor.</summary>
public sealed class RefactoringOrchestrator
{
    private static readonly Action<string> Log = m => System.Diagnostics.Debug.WriteLine($"[Refactor] {m}");

    public void Attach(WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor editor)
        => editor.RefactoringMenuRequested += (_, e) => Handle(editor, e);

    private static void Handle(WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor editor, RefactoringMenuRequestedEventArgs e)
    {
        var context = BuildContext(e);

        var refactoring = Create(e.Kind, context);
        if (refactoring is null || !refactoring.CanApply(context)) return;

        var edits = refactoring.Apply(context);
        if (edits.Count == 0) return;

        var rows = RefactoringPreviewBuilder.Build(edits);
        var dlg  = new RefactoringPreviewDialog(rows) { Owner = Window.GetWindow(editor) };
        if (dlg.ShowDialog() != true) return;

        ApplyEdits(edits, e);
    }

    private static RefactoringContext BuildContext(RefactoringMenuRequestedEventArgs e) => new()
    {
        DocumentText    = e.DocumentText,
        FilePath        = e.FilePath,
        CaretOffset     = e.CaretOffset,
        SelectionStart  = e.SelectionStart > 0 ? e.SelectionStart : e.CaretOffset,
        SelectionLength = e.SelectionLength,
    };

    private static IRefactoring? Create(RefactoringKind kind, RefactoringContext context) => kind switch
    {
        RefactoringKind.Rename            => new RenameRefactoring { NewName = DefaultRenameTarget(context) },
        RefactoringKind.ExtractMethod     => new ExtractMethodRefactoring(),
        RefactoringKind.ExtractClass      => new ExtractClassRefactoring(),
        RefactoringKind.IntroduceVariable => new IntroduceVariableRefactoring(),
        RefactoringKind.InlineMethod      => new InlineMethodRefactoring(),
        _                                 => null,
    };

    private static string DefaultRenameTarget(RefactoringContext context)
    {
        var word = ExtractWordAtCaret(context);
        return string.IsNullOrEmpty(word) ? "NewName" : word + "_renamed";
    }

    private static string ExtractWordAtCaret(RefactoringContext context)
    {
        var t = context.DocumentText;
        if (string.IsNullOrEmpty(t)) return "";
        int s = Math.Clamp(context.CaretOffset, 0, t.Length - 1);
        while (s > 0 && IsIdent(t[s - 1])) s--;
        int e = s;
        while (e < t.Length && IsIdent(t[e])) e++;
        return e > s ? t[s..e] : "";
    }

    private static bool IsIdent(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static void ApplyEdits(IReadOnlyList<TextEdit> edits, RefactoringMenuRequestedEventArgs activeArgs)
    {
        foreach (var group in edits.GroupBy(e => e.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            var ordered     = group.OrderByDescending(e => e.StartOffset);
            var isActive    = string.Equals(group.Key, activeArgs.FilePath, StringComparison.OrdinalIgnoreCase);
            var seed        = isActive ? activeArgs.DocumentText : SafeRead(group.Key);
            if (seed is null) continue;

            SafeWrite(group.Key, ApplyOrdered(seed, ordered));
        }
    }

    private static string? SafeRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch (Exception ex) { Log($"read failed: {path} — {ex.Message}"); return null; }
    }

    private static void SafeWrite(string path, string content)
    {
        try { File.WriteAllText(path, content); }
        catch (Exception ex) { Log($"write failed: {path} — {ex.Message}"); }
    }

    private static string ApplyOrdered(string seed, IEnumerable<TextEdit> orderedDescending)
    {
        var sb = new StringBuilder(seed);
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
