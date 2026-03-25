// ==========================================================
// Project: WpfHexEditor.Terminal
// File: RichTextBoxFindService.cs
// Description:
//     Encapsulates forward/backward text search within a WPF FlowDocument
//     rendered by a RichTextBox. Owns cursor state (_lastFindPointer) and
//     manages selection highlighting on match.
// Architecture:
//     Terminal UI layer. Extracted from TerminalPanel code-behind.
//     Depends on System.Windows.Documents only (WPF).
// ==========================================================

using System.Windows.Controls;
using System.Windows.Documents;

namespace WpfHexEditor.Terminal;

/// <summary>
/// Provides incremental forward/backward text search across a <see cref="FlowDocument"/>.
/// </summary>
internal sealed class RichTextBoxFindService
{
    private readonly FlowDocument _document;
    private readonly RichTextBox  _richTextBox;
    private TextPointer?          _lastFindPointer;

    public RichTextBoxFindService(FlowDocument document, RichTextBox richTextBox)
    {
        _document    = document    ?? throw new ArgumentNullException(nameof(document));
        _richTextBox = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));
    }

    /// <summary>Resets the search position (e.g. after the document is cleared).</summary>
    public void Reset() => _lastFindPointer = null;

    /// <summary>
    /// Finds and highlights the next occurrence of <paramref name="term"/> (wraps around).
    /// Returns <see langword="true"/> when a match is found.
    /// </summary>
    public bool FindNext(string term)
    {
        var start = _lastFindPointer ?? _document.ContentStart;
        var found = FindTextForward(start, term)
                 ?? FindTextForward(_document.ContentStart, term); // wrap

        if (found is null) return false;
        HighlightFound(found, term.Length);
        return true;
    }

    /// <summary>
    /// Finds and highlights the previous occurrence of <paramref name="term"/> (wraps around).
    /// Returns <see langword="true"/> when a match is found.
    /// </summary>
    public bool FindPrev(string term)
    {
        var end   = _lastFindPointer ?? _document.ContentEnd;
        var found = FindTextBackward(end, term)
                 ?? FindTextBackward(_document.ContentEnd, term); // wrap

        if (found is null) return false;
        HighlightFound(found, term.Length);
        return true;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private TextPointer? FindTextForward(TextPointer start, string term)
    {
        var pointer = start;
        while (pointer is not null)
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var text = pointer.GetTextInRun(LogicalDirection.Forward);
                var idx  = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    _lastFindPointer = pointer.GetPositionAtOffset(idx + term.Length);
                    return pointer.GetPositionAtOffset(idx);
                }
            }
            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }
        return null;
    }

    private TextPointer? FindTextBackward(TextPointer end, string term)
    {
        var runs = new List<(TextPointer start, string text)>();
        var ptr  = _document.ContentStart;

        while (ptr is not null && ptr.CompareTo(end) < 0)
        {
            if (ptr.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var text = ptr.GetTextInRun(LogicalDirection.Forward);
                if (!string.IsNullOrEmpty(text))
                    runs.Add((ptr, text));
            }
            ptr = ptr.GetNextContextPosition(LogicalDirection.Forward);
        }

        for (int r = runs.Count - 1; r >= 0; r--)
        {
            var (runStart, runText) = runs[r];
            var idx = runText.LastIndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var found = runStart.GetPositionAtOffset(idx);
                _lastFindPointer = found;
                return found;
            }
        }

        return null;
    }

    private void HighlightFound(TextPointer start, int length)
    {
        var end = start.GetPositionAtOffset(length);
        if (end is null) return;
        _richTextBox.Selection.Select(start, end);
        start.Paragraph?.BringIntoView();
    }
}
