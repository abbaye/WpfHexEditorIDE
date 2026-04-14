//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Custom CodeEditor - Main Editor Control (Phase 1 - Foundation)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
// Inspired by HexViewport.cs custom rendering pattern
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Folding;
using WpfHexEditor.Editor.CodeEditor.Models;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Rendering;
using WpfHexEditor.Editor.CodeEditor.Services;
using WpfHexEditor.Editor.CodeEditor.Snippets;
using WpfHexEditor.Editor.CodeEditor.NavigationBar;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Settings;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Editor.CodeEditor.Options;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.CodeEditor.Selection;
using WpfHexEditor.Editor.CodeEditor.Input;
using WpfHexEditor.Editor.CodeEditor.MultiCaret;

namespace WpfHexEditor.Editor.CodeEditor.Controls
{
    public partial class CodeEditor
    {

        #region Undo/Redo Operations

        public void Undo()
        {
            // In shared mode, route through the shared engine which calls Revert() on the entry.
            if (_sharedUndoEngine is not null)
            {
                SharedUndo();
                return;
            }

            if (!_undoEngine.CanUndo) return;

            try
            {
                _isInternalEdit = true;
                var entry = _undoEngine.TryUndo();
                if (entry != null)
                {
                    ApplyInverseEntry(entry);
                    InvalidateVisual();
                }
            }
            finally
            {
                _isInternalEdit = false;
                // Rebuild change markers from scratch after undo (document state may differ from incremental tracking).
                _changeTracker.RebuildFromLines(_document.Lines.Select(l => l.Text).ToList());
            }
        }

        public void Redo()
        {
            // In shared mode, route through the shared engine which calls Apply() on the entry.
            if (_sharedUndoEngine is not null)
            {
                SharedRedo();
                return;
            }

            if (!_undoEngine.CanRedo) return;

            try
            {
                _isInternalEdit = true;
                var entry = _undoEngine.TryRedo();
                if (entry != null)
                {
                    ApplyForwardEntry(entry);
                    InvalidateVisual();
                }
            }
            finally
            {
                _isInternalEdit = false;
                // Rebuild change markers from scratch after redo.
                _changeTracker.RebuildFromLines(_document.Lines.Select(l => l.Text).ToList());
            }
        }

        // Apply the entry in the forward (redo) direction.
        private void ApplyForwardEntry(WpfHexEditor.Editor.Core.Undo.IUndoEntry entry)
        {
            if (entry is WpfHexEditor.Editor.Core.Undo.CompositeUndoEntry composite)
            {
                foreach (var child in composite.Children)
                    ApplyForwardEntry(child);
                return;
            }

            if (entry is not Models.CodeEditorUndoEntry e) return;

            switch (e.Kind)
            {
                case Models.CodeEditKind.Insert:
                {
                    _document.InsertText(e.Position, e.Text);
                    // Compute cursor end — accounts for embedded newlines in multi-line pastes.
                    var fwdLines = e.Text.Split('\n');
                    if (fwdLines.Length == 1)
                    {
                        _cursorLine   = e.Position.Line;
                        _cursorColumn = e.Position.Column + e.Text.Length;
                    }
                    else
                    {
                        _cursorLine   = e.Position.Line + fwdLines.Length - 1;
                        _cursorColumn = fwdLines[^1].Length;
                    }
                    break;
                }

                case Models.CodeEditKind.Delete:
                {
                    // Compute the actual end of the deleted range — accounts for multi-line text.
                    var fwdDelLines = e.Text.Split('\n');
                    var delEnd = fwdDelLines.Length == 1
                        ? new TextPosition(e.Position.Line, e.Position.Column + e.Text.Length)
                        : new TextPosition(e.Position.Line + fwdDelLines.Length - 1, fwdDelLines[^1].Length);
                    _document.DeleteRange(e.Position, delEnd);
                    _cursorLine   = e.Position.Line;
                    _cursorColumn = e.Position.Column;
                    break;
                }

                case Models.CodeEditKind.NewLine:
                    // Redo: re-split the line at the original position.
                    _document.InsertNewLine(e.Position.Line, e.Position.Column);
                    _cursorLine   = e.Position.Line + 1;
                    _cursorColumn = 0;
                    break;

                case Models.CodeEditKind.DeleteLine:
                    // Redo: re-merge. At redo time Lines[line-1]=part1, Lines[line]=e.Text.
                    int prevLen = _document.Lines[e.Position.Line - 1].Text.Length;
                    _document.Lines[e.Position.Line - 1].Text += e.Text;  // direct merge (no event)
                    _document.DeleteLine(e.Position.Line);                 // _isInternalEdit=true → not re-recorded
                    _cursorLine   = e.Position.Line - 1;
                    _cursorColumn = prevLen;
                    break;
            }
        }

