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
            }
        }

        public void Redo()
        {
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
                _lspChangeTimer?.Stop();
                _lspChangeTimer?.Start();
            }

            // Incremental max-width update (P1-CE-02) — O(1) on growth, O(n) only on shrink
            int changedLine    = e.Position.Line;
            int prevMaxLength  = _cachedMaxLineLength;
            if (changedLine >= 0 && changedLine < _document.Lines.Count)
            {
                int newLen = _document.Lines[changedLine].Text.Length;
                if (newLen > _cachedMaxLineLength)
                    _cachedMaxLineLength = newLen;
                else if (newLen < _cachedMaxLineLength)
                    _cachedMaxLineLength = _document.Lines.Count > 0
                        ? _document.Lines.Max(l => l.Text.Length) : 0;
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
                InvalidateMeasure(); // scrollbar ranges may have changed
            else
                InvalidateVisual();  // layout unaffected — redraw only
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
        private void TriggerSmartCompleteWithDelay()
        {
            if (!_enableSmartComplete || _smartCompletePopup == null)
                return;

            _smartCompletePopup.TriggerWithDelay(SmartCompleteDelay);
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
            // Trigger on: quote (start of key/value), colon (after key), comma (new item), opening brace/bracket
            return ch == '"' || ch == ':' || ch == ',' || ch == '{' || ch == '[';
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
                _validationErrors = _validator.Validate(textToValidate);
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
        /// Check if can undo
        /// </summary>
        public bool CanUndo => _undoEngine.CanUndo;

        /// <summary>
        /// Check if can redo
        /// </summary>
        public bool CanRedo => _undoEngine.CanRedo;

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
                await SaveAsAsync(_currentFilePath, ct);
        }

        public async System.Threading.Tasks.Task SaveAsAsync(string filePath, System.Threading.CancellationToken ct = default)
        {
            // Snapshot text on the UI thread before switching threads.
            var text = GetText();
            try
            {
                await Task.Run(() => File.WriteAllText(filePath, text, System.Text.Encoding.UTF8), ct);
            }
            catch (Exception ex)
            {
                StatusMessage?.Invoke(this, $"Save failed: {ex.Message}");
                return;
            }

            _currentFilePath = filePath;
            if (_smartCompletePopup is not null) _smartCompletePopup.CurrentFilePath = filePath;
            _undoEngine.MarkSaved();  // Stamp save-point so Undo can detect "back to clean".
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
            if (_smartCompletePopup is not null) _smartCompletePopup.CurrentFilePath = filePath;
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
            _undoEngine.Reset();    // Loaded content is the new baseline.
            _undoEngine.MarkSaved();
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
            _currentFilePath = filePath;
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

    }
}
