//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using WpfHexEditor.Editor.TextEditor.Highlighting;
using WpfHexEditor.Editor.TextEditor.Services;

namespace WpfHexEditor.Editor.TextEditor.ViewModels;

/// <summary>
/// Internal view-model backing <see cref="Controls.TextEditor"/>.
/// </summary>
internal sealed class TextEditorViewModel : INotifyPropertyChanged
{
    // -----------------------------------------------------------------------
    // Document state
    // -----------------------------------------------------------------------

    private readonly List<string> _lines = [""];
    private bool _isDirty;
    private bool _isReadOnly;
    private string _filePath = string.Empty;
    private Encoding _encoding = Encoding.UTF8;
    private SyntaxDefinition? _syntaxDefinition;
    private RegexSyntaxHighlighter? _highlighter;

    // Incremental max-width tracking (P1-TE-01) — O(1) on growth, O(n) only on shrink
    private int _cachedMaxLineLength;

    // Background highlight pipeline (P1-TE-06)
    private CancellationTokenSource? _highlightCts;
    private readonly SynchronizationContext? _syncContext = SynchronizationContext.Current;

    // Undo/redo
    private readonly UndoRedoStack _undoRedo = new();

    // -----------------------------------------------------------------------
    // Caret / selection
    // -----------------------------------------------------------------------

    private int _caretLine;
    private int _caretColumn;
    private int _selAnchorLine = -1;
    private int _selAnchorColumn = -1;

    // -----------------------------------------------------------------------
    // Properties
    // -----------------------------------------------------------------------

    public IReadOnlyList<string> Lines => _lines;

    public int LineCount => _lines.Count;