        // Apply the inverse (undo) direction.
        private void ApplyInverseEntry(WpfHexEditor.Editor.Core.Undo.IUndoEntry entry)
        {
            if (entry is WpfHexEditor.Editor.Core.Undo.CompositeUndoEntry composite)
            {
                // Apply children in reverse order for undo.
                for (int i = composite.Children.Count - 1; i >= 0; i--)
                    ApplyInverseEntry(composite.Children[i]);
                return;
            }

            if (entry is not Models.CodeEditorUndoEntry e) return;

            switch (e.Kind)
            {
                case Models.CodeEditKind.Insert:
                {
                    // Inverse of insert = delete the inserted text.
                    // For multi-line text the end position is on a different line.
                    var invLines = e.Text.Split('\n');
                    var insEnd = invLines.Length == 1
                        ? new TextPosition(e.Position.Line, e.Position.Column + e.Text.Length)
                        : new TextPosition(e.Position.Line + invLines.Length - 1, invLines[^1].Length);
                    _document.DeleteRange(e.Position, insEnd);
                    _cursorLine   = e.Position.Line;
                    _cursorColumn = e.Position.Column;
                    break;
                }

                case Models.CodeEditKind.Delete:
                {
                    // Inverse of delete = re-insert the deleted text.
                    _document.InsertText(e.Position, e.Text);
                    _cursorLine   = e.Position.Line;
                    _cursorColumn = e.Position.Column;
                    break;
                }

                case Models.CodeEditKind.NewLine:
                    // e.Text = right part (content of line+1) stored at recording time.
                    // At undo time (LIFO): Lines[line].Text = left, Lines[line+1].Text = e.Text.
                    var leftText = _document.Lines[e.Position.Line].Text;
                    _document.Lines[e.Position.Line].Text = leftText + e.Text;
                    if (e.Position.Line + 1 < _document.Lines.Count)
                        _document.DeleteLine(e.Position.Line + 1);  // _isInternalEdit=true → not re-recorded
                    _cursorLine   = e.Position.Line;
                    _cursorColumn = e.Position.Column;
                    break;

                case Models.CodeEditKind.DeleteLine:
                    // e.Position=(deletedLine,0), e.Text=content of the deleted line.
                    // At undo time (LIFO): Lines[line-1].Text = part1 + e.Text (merged).
                    var mergedText = _document.Lines[e.Position.Line - 1].Text;
                    int splitAt    = mergedText.Length - e.Text.Length;
                    _document.Lines[e.Position.Line - 1].Text = splitAt > 0 ? mergedText[..splitAt] : string.Empty;
                    _document.InsertNewLine(e.Position.Line - 1,
                        _document.Lines[e.Position.Line - 1].Text.Length);  // splits at end → creates empty line+1
                    _document.Lines[e.Position.Line].Text = e.Text;          // restore deleted line content
                    _cursorLine   = e.Position.Line;
                    _cursorColumn = 0;
                    break;
            }
        }

