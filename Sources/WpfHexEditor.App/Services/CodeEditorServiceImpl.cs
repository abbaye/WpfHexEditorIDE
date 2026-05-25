
//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Adapts the active CodeEditor control to the ICodeEditorService SDK contract.
/// MainWindow calls SetActiveEditor when the focused document changes.
/// A WeakReference is used so the service does not prevent editor GC on tab close.
/// </summary>
public sealed class CodeEditorServiceImpl : ICodeEditorService
{
    private WeakReference<CodeEditor>? _activeEditor;

    // ── ICodeEditorService ────────────────────────────────────────────────────

    public bool IsActive => TryGet(out _);

    public string? CurrentLanguage => TryGet(out var ed) ? ed.CurrentLanguage : null;

    public string? CurrentFilePath => TryGet(out var ed) ? ed.CurrentFilePath : null;

    public int CaretLine   => TryGet(out var ed) ? ed.CaretLine   : 0;
    public int CaretColumn => TryGet(out var ed) ? ed.CaretColumn : 0;

    public event EventHandler? DocumentChanged;

    public string? GetContent()      => TryGet(out var ed) ? ed.GetContent()      : null;
    public string  GetSelectedText() => TryGet(out var ed) ? ed.GetSelectedText() : string.Empty;

    public void NavigateToLine(int line, int column = 1)
    {
        if (TryGet(out var ed)) ed.NavigateToLine(line, column);
    }

    public void AddLineHighlight(int line, SolidColorBrush color, string description, string tag, double opacity = 0.35)
    {
        if (TryGet(out var ed)) ed.AddLineHighlight(line, color, description, tag, opacity);
    }

    public void ClearLineHighlightsByTag(string tag)
    {
        if (TryGet(out var ed)) ed.ClearLineHighlightsByTag(tag);
    }

    // ── Internal wiring (called by MainWindow on ActiveDocumentChanged) ───────

    /// <summary>
    /// Point this service at the given <paramref name="editor"/>, or pass <c>null</c>
    /// to indicate no active CodeEditor (e.g. a HexEditor tab is focused).
    /// </summary>
    public void SetActiveEditor(CodeEditor? editor)
    {
        _activeEditor = editor is not null ? new WeakReference<CodeEditor>(editor) : null;
        FireDocumentChanged();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryGet(out CodeEditor editor)
    {
        if (_activeEditor is not null && _activeEditor.TryGetTarget(out var target))
        {
            editor = target;
            return true;
        }
        editor = null!;
        return false;
    }

    private void FireDocumentChanged() =>
        DocumentChanged?.Invoke(this, EventArgs.Empty);
}