    public bool IsDirty
    {
        get => _isDirty;
        set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); } }
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set { if (_isReadOnly != value) { _isReadOnly = value; OnPropertyChanged(); } }
    }

    public string FilePath
    {
        get => _filePath;
        set { if (_filePath != value) { _filePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); } }
    }

    public string Title
    {
        get
        {
            var name = string.IsNullOrEmpty(_filePath) ? "untitled" : Path.GetFileName(_filePath);
            return IsDirty ? $"{name} *" : name;
        }
    }

    public Encoding Encoding
    {
        get => _encoding;
        set { if (!Equals(_encoding, value)) { _encoding = value; OnPropertyChanged(); } }
    }

    public SyntaxDefinition? SyntaxDefinition
    {
        get => _syntaxDefinition;
        set
        {
            if (_syntaxDefinition != value)
            {
                _syntaxDefinition = value;
                _highlighter = value is not null ? new RegexSyntaxHighlighter(value) : null;
                InvalidateHighlightCache();
                OnPropertyChanged();
            }
        }
    }

    public RegexSyntaxHighlighter? Highlighter => _highlighter;

    public int CaretLine    { get => _caretLine;    set { _caretLine   = Math.Clamp(value, 0, Math.Max(0, _lines.Count - 1)); OnPropertyChanged(); OnPropertyChanged(nameof(CaretStatus)); } }
    public int CaretColumn  { get => _caretColumn;  set { _caretColumn = Math.Max(0, value); OnPropertyChanged(); OnPropertyChanged(nameof(CaretStatus)); } }

    public bool HasSelection => _selAnchorLine >= 0;
    public int SelectionAnchorLine   { get => _selAnchorLine;   set { _selAnchorLine   = value; OnPropertyChanged(nameof(HasSelection)); } }
    public int SelectionAnchorColumn { get => _selAnchorColumn; set { _selAnchorColumn = value; } }

    public string CaretStatus => $"Ln {_caretLine + 1}, Col {_caretColumn + 1}";

    public bool CanUndo => _undoRedo.CanUndo;
    public bool CanRedo => _undoRedo.CanRedo;

    /// <summary>Maximum line length in characters (O(1) lookup, updated incrementally).</summary>
    public int MaxLineLength => _cachedMaxLineLength;

    /// <summary>
    /// Fired on the UI thread when background highlights become available for a line range.
    /// Viewport should clear its FormattedText cache for (firstLine, lastLine) and re-render.
    /// </summary>
    public event Action<int, int>? HighlightsComputed;

    // -----------------------------------------------------------------------
    // Highlight cache
    // -----------------------------------------------------------------------

    // Per-line highlight cache: null means "needs recompute".
    // List<T> uses doubling strategy → O(n) total copy work vs O(n²) for chunk-64 Array.Resize.
    // List element assignment is atomic for reference types → safe for concurrent background writes.
    private readonly List<IReadOnlyList<ColoredSpan>?> _highlightCache = new(256);

    /// <summary>
    /// Returns cached highlight spans for <paramref name="lineIndex"/>.
    /// Returns empty immediately if not yet computed — caller should invoke
    /// <see cref="ScheduleHighlightAsync"/> to populate in the background.
    /// </summary>
    public IReadOnlyList<ColoredSpan> GetHighlightedSpans(int lineIndex)
    {
        if (_highlighter is null || lineIndex < 0 || lineIndex >= _lines.Count)
            return [];

        GrowHighlightCacheIfNeeded(lineIndex);

        // Return cached result (may be null → render plain, background will fill later).
        return _highlightCache[lineIndex] ?? [];
    }

    private void GrowHighlightCacheIfNeeded(int requiredIndex)
    {
        // Extend the list with nulls until it covers requiredIndex.
        while (_highlightCache.Count <= requiredIndex)
            _highlightCache.Add(null);
    }

    public void InvalidateHighlightCache(int fromLine = 0)
    {
        int limit = Math.Min(fromLine < 0 ? 0 : fromLine, _highlightCache.Count);
        for (int i = limit; i < _highlightCache.Count; i++)
            _highlightCache[i] = null;
    }

    public void InvalidateHighlightLine(int lineIndex)
    {
        if (lineIndex >= 0 && lineIndex < _highlightCache.Count)
            _highlightCache[lineIndex] = null;
    }

    /// <summary>
    /// Schedules syntax highlighting for the visible range (and a ±20-line buffer) on a
    /// background thread. Visible lines are highlighted first. Cancels any in-flight task.
    /// When complete, raises <see cref="HighlightsComputed"/> on the UI thread.
    /// </summary>
    public void ScheduleHighlightAsync(int firstVisible, int lastVisible)
    {
        if (_highlighter is null || _lines.Count == 0) return;

        _highlightCts?.Cancel();
        _highlightCts = new CancellationTokenSource();
        var token = _highlightCts.Token;

        // Extend buffer — pre-warm nearby lines for smooth scrolling
        int bufStart = Math.Max(0, firstVisible - 20);
        int bufEnd   = Math.Min(_lines.Count - 1, lastVisible + 20);
        if (bufEnd < bufStart) return;

        // Grow cache on UI thread before handing anything to background thread
        GrowHighlightCacheIfNeeded(bufEnd);

        // Capture snapshot of line texts + which lines actually need highlighting
        // (safe: we're on the UI thread here)
        int count     = bufEnd - bufStart + 1;
        var indices   = new int[count];
        var texts     = new string[count];
        var needed    = new bool[count];
        for (int i = 0; i < count; i++)
        {
            int li     = bufStart + i;
            indices[i] = li;
            needed[i]  = _highlightCache[li] is null;
            texts[i]   = needed[i] ? _lines[li] : string.Empty;
        }

        var cache      = _highlightCache;
        var highlighter = _highlighter;
        var syncCtx    = _syncContext;

        Task.Run(() =>
        {
            // Pass 1 — visible range first (lowest latency)
            for (int i = 0; i < count && !token.IsCancellationRequested; i++)
            {
                int li = indices[i];
                if (!needed[i] || li < firstVisible || li > lastVisible) continue;
                var result = highlighter.Highlight(texts[i]);
                if (li < cache.Count && !token.IsCancellationRequested)
                    cache[li] = result;
            }

            // Pass 2 — buffer lines (smoother pre-fetch for upcoming scroll)
            for (int i = 0; i < count && !token.IsCancellationRequested; i++)
            {
                int li = indices[i];
                if (!needed[i] || (li >= firstVisible && li <= lastVisible)) continue;
                var result = highlighter.Highlight(texts[i]);
                if (li < cache.Count && !token.IsCancellationRequested)
                    cache[li] = result;
            }

            if (!token.IsCancellationRequested && syncCtx is not null)
            {
                int completedFirst = firstVisible;
                int completedLast  = Math.Min(lastVisible, bufEnd);
                syncCtx.Post(_ => HighlightsComputed?.Invoke(completedFirst, completedLast), null);
            }
        }, token);
    }

    // -----------------------------------------------------------------------
    // Document operations
    // -----------------------------------------------------------------------

    /// <summary>
    /// Replaces the entire document content.
    /// </summary>
    public void SetText(string text, bool clearUndoHistory = true)
    {
        _lines.Clear();
        _lines.AddRange(SplitLines(text));
        if (_lines.Count == 0) _lines.Add(string.Empty);
        if (clearUndoHistory) _undoRedo.Clear();
        IsDirty = false;
        _caretLine = 0;
        _caretColumn = 0;
        _selAnchorLine = -1;
        // Single O(n) scan at load time — acceptable cost
        RebuildMaxLineLength();
        InvalidateHighlightCache();
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(LineCount));
    }

    /// <summary>
    /// Returns the full text of the document (lines joined with LF).
    /// Uses a pre-allocated StringBuilder to avoid repeated buffer doublings.
    /// </summary>
    public string GetText()
    {
        if (_lines.Count == 0) return string.Empty;
        int capacity = _lines.Count - 1; // newlines
        foreach (var l in _lines) capacity += l.Length;
        var sb = new StringBuilder(capacity);
        for (int i = 0; i < _lines.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(_lines[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns the text of a single line (bounds-checked).
    /// </summary>
    public string GetLine(int index)
        => (index >= 0 && index < _lines.Count) ? _lines[index] : string.Empty;

    /// <summary>
    /// Inserts a character at the current caret position, with undo support.
    /// </summary>
    public void InsertChar(char c)
    {
        if (_isReadOnly) return;
        var line = _lines[_caretLine];
        var col  = Math.Min(_caretColumn, line.Length);

        if (c == '\n' || c == '\r')
        {
            var before      = line[..col];
            var after       = line[col..];
            var edit        = new TextEdit(TextEditType.NewLine, _caretLine, col, "\n", before, after);
            int splitLine   = _caretLine; // capture before increment
            _lines[_caretLine] = before;
            _lines.Insert(_caretLine + 1, after);
            _undoRedo.Push(edit);
            _caretLine++;
            _caretColumn = 0;
            // Newline splits line — both halves are shorter; max may have decreased
            OnLineLengthMayHaveShrunk();
            InvalidateHighlightCache(splitLine);
        }
        else
        {
            var newLine = line.Insert(col, c.ToString());
            var edit    = new TextEdit(TextEditType.Insert, _caretLine, col, c.ToString(), line, newLine);
            _lines[_caretLine] = newLine;
            _undoRedo.Push(edit);
            _caretColumn = col + 1;
            OnLineLengthGrew(_caretLine);
            InvalidateHighlightLine(_caretLine);
        }
        IsDirty = true;
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(LineCount));
    }

    /// <summary>
    /// Deletes the character before the caret (Backspace).
    /// </summary>
    public void Backspace()
    {
        if (_isReadOnly) return;
        if (_caretColumn > 0)
        {
            var line    = _lines[_caretLine];
            var col     = _caretColumn - 1;
            var newLine = line.Remove(col, 1);
            var edit    = new TextEdit(TextEditType.Delete, _caretLine, col, line[col].ToString(), line, newLine);
            _lines[_caretLine] = newLine;
            _undoRedo.Push(edit);
            _caretColumn = col;
            OnLineLengthMayHaveShrunk();
        }
        else if (_caretLine > 0)
        {
            // Merge with previous line — merged line can be longer than either half
            var prevLine  = _lines[_caretLine - 1];
            var curLine   = _lines[_caretLine];
            var merged    = prevLine + curLine;
            var edit      = new TextEdit(TextEditType.DeleteLine, _caretLine, 0, "\n", curLine, string.Empty);
            int mergeLine = _caretLine - 1; // line that absorbs content
            _lines[_caretLine - 1] = merged;
            _lines.RemoveAt(_caretLine);
            _undoRedo.Push(edit);
            _caretLine--;
            _caretColumn = prevLine.Length;
            OnLineLengthGrew(mergeLine); // merged line may be longer than previous max
            InvalidateHighlightCache(mergeLine);
            IsDirty = true;
            OnPropertyChanged(nameof(Lines));
            OnPropertyChanged(nameof(LineCount));
            return;
        }
        InvalidateHighlightLine(_caretLine);
        IsDirty = true;
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(LineCount));
    }

    /// <summary>
    /// Deletes the character at the caret (Delete key).
    /// </summary>
    public void DeleteForward()
    {
        if (_isReadOnly) return;
        var line = _lines[_caretLine];
        if (_caretColumn < line.Length)
        {
            var col     = _caretColumn;
            var newLine = line.Remove(col, 1);
            var edit    = new TextEdit(TextEditType.Delete, _caretLine, col, line[col].ToString(), line, newLine);
            _lines[_caretLine] = newLine;
            _undoRedo.Push(edit);
            OnLineLengthMayHaveShrunk();
        }
        else if (_caretLine < _lines.Count - 1)
        {
            var nextLine = _lines[_caretLine + 1];
            var merged   = line + nextLine;
            var edit     = new TextEdit(TextEditType.DeleteLine, _caretLine + 1, 0, "\n", nextLine, string.Empty);
            _lines[_caretLine] = merged;
            _lines.RemoveAt(_caretLine + 1);
            _undoRedo.Push(edit);
            OnLineLengthGrew(_caretLine); // merged line may be longer than previous max
            InvalidateHighlightCache(_caretLine);
            IsDirty = true;
            OnPropertyChanged(nameof(Lines));
            OnPropertyChanged(nameof(LineCount));
            return;
        }
        InvalidateHighlightLine(_caretLine);
        IsDirty = true;
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(LineCount));
    }

    // -----------------------------------------------------------------------
    // Undo / Redo
    // -----------------------------------------------------------------------

    public void Undo()
    {
        if (!_undoRedo.CanUndo) return;
        var edit = _undoRedo.Undo();
        ApplyEditInverse(edit);
        if (edit.Type is TextEditType.NewLine or TextEditType.DeleteLine or TextEditType.Replace)
            InvalidateHighlightCache(edit.Line);
        else
            InvalidateHighlightLine(_caretLine);
        OnLineLengthMayHaveShrunk(); // conservative: undo can shrink lines
        IsDirty = _undoRedo.CanUndo;
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(LineCount));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public void Redo()
    {
        if (!_undoRedo.CanRedo) return;
        var edit = _undoRedo.Redo();
        ApplyEdit(edit);
        if (edit.Type is TextEditType.NewLine or TextEditType.DeleteLine or TextEditType.Replace)
            InvalidateHighlightCache(edit.Line);
        else
            InvalidateHighlightLine(_caretLine);
        OnLineLengthMayHaveShrunk(); // conservative: redo can both grow and shrink
        IsDirty = true;
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(LineCount));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void ApplyEdit(TextEdit edit)
    {
        _caretLine   = edit.Line;
        _caretColumn = edit.Column;

        switch (edit.Type)
        {
            case TextEditType.Insert:
                _lines[edit.Line] = edit.NewText;
                _caretColumn = edit.Column + edit.Text.Length;
                break;
            case TextEditType.Delete:
                _lines[edit.Line] = edit.NewText;
                break;
            case TextEditType.NewLine:
                // OldText = "before" (prefix up to col), NewText = "after" (suffix from col).
                // Restore both halves explicitly — OldText[col..] would always be "" and is wrong.
                _lines[edit.Line] = edit.OldText ?? string.Empty;
                _lines.Insert(edit.Line + 1, edit.NewText ?? string.Empty);
                _caretLine++;
                _caretColumn = 0;
                break;
            case TextEditType.DeleteLine:
                var prevLine = _lines[edit.Line - 1];
                _lines[edit.Line - 1] = prevLine + _lines[edit.Line];
                _lines.RemoveAt(edit.Line);
                _caretLine   = edit.Line - 1;
                _caretColumn = prevLine.Length;
                break;

            case TextEditType.Replace:
            {
                // Redo a multi-line selection delete.
                // edit.Text    = selected text (\n-separated) — tells us how many lines to remove.
                // At redo time the lines have been restored by the undo, so we re-merge them.
                var parts   = edit.Text.Split('\n');
                int endLine = edit.Line + parts.Length - 1;
                var lastLine = _lines[endLine];
                int endCol   = Math.Min(parts[^1].Length, lastLine.Length);
                var merged   = _lines[edit.Line][..Math.Min(edit.Column, _lines[edit.Line].Length)]
                             + lastLine[endCol..];
                _lines[edit.Line] = merged;
                if (endLine > edit.Line)
                    _lines.RemoveRange(edit.Line + 1, endLine - edit.Line);
                _caretLine   = edit.Line;
                _caretColumn = edit.Column;
                break;
            }
        }
    }

    private void ApplyEditInverse(TextEdit edit)
    {
        _caretLine   = edit.Line;
        _caretColumn = edit.Column;

        switch (edit.Type)
        {
            case TextEditType.Insert:
                _lines[edit.Line] = edit.OldText ?? string.Empty;
                break;
            case TextEditType.Delete:
                _lines[edit.Line] = edit.OldText ?? string.Empty;
                _caretColumn = edit.Column + edit.Text.Length;
                break;
            case TextEditType.NewLine:
                var before = _lines[edit.Line];
                var after  = _lines.Count > edit.Line + 1 ? _lines[edit.Line + 1] : string.Empty;
                _lines[edit.Line] = before + after;
                if (_lines.Count > edit.Line + 1)
                    _lines.RemoveAt(edit.Line + 1);
                _caretColumn = before.Length;
                break;
            case TextEditType.DeleteLine:
                _lines[edit.Line - 1] = edit.OldText ?? string.Empty;
                _lines.Insert(edit.Line, edit.NewText ?? string.Empty);
                _caretLine   = edit.Line;
                _caretColumn = 0;
                break;

            case TextEditType.Replace:
            {
                // Undo a multi-line selection delete.
                // edit.Text    = original selected text (\n-separated)
                // edit.OldText = merged line that replaced the selection
                //
                // At undo time _lines[edit.Line] == edit.OldText (all subsequent
                // char-inserts have already been undone).  Re-expand it.
                var mergedLine = edit.OldText ?? string.Empty;
                var prefix     = mergedLine[..Math.Min(edit.Column, mergedLine.Length)];
                var suffix     = mergedLine[Math.Min(edit.Column, mergedLine.Length)..];
                var parts      = edit.Text.Split('\n');

                _lines[edit.Line] = prefix + parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var lineContent = i == parts.Length - 1
                        ? parts[i] + suffix
                        : parts[i];
                    _lines.Insert(edit.Line + i, lineContent);
                }
                _caretLine   = edit.Line + parts.Length - 1;
                _caretColumn = parts[^1].Length;
                break;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Clipboard helpers
    // -----------------------------------------------------------------------

    public string GetSelectedText()
    {
        if (!HasSelection) return string.Empty;

        NormalizeSelection(out int startLine, out int startCol, out int endLine, out int endCol);

        if (startLine == endLine)
        {
            var line = _lines[startLine];
            startCol = Math.Min(startCol, line.Length);
            endCol   = Math.Min(endCol,   line.Length);
            return line[startCol..endCol];
        }

        var sb = new System.Text.StringBuilder();
        var firstLine = _lines[startLine];
        sb.Append(firstLine[Math.Min(startCol, firstLine.Length)..]);
        for (int i = startLine + 1; i < endLine; i++)
        {
            sb.Append('\n');
            sb.Append(_lines[i]);
        }
        sb.Append('\n');
        var lastLine = _lines[endLine];
        sb.Append(lastLine[..Math.Min(endCol, lastLine.Length)]);
        return sb.ToString();
    }

    /// <summary>
    /// Deletes the currently selected text and positions the caret at the selection start.
    /// No-op if there is no selection.
    /// </summary>
    public void DeleteSelectedText()
    {
        if (!HasSelection || _isReadOnly) return;

        NormalizeSelection(out int startLine, out int startCol, out int endLine, out int endCol);

        if (startLine == endLine)
        {
            var line    = _lines[startLine];
            startCol    = Math.Min(startCol, line.Length);
            endCol      = Math.Min(endCol,   line.Length);
            var deleted = line[startCol..endCol];
            var newLine = line[..startCol] + line[endCol..];
            _undoRedo.Push(new TextEdit(TextEditType.Delete, startLine, startCol, deleted, line, newLine));
            _lines[startLine] = newLine;
        }
        else
        {
            // Fuse first line prefix + last line suffix; remove intermediate lines.
            var first  = _lines[startLine];
            var last   = _lines[endLine];
            startCol   = Math.Min(startCol, first.Length);
            endCol     = Math.Min(endCol,   last.Length);
            var merged = first[..startCol] + last[endCol..];

            // Push a replace edit storing the merged line in OldText so that
            // ApplyEditInverse can reconstruct all original lines without reading
            // stale _lines state.
            _undoRedo.Push(new TextEdit(TextEditType.Replace, startLine, startCol,
                GetSelectedText(), merged, null));

            _lines[startLine] = merged;
            _lines.RemoveRange(startLine + 1, endLine - startLine);
        }

        _caretLine   = startLine;
        _caretColumn = startCol;
        ClearSelection();
        InvalidateHighlightCache(startLine);
        OnLineLengthMayHaveShrunk(); // deletion always removes content
        IsDirty = true;
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(LineCount));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    /// <summary>
    /// Inserts <paramref name="text"/> at the current caret, replacing any active selection.
    /// </summary>
    public void InsertText(string text)
    {
        if (_isReadOnly) return;
        if (HasSelection) DeleteSelectedText();
        foreach (var c in text) InsertChar(c);
    }

    public void ClearSelection()
    {
        _selAnchorLine   = -1;
        _selAnchorColumn = -1;
        OnPropertyChanged(nameof(HasSelection));
    }

    // Returns (startLine, startCol, endLine, endCol) in document order (start <= end).
    private void NormalizeSelection(out int startLine, out int startCol, out int endLine, out int endCol)
    {
        bool anchorFirst = _selAnchorLine < _caretLine
                        || (_selAnchorLine == _caretLine && _selAnchorColumn <= _caretColumn);
        if (anchorFirst)
        {
            startLine = _selAnchorLine; startCol = _selAnchorColumn;
            endLine   = _caretLine;     endCol   = _caretColumn;
        }
        else
        {
            startLine = _caretLine;     startCol = _caretColumn;
            endLine   = _selAnchorLine; endCol   = _selAnchorColumn;
        }
    }

    // -----------------------------------------------------------------------
    // Load / Save
    // -----------------------------------------------------------------------

    public async Task LoadFileAsync(string filePath, Encoding? encoding = null, CancellationToken ct = default)
    {
        encoding ??= Encoding.UTF8;
        var text = await File.ReadAllTextAsync(filePath, encoding, ct);

        // SplitLines is pure computation (no WPF access) — safe on background thread.
        // This prevents the UI thread from being blocked on large file parsing.
        var splitLines = await Task.Run(() => SplitLines(text).ToList(), ct);

        // UI-thread section: minimal work — only a List<string> swap
        FilePath = filePath;
        Encoding = encoding;
        _lines.Clear();
        _lines.AddRange(splitLines);
        if (_lines.Count == 0) _lines.Add(string.Empty);
        _undoRedo.Clear();
        IsDirty = false;
        _caretLine = 0;
        _caretColumn = 0;
        _selAnchorLine = -1;
        RebuildMaxLineLength(); // single O(n) scan at load time
        InvalidateHighlightCache();
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(LineCount));

        // Auto-detect syntax.
        var ext = Path.GetExtension(filePath);
        SyntaxDefinition = SyntaxDefinitionCatalog.Instance.FindByExtension(ext);
    }

    public async Task SaveFileAsync(string filePath, CancellationToken ct = default)
    {
        await File.WriteAllTextAsync(filePath, GetText(), _encoding, ct);
        FilePath = filePath;
        IsDirty = false;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    // -- Max-width incremental tracking (P1-TE-01) -------------------------

    /// <summary>O(1) — called when a line grows (insert, merge).</summary>
    private void OnLineLengthGrew(int lineIndex)
    {
        if (lineIndex >= 0 && lineIndex < _lines.Count)
        {
            int len = _lines[lineIndex].Length;
            if (len > _cachedMaxLineLength) _cachedMaxLineLength = len;
        }
    }

    /// <summary>O(n) — called only when a line may have shrunk (delete, split, paste-delete).</summary>
    private void OnLineLengthMayHaveShrunk()
    {
        _cachedMaxLineLength = _lines.Count > 0 ? _lines.Max(l => l.Length) : 0;
    }

    /// <summary>Full O(n) rebuild — used at initial load only.</summary>
    private void RebuildMaxLineLength()
    {
        _cachedMaxLineLength = _lines.Count > 0 ? _lines.Max(l => l.Length) : 0;
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text)) { yield return string.Empty; yield break; }
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                yield return text[start..i];
                start = (i + 1 < text.Length && text[i + 1] == '\n') ? i + 2 : i + 1;
                i = start - 1;
            }
            else if (text[i] == '\n')
            {
                yield return text[start..i];
                start = i + 1;
            }
        }
        yield return text[start..];
    }

    // -----------------------------------------------------------------------
    // INotifyPropertyChanged
    // -----------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// -----------------------------------------------------------------------
