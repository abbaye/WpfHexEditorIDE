//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Custom CodeEditor - Document Model
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace WpfHexEditor.Editor.CodeEditor.Models
{
    /// <summary>
    /// Represents a JSON text document with line-based storage.
    /// Handles text editing operations, modification tracking, and change notifications.
    /// Inspired by HexEditor's document model pattern.
    /// </summary>
    public class CodeDocument : INotifyPropertyChanged
    {
        private ObservableCollection<CodeLine> _lines;
        private bool _isModified;
        private string _filePath;
        private int _indentSize = 2; // Default 2 spaces for JSON

        // Batch update support (P1-CE-04) — suppresses per-item CollectionChanged during bulk load
        private bool _suppressCollectionNotifications;

        // Dirty-line tracking (P1-CE-07) — enables incremental validation
        private readonly HashSet<int> _dirtyLines = new();

        // TotalCharacters incremental cache — avoids O(n) LINQ Sum on every property notification (OPT-PERF-03).
        private int  _totalChars      = 0;
        private bool _totalCharsDirty = true;

        /// <summary>
        /// Lines that have changed since the last validation pass.
        /// Cleared by <see cref="ClearDirtyLines"/>; populated by all text-change operations.
        /// </summary>
        public IReadOnlySet<int> DirtyLines => _dirtyLines;

        #region Properties

        /// <summary>
        /// Lines of text in the document
        /// </summary>
        public ObservableCollection<CodeLine> Lines
        {
            get => _lines;
            private set
            {
                if (_lines != null)
                    _lines.CollectionChanged -= Lines_CollectionChanged;

                _lines = value;

                if (_lines != null)
                    _lines.CollectionChanged += Lines_CollectionChanged;

                _totalCharsDirty = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalLines));
                OnPropertyChanged(nameof(TotalCharacters));
            }
        }

        /// <summary>
        /// Total number of lines in document
        /// </summary>
        public int TotalLines => _lines?.Count ?? 0;

        /// <summary>
        /// Total number of characters in document.
        /// Computed lazily via a for-loop (no LINQ allocator) and cached until the next mutation (OPT-PERF-03).
        /// </summary>
        public int TotalCharacters
        {
            get
            {
                if (!_totalCharsDirty) return _totalChars;
                int total = 0;
                int nl    = Environment.NewLine.Length;
                if (_lines != null)
                    foreach (var line in _lines) total += line.Length + nl;
                _totalChars      = total;
                _totalCharsDirty = false;
                return _totalChars;
            }
        }

        /// <summary>
        /// Has document been modified since last save?
        /// </summary>
        public bool IsModified
        {
            get => _isModified;
            set
            {
                if (_isModified != value)
                {
                    _isModified = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// File path (null if not saved)
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Indent size in spaces (for auto-indent)
        /// </summary>
        public int IndentSize
        {
            get => _indentSize;
            set
            {
                if (_indentSize != value && value > 0)
                {
                    _indentSize = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when text changes (for undo/redo and external listeners)
        /// </summary>
        public event EventHandler<TextChangedEventArgs> TextChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Create empty document with single empty line
        /// </summary>
        public CodeDocument()
        {
            // CRITICAL: Initialize Lines collection FIRST before calling any methods
            Lines = new ObservableCollection<CodeLine>();
            Lines.Add(new CodeLine(string.Empty, 0));
            IsModified = false;
        }

        /// <summary>
        /// Create document from text content
        /// </summary>
        public CodeDocument(string content) : this()
        {
            if (!string.IsNullOrEmpty(content))
                LoadFromString(content);
        }

        #endregion

        #region Text Operations

        /// <summary>
        /// Insert character at position
        /// </summary>
        public void InsertChar(int line, int column, char ch)
        {
            if (line < 0 || line >= Lines.Count)
                return;

            var jsonLine = Lines[line];
            column = Math.Max(0, Math.Min(column, jsonLine.Length));

            jsonLine.Text = jsonLine.Text.Insert(column, ch.ToString());
            IsModified = true;

            OnTextChanged(new TextChangedEventArgs
            {
                ChangeType = TextChangeType.Insert,
                Position = new TextPosition(line, column),
                Text = ch.ToString(),
                Length = 1
            });
        }

        /// <summary>
        /// Insert text at position (supports multi-line)
        /// </summary>
        public void InsertText(TextPosition position, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Handle multi-line insertion
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            if (lines.Length == 1)
            {
                // Single line insertion
                var jsonLine = Lines[position.Line];

                // Clamp column to valid range
                int col = Math.Max(0, Math.Min(position.Column, jsonLine.Text.Length));
                jsonLine.Text = jsonLine.Text.Insert(col, text);
            }
            else
            {
                // Multi-line insertion
                var currentLine = Lines[position.Line];

                // Clamp column to valid range
                int col = Math.Max(0, Math.Min(position.Column, currentLine.Text.Length));

                var leftPart = currentLine.Text.Substring(0, col);
                var rightPart = currentLine.Text.Substring(col);

                // Update first line
                currentLine.Text = leftPart + lines[0];

                // Insert middle lines
                for (int i = 1; i < lines.Length - 1; i++)
                {
                    Lines.Insert(position.Line + i, new CodeLine(lines[i], position.Line + i));
                }

                // Insert last line
                Lines.Insert(position.Line + lines.Length - 1,
                    new CodeLine(lines[lines.Length - 1] + rightPart, position.Line + lines.Length - 1));

                // Update line numbers
                UpdateLineNumbers(position.Line);
            }

            IsModified = true;
            OnTextChanged(new TextChangedEventArgs
            {
                ChangeType = TextChangeType.Insert,
                Position = position,
                Text = text,
                Length = text.Length
            });
        }

        /// <summary>
        /// Delete character at position
        /// </summary>
        public void DeleteChar(int line, int column)
        {
            if (line < 0 || line >= Lines.Count)
                return;

            var jsonLine = Lines[line];
            if (column < 0 || column >= jsonLine.Length)
                return;

            char deletedChar = jsonLine.Text[column];
            jsonLine.Text = jsonLine.Text.Remove(column, 1);
            IsModified = true;

            OnTextChanged(new TextChangedEventArgs
            {
                ChangeType = TextChangeType.Delete,
                Position = new TextPosition(line, column),
                Text = deletedChar.ToString(),
                Length = 1
            });
        }

        /// <summary>
        /// Insert new line (Enter key handling)
        /// </summary>
        public void InsertNewLine(int line, int column)
        {
            if (line < 0 || line >= Lines.Count)
                return;

            var currentLine = Lines[line];
            column = Math.Max(0, Math.Min(column, currentLine.Length));

            var leftPart = currentLine.Text.Substring(0, column);
            var rightPart = currentLine.Text.Substring(column);

            // Auto-indent: inherit the leading whitespace of the current line so the caret
            // lands at the same column level regardless of brace count or language.
            string indent = GetLeadingWhitespace(currentLine.Text);

            // If the cursor is directly after an opening brace/bracket, add one extra indent level.
            bool insideBraces = leftPart.TrimEnd().EndsWith("{") || leftPart.TrimEnd().EndsWith("[");
            if (insideBraces)
                indent += new string(' ', IndentSize);

            // Update current line
            currentLine.Text = leftPart;

            // Insert new line
            Lines.Insert(line + 1, new CodeLine(indent + rightPart, line + 1));

            // Update line numbers
            UpdateLineNumbers(line + 1);

            IsModified = true;
            OnTextChanged(new TextChangedEventArgs
            {
                ChangeType = TextChangeType.NewLine,
                Position = new TextPosition(line, column),
                Text = Environment.NewLine,
                Length = 0
            });
        }

        /// <summary>
        /// Delete line
        /// </summary>
        public void DeleteLine(int line)
        {
            if (line < 0 || line >= Lines.Count)
                return;

            // Keep at least one line
            if (Lines.Count <= 1)
            {
                Lines[0].Text = string.Empty;
                return;
            }

            string deletedText = Lines[line].Text;
            Lines.RemoveAt(line);

            // Update line numbers
            UpdateLineNumbers(line);

            IsModified = true;
            OnTextChanged(new TextChangedEventArgs
            {
                ChangeType = TextChangeType.DeleteLine,
                Position = new TextPosition(line, 0),
                Text = deletedText,
                Length = deletedText.Length
            });
        }

        /// <summary>
        /// Delete text range
        /// </summary>
        public void DeleteRange(TextPosition start, TextPosition end)
        {
            if (start >= end)
                return;

            // Normalize positions
            if (start > end)
            {
                var temp = start;
                start = end;
                end = temp;
            }

            // Single line deletion
            if (start.Line == end.Line)
            {
                var line = Lines[start.Line];

                //crash Clamp columns to valid range
                int startCol = Math.Max(0, Math.Min(start.Column, line.Text.Length));
                int endCol = Math.Max(startCol, Math.Min(end.Column, line.Text.Length));
                int length = endCol - startCol;

                // Skip if nothing to delete
                if (length <= 0)
                    return;

                string deleted = line.Text.Substring(startCol, length);
                line.Text = line.Text.Remove(startCol, length);

                OnTextChanged(new TextChangedEventArgs
                {
                    ChangeType = TextChangeType.Delete,
                    Position = start,
                    Text = deleted,
                    Length = deleted.Length
                });
            }
            else
            {
                // Multi-line deletion
                var firstLine = Lines[start.Line];
                var lastLine = Lines[end.Line];

                // Clamp columns to valid ranges
                int startCol = Math.Max(0, Math.Min(start.Column, firstLine.Text.Length));
                int endCol = Math.Max(0, Math.Min(end.Column, lastLine.Text.Length));

                string leftPart = firstLine.Text.Substring(0, startCol);
                string rightPart = lastLine.Text.Substring(endCol);

                // Update first line
                firstLine.Text = leftPart + rightPart;

                // Remove middle and last lines
                for (int i = end.Line; i > start.Line; i--)
                {
                    Lines.RemoveAt(i);
                }

                UpdateLineNumbers(start.Line);
            }

            IsModified = true;
        }

        /// <summary>
        /// Get text in range
        /// </summary>
        public string GetText(TextPosition start, TextPosition end)
        {
            if (start >= end)
                return string.Empty;

            // Single line
            if (start.Line == end.Line)
            {
                return Lines[start.Line].Text.Substring(start.Column, end.Column - start.Column);
            }

            // Multi-line
            var sb = new StringBuilder();

            // First line
            sb.AppendLine(Lines[start.Line].Text.Substring(start.Column));

            // Middle lines
            for (int i = start.Line + 1; i < end.Line; i++)
            {
                sb.AppendLine(Lines[i].Text);
            }

            // Last line
            sb.Append(Lines[end.Line].Text.Substring(0, end.Column));

            return sb.ToString();
        }

        /// <summary>
        /// Replace text in range
        /// </summary>
        public void ReplaceText(TextPosition start, TextPosition end, string text)
        {
            DeleteRange(start, end);
            InsertText(start, text);
        }

        #endregion

        #region Load/Save

        /// <summary>
        /// Load document from string
        /// </summary>
        public void LoadFromString(string content)
        {
            if (content == null) content = string.Empty;
            var parts = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var arr   = new CodeLine[parts.Length == 0 ? 1 : parts.Length];
            for (int i = 0; i < parts.Length; i++) arr[i] = new CodeLine(parts[i], i);
            if (arr.Length == 0) arr[0] = new CodeLine(string.Empty, 0);
            LoadLines(arr, content);
        }

        /// <summary>
        /// Swaps a pre-built <see cref="CodeLine"/> array (produced on a background thread) into
        /// the document.  Only the ObservableCollection mutation runs on the UI thread (OPT-PERF-05).
        /// </summary>
        public void LoadLines(CodeLine[] preBuilt, string originalText)
        {
            // Suppress per-item CollectionChanged events during bulk load (P1-CE-04)
            _suppressCollectionNotifications = true;
            try
            {
                Lines.Clear();
                foreach (var line in preBuilt)
                    Lines.Add(line);
            }
            finally
            {
                _suppressCollectionNotifications = false;
            }

            _totalCharsDirty = true;
            OnPropertyChanged(nameof(TotalLines));
            OnPropertyChanged(nameof(TotalCharacters));

            _dirtyLines.Clear(); // fresh load — no dirty lines
            IsModified = false;
            InvalidateAllCache();
        }

        /// <summary>
        /// Save document to string
        /// </summary>
        public string SaveToString()
        {
            return string.Join(Environment.NewLine, Lines.Select(l => l.Text));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Calculate indentation level from line content
        /// Counts opening braces/brackets vs closing ones
        /// </summary>
        /// <summary>
        /// Returns the leading whitespace (spaces and tabs) of the given line text.
        /// Used by <see cref="InsertNewLine"/> to preserve the current line's indent on Enter.
        /// </summary>
        private static string GetLeadingWhitespace(string text)
        {
            int i = 0;
            while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
                i++;
            return text[..i];
        }

        /// <summary>
        /// Update line numbers after insertion/deletion
        /// </summary>
        private void UpdateLineNumbers(int startLine)
        {
            for (int i = startLine; i < Lines.Count; i++)
            {
                Lines[i].LineNumber = i;
            }
        }

        /// <summary>
        /// Invalidate all line caches (force re-highlighting)
        /// </summary>
        public void InvalidateAllCache()
        {
            foreach (var line in Lines)
            {
                line.InvalidateCache();
            }
        }

        /// <summary>
        /// Invalidate specific line caches
        /// </summary>
        public void InvalidateLineCache(params int[] lineNumbers)
        {
            foreach (int lineNum in lineNumbers)
            {
                if (lineNum >= 0 && lineNum < Lines.Count)
                {
                    Lines[lineNum].InvalidateCache();
                }
            }
        }

        #endregion

        #region Collection Changed Handler

        private void Lines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Suppressed during bulk load (P1-CE-04) — single notification fired at end of batch
            if (_suppressCollectionNotifications) return;
            _totalCharsDirty = true;
            OnPropertyChanged(nameof(TotalLines));
            OnPropertyChanged(nameof(TotalCharacters));
        }

        /// <summary>Clears the dirty-line set after validation has consumed it (P1-CE-07).</summary>
        public void ClearDirtyLines() => _dirtyLines.Clear();

        #endregion

        #region Event Raising

        protected virtual void OnTextChanged(TextChangedEventArgs e)
        {
            // Track changed line for incremental validation (P1-CE-07)
            _dirtyLines.Add(e.Position.Line);
            _totalCharsDirty = true; // character count changed — invalidate cache (OPT-PERF-03)
            TextChanged?.Invoke(this, e);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region LineCache Management (Phase 11.4)

        /// <summary>
        /// Clean up token cache to respect MaxCachedLines limit using LRU eviction.
        /// Called periodically during rendering or after document changes.
        /// </summary>
        public void CleanupTokenCache(int maxCachedLines)
        {
            if (maxCachedLines <= 0)
                return;

            // Count lines with valid cache
            var cachedLines = Lines.Where(line => line.TokensCache != null && !line.IsCacheDirty).ToList();

            if (cachedLines.Count <= maxCachedLines)
                return; // Within limit

            // Sort by LastAccessTime (oldest first) and clear oldest caches
            var linesToEvict = cachedLines
                .OrderBy(line => line.LastAccessTime)
                .Take(cachedLines.Count - maxCachedLines);

            foreach (var line in linesToEvict)
            {
                line.TokensCache       = null;
                line.IsCacheDirty      = true;
                line.GlyphRunCache     = null;   // P1-CE-05: evict GlyphRun cache together
                line.IsGlyphCacheDirty = true;
                line.CachedUrlZones    = null;
            }
        }

        /// <summary>
        /// Get cache statistics for debugging
        /// </summary>
        public (int cached, int dirty, int total) GetCacheStatistics()
        {
            int cached = Lines.Count(line => line.TokensCache != null && !line.IsCacheDirty);
            int dirty = Lines.Count(line => line.IsCacheDirty);
            int total = Lines.Count;

            return (cached, dirty, total);
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// Event arguments for text change events
    /// </summary>
    public class TextChangedEventArgs : EventArgs
    {
        public TextChangeType ChangeType { get; set; }
        public TextPosition Position { get; set; }
        public string Text { get; set; }
        public int Length { get; set; }
    }

    /// <summary>
    /// Type of text change
    /// </summary>
    public enum TextChangeType
    {
        Insert,
        Delete,
        Replace,
        NewLine,
        DeleteLine
    }

    #endregion
}
