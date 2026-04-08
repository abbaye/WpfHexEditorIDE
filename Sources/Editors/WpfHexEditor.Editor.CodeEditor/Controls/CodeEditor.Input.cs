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
        #region Keyboard Input Handling (Phase 1)

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Pan mode: Escape exits without consuming other keys
            if (_panMode.HandleKeyDown(e)) return;

            // Track Ctrl key to enable symbol underline + Ctrl+Click navigation.
            if ((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) && !_ctrlDown)
            {
                _ctrlDown = true;
                InvalidateRegion(RenderDirtyFlags.Overlays); // symbol underline overlay only
            }

            // Reset caret blink on keypress
            ResetCaretBlink();

            // Block editing input when read-only (navigation keys still allowed below)
            if (IsReadOnly)
            {
                bool isNavigationOrCopy = e.Key is Key.Left or Key.Right or Key.Up or Key.Down
                    or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Escape
                    || (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                    || (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) != 0);
                if (!isNavigationOrCopy) { e.Handled = true; return; }
            }

            bool ctrlPressed  = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift)   != 0;
            bool altPressed   = (Keyboard.Modifiers & ModifierKeys.Alt)     != 0;

            // Inline peek keyboard interception — must be first so Escape/F12/arrows are captured.
            if (_inlinePeekHost != null && _peekHostLine >= 0)
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    CloseInlinePeek();
                    return;
                }
                if (e.Key == Key.F12 && !altPressed)
                {
                    e.Handled = true;
                    CloseInlinePeek();
                    _ = GoToDefinitionAtCaretAsync();
                    return;
                }
                if (e.Key == Key.Up && !ctrlPressed)
                {
                    e.Handled = true;
                    _inlinePeekHost.ScrollUp();
                    return;
                }
                if (e.Key == Key.Down && !ctrlPressed)
                {
                    e.Handled = true;
                    _inlinePeekHost.ScrollDown();
                    return;
                }
            }

            // Alt+Z — toggle word wrap
            if (e.Key == Key.Z && altPressed && !ctrlPressed && !shiftPressed)
            {
                IsWordWrapEnabled = !IsWordWrapEnabled;
                e.Handled = true;
                return;
            }

            // Ctrl+. — LSP Code Actions (quick fix / refactor)
            if (e.Key == Key.OemPeriod && ctrlPressed && !shiftPressed && !altPressed)
            {
                e.Handled = true;
                _ = ShowCodeActionsAsync();
                return;
            }

            // F2 — LSP Rename Symbol
            if (e.Key == Key.F2 && !ctrlPressed && !shiftPressed && !altPressed)
            {
                e.Handled = true;
                _ = StartRenameAsync();
                return;
            }

            // F12 — Go to Definition
            if (e.Key == Key.F12 && !ctrlPressed && !shiftPressed && !altPressed)
            {
                e.Handled = true;
                _ = GoToDefinitionAtCaretAsync();
                return;
            }

            // Alt+F12 — Peek Definition (inline popup)
            if (e.Key == Key.F12 && altPressed && !ctrlPressed && !shiftPressed)
            {
                e.Handled = true;
                _ = ShowPeekDefinitionAsync();
                return;
            }

            // Ctrl+F12 — Go to Implementation
            if (e.Key == Key.F12 && ctrlPressed && !shiftPressed && !altPressed)
            {
                e.Handled = true;
                _ = GoToImplementationAtCaretAsync();
                return;
            }

            // Shift+F12 — Find All References
            if (e.Key == Key.F12 && shiftPressed && !ctrlPressed && !altPressed)
            {
                e.Handled = true;
                _ = FindAllReferencesAsync();
                return;
            }

            // Shift+Alt+H — Show Call Hierarchy
            if (e.Key == Key.H && shiftPressed && altPressed && !ctrlPressed)
            {
                e.Handled = true;
                _ = PrepareCallHierarchyAtCaretAsync();
                return;
            }

            // Ctrl+Alt+F12 — Show Type Hierarchy
            if (e.Key == Key.F12 && ctrlPressed && altPressed && !shiftPressed)
            {
                e.Handled = true;
                _ = PrepareTypeHierarchyAtCaretAsync();
                return;
            }

            // Alt+Left — Navigate Back
            if (e.Key == Key.Left && altPressed && !ctrlPressed && !shiftPressed)
            {
                e.Handled = true;
                NavigateBack();
                return;
            }

            // Alt+Right — Navigate Forward
            if (e.Key == Key.Right && altPressed && !ctrlPressed && !shiftPressed)
            {
                e.Handled = true;
                NavigateForward();
                return;
            }

            // Cancel any pending chords when Ctrl is released.
            if (!ctrlPressed) { _outlineChordPending = false; _formatChordPending = false; }

            switch (e.Key)
            {
                case Key.Left:
                    if (ctrlPressed) MoveWordLeft(shiftPressed);
                    else             MoveCursor(-1, 0, shiftPressed);
                    e.Handled = true;
                    break;

                case Key.Right:
                    if (ctrlPressed) MoveWordRight(shiftPressed);
                    else             MoveCursor(1, 0, shiftPressed);
                    e.Handled = true;
                    break;

                case Key.Up:
                    MoveCursor(0, -1, shiftPressed);
                    e.Handled = true;
                    break;

                case Key.Down:
                    MoveCursor(0, 1, shiftPressed);
                    e.Handled = true;
                    break;

                case Key.Home:
                    if (ctrlPressed) MoveCursorToDocumentStart(shiftPressed);
                    else             MoveCursorToLineStart(shiftPressed);
                    e.Handled = true;
                    break;

                case Key.End:
                    if (ctrlPressed) MoveCursorToDocumentEnd(shiftPressed);
                    else             MoveCursorToLineEnd(shiftPressed);
                    e.Handled = true;
                    break;

                case Key.Enter:
                    InsertNewLine();
                    e.Handled = true;
                    break;

                case Key.Back:
                    if (!_selection.IsEmpty)
                        DeleteSelection();
                    else
                        DeleteCharBefore();
                    e.Handled = true;
                    break;

                case Key.Delete:
                    DeleteCharAfter();
                    e.Handled = true;
                    break;

                case Key.Tab:
                    // Try snippet expansion first; fall back to regular tab insertion.
                    if (!TryExpandSnippet())
                        InsertTab();
                    e.Handled = true;
                    break;

                // Clipboard operations (Phase 3)
                case Key.C:
                    if (ctrlPressed)
                    {
                        CopyToClipboard();
                        e.Handled = true;
                    }
                    break;

                case Key.V:
                    if (ctrlPressed)
                    {
                        PasteFromClipboard();
                        e.Handled = true;
                    }
                    break;

                case Key.X:
                    if (ctrlPressed)
                    {
                        CutToClipboard();
                        e.Handled = true;
                    }
                    break;

                // Undo/Redo
                case Key.Z:
                    if (ctrlPressed && shiftPressed)   // Ctrl+Shift+Z = alternate Redo
                    {
                        Redo();
                        e.Handled = true;
                    }
                    else if (ctrlPressed)
                    {
                        Undo();
                        e.Handled = true;
                    }
                    break;

                case Key.Y:
                    if (ctrlPressed)
                    {
                        Redo();
                        e.Handled = true;
                    }
                    break;

                // Select All (Phase 3)
                case Key.A:
                    if (ctrlPressed)
                    {
                        SelectAll();
                        e.Handled = true;
                    }
                    break;

                // SmartComplete trigger (Phase 4)
                case Key.Space:
                    if (ctrlPressed && _enableSmartComplete)
                    {
                        TriggerSmartComplete();
                        e.Handled = true;
                    }
                    break;

                // Go to Symbol palette (Ctrl+T)
                case Key.T:
                    if (ctrlPressed)
                    {
                        ShowGoToSymbolPopup();
                        e.Handled = true;
                    }
                    break;

                // ── Folding keyboard shortcuts (P2-02) ─────────────────────
                // Ctrl+M → toggle fold at caret line
                // ── Outlining chord: Ctrl+M arms the chord; second key executes action ────
                case Key.M:
                    if (ctrlPressed && IsFoldingEnabled)
                    {
                        if (_outlineChordPending)
                        {
                            _outlineChordPending = false;
                            OutlineToggleCurrent(); // Ctrl+M, Ctrl+M
                        }
                        else
                        {
                            _outlineChordPending = true; // arm chord, wait for second key
                        }
                        e.Handled = true;
                    }
                    break;

                case Key.L:
                    if (ctrlPressed && _outlineChordPending)
                    {
                        _outlineChordPending = false;
                        OutlineToggleAll(); // Ctrl+M, Ctrl+L
                        e.Handled = true;
                    }
                    break;

                case Key.P:
                    if (ctrlPressed && _outlineChordPending)
                    {
                        _outlineChordPending = false;
                        OutlineStop(); // Ctrl+M, Ctrl+P
                        e.Handled = true;
                    }
                    break;

                case Key.U:
                    if (ctrlPressed && _outlineChordPending)
                    {
                        _outlineChordPending = false;
                        OutlineStopHidingCurrent(); // Ctrl+M, Ctrl+U
                        e.Handled = true;
                    }
                    break;

                case Key.O:
                    if (ctrlPressed && _outlineChordPending)
                    {
                        _outlineChordPending = false;
                        OutlineCollapseToDefinitions(); // Ctrl+M, Ctrl+O
                        e.Handled = true;
                    }
                    break;
                // ── Formatting chord: Ctrl+K arms the chord; second key executes action ──
                // Ctrl+K, Ctrl+D → Format Document   Ctrl+K, Ctrl+F → Format Selection
                case Key.K:
                    if (ctrlPressed)
                    {
                        _formatChordPending = true;
                        e.Handled = true;
                    }
                    break;

                case Key.D:
                    if (ctrlPressed && _formatChordPending)
                    {
                        _formatChordPending = false;
                        _ = FormatDocumentAsync(); // Ctrl+K, Ctrl+D
                        e.Handled = true;
                    }
                    break;

                case Key.F:
                    if (ctrlPressed && _formatChordPending)
                    {
                        _formatChordPending = false;
                        _ = FormatSelectionAsync(); // Ctrl+K, Ctrl+F
                        e.Handled = true;
                    }
                    break;

                // ────────────────────────────────────────────────────────────────────────
                // Ctrl+Shift+[ → collapse all folds
                case Key.OemOpenBrackets:
                    if (ctrlPressed && shiftPressed && IsFoldingEnabled && _foldingEngine != null)
                    {
                        _foldingEngine.CollapseAll();
                        InvalidateVisual();
                        e.Handled = true;
                    }
                    break;
                // Ctrl+Shift+] → expand all folds
                case Key.OemCloseBrackets:
                    if (ctrlPressed && shiftPressed && IsFoldingEnabled && _foldingEngine != null)
                    {
                        _foldingEngine.ExpandAll();
                        InvalidateVisual();
                        e.Handled = true;
                    }
                    break;
                // ───────────────────────────────────────────────────────────

                case Key.Escape:
                    // Feature A: clear rectangular selection first.
                    if (!_rectSelection.IsEmpty)
                    {
                        _rectSelection.Clear();
                        _isRectSelecting = false;
                        InvalidateRegion(RenderDirtyFlags.Selection);
                        e.Handled = true;
                        break;
                    }

                    // Feature B: cancel active drag-to-move.
                    if (_dragDrop.Phase != DragPhase.None)
                    {
                        if (_dragDrop.Phase == DragPhase.Dragging)
                            ReleaseMouseCapture();
                        Cursor = Cursors.IBeam;
                        // Restore original selection.
                        _selection.Start = _dragDrop.SelectionStart;
                        _selection.End   = _dragDrop.SelectionEnd;
                        _cursorLine   = _dragDrop.SelectionEnd.Line;
                        _cursorColumn = _dragDrop.SelectionEnd.Column;
                        _dragDrop.Reset();
                        InvalidateRegion(RenderDirtyFlags.Selection);
                        e.Handled = true;
                        break;
                    }

                    // Feature C: annuler la sélection de texte normale.
                    if (!_selection.IsEmpty)
                    {
                        _selection.Start = _selection.End; // replier sur la position courante du curseur
                        InvalidateRegion(RenderDirtyFlags.Selection);
                        e.Handled = true;
                        break;
                    }

                    // Dismiss Quick Info popup on Escape.
                    _quickInfoPopup?.Hide();
                    _hoverQuickInfoService?.Cancel();
                    // Dismiss end-of-block hint on Escape.
                    DismissEndBlockHint();
                    e.Handled = _quickInfoPopup?.IsShowing == true || _endBlockHintPopup?.IsOpen == true;
                    break;
            }

            InvalidateVisual();
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);

            // Reset caret blink on text input
            ResetCaretBlink();

            if (!string.IsNullOrEmpty(e.Text))
            {
                foreach (char ch in e.Text)
                {
                    // Skip control characters
                    if (char.IsControl(ch))
                        continue;

                    // Wrap-selection: if a selection is active and ch is an opening char,
                    // surround the selection with the matching pair.
                    if (WrapSelectionInPairs && !_selection.IsEmpty && IsOpeningChar(ch))
                    {
                        WrapSelectionWith(ch, GetClosingChar(ch));
                        continue;
                    }

                    // Skip-over: if ch is a closing char and the char already at the cursor
                    // position is the same closing char, just advance the cursor.
                    if (SkipOverClosingChar && IsClosingChar(ch) && CharAtCursor() == ch)
                    {
                        _cursorColumn++;
                        EnsureCursorVisible();
                        InvalidateVisual();
                        continue;
                    }

                    InsertChar(ch);

                    // Auto-close brackets and quotes
                    if (ShouldAutoClose(ch))
                    {
                        char closingChar = GetClosingChar(ch);
                        InsertChar(closingChar);
                        // Move cursor back one position to be inside the pair
                        _cursorColumn--;
                    }

                    // Auto-trigger SmartComplete on specific characters (Phase 4)
                    if (EnableSmartComplete && ShouldAutoTriggerSmartComplete(ch))
                    {
                        TriggerSmartCompleteWithDelay(ch);
                    }

                    // Trigger LSP Signature Help on '('
                    if (ch == '(' && _lspClient is not null)
                        _ = TriggerSignatureHelpAsync();

                    // Update active parameter tracking while SignatureHelp is open
                    UpdateSignatureHelpOnChar(ch);
                }
                // OPT-B: InvalidateVisual() removed — Document_TextChanged fires in the same
                // call stack and already calls InvalidateVisual() or InvalidateMeasure() as
                // appropriate via smart-invalidation routing.  Calling it again here produced
                // a guaranteed double render on every single keystroke.
            }
            EnsureCursorVisible();
        }

        #endregion

        #region Cursor Movement

        private void MoveCursor(int deltaColumn, int deltaLine, bool extendSelection)
        {
            // Save old position for selection
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);

            // Move line
            if (deltaLine != 0)
            {
                _cursorLine = Math.Max(0, Math.Min(_document.Lines.Count - 1, _cursorLine + deltaLine));
                // Clamp column to line length
                _cursorColumn = Math.Min(_cursorColumn, _document.Lines[_cursorLine].Length);
            }

            // Move column
            if (deltaColumn != 0)
            {
                _cursorColumn += deltaColumn;

                // Handle line boundaries
                if (_cursorColumn < 0 && _cursorLine > 0)
                {
                    // Move to end of previous line
                    _cursorLine--;
                    _cursorColumn = _document.Lines[_cursorLine].Length;
                }
                else if (_cursorColumn > _document.Lines[_cursorLine].Length && _cursorLine < _document.Lines.Count - 1)
                {
                    // Move to start of next line
                    _cursorLine++;
                    _cursorColumn = 0;
                }
                else
                {
                    // Clamp to line bounds
                    _cursorColumn = Math.Max(0, Math.Min(_document.Lines[_cursorLine].Length, _cursorColumn));
                }
            }

            // Handle selection
            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }

            // Phase 11.3: Ensure cursor stays visible when using virtual scrolling
            EnsureCursorVisible();

            // Schedule lightbulb check whenever the cursor line changes
            ScheduleLightbulbCheck();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            // Clear Ctrl+hover state when Ctrl is released.
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _ctrlDown = false;
                _hoveredSymbolZone = null;
                _ctrlClickService?.Cancel();
                Cursor = Cursors.IBeam;
                InvalidateRegion(RenderDirtyFlags.Overlays); // clear symbol underline overlay
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hoveredLinkZone.HasValue)
            {
                _hoveredLinkZone = null;
                Cursor = Cursors.IBeam;
                HideUrlTooltip();
                InvalidateVisual();
            }

            if (_hoveredHintsLine >= 0)
            {
                _hoveredHintsLine = -1;
                HideHintTooltip();
                InvalidateVisual();
            }

            // Cancel fold peek on editor exit.
            _foldPeekTargetLine = -1;
            _foldPeekTimer?.Stop();
            _foldPeekPopup?.Hide();

            // Start end-block hint grace timer — popup stays open 200 ms so mouse can enter it.
            _endBlockHintTimer?.Stop();
            _endBlockHintHoveredLine  = -1;
            _endBlockHintActiveRegion = null;
            _endBlockHintPopup?.OnEditorMouseLeft();

            // Start Quick Info grace timer — popup stays open for 200 ms so mouse can enter it.
            _quickInfoPopup?.OnEditorMouseLeft();
            _hoverQuickInfoService?.Cancel();

            // Start breakpoint hover grace timer.
            _bpHoverTimer?.Stop();
            _bpHoverLine = -1;
            _bpHoverPopup?.OnEditorMouseLeft();

            // Clear Ctrl+hover state on mouse leave.
            if (_ctrlDown)
            {
                _ctrlDown = false;
                _hoveredSymbolZone = null;
                _ctrlClickService?.Cancel();
                InvalidateVisual();
            }
        }

        private void ShowUrlTooltip(bool isEmail = false)
        {
            _urlTooltip ??= new ToolTip();
            _urlTooltip.Content         = isEmail ? "Ctrl+Click to send email" : "Ctrl+Click to open";
            _urlTooltip.PlacementTarget = this;
            _urlTooltip.Placement       = System.Windows.Controls.Primitives.PlacementMode.Mouse;
            _urlTooltip.IsOpen          = true;
        }

        private void HideUrlTooltip()
        {
            if (_urlTooltip is not null)
                _urlTooltip.IsOpen = false;
        }

        private void ShowHintTooltip(string text)
        {
            _hintTooltip ??= new ToolTip();
            _hintTooltip.Content         = text;
            _hintTooltip.PlacementTarget = this;
            _hintTooltip.Placement       = System.Windows.Controls.Primitives.PlacementMode.Mouse;
            _hintTooltip.IsOpen          = true;
        }

        private void HideHintTooltip()
        {
            if (_hintTooltip is not null)
                _hintTooltip.IsOpen = false;
        }

        private void MoveCursorToLineStart(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            _cursorColumn = 0;

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }
        }

        private void MoveCursorToLineEnd(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            _cursorColumn = _document.Lines[_cursorLine].Length;

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }
        }

        private void MoveCursorToDocumentStart(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            _cursorLine   = 0;
            _cursorColumn = 0;

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }

            EnsureCursorVisible();
        }

        private void MoveCursorToDocumentEnd(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            _cursorLine   = Math.Max(0, _document.Lines.Count - 1);
            _cursorColumn = _document.Lines[_cursorLine].Length;

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }

            EnsureCursorVisible();
        }

        private void MoveWordLeft(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            string line = _document.Lines[_cursorLine].Text;
            int col = _cursorColumn;

            // Skip non-word chars to the left (punctuation, whitespace)
            while (col > 0 && !IsWordChar(line[col - 1])) col--;
            // Skip word chars to the left
            while (col > 0 && IsWordChar(line[col - 1])) col--;

            if (col == _cursorColumn && _cursorLine > 0)
            {
                // Step to end of previous line
                _cursorLine--;
                _cursorColumn = _document.Lines[_cursorLine].Text.Length;
            }
            else
            {
                _cursorColumn = col;
            }

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }

            EnsureCursorVisible();
        }

        private void MoveWordRight(bool extendSelection)
        {
            var oldPosition = new TextPosition(_cursorLine, _cursorColumn);
            string line = _document.Lines[_cursorLine].Text;
            int col = _cursorColumn;

            // Skip word chars to the right
            while (col < line.Length && IsWordChar(line[col])) col++;
            // Skip non-word chars to the right (punctuation, whitespace)
            while (col < line.Length && !IsWordChar(line[col])) col++;

            if (col == _cursorColumn && _cursorLine < _document.Lines.Count - 1)
            {
                // Step to start of next line
                _cursorLine++;
                _cursorColumn = 0;
            }
            else
            {
                _cursorColumn = col;
            }

            if (extendSelection)
            {
                if (_selection.IsEmpty)
                    _selection.Start = oldPosition;
                _selection.End = new TextPosition(_cursorLine, _cursorColumn);
            }
            else
            {
                _selection.Clear();
            }

            EnsureCursorVisible();
        }

        #endregion

        #region Text Editing Operations

        private void InsertChar(char ch)
        {
            _document.InsertChar(_cursorLine, _cursorColumn, ch);
            _cursorColumn++;
        }

        private void InsertNewLine()
        {
            if (!_selection.IsEmpty)
                DeleteSelection();

            _document.InsertNewLine(_cursorLine, _cursorColumn, (int)AutoIndentMode);
            _cursorLine++;
            _cursorColumn = CalculateAutoIndentColumn();
            _selection.Clear();
            EnsureCursorVisible();
        }

        private void InsertTab()
        {
            // Insert spaces for tab (respects IndentSize)
            int spacesToInsert = _document.IndentSize - (_cursorColumn % _document.IndentSize);
            for (int i = 0; i < spacesToInsert; i++)
            {
                InsertChar(' ');
            }
        }

        /// <summary>
        /// Reads the word immediately left of the cursor and tries to expand it as a snippet.
        /// </summary>
        /// <returns><c>true</c> if a snippet was found and applied.</returns>
        private bool TryExpandSnippet()
        {
            var mgr = SnippetManager;
            if (mgr == null || _cursorColumn == 0)
                return false;

            string lineText = _document.Lines[_cursorLine].Text ?? string.Empty;

            // Extract the non-whitespace word immediately to the left of the caret.
            int end   = _cursorColumn;
            int start = end - 1;
            while (start > 0 && !char.IsWhiteSpace(lineText[start - 1]))
                start--;

            if (start >= end)
                return false;

            string word = lineText.Substring(start, end - start);

            if (!mgr.TryExpand(word, out var snippet))
                return false;

            var expansion = SnippetManager.BuildExpansion(snippet, _cursorLine, start, word.Length);
            ApplySnippetExpansion(expansion);
            return true;
        }

        /// <summary>
        /// Deletes the trigger text and inserts the expanded snippet body,
        /// then positions the caret at the <c>$cursor</c> marker location.
        /// </summary>
        private void ApplySnippetExpansion(SnippetExpansion expansion)
        {
            // Delete trigger: range [InsertColumn .. InsertColumn + TriggerLength).
            _document.DeleteRange(
                new TextPosition(expansion.InsertLine, expansion.InsertColumn),
                new TextPosition(expansion.InsertLine, expansion.InsertColumn + expansion.TriggerLength));

            // Insert expanded body at the now-empty position.
            _document.InsertText(
                new TextPosition(expansion.InsertLine, expansion.InsertColumn),
                expansion.ExpandedText);

            // Move caret to the $cursor position.
            _cursorLine   = expansion.CaretLine;
            _cursorColumn = expansion.CaretColumn;

            EnsureCursorVisible();
            InvalidateVisual();
        }

        /// <summary>
        /// Check if a character should trigger auto-closing
        /// </summary>
        private bool ShouldAutoClose(char ch)
        {
            switch (ch)
            {
                case '{':
                case '[':
                case '(':
                    return EnableAutoClosingBrackets;
                case '"':
                case '\'':
                    return EnableAutoClosingQuotes;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Get the closing character for an opening character
        /// </summary>
        private char GetClosingChar(char ch)
        {
            switch (ch)
            {
                case '{': return '}';
                case '[': return ']';
                case '(': return ')';
                case '"': return '"';
                case '\'': return '\'';
                default: return ch;
            }
        }

        private static bool IsOpeningChar(char ch)
            => ch is '{' or '[' or '(' or '"' or '\'';

        private static bool IsClosingChar(char ch)
            => ch is '}' or ']' or ')' or '"' or '\'';

        private char CharAtCursor()
        {
            if (_cursorLine >= _document.Lines.Count) return '\0';
            var line = _document.Lines[_cursorLine];
            if (_cursorColumn >= line.Length) return '\0';
            return line.Text[_cursorColumn];
        }

        private void WrapSelectionWith(char open, char close)
        {
            var start = _selection.NormalizedStart;
            var end   = _selection.NormalizedEnd;

            // Insert closing char first (at end, so start offsets stay valid).
            _document.InsertChar(end.Line, end.Column, close);
            // Insert opening char before the selection.
            _document.InsertChar(start.Line, start.Column, open);

            // Restore selection to wrap the text (shift both positions by 1 col on same line or not).
            _selection.Start = new TextPosition(start.Line, start.Column + 1);
            _selection.End   = end.Line == start.Line
                ? new TextPosition(end.Line, end.Column + 1)   // same line: shifted by the opening char
                : new TextPosition(end.Line, end.Column);       // different line: unaffected

            _cursorLine   = _selection.End.Line;
            _cursorColumn = _selection.End.Column;
            EnsureCursorVisible();
            InvalidateVisual();
        }

        private void DeleteCharBefore()
        {
            if (_cursorColumn > 0)
            {
                // SmartBackspace: Delete by indent level if on leading whitespace
                if (SmartBackspace && IsOnLeadingWhitespace())
                {
                    var line = _document.Lines[_cursorLine];
                    int spaces = _cursorColumn;
                    int indentSize = IndentSize;

                    // Calculate how many spaces to delete to reach previous indent level
                    int spacesToDelete = spaces % indentSize;
                    if (spacesToDelete == 0)
                        spacesToDelete = indentSize;

                    // Delete multiple spaces
                    for (int i = 0; i < spacesToDelete && _cursorColumn > 0; i++)
                    {
                        _cursorColumn--;
                        _document.DeleteChar(_cursorLine, _cursorColumn);
                    }
                }
                else
                {
                    // Regular backspace - delete single character
                    _cursorColumn--;
                    _document.DeleteChar(_cursorLine, _cursorColumn);
                }
            }
            else if (_cursorLine > 0)
            {
                // Delete newline - merge with previous line
                int prevLineLength = _document.Lines[_cursorLine - 1].Length;
                _document.Lines[_cursorLine - 1].Text += _document.Lines[_cursorLine].Text;
                _document.DeleteLine(_cursorLine);
                _cursorLine--;
                _cursorColumn = prevLineLength;
            }
        }

        /// <summary>
        /// Check if cursor is on leading whitespace
        /// </summary>
        private bool IsOnLeadingWhitespace()
        {
            if (_cursorLine >= _document.Lines.Count)
                return false;

            var line = _document.Lines[_cursorLine];

            // Check if all characters before cursor are spaces
            for (int i = 0; i < _cursorColumn && i < line.Text.Length; i++)
            {
                if (line.Text[i] != ' ')
                    return false;
            }

            return true;
        }

        private void DeleteCharAfter()
        {
            var currentLine = _document.Lines[_cursorLine];

            if (_cursorColumn < currentLine.Length)
            {
                _document.DeleteChar(_cursorLine, _cursorColumn);
            }
            else if (_cursorLine < _document.Lines.Count - 1)
            {
                // Delete newline - merge with next line
                currentLine.Text += _document.Lines[_cursorLine + 1].Text;
                _document.DeleteLine(_cursorLine + 1);
            }
        }

        private int CalculateAutoIndentColumn()
        {
            // The document's InsertNewLine already placed the correct indent on the new line
            // based on AutoIndentMode. Read back the leading whitespace so the caret lands
            // at the first content column.
            if (_cursorLine >= _document.Lines.Count)
                return 0;

            var text = _document.Lines[_cursorLine].Text ?? string.Empty;
            int col = 0;
            foreach (char ch in text)
            {
                if (ch == ' ' || ch == '\t') col++;
                else break;
            }
            return col;
        }

        #endregion

        #region Mouse Input Handling (Phase 3)

        /// <summary>
        /// Handle mouse wheel for vertical scrolling (Phase 11.3)
        /// </summary>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            // Swallow wheel events while the references popup is open so the editor
            // does not scroll under it. The user must dismiss the popup first.
            if (_referencesPopup?.IsOpen == true)
            {
                e.Handled = true;
                return;
            }

            _quickInfoPopup?.Hide();
            DismissEndBlockHint();

            // Ctrl + wheel → zoom in / out (B6).
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                double step  = e.Delta > 0 ? 0.1 : -0.1;
                ZoomLevel = Math.Clamp(ZoomLevel + step, 0.5, 4.0);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                // Horizontal scroll: one notch = resolved speed chars (matches HexEditor model).
                int hSpeed    = MouseWheelSpeed == MouseWheelSpeed.System
                    ? SystemParameters.WheelScrollLines
                    : (int)MouseWheelSpeed;
                double hDelta = -Math.Sign(e.Delta) * hSpeed * _charWidth * HorizontalScrollSensitivity;
                double maxH   = _hScrollBar?.Maximum ?? 0;
                _horizontalScrollOffset = Math.Max(0, Math.Min(maxH, _horizontalScrollOffset + hDelta));
                SyncHScrollBar();
                InvalidateVisual();
                e.Handled = true;
            }
            else if (EnableVirtualScrolling && _virtualizationEngine != null)
            {
                // Vertical scroll: same model as HexEditor.
                // MouseWheelSpeed.System → WheelScrollLines, else cast enum value directly.
                int    speed      = MouseWheelSpeed == MouseWheelSpeed.System
                    ? SystemParameters.WheelScrollLines
                    : (int)MouseWheelSpeed;
                double pixelDelta = -Math.Sign(e.Delta) * speed * _lineHeight;
                ScrollVertical(pixelDelta);

                // If a drag-selection is in progress, keep the selection end anchored to
                // the text position under the mouse after the viewport has moved.
                if (_isSelecting)
                {
                    var mousePos = e.GetPosition(this);
                    _lastMousePosition = mousePos;
                    var textPos = PixelToTextPosition(mousePos);
                    _selection.End = textPos;
                    _cursorLine    = textPos.Line;
                    _cursorColumn  = textPos.Column;
                }

                e.Handled = true;
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            // Middle-click toggles pan mode; any other click while active exits it.
            if (_panMode.HandleMouseDown(e)) return;

            // Dismiss any open references popup on any click in the editor,
            // but NOT when the click originated inside the popup itself.
            // Two complementary guards cover both WPF-routing and Win32 click-through paths:
            //   1. IsEventFromInsidePopup — detects via PresentationSource (HWND-aware)
            //   2. IsClickInsidePopupBounds — screen-coordinate fallback
            if (!IsEventFromInsidePopup(e.OriginalSource) && !IsClickInsidePopupBounds(e.GetPosition(this)))
                _referencesPopup?.Close();

            // Dismiss Quick Info popup and cancel pending hover on any click.
            _quickInfoPopup?.Hide();
            _hoverQuickInfoService?.Cancel();

            Focus(); // Ensure editor gets keyboard focus

            var pos = e.GetPosition(this);
            var textPos = PixelToTextPosition(pos);

            // Right-click behavior: don't clear selection if clicking inside it
            if (e.RightButton == MouseButtonState.Pressed)
            {
                // Check if click is inside existing selection
                if (!_selection.IsEmpty && IsPositionInSelection(textPos))
                {
                    // Don't clear selection, just let context menu open
                    e.Handled = true;
                    return;
                }
                else
                {
                    // Click outside selection - move cursor and clear selection
                    _cursorLine = textPos.Line;
                    _cursorColumn = textPos.Column;
                    _selection.Start = textPos;
                    _selection.End = textPos;
                    InvalidateVisual();
                    NotifyCaretMovedIfChanged();
                    return;
                }
            }

            // Left-click (or double-click when FoldToggleOnDoubleClick is set) on an inline
            // fold-collapse label → toggle the fold.
            bool foldClickOk = e.LeftButton == MouseButtonState.Pressed
                && (!FoldToggleOnDoubleClick || e.ClickCount == 2);
            if (foldClickOk && _foldLabelHitZones.Count > 0)
            {
                var clickPos = e.GetPosition(this);
                foreach (var (rect, line) in _foldLabelHitZones)
                {
                    if (rect.Contains(clickPos))
                    {
                        _foldingEngine?.ToggleRegion(line);
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Left-click on lightbulb glyph → show code actions popup
            if (e.LeftButton == MouseButtonState.Pressed && _lightbulbLine >= 0 && ShowLineNumbers)
            {
                var clickPos = e.GetPosition(this);
                // Lightbulb is drawn just to the right of the separator at x=LineNumberWidth
                double glyphSize = Math.Min(_lineHeight * 0.55, 11);
                double glyphX    = LineNumberWidth + 4;
                double glyphY    = GetFoldAwareLineY(_lightbulbLine - _firstVisibleLine);
                var bulbRect     = new Rect(glyphX, glyphY, glyphSize + 6, _lineHeight);
                if (bulbRect.Contains(clickPos))
                {
                    _ = ShowCodeActionsAsync();
                    e.Handled = true;
                    return;
                }
            }

            // Left-click on a InlineHints hint → navigate cursor onto the symbol and open references popup.
            if (ShowInlineHints && e.LeftButton == MouseButtonState.Pressed && _hintsHitZones.Count > 0)
            {
                var clickPos = e.GetPosition(this);
                foreach (var (zone, lineIdx, symbol) in _hintsHitZones)
                {
                    if (zone.Contains(clickPos))
                    {
                        // Do NOT move the caret — pass line/symbol directly so the
                        // user's cursor position is preserved.
                        _ = FindAllReferencesAsync(lineOverride: lineIdx, symbolOverride: symbol);
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Left-click on a color swatch → raise ColorSwatchClicked event.
            if (ColorSwatchPreviewEnabled && e.LeftButton == MouseButtonState.Pressed
                && _colorSwatchRenderer.LastHitAreas.Count > 0)
            {
                var swatchClickPos = e.GetPosition(this);
                foreach (var area in _colorSwatchRenderer.LastHitAreas)
                {
                    if (area.Bounds.Contains(swatchClickPos))
                    {
                        ColorSwatchClicked?.Invoke(this, new ColorSwatchClickedEventArgs(area.Color, area.Line));
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Ctrl+Left-click on a URL or email → open in browser / mail client.
            if (e.LeftButton == MouseButtonState.Pressed
                && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var linkZone = FindLinkZone(textPos.Line, textPos.Column);
                if (linkZone.HasValue)
                {
                    var target = linkZone.Value.IsEmail
                        ? $"mailto:{linkZone.Value.Url}"
                        : linkZone.Value.Url;
                    try
                    {
                        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                    }
                    catch { /* Ignore failures to launch browser / mail client */ }
                    e.Handled = true;
                    return;
                }

                // Ctrl+Left-click on a symbol → Go to Definition.
                if (_hoveredSymbolZone.HasValue)
                {
                    _ = NavigateToDefinitionAsync(_hoveredSymbolZone.Value);
                    e.Handled = true;
                    return;
                }
            }

            // Block caret placement when clicking in the InlineHints hint zone
            // (the HintLineHeight strip above the code text of a declaration line).
            if (ShowInlineHints
                && _lineYLookup.TryGetValue(textPos.Line, out double codeTextY)
                && pos.Y < codeTextY)
            {
                e.Handled = true;
                return;
            }

            // Ctrl+Alt+Click → add a secondary caret at the clicked position.
            bool ctrlAltDown = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt))
                                == (ModifierKeys.Control | ModifierKeys.Alt);
            if (ctrlAltDown && e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
            {
                _caretManager.AddCaret(textPos.Line, textPos.Column);
                e.Handled = true;
                return;
            }

            // Feature A: Alt+LeftClick → start rectangular selection.
            // e.Handled = true prevents menu-bar Alt activation.
            bool altDown = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            if (altDown && e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
            {
                _isSelecting    = false;
                _isRectSelecting = true;
                _selection.Clear();
                _rectSelection.Begin(textPos);
                _cursorLine   = textPos.Line;
                _cursorColumn = textPos.Column;
                CaptureMouse();
                InvalidateVisual();
                NotifyCaretMovedIfChanged();
                e.Handled = true;
                return;
            }

            // Any non-Alt, single click: check for rect-block drag BEFORE clearing the rect.
            if (!_rectSelection.IsEmpty && e.ClickCount == 1
                && IsInsideRectBlock(textPos.Line, textPos.Column))
            {
                // Click inside the active rect block → start potential rect drag-to-move.
                _dragDrop.Phase           = DragPhase.Pending;
                _dragDrop.ClickPixel      = pos;
                _dragDrop.ClickedPosition = textPos;
                _dragDrop.SelectionStart  = new TextPosition(_rectSelection.TopLine,    _rectSelection.LeftColumn);
                _dragDrop.SelectionEnd    = new TextPosition(_rectSelection.BottomLine, _rectSelection.RightColumn);
                _isRectDrag = true;
                e.Handled   = true;
                return;
            }

            // Non-Alt click with no rect-drag → clear rectangular selection.
            if (!_rectSelection.IsEmpty)
            {
                _rectSelection.Clear();
                _isRectSelecting = false;
            }
            _isRectDrag = false;

            // Left-click behavior (unchanged)
            _cursorLine = textPos.Line;
            _cursorColumn = textPos.Column;

            if (e.ClickCount == 2) // Double-click = select word
            {
                SelectWordAtPosition(textPos);
                e.Handled = true;
            }
            else if (e.ClickCount == 3) // Triple-click = select line
            {
                SelectLineAtPosition(textPos);
                e.Handled = true;
            }
            else
            {
                // Feature B: click inside existing text selection → potential drag-to-move.
                if (!_selection.IsEmpty && IsPositionInSelection(textPos))
                {
                    _dragDrop.Phase           = DragPhase.Pending;
                    _dragDrop.ClickPixel      = pos;
                    _dragDrop.ClickedPosition = textPos;
                    _dragDrop.SelectionStart  = _selection.NormalizedStart;
                    _dragDrop.SelectionEnd    = _selection.NormalizedEnd;
                    // Do NOT set _isSelecting — wait to see if threshold is crossed.
                    e.Handled = true;
                    return;
                }

                // Start normal selection
                _isSelecting = true;
                _mouseDownPosition = textPos;
                _selection.Start = textPos;
                _selection.End = textPos;
                CaptureMouse();
            }

            InvalidateVisual();
            NotifyCaretMovedIfChanged();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // In pan mode: update directional cursor; suppress normal hover logic.
            if (_panMode.HandleMouseMove(e)) return;

            // Link hover: show Hand cursor + underline when the pointer is over a URL/email zone.
            if (!_isSelecting)
            {
                var hoverPos = PixelToTextPosition(e.GetPosition(this));
                var linkZone = FindLinkZone(hoverPos.Line, hoverPos.Column);

                // Only repaint when the hovered zone actually changes (avoids per-mousemove redraws).
                if (linkZone != _hoveredLinkZone)
                {
                    _hoveredLinkZone = linkZone;
                    InvalidateVisual();
                }

                // InlineHints hint zones: Hand cursor, hover highlight, and tooltip.
                var mousePixel = e.GetPosition(this);
                int prevHover  = _hoveredHintsLine;
                _hoveredHintsLine = -1;
                string? lensTooltip = null;
                foreach (var (zone, lineIdx, sym) in _hintsHitZones)
                {
                    if (zone.Contains(mousePixel))
                    {
                        _hoveredHintsLine = lineIdx;
                        if (_hintsData.TryGetValue(lineIdx, out var entry))
                        {
                            string refText = entry.Count == 1
                                ? $"1 reference to '{sym}'"
                                : $"{entry.Count} references to '{sym}'";
                            string source = entry.IsRoslyn
                                ? "Roslyn (semantic)"
                                : "Regex (text scan)";
                            lensTooltip = $"{refText}  ·  Source: {source}  (Alt+3)";
                        }
                        break;
                    }
                }
                if (_hoveredHintsLine != prevHover)
                    InvalidateVisual();

                bool overLens = _hoveredHintsLine >= 0;
                if (overLens && lensTooltip is not null)
                    ShowHintTooltip(lensTooltip);
                else
                    HideHintTooltip();

                // Fold label zones — Hand cursor + 1.5s peek-on-hover.
                int newHoveredFoldLine = -1;
                foreach (var (rect, line) in _foldLabelHitZones)
                    if (rect.Contains(mousePixel)) { newHoveredFoldLine = line; break; }

                bool overFoldLabel = newHoveredFoldLine >= 0;

                if (overFoldLabel)
                {
                    if (newHoveredFoldLine != _foldPeekTargetLine)
                    {
                        // Mouse moved to a different label — restart peek timer + repaint hover.
                        _foldPeekTargetLine = newHoveredFoldLine;
                        _foldPeekTimer?.Stop();
                        _foldPeekPopup?.Hide();
                        _foldPeekTimer?.Start();
                        InvalidateVisual();
                    }
                    // else: still on same label — timer already running.
                }
                else
                {
                    if (_foldPeekTargetLine >= 0)
                    {
                        // Mouse left all fold labels — cancel, close, repaint to remove hover style.
                        _foldPeekTargetLine = -1;
                        _foldPeekTimer?.Stop();
                        _foldPeekPopup?.Hide();
                        InvalidateVisual();
                    }
                }

                if (overLens)
                {
                    Cursor = Cursors.Hand;
                    HideUrlTooltip();
                }
                else if (linkZone.HasValue)
                {
                    Cursor = Cursors.Hand;
                    ShowUrlTooltip(linkZone.Value.IsEmail);
                }
                else if (overFoldLabel)
                {
                    Cursor = Cursors.Hand;
                    HideUrlTooltip();
                }
                else
                {
                    Cursor = mousePixel.X < TextAreaLeftOffset ? Cursors.Arrow : Cursors.IBeam;
                    HideUrlTooltip();
                }

                // Quick Info hover — dispatch after cursor state is settled
                if (ShowQuickInfo && _hoverQuickInfoService is not null && !_isSelecting)
                    HandleQuickInfoHover(hoverPos, e.GetPosition(this));

                // End-of-block hint — show popup when cursor is on a region's closing line.
                HandleEndBlockHintHover(hoverPos.Line);


                // Ctrl+hover symbol underline.
                // Enabled when: (a) no LanguageDefinition is registered for this file type
                // (Language == null → backward-compatible default ON), or (b) the language
                // explicitly declares EnableCtrlClickNavigation = true.
                // Languages that set EnableCtrlClickNavigation = false (e.g. JSON, YAML, HTML)
                // suppress the hand-cursor and block navigation.
                if (_ctrlDown && (Language is null || Language.EnableCtrlClickNavigation))
                {
                    HandleCtrlHover(hoverPos);
                    if (!overLens && !linkZone.HasValue)
                        Cursor = _hoveredSymbolZone.HasValue ? Cursors.Hand
                        : mousePixel.X < TextAreaLeftOffset ? Cursors.Arrow
                        : Cursors.IBeam;
                }
            }

            // Feature A: extend rectangular selection during Alt+drag.
            if (_isRectSelecting && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos     = e.GetPosition(this);
                var textPos = PixelToTextPosition(pos);
                _rectSelection.Extend(textPos);
                _cursorLine   = textPos.Line;
                _cursorColumn = textPos.Column;
                if (!_selectionRenderPending)
                {
                    _selectionRenderPending = true;
                    Dispatcher.InvokeAsync(() =>
                    {
                        _selectionRenderPending = false;
                        InvalidateVisual();
                    }, System.Windows.Threading.DispatcherPriority.Render);
                }
                return;
            }

            // Feature B: handle drag-pending or drag-in-progress state.
            if (_dragDrop.Phase != DragPhase.None && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos     = e.GetPosition(this);
                var textPos = PixelToTextPosition(pos);

                if (_dragDrop.Phase == DragPhase.Pending && _dragDrop.HasMovedBeyondThreshold(pos))
                {
                    _dragDrop.Phase = DragPhase.Dragging;
                    CaptureMouse();
                    Cursor = Cursors.SizeAll;
                }

                if (_dragDrop.Phase == DragPhase.Dragging)
                {
                    _dragDrop.DropPosition = textPos;
                    if (!_selectionRenderPending)
                    {
                        _selectionRenderPending = true;
                        Dispatcher.InvokeAsync(() =>
                        {
                            _selectionRenderPending = false;
                            InvalidateVisual();
                        }, System.Windows.Threading.DispatcherPriority.Render);
                    }
                }
                return;
            }

            if (_isSelecting && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                _lastMousePosition = pos;

                // Start or stop the auto-scroll timer based on whether the mouse
                // is outside the visible viewport bounds.
                bool outsideBounds = pos.Y < 0 || pos.Y > ActualHeight;
                if (outsideBounds && !_autoScrollTimer.IsEnabled)
                    _autoScrollTimer.Start();
                else if (!outsideBounds && _autoScrollTimer.IsEnabled)
                    _autoScrollTimer.Stop();

                var textPos = PixelToTextPosition(pos);

                // Guard: skip re-render if the selection endpoint hasn't moved to a new cell.
                // Mouse-move events can fire at 200–1000 Hz; many will resolve to the same
                // text position and would trigger a full OnRender() for nothing.
                if (textPos == _selection.End) return;

                _selection.End = textPos;
                _cursorLine    = textPos.Line;
                _cursorColumn  = textPos.Column;

                // Coalesce: queue at most one render per WPF frame (~60 Hz) instead of
                // one per OS mouse event.  _selectionRenderPending is cleared inside the
                // dispatched lambda so subsequent events re-arm correctly.
                if (_selectionRenderPending) return;
                _selectionRenderPending = true;
                Dispatcher.InvokeAsync(() =>
                {
                    _selectionRenderPending = false;
                    InvalidateRegion(RenderDirtyFlags.Selection);
                }, System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            // Feature A: terminate rectangular selection drag.
            if (_isRectSelecting)
            {
                _isRectSelecting = false;
                _autoScrollTimer.Stop();
                ReleaseMouseCapture();
                return;
            }

            // Feature B: commit or cancel text drag-to-move.
            if (_dragDrop.Phase != DragPhase.None)
            {
                ReleaseMouseCapture();
                Cursor = Cursors.IBeam;

                if (_dragDrop.Phase == DragPhase.Dragging)
                {
                    if (_isRectDrag) CommitRectDrop();
                    else             CommitTextDrop();
                }
                else
                {
                    // Pending phase (no threshold crossed): clear selection, place caret.
                    _cursorLine   = _dragDrop.ClickedPosition.Line;
                    _cursorColumn = _dragDrop.ClickedPosition.Column;
                    _selection.Clear();
                    _rectSelection.Clear();
                    InvalidateVisual();
                    NotifyCaretMovedIfChanged();
                }

                _isRectDrag = false;
                _dragDrop.Reset();
                return;
            }

            if (_isSelecting)
            {
                _isSelecting = false;
                _autoScrollTimer.Stop();
                ReleaseMouseCapture();
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);

            // Start caret blinking when focused
            if (_caretTimer != null && CaretBlinkRate > 0)
            {
                _caretVisible = true;
                _caretTimer.Stop();
                _caretTimer.Start();
            }
            else if (_caretTimer != null)
            {
                // If blink rate is 0 (always visible), ensure caret is shown
                _caretVisible = true;
            }

            // Force immediate repaint to show caret and active selection
            InvalidateVisual();

            // Force update layout to ensure cursor is visible immediately
            UpdateLayout();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            // Exit pan mode on focus loss (Alt-Tab, click elsewhere, etc.)
            _panMode.HandleLostFocus();

            // Stop caret blinking when not focused
            if (_caretTimer != null)
            {
                _caretTimer.Stop();
                _caretVisible = false;
            }

            // Clear Ctrl+hover and Quick Info state on focus loss.
            _quickInfoPopup?.Hide();
            _hoverQuickInfoService?.Cancel();
            if (_ctrlDown)
            {
                _ctrlDown = false;
                _hoveredSymbolZone = null;
                _ctrlClickService?.Cancel();
            }

            // Repaint to show inactive selection
            InvalidateVisual();
        }

        /// <summary>
        /// Returns the <see cref="LinkHitZone"/> (URL or email) that contains <paramref name="column"/> on
        /// <paramref name="line"/>, or <see langword="null"/> if no link occupies that position.
        /// Hit-zones are rebuilt on every render pass by <see cref="OverlayUrlTokens"/>.
        /// </summary>
        private LinkHitZone? FindLinkZone(int line, int column)
        {
            foreach (var zone in _linkHitZones)
            {
                if (zone.Line == line && column >= zone.StartCol && column < zone.EndCol)
                    return zone;
            }
            return null;
        }

        /// <summary>
        /// Convert pixel position to text position (line, column)
        /// </summary>
        private TextPosition PixelToTextPosition(Point pixel)
        {
            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            int line;

            if (ShowInlineHints && _visLinePositions.Count > 0)
            {
                // Variable-height scan: InlineHints declaration lines have a taller slot.
                // Each lens-line slot spans (codeY - HintLineHeight → codeY + _lineHeight).
                // Each normal-line slot spans (codeY → codeY + _lineHeight).
                line = _visLinePositions[^1].LineIndex; // default: last visible line
                for (int k = 0; k < _visLinePositions.Count; k++)
                {
                    var (lineIdx, codeY) = _visLinePositions[k];
                    double slotTop = IsHintEntryVisible(lineIdx) ? codeY - HintLineHeight : codeY;
                    if (pixel.Y >= slotTop && pixel.Y < codeY + _lineHeight)
                    {
                        line = lineIdx;
                        break;
                    }
                }
            }
            else
            {
                // Uniform-height path: use VirtualizationEngine for sub-line scroll accuracy.
                line = EnableVirtualScrolling && _virtualizationEngine != null
                    ? _virtualizationEngine.GetLineAtYPosition(pixel.Y - TopMargin)
                    : _firstVisibleLine + (int)((pixel.Y - TopMargin) / _lineHeight);
            }

            line = Math.Max(0, Math.Min(_document.Lines.Count - 1, line));

            // Calculate column (account for horizontal scroll offset + tab expansion)
            string clickLineText = _document?.Lines[line]?.Text ?? string.Empty;
            int column = _glyphRenderer?.ComputeColumnFromVisualX(clickLineText, pixel.X - leftEdge + _horizontalScrollOffset)
                         ?? (int)((pixel.X - leftEdge + _horizontalScrollOffset) / _charWidth);

            // Word wrap: the click may be on a sub-row — add the sub-row column offset.
            if (IsWordWrapEnabled && _charsPerVisualLine > 0)
            {
                // Find which sub-row was clicked by scanning _visLinePositions.
                int subRow = 0;
                for (int k = 0; k < _visLinePositions.Count; k++)
                {
                    if (_visLinePositions[k].LineIndex != line) continue;
                    double codeY = _visLinePositions[k].Y;
                    if (pixel.Y >= codeY && pixel.Y < codeY + _lineHeight)
                    {
                        subRow = k < _visLineSubRows.Count ? _visLineSubRows[k] : 0;
                        break;
                    }
                }
                column = subRow * _charsPerVisualLine + column;
            }

            column = Math.Max(0, Math.Min(_document.Lines[line].Length, column));

            return new TextPosition(line, column);
        }

        /// <summary>
        /// Select word at position (double-click handler)
        /// </summary>
        private void SelectWordAtPosition(TextPosition pos)
        {
            if (pos.Line < 0 || pos.Line >= _document.Lines.Count)
                return;

            var line = _document.Lines[pos.Line];
            if (string.IsNullOrEmpty(line.Text) || pos.Column >= line.Text.Length)
            {
                _selection.Clear();
                return;
            }

            // Find word boundaries
            int start = pos.Column;
            int end = pos.Column;

            // Expand left
            while (start > 0 && IsWordChar(line.Text[start - 1]))
                start--;

            // Expand right
            while (end < line.Text.Length && IsWordChar(line.Text[end]))
                end++;

            _selection.Start = new TextPosition(pos.Line, start);
            _selection.End = new TextPosition(pos.Line, end);
        }

        /// <summary>
        /// Select entire line at position (triple-click handler)
        /// </summary>
        private void SelectLineAtPosition(TextPosition pos)
        {
            if (pos.Line < 0 || pos.Line >= _document.Lines.Count)
                return;

            _selection.Start = new TextPosition(pos.Line, 0);
            _selection.End = new TextPosition(pos.Line, _document.Lines[pos.Line].Length);
        }

        /// <summary>
        /// Check if a position is inside the current selection
        /// </summary>
        private bool IsPositionInSelection(TextPosition pos)
        {
            if (_selection.IsEmpty)
                return false;

            var start = _selection.NormalizedStart;
            var end = _selection.NormalizedEnd;

            // Single line selection
            if (start.Line == end.Line)
            {
                return pos.Line == start.Line && pos.Column >= start.Column && pos.Column <= end.Column;
            }

            // Multi-line selection
            if (pos.Line < start.Line || pos.Line > end.Line)
                return false;

            if (pos.Line == start.Line)
                return pos.Column >= start.Column;

            if (pos.Line == end.Line)
                return pos.Column <= end.Column;

            // Middle lines are always inside
            return true;
        }

        /// <summary>
        /// Check if character is part of a word (alphanumeric or underscore)
        /// </summary>
        private bool IsWordChar(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch == '_';
        }

        #endregion

        #region Clipboard Operations (Phase 3)

        private void CopyToClipboard()
        {
            // Feature A: rectangular selection takes priority.
            if (!_rectSelection.IsEmpty) { CopyRectSelection(); return; }

            if (_selection.IsEmpty)
                return;

            try
            {
                string selectedText = _document.GetText(_selection.NormalizedStart, _selection.NormalizedEnd);
                Clipboard.SetText(selectedText);
            }
            catch (Exception)
            {
                // Silently ignore clipboard errors
            }
        }

        private void CopyRectSelection()
        {
            if (_rectSelection.IsEmpty || _document is null) return;
            var lines = _document.Lines.Select(l => l.Text).ToList();
            string text = _rectSelection.ExtractText(lines);
            if (!string.IsNullOrEmpty(text))
            {
                try { Clipboard.SetText(text); }
                catch { /* Silently ignore clipboard errors */ }
            }
        }

        private void CutRectSelection()
        {
            if (_rectSelection.IsEmpty || _document is null || IsReadOnly) return;
            CopyRectSelection();
            DeleteRectSelection();
        }

        private void DeleteRectSelection()
        {
            if (_rectSelection.IsEmpty || _document is null || IsReadOnly) return;

            var (left, right) = _rectSelection.GetColumnRange();

            // Wrap all per-line deletes in a single atomic undo step.
            using (_undoEngine.BeginTransaction("Delete Rectangular Selection"))
            {
                // Iterate bottom-to-top so that per-line deletions don't shift line indices.
                for (int li = _rectSelection.BottomLine; li >= _rectSelection.TopLine; li--)
                {
                    if (li >= _document.Lines.Count) continue;
                    var line  = _document.Lines[li].Text;
                    int safeL = Math.Min(left,  line.Length);
                    int safeR = Math.Min(right, line.Length);
                    if (safeR <= safeL) continue;

                    var delStart = new TextPosition(li, safeL);
                    var delEnd   = new TextPosition(li, safeR);
                    _document.DeleteRange(delStart, delEnd);
                }
            }

            _cursorLine   = _rectSelection.TopLine;
            _cursorColumn = _rectSelection.LeftColumn;
            _rectSelection.Clear();
            InvalidateVisual();
            NotifyCaretMovedIfChanged();
        }

        private void PasteFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText()) return;

                string text = Clipboard.GetText();

                // Wrap the entire paste (selection delete + multi-line insert) atomically.
                using (_undoEngine.BeginTransaction("Paste"))
                {
                    if (!_selection.IsEmpty)
                        DeleteSelection();

                    _document.InsertText(new TextPosition(_cursorLine, _cursorColumn), text);

                    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    if (lines.Length == 1)
                        _cursorColumn += text.Length;
                    else
                    {
                        _cursorLine  += lines.Length - 1;
                        _cursorColumn = lines[lines.Length - 1].Length;
                    }
                }

                _selection.Clear();
                EnsureCursorVisible();
                InvalidateVisual();
            }
            catch (Exception)
            {
                // Silently ignore clipboard errors
            }
        }

        private void CutToClipboard()
        {
            // Feature A: rectangular selection takes priority.
            if (!_rectSelection.IsEmpty) { CutRectSelection(); return; }

            if (_selection.IsEmpty)
                return;

            CopyToClipboard();
            using (_undoEngine.BeginTransaction("Cut"))
                DeleteSelection();
        }

        private void DeleteSelection()
        {
            if (_selection.IsEmpty)
                return;

            var start = _selection.NormalizedStart;
            var end = _selection.NormalizedEnd;

            _document.DeleteRange(start, end);

            _cursorLine = start.Line;
            _cursorColumn = start.Column;
            _selection.Clear();
        }

        private void SelectAll()
        {
            if (_document.Lines.Count == 0)
                return;

            _selection.Start = new TextPosition(0, 0);
            _selection.End = new TextPosition(_document.Lines.Count - 1, _document.Lines[_document.Lines.Count - 1].Length);
            InvalidateVisual();
        }

        // -----------------------------------------------------------------------
        // Feature B — Text drag-and-drop commit logic
        // -----------------------------------------------------------------------

        /// <summary>
        /// Executes the text move: deletes source selection, adjusts drop position for the
        /// deletion offset, then inserts at the adjusted target.
        /// </summary>
        private void CommitTextDrop()
        {
            if (_document is null || IsReadOnly) return;

            var drop     = _dragDrop.DropPosition;
            var srcStart = _dragDrop.SelectionStart;
            var srcEnd   = _dragDrop.SelectionEnd;

            // Drop inside the original selection → cancel (no-op move).
            if (DragDropState.IsDropInsideSelection(drop, srcStart, srcEnd))
            {
                _selection.Start = srcStart;
                _selection.End   = srcEnd;
                _cursorLine   = srcEnd.Line;
                _cursorColumn = srcEnd.Column;
                InvalidateVisual();
                return;
            }

            string movedText = _document.GetText(srcStart, srcEnd);
            bool dropBefore  = drop < srcStart;

            _document.DeleteRange(srcStart, srcEnd);
            _selection.Clear();

            // If the drop target came after the deleted range, shift it to account for
            // the removed content.
            TextPosition insertAt = dropBefore
                ? drop
                : AdjustPositionAfterDelete(drop, srcStart, srcEnd);

            _document.InsertText(insertAt, movedText);

            _cursorLine   = insertAt.Line;
            _cursorColumn = insertAt.Column;
            InvalidateVisual();
            NotifyCaretMovedIfChanged();
        }

        /// <summary>
        /// Returns true when (line, col) falls inside the active rectangular selection block.
        /// </summary>
        private bool IsInsideRectBlock(int line, int col)
            => !_rectSelection.IsEmpty
               && line >= _rectSelection.TopLine    && line <= _rectSelection.BottomLine
               && col  >= _rectSelection.LeftColumn && col  <= _rectSelection.RightColumn;

        /// <summary>
        /// Executes a rect-block move: extracts the selected column block, deletes it, then
        /// re-inserts each row at the drop column, preserving the block's row count.
        /// </summary>
        private void CommitRectDrop()
        {
            if (_document is null || IsReadOnly) { _isRectDrag = false; _dragDrop.Reset(); return; }

            int topLine    = _rectSelection.TopLine;
            int bottomLine = _rectSelection.BottomLine;
            int leftCol    = _rectSelection.LeftColumn;
            int rightCol   = _rectSelection.RightColumn;
            int blockWidth = rightCol - leftCol;
            int blockHeight= bottomLine - topLine + 1;

            int dropLine   = _dragDrop.DropPosition.Line;
            int dropCol    = _dragDrop.DropPosition.Column;

            // Drop inside the original block → no-op.
            if (dropLine >= topLine && dropLine <= bottomLine
                && dropCol >= leftCol && dropCol <= rightCol)
            {
                _isRectDrag = false;
                _dragDrop.Reset();
                return;
            }

            // Snapshot block text before deletion.
            var lineTexts = _document.Lines.Select(l => l.Text).ToList();
            string blockText = _rectSelection.ExtractText(lineTexts);
            string[] blockLines = blockText.Split('\n');

            // Delete the source block (bottom-to-top to preserve indices).
            using (_undoEngine.BeginTransaction("Move Rectangular Block"))
            {
                for (int li = bottomLine; li >= topLine; li--)
                {
                    if (li >= _document.Lines.Count) continue;
                    var lineText = _document.Lines[li].Text;
                    int safeL = Math.Min(leftCol,  lineText.Length);
                    int safeR = Math.Min(rightCol, lineText.Length);
                    if (safeR > safeL)
                        _document.DeleteRange(new TextPosition(li, safeL), new TextPosition(li, safeR));
                }

                // Adjust drop column when drop is on an affected line and after the deleted block.
                if (dropLine >= topLine && dropLine <= bottomLine && dropCol > rightCol)
                    dropCol = Math.Max(leftCol, dropCol - blockWidth);

                // Insert each block row at the drop column.
                for (int i = 0; i < blockHeight; i++)
                {
                    int targetLine = dropLine + i;
                    if (targetLine >= _document.Lines.Count) break;
                    string lineContent = i < blockLines.Length ? blockLines[i] : string.Empty;
                    if (!string.IsNullOrEmpty(lineContent))
                        _document.InsertText(new TextPosition(targetLine, Math.Min(dropCol, _document.Lines[targetLine].Length)), lineContent);
                }
            }

            // Reposition rect selection at the new block location.
            _rectSelection.Begin(new TextPosition(dropLine, dropCol));
            _rectSelection.Extend(new TextPosition(Math.Min(dropLine + blockHeight - 1, _document.Lines.Count - 1), dropCol + blockWidth));

            _cursorLine   = dropLine;
            _cursorColumn = dropCol;
            _isRectDrag   = false;
            _dragDrop.Reset();
            InvalidateVisual();
            NotifyCaretMovedIfChanged();
        }

        /// <summary>
        /// Shifts <paramref name="pos"/> to account for the text that was deleted between
        /// <paramref name="delStart"/> and <paramref name="delEnd"/>.
        /// Used when the drop target lies after the deleted source range.
        /// </summary>
        private static TextPosition AdjustPositionAfterDelete(
            TextPosition pos,
            TextPosition delStart,
            TextPosition delEnd)
        {
            int newLine = pos.Line - (delEnd.Line - delStart.Line);
            int newCol  = pos.Column;

            // If the drop was on the same line where the deletion ended, adjust column.
            if (pos.Line == delEnd.Line)
                newCol = delStart.Column + (pos.Column - delEnd.Column);

            return new TextPosition(Math.Max(0, newLine), Math.Max(0, newCol));
        }

        #endregion
    }
}