// Undo/Redo stack (self-contained, no external dependency)
// -----------------------------------------------------------------------

internal enum TextEditType { Insert, Delete, NewLine, DeleteLine, Replace }

internal sealed class TextEdit
{
    public TextEditType Type    { get; }
    public int          Line    { get; }
    public int          Column  { get; }
    public string       Text    { get; }
    public string?      OldText { get; }
    public string?      NewText { get; }

    public TextEdit(TextEditType type, int line, int column, string text, string? oldText = null, string? newText = null)
    {
        Type    = type;
        Line    = line;
        Column  = column;
        Text    = text;
        OldText = oldText;
        NewText = newText;
    }
}

internal sealed class UndoRedoStack
{
    private const int MaxStack = 1000;
    private readonly Stack<TextEdit> _undo = new();
    private readonly Stack<TextEdit> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Push(TextEdit edit)
    {
        _redo.Clear();
        if (_undo.Count >= MaxStack) { /* drop oldest — stacks can't remove bottom */ }
        _undo.Push(edit);
    }

    public TextEdit Undo() { var e = _undo.Pop(); _redo.Push(e); return e; }
    public TextEdit Redo() { var e = _redo.Pop(); _undo.Push(e); return e; }

    public void Clear() { _undo.Clear(); _redo.Clear(); }
}