        // Handles UndoEngine state changes: updates _isDirty and fires events.
        private void OnUndoEngineStateChanged(object? sender, EventArgs e)
        {
            bool dirty = !_undoEngine.IsAtSavePoint;
            if (dirty != _isDirty)
            {
                _isDirty = dirty;
                ModifiedChanged?.Invoke(this, EventArgs.Empty);
                TitleChanged?.Invoke(this, BuildTitle());
            }
            CanUndoChanged?.Invoke(this, EventArgs.Empty);
            CanRedoChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Document Event Handlers

        private void Document_TextChanged(object sender, Models.TextChangedEventArgs e)
        {
            // Dismiss Quick Info when the document changes — content is stale.
            _quickInfoPopup?.Hide();
            _hoverQuickInfoService?.Cancel();
            DismissEndBlockHint();

            // Record in undo engine (unless replaying an undo/redo operation).
            if (!_isInternalEdit)
            {
                var kind = (Models.CodeEditKind)(int)e.ChangeType;

                // For NewLine, store the RIGHT part (content that went to line+1).
                // e.Text = Environment.NewLine is useless for reconstruction; we need
                // the actual text so ApplyInverseEntry can merge the lines back.
                string recordedText = e.ChangeType == Models.TextChangeType.NewLine
                    ? (e.Position.Line + 1 < _document.Lines.Count
                        ? _document.Lines[e.Position.Line + 1].Text
                        : string.Empty)
                    : e.Text;

                var entry = new Models.CodeEditorUndoEntry(kind, e.Position, recordedText);
                _undoEngine.Push(entry);
                // _isDirty + events are handled by OnUndoEngineStateChanged via StateChanged.

                // Dual-push: also promote to the shared engine when co-editing.
                // _suppressLocalUndoRedo guard prevents re-push during replay.
                if (!_suppressLocalUndoRedo)
                    PushToSharedEngine(entry);
            }

            // Phase 5: Trigger validation with debounce
            if (EnableValidation)
            {
                _validationTimer.Stop();
                _validationTimer.Start();
            }

            // Refresh folding regions — debounced 500ms to avoid O(n) scan on every keystroke (P1-CE-01).
            if (IsFoldingEnabled && _foldingEngine != null)
            {
                _foldingDebounceTimer?.Stop();
                _foldingDebounceTimer?.Start();
            }

            // Debounce LSP didChange — 300 ms (Phase 4: LSP Integration).
            if (_lspClient is not null && _currentFilePath is not null)
            {
                // Capture incremental delta for servers that declared TextDocumentSyncKind=2.
                // Multi-change operations (DeleteLine, multi-caret) collapse to full-text fallback.
                if (_lspClient is WpfHexEditor.Editor.Core.LSP.IIncrementalSyncClient inc
                    && inc.SupportsIncrementalSync)
                {
                    if (_pendingLspChange is not null)
                    {
                        // Second change within the debounce window → fall back to full text.
                        _pendingLspChange = null;
                    }
                    else
                    {
                        _pendingLspChange = e.ChangeType switch
                        {
                            Models.TextChangeType.Insert =>
                                (e.Position.Line, e.Position.Column,
                                 e.Position.Line, e.Position.Column, 0, e.Text),
                            Models.TextChangeType.NewLine =>
                                (e.Position.Line, e.Position.Column,
                                 e.Position.Line, e.Position.Column, 0, "\n"),
                            Models.TextChangeType.Delete =>
                                (e.Position.Line, e.Position.Column,
                                 e.Position.Line, e.Position.Column + e.Length, e.Length, string.Empty),
                            Models.TextChangeType.Replace =>
                                (e.Position.Line, e.Position.Column,
                                 e.Position.Line, e.Position.Column + e.Length, e.Length, e.Text),
                            _ => null, // DeleteLine and others → full-text fallback
                        };
                    }
                }

                _lspChangeTimer?.Stop();
                _lspChangeTimer?.Start();
            }

            // Debounce linked editing sync — 150 ms (C2).
            if (!_applyingLinkedEdit && _lspClient is not null && _currentFilePath is not null)
                ScheduleLinkedEditQuery();

            // Incremental max-width update (P1-CE-02).
            // _maxLengthCount tracks how many lines sit at the current max so we rescan
            // only when the last line at that length shrinks — avoids O(n) LINQ on every keystroke.
            int changedLine   = e.Position.Line;
            int prevMaxLength = _cachedMaxLineLength;
            if (changedLine >= 0 && changedLine < _document.Lines.Count)
            {
                int newLen = _document.Lines[changedLine].Text.Length;
                if (newLen > _cachedMaxLineLength)
                {
                    _cachedMaxLineLength = newLen;
                    _maxLengthCount = 1;
                }
                else if (newLen == _cachedMaxLineLength)
                {
                    // Line is still at max — count is already correct (same line, same length).
                }
                else if (newLen == _cachedMaxLineLength - 1)
                {
                    // Line shrank by 1. Count unknown — do a single O(n) for-loop (no LINQ, no lambda).
                    int max = 0, count = 0;
                    var lines = _document.Lines;
                    for (int k = 0; k < lines.Count; k++)
                    {
                        int len = lines[k].Text.Length;
                        if (len > max) { max = len; count = 1; }
                        else if (len == max) count++;
                    }
                    _cachedMaxLineLength = max;
                    _maxLengthCount      = count;
                }
                // else: newLen < max - 1 → max is still held by other lines, no change needed.
            }
            bool maxWidthChanged = _cachedMaxLineLength != prevMaxLength;

            // Invalidate line-number cache for the changed line (P1-CE-03)
            _lineNumberCache.Remove(changedLine);

            // OPT-B: Smart invalidation routing — only trigger a full layout pass when
            // the document *structure* changes (line count or max-line-width).  For a plain
            // char insert/delete on an existing line the scrollbar ranges are unchanged, so
            // InvalidateVisual() is sufficient and avoids the heavier Measure→Arrange chain.
            bool lineCountChanged = e.ChangeType is TextChangeType.NewLine
                                                 or TextChangeType.DeleteLine;
            if (lineCountChanged)
                _linePositionsDirty = true; // OPT-D: new/deleted lines shift subsequent Y positions

            // Propagate change to shared buffer (IBufferAwareEditor).
            if (_buffer is not null && !_suppressBufferSync)
            {
                _suppressBufferSync = true;
                try   { _buffer.SetText(GetText(), source: this); }
                finally { _suppressBufferSync = false; }
            }

            if (lineCountChanged || maxWidthChanged)
            {
                InvalidateMeasure(); // scrollbar ranges may have changed
            }
            else
            {
                // Capture dirty line range before clearing so OnRender can clip to it.
                var dirty = _document.DirtyLines;
                if (dirty.Count > 0)
                {
                    _dirtyLineRange = (Enumerable.Min(dirty), Enumerable.Max(dirty));
                    _document.ClearDirtyLines();
                }
                else
                {
                    _dirtyLineRange = (e.Position.Line, e.Position.Line);
                }
                InvalidateRegion(RenderDirtyFlags.TextLines);
            }

            // Incremental change-marker tracking (gutter change indicators).
            if (!_isInternalEdit)
            {
                switch (e.ChangeType)
                {
                    case TextChangeType.NewLine:
                        _changeTracker.OnLineInserted(e.Position.Line + 1);
                        if (e.Position.Line < _document.Lines.Count)
                            _changeTracker.OnLineModified(e.Position.Line, _document.Lines[e.Position.Line].Text);
                        break;
                    case TextChangeType.DeleteLine:
                        _changeTracker.OnLineDeleted(e.Position.Line);
                        break;
                    default:
                        if (e.Position.Line < _document.Lines.Count)
                            _changeTracker.OnLineModified(e.Position.Line, _document.Lines[e.Position.Line].Text);
                        break;
                }
            }

            MinimapRefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region SmartComplete Methods (Phase 4)

        /// <summary>
        /// Trigger SmartComplete immediately (Ctrl+Space)
        /// </summary>
        private void TriggerSmartComplete()
        {
            if (!_enableSmartComplete || _smartCompletePopup == null)
                return;

            _smartCompletePopup.TriggerImmediate();
        }

        /// <summary>
        /// Trigger SmartComplete with delay (auto-trigger)
        /// </summary>
        private void TriggerSmartCompleteWithDelay(char? triggerChar = null)
        {
            if (!_enableSmartComplete || _smartCompletePopup == null)
                return;

            _smartCompletePopup.TriggerWithDelay(SmartCompleteDelay, triggerChar);
        }

        /// <summary>
        /// Computes the screen coordinates of the current caret position.
        /// When <paramref name="belowCaret"/> is true the Y coordinate is shifted one line down
        /// so popups appear beneath the caret line rather than overlapping it.
        /// </summary>
        private Point ComputeCaretScreenPoint(bool belowCaret = false)
        {
            var textLeft = ShowLineNumbers ? 70.0 : 4.0;
            var localX   = textLeft + _cursorColumn * _charWidth - _horizontalScrollOffset;
            var localY   = (_cursorLine - _firstVisibleLine + (belowCaret ? 1 : 0)) * _lineHeight;
            return PointToScreen(new Point(localX, localY));
        }

        /// <summary>
        /// Returns the caret's bounding rectangle in the editor's local coordinate space.
        /// Used by <see cref="SmartCompletePopup"/> for DPI-safe popup placement via
        /// <see cref="System.Windows.Controls.Primitives.PlacementMode.Bottom"/>.
        /// </summary>
        internal Rect GetCaretDisplayRect()
        {
            var textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            var x        = textLeft + _cursorColumn * _charWidth - _horizontalScrollOffset;
            var y        = (_cursorLine - _firstVisibleLine) * _lineHeight;
            return new Rect(x, y, 1, _lineHeight);
        }

        /// <summary>
        /// Check if character should auto-trigger SmartComplete
        /// </summary>
        private bool ShouldAutoTriggerSmartComplete(char ch)
        {
            // Trigger on: dot (member access), quote (start of key/value), colon (after key), comma (new item), opening brace/bracket
            return ch == '.' || ch == '"' || ch == ':' || ch == ',' || ch == '{' || ch == '[';
        }

        #endregion

        #region Validation Methods (Phase 5)

        /// <summary>
        /// Trigger validation timer
        /// </summary>
        private void ValidationTimer_Tick(object sender, EventArgs e)
        {
            _validationTimer.Stop();
            PerformValidation();
        }

        /// <summary>
        /// Perform validation immediately
        /// </summary>
        private void PerformValidation()
        {
            if (!EnableValidation || _validator == null || _document == null)
                return;

            try
            {
                string textToValidate;
                var dirty = _document.DirtyLines;

                // Incremental path (P1-CE-07): validate only the dirty region + context when
                // fewer than 10% of lines changed.  Full pass otherwise (initial load, paste, etc.).
                if (dirty.Count > 0 && dirty.Count < _document.TotalLines / 10)
                {
                    int minDirty = dirty.Min();
                    int maxDirty = dirty.Max();
                    // Include 5-line context above and below for accurate state-dependent validators
                    int rangeStart = Math.Max(0, minDirty - 5);
                    int rangeEnd   = Math.Min(_document.Lines.Count - 1, maxDirty + 5);
                    textToValidate = string.Join(Environment.NewLine,
                        _document.Lines.Skip(rangeStart).Take(rangeEnd - rangeStart + 1).Select(l => l.Text));
                }
                else
                {
                    textToValidate = _document.SaveToString();
                }

                _document.ClearDirtyLines();

                // FormatSchemaValidator validates JSON-based .whfmt format definitions.
                // Skip it for named languages (C#, Python, etc.) — LSP handles those.
                var langId = Language?.Id;
                if (langId is null || langId == "json" || langId == "whfmt")
                {
                    _validationErrors = _validator.Validate(textToValidate);

                    // Apply language-specific prefix to raw error codes (e.g. "JSON_SYNTAX" → "JSON_SYNTAX").
                    // For named languages the prefix comes from the LanguageDefinition.DiagnosticPrefix.
                    var prefix = Language?.DiagnosticPrefix;
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        foreach (var err in _validationErrors)
                            if (!string.IsNullOrEmpty(err.ErrorCode) && !err.ErrorCode.StartsWith(prefix, StringComparison.Ordinal))
                                err.ErrorCode = $"{prefix}_{err.ErrorCode}";
                    }
                }
                else
                {
                    // Non-JSON language: clear schema errors, keep LSP-layer errors.
                    _validationErrors.RemoveAll(v => v.Layer != WpfHexEditor.Editor.CodeEditor.Models.ValidationLayer.Lsp);
                }

                RebuildValidationIndex();
                InvalidateVisual();
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Silently ignore validation errors
                _document.ClearDirtyLines();
                _validationErrors.Clear();
                _validationByLine.Clear();
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Rebuilds the line→errors dictionary used by RenderValidationGlyph for O(1) lookup.
        /// Must be called whenever _validationErrors is replaced or bulk-modified (OPT-PERF-01).
        /// </summary>
        private void RebuildValidationIndex()
        {
            _validationByLine.Clear();
            foreach (var error in _validationErrors)
            {
                if (!_validationByLine.TryGetValue(error.Line, out var list))
                {
                    list = new List<Models.ValidationError>(2);
                    _validationByLine[error.Line] = list;
                }
                list.Add(error);
            }
        }

        /// <summary>
        /// Trigger validation manually (public API)
        /// </summary>
        public void TriggerValidation()
        {
            if (EnableValidation)
            {
                PerformValidation();
            }
        }

        /// <summary>
        /// Get current validation errors
        /// </summary>
        public List<Models.ValidationError> ValidationErrors => _validationErrors;

        #endregion

        #region Public API

        /// <summary>
        /// Get the document model
        /// </summary>
        public CodeDocument Document => _document;

        /// <summary>
        /// Replaces the document model with an externally supplied instance.
        /// Used by <see cref="CodeEditorSplitHost"/> to share one <see cref="CodeDocument"/>
        /// between the primary and secondary editor panes.
        /// </summary>
        public void SetDocument(CodeDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            // Unsubscribe from the old document.
            _document.TextChanged -= Document_TextChanged;

            _document = document;

            // Subscribe to the new document.
            _document.TextChanged += Document_TextChanged;

            // Re-attach InlineHints service to the new document.
            _inlineHintsService.Attach(_document, _currentFilePath);

            // Reset view state.
            _cursorLine = 0;
            _cursorColumn = 0;
            _selection.Clear();
            _verticalScrollOffset   = 0;
            _currentScrollOffset    = 0;
            _targetScrollOffset     = 0;
            _horizontalScrollOffset = 0;

            // Clear word highlights so stale decorations don't bleed into the new document.
            _wordHighlights.Clear();
            _wordHighlightLines.Clear();
            _wordHighlightLineSet.Clear();
            _wordHighlightWord        = string.Empty;
            _wordHighlightLen         = 0;
            _wordHighlightTrackedLine = -1;
            _wordHighlightTrackedCol  = -1;

            UpdateVirtualization();
            RebuildMaxLineLength(); // O(n) scan at doc swap — acceptable

            if (IsFoldingEnabled && _foldingEngine != null)
                _foldingEngine.Analyze(_document.Lines);

            _lineNumberCache.Clear();
            InvalidateMeasure();
        }

        /// <summary>
        /// Load text content
        /// </summary>
        public void LoadText(string text)
        {
            _document.LoadFromString(text);
            _cursorLine = 0;
            _cursorColumn = 0;
            _selection.Clear();
            _verticalScrollOffset   = 0;
            _currentScrollOffset    = 0;
            _targetScrollOffset     = 0;
            _horizontalScrollOffset = 0;

            // Clear word highlights so stale decorations don't bleed into the reloaded content.
            _wordHighlights.Clear();
            _wordHighlightLines.Clear();
            _wordHighlightLineSet.Clear();
            _wordHighlightWord        = string.Empty;
            _wordHighlightLen         = 0;
            _wordHighlightTrackedLine = -1;
            _wordHighlightTrackedCol  = -1;

            // Clear undo history — loaded content is the new baseline.
            _undoEngine.Reset();
            _undoEngine.MarkSaved();
            _isDirty = false;

            // Sync TotalLines so the virtualization engine reflects the newly loaded document.
            UpdateVirtualization();
            RebuildMaxLineLength();

            if (IsFoldingEnabled && _foldingEngine != null)
                _foldingEngine.Analyze(_document.Lines);

            _lineNumberCache.Clear();
            InvalidateMeasure();
            InvalidateVisual();   // Force re-render when content is loaded after initial layout pass
        }

        /// <summary>
        /// Get current text content
        /// </summary>
        public string GetText()
        {
            return _document.SaveToString();
        }

        /// <summary>
        /// Get current cursor position
        /// </summary>
        public TextPosition CursorPosition => new TextPosition(_cursorLine, _cursorColumn);

        /// <summary>
        /// Get current selection
        /// </summary>
        public TextSelection Selection => _selection;

        /// <summary>
        /// Check if can undo. Delegates to the shared engine when co-editing,
        /// or to the local <c>_undoEngine</c> in standalone mode.
        /// </summary>
        public bool CanUndo => _sharedUndoEngine?.CanUndo ?? _undoEngine.CanUndo;

        /// <summary>
        /// Check if can redo. Delegates to the shared engine when co-editing,
        /// or to the local <c>_undoEngine</c> in standalone mode.
        /// </summary>
        public bool CanRedo => _sharedUndoEngine?.CanRedo ?? _undoEngine.CanRedo;

        /// <summary>Number of available undo steps.</summary>
        public int UndoCount => _undoEngine.UndoCount;

        /// <summary>Number of available redo steps.</summary>
        public int RedoCount => _undoEngine.RedoCount;

        /// <summary>
        /// Get validation error count
        /// </summary>
        public int ValidationErrorCount => _validationErrors?.Count(e => e.Severity == ValidationSeverity.Error) ?? 0;

        /// <summary>
        /// Get validation warning count
        /// </summary>
        public int ValidationWarningCount => _validationErrors?.Count(e => e.Severity == ValidationSeverity.Warning) ?? 0;

        #endregion

        #region IDocumentEditor

        // -- State ----------------------------------------------------------

        /// <summary>
        /// True when the document has unsaved changes.
        /// </summary>
        public bool IsDirty => _isDirty;

        // -- IsReadOnly DP -------------------------------------------------

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(CodeEditor),
                new System.Windows.PropertyMetadata(false, (_, _) => { }));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        // -- Title ---------------------------------------------------------

        public string Title => BuildTitle();

        // -- Commands ------------------------------------------------------

        public System.Windows.Input.ICommand UndoCommand => new JsonRelayCommand(_ => Undo(), _ => CanUndo);
        public System.Windows.Input.ICommand RedoCommand => new JsonRelayCommand(_ => Redo(), _ => CanRedo);
        public System.Windows.Input.ICommand SaveCommand => new JsonRelayCommand(_ => Save());
        public System.Windows.Input.ICommand CopyCommand => new JsonRelayCommand(_ => CopyToClipboard(), _ => !_selection.IsEmpty);
        public System.Windows.Input.ICommand CutCommand => new JsonRelayCommand(_ => CutToClipboard(), _ => !_selection.IsEmpty && !IsReadOnly);
        public System.Windows.Input.ICommand PasteCommand => new JsonRelayCommand(_ => PasteFromClipboard(), _ => !IsReadOnly && Clipboard.ContainsText());
        public System.Windows.Input.ICommand DeleteCommand => new JsonRelayCommand(_ => DeleteSelection(), _ => !_selection.IsEmpty && !IsReadOnly);
        public System.Windows.Input.ICommand SelectAllCommand => new JsonRelayCommand(_ => SelectAll());

        // -- Methods -------------------------------------------------------

        public void Save()
        {
            // Delegate to SaveAsync so file I/O runs off the UI thread.
            // Fire-and-forget: the async path handles status/dirty updates.
            if (!string.IsNullOrEmpty(_currentFilePath))
                _ = SaveAsync();
        }

        public async System.Threading.Tasks.Task SaveAsync(System.Threading.CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                if (_formatOnSave)
                    await FormatDocumentAsync(ct).ConfigureAwait(true);
                await SaveAsAsync(_currentFilePath, ct);
            }
        }

        public async System.Threading.Tasks.Task SaveAsAsync(string filePath, System.Threading.CancellationToken ct = default)
        {
            // Snapshot text on the UI thread before switching threads.
            var text = GetText();

            // Guard: never write 0 bytes to a file that already has content on disk.
            // This prevents catastrophic data loss when the document is empty due to a
            // timing issue (e.g. OpenAsync not yet complete) or a buffer sync race.
            if (text.Length == 0 && File.Exists(filePath) && new FileInfo(filePath).Length > 0)
            {
                StatusMessage?.Invoke(this, $"Save aborted — document is empty but '{Path.GetFileName(filePath)}' has content on disk.");
                return;
            }

            try
            {
                BeforeSaveCallback?.Invoke(filePath);
                await Task.Run(() => File.WriteAllText(filePath, text, System.Text.Encoding.UTF8), ct);
            }
            catch (Exception ex)
            {
                StatusMessage?.Invoke(this, $"Save failed: {ex.Message}");
                return;
            }

            _currentFilePath = filePath;
            _breakpointGutterControl?.SetFilePath(_currentFilePath);
            if (_smartCompletePopup is not null) _smartCompletePopup.CurrentFilePath = filePath;
            _undoEngine.MarkSaved();  // Stamp save-point so Undo can detect "back to clean".
            MarkSharedSaved();        // Also stamp the shared engine when co-editing.
            _changeTracker.MarkSavePoint(_document.Lines.Select(l => l.Text).ToList());
            _lspClient?.SaveDocument(filePath);
            _isDirty = false;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, BuildTitle());
            StatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(filePath)}");
        }

        // -- Open file -----------------------------------------------------

        public void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                StatusMessage?.Invoke(this, $"File not found: {Path.GetFileName(filePath)}");
                return;
            }

            string text;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, System.Text.Encoding.UTF8))
                text = sr.ReadToEnd();
            LoadText(text);
            _currentFilePath = filePath;
            _breakpointGutterControl?.SetFilePath(_currentFilePath);
            if (_smartCompletePopup is not null) _smartCompletePopup.CurrentFilePath = filePath;
            // Seed save-point AFTER load so opened content is the baseline (not the empty doc).
            _changeTracker.MarkSavePoint(_document.Lines.Select(l => l.Text).ToList());
            TitleChanged?.Invoke(this, BuildTitle());
            StatusMessage?.Invoke(this, $"Opened: {Path.GetFileName(filePath)}");
            RefreshJsonStatusBarItems();
        }

        async Task IOpenableDocument.OpenAsync(string filePath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
            {
                StatusMessage?.Invoke(this, $"File not found: {Path.GetFileName(filePath)}");
                return;
            }

            // Read + split on a background thread to keep the UI responsive (P1-TE-05 / OPT-PERF-05).
            // content.Split + new CodeLine[] are pure computation with no WPF dependency.
            string text;
            CodeLine[] lines;
            try
            {
                (text, lines) = await Task.Run(() =>
                {
                    string raw;
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs, System.Text.Encoding.UTF8))
                        raw = sr.ReadToEnd();
                    var parts = raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    var arr   = new CodeLine[parts.Length == 0 ? 1 : parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                        arr[i] = new CodeLine(parts[i], i);
                    if (arr.Length == 0)
                        arr[0] = new CodeLine(string.Empty, 0);
                    return (raw, arr);
                }, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                StatusMessage?.Invoke(this, $"Open failed: {ex.Message}");
                return;
            }

            // UI-thread work: swap the pre-built line array into the document, then run the same
            // post-load steps as LoadText() so VirtualizationEngine.TotalLines is updated.
            _document.LoadLines(lines, text);

            // Sync the shared buffer with the loaded content — LoadLines suppresses
            // TextChanged, so the automatic buffer sync in Document_TextChanged won't fire.
            if (_buffer is not null)
            {
                _suppressBufferSync = true;
                try   { _buffer.SetText(text, source: this); }
                finally { _suppressBufferSync = false; }
            }

            _undoEngine.Reset();    // Loaded content is the new baseline.
            _undoEngine.MarkSaved();
            _changeTracker.MarkSavePoint(_document.Lines.Select(l => l.Text).ToList());
            _isDirty = false;
            _cursorLine             = 0;
            _cursorColumn           = 0;
            _selection.Clear();
            _verticalScrollOffset   = 0;
            _currentScrollOffset    = 0;
            _targetScrollOffset     = 0;
            _horizontalScrollOffset = 0;
            UpdateVirtualization();
            RebuildMaxLineLength();
            if (IsFoldingEnabled && _foldingEngine != null)
                _foldingEngine.Analyze(_document.Lines);
            _lineNumberCache.Clear();
            InvalidateMeasure();
            InvalidateVisual();   // Force re-render when async content load completes after initial layout
            _currentFilePath = filePath;
            _breakpointGutterControl?.SetFilePath(_currentFilePath);
            if (_smartCompletePopup is not null) _smartCompletePopup.CurrentFilePath = filePath;

            // Re-attach InlineHints with the resolved file path so workspace-wide
            // counting uses the correct extension filter.
            _inlineHintsService.Attach(_document, _currentFilePath);

            // Apply .editorconfig settings (P2-03): indent style, size, EOL, etc.
            ApplyEditorConfig(Services.EditorConfigService.Resolve(filePath));

            // Notify the LSP server about the newly opened document (Phase 4).
            if (_lspClient?.IsInitialized == true)
            {
                _lspClient.OpenDocument(filePath, DetectLanguageId(filePath), text);
                _lspDocVersion = 1;
                // Schedule a highlight refresh so semantic tokens appear immediately
                // after the server processes didOpen, without waiting for a scroll/edit.
                SchedulePostOpenHighlightRefresh();
            }

            TitleChanged?.Invoke(this, BuildTitle());
            StatusMessage?.Invoke(this, $"Opened: {Path.GetFileName(filePath)}");
            RefreshJsonStatusBarItems();
        }

        /// <summary>
        /// Applies <see cref="Services.EditorConfigSettings"/> properties to the matching
        /// editor DependencyProperties.  Null settings properties are left unchanged.
        /// </summary>
        private void ApplyEditorConfig(Services.EditorConfigSettings cfg)
        {
            if (cfg.IndentSize is int indent) IndentSize = indent;
        }

        #endregion

        #region Linked Editing (C2)

        // Guard prevents recursive DidChange→ApplyLinkedEdits→DidChange cycle.
        private bool _applyingLinkedEdit;

        /// <summary>
        /// Replaces the text in each of the supplied <paramref name="otherRanges"/> with
        /// <paramref name="newText"/>, grouping all replacements into a single undoable
        /// transaction.  Ranges that are already beyond the document end are skipped.
        /// The <see cref="_applyingLinkedEdit"/> guard prevents recursion when the
        /// replacements themselves fire <c>TextChanged</c>.
        /// </summary>
        internal void ApplyLinkedEdits(
            IReadOnlyList<WpfHexEditor.Editor.Core.LSP.LspLinkedRange> otherRanges,
            string newText)
        {
            if (_applyingLinkedEdit || otherRanges.Count == 0) return;
            _applyingLinkedEdit = true;
            try
            {
                // Bottom-up application avoids line/column drift.
                var sorted = otherRanges
                    .OrderByDescending(r => r.StartLine)
                    .ThenByDescending(r => r.StartColumn)
                    .ToList();

                using var tx = _undoEngine.BeginTransaction("Linked Edit");
                foreach (var r in sorted)
                {
                    if (r.StartLine >= _document.Lines.Count) continue;
                    int safeEndLine = Math.Min(r.EndLine, _document.Lines.Count - 1);
                    int safeEndCol  = r.EndLine < _document.Lines.Count
                        ? Math.Min(r.EndColumn, _document.Lines[r.EndLine].Text.Length)
                        : 0;

                    _document.DeleteRange(
                        new Models.TextPosition(r.StartLine, r.StartColumn),
                        new Models.TextPosition(safeEndLine, safeEndCol));

                    if (newText.Length > 0)
                        _document.InsertText(new Models.TextPosition(r.StartLine, r.StartColumn), newText);
                }
                InvalidateVisual();
            }
            finally
            {
                _applyingLinkedEdit = false;
            }
        }

        /// <summary>
        /// Extracts the current text covered by <paramref name="range"/> from the document.
        /// Only single-line ranges are supported (linked editing rarely spans lines).
        /// Returns an empty string for multi-line or out-of-bounds ranges.
        /// </summary>
        internal string GetRangeText(WpfHexEditor.Editor.Core.LSP.LspLinkedRange range)
        {
            if (range.StartLine >= _document.Lines.Count) return string.Empty;
            if (range.StartLine != range.EndLine)         return string.Empty;  // multi-line: skip

            var text  = _document.Lines[range.StartLine].Text;
            var start = Math.Min(range.StartColumn, text.Length);
            var end   = Math.Min(range.EndColumn,   text.Length);
            return start < end ? text[start..end] : string.Empty;
        }

        #endregion

    }
}
