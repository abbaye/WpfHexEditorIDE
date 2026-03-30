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
        // ── Public LSP command API (called by host / MainWindow) ─────────────────

        /// <summary>Triggers Find All References at the current caret position.</summary>
        public Task FindAllReferencesAsync() => FindAllReferencesAsync(null, null);

        /// <summary>Triggers Go to Definition at the current caret position.</summary>
        public Task GoToDefinitionAsync() => GoToDefinitionAtCaretAsync();

        /// <summary>Triggers Go to Implementation at the current caret position.</summary>
        public Task GoToImplementationAsync() => GoToImplementationAtCaretAsync();

        // -- LSP Client Wiring (Phase 4) ──────────────────────────────────

        /// <summary>
        /// Injects or replaces the active LSP client.
        /// Call with <c>null</c> to detach (e.g., when closing a document).
        /// The method subscribes to <see cref="ILspClient.DiagnosticsReceived"/> and
        /// sends textDocument/didOpen when the editor already has a file loaded.
        /// </summary>
        // ── Debugger integration (ADR-DBG-01) ─────────────────────────────────────

        // ── BreakpointSettingsRequested event ─────────────────────────────────────

        /// <summary>
        /// Fired when the user clicks "Settings…" on the gutter right-click popup.
        /// The Debugger plugin subscribes to this to open <c>BreakpointConditionDialog</c>.
        /// Args: filePath, 1-based line number.
        /// </summary>
        public event Action<string, int>? BreakpointSettingsRequested;

        /// <summary>
        /// Wire the breakpoint gutter to a data source (injected by DebuggerService).
        /// Pass null to disconnect (session ended).
        /// </summary>
        public void SetBreakpointSource(IBreakpointSource? source)
        {
            _bpSource = source;
            _breakpointGutterControl?.SetBreakpointSource(source);
            _breakpointGutterControl?.SetFilePath(_currentFilePath);
        }

        /// <summary>
        /// Highlight the current execution line (1-based).
        /// Pass null to clear the highlight (session not paused).
        /// </summary>
        public void SetExecutionLine(int? oneBased)
        {
            _executionLineOneBased = oneBased;
            _breakpointGutterControl?.SetExecutionLine(oneBased);
            InvalidateVisual(); // redraw execution line highlight in content area
        }

        private void OnBreakpointRightClick(string filePath, int line1, double clickY)
        {
            if (_bpSource is null || _breakpointGutterControl is null) return;

            if (_bpInfoPopup is null)
            {
                _bpInfoPopup = new BreakpointInfoPopup();
                _bpInfoPopup.OpenSettingsRequested += (fp, ln) =>
                    BreakpointSettingsRequested?.Invoke(fp, ln);
            }

            // Position the popup to the right of the gutter, at the clicked line's Y.
            // PlacementMode.Relative offsets are relative to PlacementTarget (the gutter).
            var offset = new Point(BreakpointGutterControl.GutterWidth, clickY);
            _bpInfoPopup.Show(_breakpointGutterControl, _bpSource, filePath, line1, offset, _lineHeight);
        }

        // ── Breakpoint hover popup ────────────────────────────────────────────

        private BreakpointHoverPopup? _bpHoverPopup;
        private int _bpHoverLine = -1;
        private System.Windows.Threading.DispatcherTimer? _bpHoverTimer;

        internal void HandleBreakpointHover(int hoverLine0)
        {
            if (!ShowBreakpointLineHighlight || _bpSource is null || string.IsNullOrEmpty(_currentFilePath))
            {
                DismissBreakpointHover();
                return;
            }

            if (hoverLine0 == _bpHoverLine) return; // jitter guard
            _bpHoverLine = hoverLine0;
            _bpHoverTimer?.Stop();

            int line1 = hoverLine0 + 1;
            var info = _bpSource.GetBreakpoint(_currentFilePath, line1);
            if (info is null)
            {
                DismissBreakpointHover();
                return;
            }

            // Debounce 400ms before showing popup
            _bpHoverTimer ??= new System.Windows.Threading.DispatcherTimer();
            _bpHoverTimer.Interval = TimeSpan.FromMilliseconds(400);
            _bpHoverTimer.Tick -= OnBpHoverTimerTick;
            _bpHoverTimer.Tick += OnBpHoverTimerTick;
            _bpHoverTimer.Start();
        }

        private void OnBpHoverTimerTick(object? sender, EventArgs e)
        {
            _bpHoverTimer?.Stop();
            if (_bpSource is null || string.IsNullOrEmpty(_currentFilePath)) return;

            int line1 = _bpHoverLine + 1;
            var info = _bpSource.GetBreakpoint(_currentFilePath, line1);
            if (info is null) return;

            if (!_lineYLookup.TryGetValue(_bpHoverLine, out double lineY)) return;

            _bpHoverPopup ??= new BreakpointHoverPopup();
            _bpHoverPopup.EditConditionRequested -= OnBpHoverEditCondition;
            _bpHoverPopup.EditConditionRequested += OnBpHoverEditCondition;

            var anchorRect = new Rect(TextAreaLeftOffset, lineY, Math.Max(1, ActualWidth - TextAreaLeftOffset), _lineHeight);
            _bpHoverPopup.Show(this, _bpSource, _currentFilePath, line1, info,
                _document?.Lines, ExternalHighlighter, anchorRect);
        }

        private void OnBpHoverEditCondition(string filePath, int line1)
        {
            // _lineYLookup Y values are shared with the gutter — use directly.
            double clickY = _lineYLookup.TryGetValue(line1 - 1, out double ly) ? ly : 0;
            OnBreakpointRightClick(filePath, line1, clickY);
        }

        /// <summary>Gutter hover dwell — delegate to the same breakpoint hover pipeline.</summary>
        private void OnGutterBreakpointHover(string filePath, int line1)
        {
            HandleBreakpointHover(line1 - 1);
        }

        internal void DismissBreakpointHover()
        {
            _bpHoverTimer?.Stop();
            _bpHoverLine = -1;
            // Use the popup's own grace timer rather than immediate close,
            // so the user has time to move the mouse into the popup.
            _bpHoverPopup?.OnEditorMouseLeft();
        }

        // ─────────────────────────────────────────────────────────────────────────

        public void SetLspClient(WpfHexEditor.Editor.Core.LSP.ILspClient? client)
        {
            if (_lspClient is not null)
            {
                _lspClient.DiagnosticsReceived -= OnLspDiagnosticsReceived;
                if (_currentFilePath is not null)
                    _lspClient.CloseDocument(_currentFilePath);
            }

            _lspClient = client;
            _hoverQuickInfoService?.SetLspClient(client);
            _ctrlClickService?.SetLspClient(client);
            if (_smartCompletePopup is not null)
            {
                _smartCompletePopup.SetLspClient(client);
                _smartCompletePopup.CurrentFilePath = _currentFilePath;
            }
            _signatureHelpPopup!.IsOpen = false;
            _lspDocVersion = 0;

            if (_lspClient is null) return;

            _lspClient.DiagnosticsReceived += OnLspDiagnosticsReceived;

            // If a file is already open, send didOpen immediately.
            if (_currentFilePath is not null && _document is not null)
            {
                var langId = DetectLanguageId(_currentFilePath);
                _lspClient.OpenDocument(_currentFilePath, langId, _document.SaveToString());
            }

            // Create the change-debounce timer (300 ms) on first attach.
            if (_lspChangeTimer is null)
            {
                _lspChangeTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300),
                };
                _lspChangeTimer.Tick += (_, _) =>
                {
                    _lspChangeTimer.Stop();
                    if (_lspClient is null || _currentFilePath is null) return;
                    _lspClient.DidChange(_currentFilePath, ++_lspDocVersion, _document.SaveToString());
                };
            }
        }

        /// <summary>
        /// Injects the document manager for workspace-wide edit application (ILspAwareEditor).
        /// </summary>
        public void SetDocumentManager(IDocumentManager manager)
            => _lspDocumentManager = manager;

        /// <summary>Maps a file extension to an LSP language identifier.</summary>
        private static string DetectLanguageId(string filePath) =>
            Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".json" or ".jsonc" => "json",
                ".xml"              => "xml",
                ".xaml"             => "xaml",
                ".cs"               => "csharp",
                ".vb"               => "vbnet",
                ".fs" or ".fsx" or ".fsi" => "fsharp",
                ".ts"               => "typescript",
                ".js"               => "javascript",
                ".py"               => "python",
                ".lua"              => "lua",
                _                   => "plaintext",
            };

        /// <summary>
        /// Calls textDocument/signatureHelp and shows the result in <see cref="_signatureHelpPopup"/>.
        /// Fire-and-forget: errors are swallowed to never break typing.
        /// </summary>
        private async Task TriggerSignatureHelpAsync()
        {
            if (_lspClient is null || _currentFilePath is null) return;
            try
            {
                var sig = await _lspClient.SignatureHelpAsync(
                    _currentFilePath, _cursorLine, _cursorColumn, CancellationToken.None)
                    .ConfigureAwait(false);
                if (sig is null) return;
                await Dispatcher.InvokeAsync(() =>
                {
                    _signatureHelpPopup?.Show(sig, ComputeCaretScreenPoint(belowCaret: false));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LSP] SignatureHelp: {ex.Message}");
            }
        }

        // ── LSP Code Actions (Ctrl+.) ─────────────────────────────────────────────

        /// <summary>
        /// Invokes textDocument/codeAction at the caret position and shows a popup
        /// listing the available quick fixes / refactors.
        /// Fire-and-forget: errors are swallowed so they never break editing.
        /// </summary>
        private async Task ShowCodeActionsAsync()
        {
            if (_lspClient is null || _currentFilePath is null) return;
            try
            {
                var actions = await _lspClient.CodeActionAsync(
                    _currentFilePath,
                    _cursorLine, _cursorColumn,
                    _cursorLine, _cursorColumn,
                    CancellationToken.None).ConfigureAwait(true);

                if (actions.Count == 0) return;

                var screenPt = ComputeCaretScreenPoint(belowCaret: true);

                var selected = await _lspCodeActionPopup!
                    .ShowAsync(actions, screenPt.X, screenPt.Y).ConfigureAwait(true);

                if (selected?.Edit is not null)
                    ApplyWorkspaceEdit(selected.Edit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LSP] CodeAction: {ex.Message}");
            }
        }

        // ── LSP Rename (F2) ───────────────────────────────────────────────────────

        /// <summary>
        /// Shows an inline rename popup over the caret, then applies the workspace edit
        /// returned by textDocument/rename.
        /// </summary>
        private async Task StartRenameAsync()
        {
            if (_lspClient is null || _currentFilePath is null) return;
            try
            {
                var currentWord = GetWordAtCaret();

                var screenPt = ComputeCaretScreenPoint(belowCaret: false);

                var newName = await _lspRenamePopup!
                    .ShowAsync(currentWord, screenPt.X, screenPt.Y).ConfigureAwait(true);

                if (string.IsNullOrEmpty(newName) || newName == currentWord) return;

                var edit = await _lspClient.RenameAsync(
                    _currentFilePath, _cursorLine, _cursorColumn, newName,
                    CancellationToken.None).ConfigureAwait(true);

                if (edit is not null)
                    ApplyWorkspaceEdit(edit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LSP] Rename: {ex.Message}");
            }
        }

        // ── Workspace Edit Application ────────────────────────────────────────────

        /// <summary>
        /// Applies a workspace edit to all affected open buffers.
        /// Edits within each file are applied bottom-up to avoid offset drift.
        /// </summary>
        private void ApplyWorkspaceEdit(LspWorkspaceEdit edit)
        {
            foreach (var (filePath, edits) in edit.Changes)
            {
                var buf = _lspDocumentManager?.GetBufferForFile(filePath)
                       ?? (_buffer?.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase) == true
                           ? _buffer : null);
                if (buf is null) continue;

                var ordered = edits
                    .OrderByDescending(e => e.StartLine)
                    .ThenByDescending(e => e.StartColumn);

                var text = buf.Text;
                foreach (var e in ordered)
                    text = ApplyTextEdit(text, e);

                buf.SetText(text, source: null);
            }
        }

        /// <summary>Applies a single <see cref="LspTextEdit"/> to a text string (0-based coordinates).</summary>
        private static string ApplyTextEdit(string text, LspTextEdit edit)
        {
            var lines = text.Split('\n');
            if (edit.StartLine >= lines.Length) return text;

            var startLine = lines[edit.StartLine];
            var endLine   = edit.EndLine < lines.Length ? lines[edit.EndLine] : string.Empty;

            var startCol = Math.Min(edit.StartColumn, startLine.Length);
            var endCol   = Math.Min(edit.EndColumn,   endLine.Length);

            var before = startLine[..startCol];
            var after  = endLine[endCol..];

            var newLines = new List<string>(lines.Length);
            for (var i = 0; i < edit.StartLine; i++) newLines.Add(lines[i]);
            newLines.Add(before + edit.NewText + after);
            for (var i = edit.EndLine + 1; i < lines.Length; i++) newLines.Add(lines[i]);

            return string.Join("\n", newLines);
        }

        /// <summary>Returns the word under the current caret position (identifier chars only).</summary>
        private string GetWordAtCaret()
        {
            if (_document is null || _cursorLine >= _document.Lines.Count) return string.Empty;
            var line = _document.Lines[_cursorLine].Text ?? string.Empty;
            if (_cursorColumn > line.Length) return string.Empty;

            var start = _cursorColumn;
            while (start > 0 && (char.IsLetterOrDigit(line[start - 1]) || line[start - 1] == '_'))
                start--;

            var end = _cursorColumn;
            while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
                end++;

            return line[start..end];
        }

        // ── Navigation History (Alt+Left / Alt+Right) ────────────────────────────

        private readonly record struct NavEntry(string? FilePath, int Line, int Column);
        private readonly List<NavEntry> _navHistory = new(64);
        private int _navIndex = -1;

        private void PushNavigation(string? filePath, int line, int col)
        {
            // Truncate any forward history on new navigation.
            if (_navIndex < _navHistory.Count - 1)
                _navHistory.RemoveRange(_navIndex + 1, _navHistory.Count - _navIndex - 1);
            _navHistory.Add(new NavEntry(filePath, line, col));
            if (_navHistory.Count > 50) _navHistory.RemoveAt(0);
            _navIndex = _navHistory.Count - 1;
        }

        private void NavigateBack()
        {
            if (_navIndex <= 0) return;
            _navIndex--;
            ApplyNavEntry(_navHistory[_navIndex]);
        }

        private void NavigateForward()
        {
            if (_navIndex >= _navHistory.Count - 1) return;
            _navIndex++;
            ApplyNavEntry(_navHistory[_navIndex]);
        }

        private void ApplyNavEntry(NavEntry entry)
        {
            if (entry.FilePath is null
                || entry.FilePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
            {
                NavigateToLine(entry.Line);
            }
            else
            {
                ReferenceNavigationRequested?.Invoke(this, new ReferencesNavigationEventArgs
                {
                    FilePath = entry.FilePath,
                    Line     = entry.Line   + 1,
                    Column   = entry.Column + 1
                });
            }
        }

        // ── Keyboard-shortcut definition navigation helpers ───────────────────────

        /// <summary>
        /// Invoked by F12 — resolves definition at the caret and navigates.
        /// </summary>
        private async Task GoToDefinitionAtCaretAsync()
        {
            var word = GetWordAtCaret();
            if (string.IsNullOrEmpty(word)) return;
            var zone = new SymbolHitZone(_cursorLine, _cursorColumn,
                _cursorColumn + word.Length, word, string.Empty, 0, 0, false);
            await NavigateToDefinitionAsync(zone).ConfigureAwait(true);
        }

        /// <summary>
        /// Invoked by Alt+F12 — shows a Peek Definition popup below the caret.
        /// </summary>
        private async Task ShowPeekDefinitionAsync()
        {
            _foldPeekPopup ??= new FoldPeekPopup();
            _foldPeekPopup.GoToDefinitionRequested = () => _ = GoToDefinitionAtCaretAsync();

            var word = _hoveredSymbolZone?.SymbolName ?? GetWordAtCaret();

            await _foldPeekPopup.ShowDefinitionAsync(this, word, async () =>
            {
                if (_lspClient?.IsInitialized == true && _currentFilePath is not null)
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var locs = await _lspClient.DefinitionAsync(
                        _currentFilePath, _cursorLine, _cursorColumn, cts.Token).ConfigureAwait(true);
                    if (locs.Count > 0)
                    {
                        var loc  = locs[0];
                        bool isMeta = loc.Uri.Contains("metadata:", StringComparison.OrdinalIgnoreCase);
                        if (!isMeta && Uri.TryCreate(loc.Uri, UriKind.Absolute, out var u)
                            && System.IO.File.Exists(u.LocalPath))
                        {
                            var text = await System.IO.File.ReadAllTextAsync(u.LocalPath).ConfigureAwait(true);
                            return (text, loc.StartLine + 1);
                        }
                    }
                }
                return (string.Empty, 0);
            }).ConfigureAwait(true);
        }

        /// <summary>
        /// Invoked by Ctrl+F12 — go to all implementations of the symbol at caret.
        /// </summary>
        private async Task GoToImplementationAtCaretAsync()
        {
            if (_lspClient?.IsInitialized != true || _currentFilePath is null) return;
            PushNavigation(_currentFilePath, _cursorLine, _cursorColumn);
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var locs = await _lspClient.ImplementationAsync(
                    _currentFilePath, _cursorLine, _cursorColumn, cts.Token).ConfigureAwait(true);
                if (locs.Count > 0)
                    await HandleDefinitionLocationsAsync(locs, GetWordAtCaret()).ConfigureAwait(true);
            }
            catch { /* LSP unavailable — silently ignore */ }
        }

        /// Feeds LSP push diagnostics into the editor's validation error list.
        /// Always called on the UI thread (guaranteed by LspClientImpl).
        /// </summary>
        private void OnLspDiagnosticsReceived(
            object? sender,
            WpfHexEditor.Editor.Core.LSP.LspDiagnosticsReceivedEventArgs e)
        {
            if (_currentFilePath is null) return;
            if (!new Uri(_currentFilePath).AbsoluteUri.Equals(e.DocumentUri, StringComparison.OrdinalIgnoreCase))
                return;

            // Remove any previous LSP-sourced errors and replace with the new set.
            _validationErrors.RemoveAll(v => v.Layer == Models.ValidationLayer.Lsp);
            foreach (var d in e.Diagnostics)
            {
                _validationErrors.Add(new Models.ValidationError
                {
                    Line     = d.StartLine,
                    Column   = d.StartColumn,
                    Message  = d.Message,
                    Severity = d.Severity == "error"   ? Models.ValidationSeverity.Error
                             : d.Severity == "warning" ? Models.ValidationSeverity.Warning
                                                       : Models.ValidationSeverity.Info,
                    Layer    = Models.ValidationLayer.Lsp,
                });
            }
            RebuildValidationIndex();
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);

            // Coalesce rapid diagnostic batches into a single render pass (OPT-PERF-05).
            if (!_diagnosticsRenderPending)
            {
                _diagnosticsRenderPending = true;
                Dispatcher.InvokeAsync(() =>
                {
                    if (!_diagnosticsRenderPending) return;
                    _diagnosticsRenderPending = false;
                    InvalidateVisual();
                }, System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        // -- Find All References (LSP) ------------------------------------

        /// <summary>
        /// Returns the identifier word (letters, digits, underscore) at the current caret
        /// position, or <see cref="string.Empty"/> when the caret is not on a word character.
        /// Reuses the same boundary logic as <see cref="SelectWordAtPosition"/>.
        /// </summary>
        private string GetWordAtCursor()
        {
            if (_document is null
                || _cursorLine  < 0
                || _cursorLine  >= _document.Lines.Count)
                return string.Empty;

            var lineText = _document.Lines[_cursorLine].Text;
            if (string.IsNullOrEmpty(lineText) || _cursorColumn > lineText.Length)
                return string.Empty;

            // Snap column to valid range
            int col   = Math.Min(_cursorColumn, lineText.Length - 1);
            if (!IsWordChar(lineText[col]))
                return string.Empty;

            int start = col;
            int end   = col;

            while (start > 0 && IsWordChar(lineText[start - 1]))
                start--;

            while (end < lineText.Length && IsWordChar(lineText[end]))
                end++;

            return lineText[start..end];
        }

        /// <summary>
        /// Invokes <c>textDocument/references</c> for the symbol at the caret, groups
        /// the results by file, reads snippets, then shows <see cref="ReferencesPopup"/>.
        /// </summary>
        private async Task FindAllReferencesAsync(int? lineOverride = null, string? symbolOverride = null)
        {
            if (_document is null) return;

            // When called from a InlineHints click, line/symbol are supplied directly
            // so the caret is never mutated. The Shift+F12 path supplies no overrides
            // and reads _cursorLine / _cursorColumn as before.
            int    line   = lineOverride ?? _cursorLine;
            int    column = lineOverride.HasValue
                                ? FindSymbolColumnInLine(line, symbolOverride ?? string.Empty)
                                : _cursorColumn;
            string symbol = symbolOverride ?? GetWordAtCursor();

            if (string.IsNullOrEmpty(symbol))
            {
                StatusMessage?.Invoke(this, "Place the caret on a symbol to find references.");
                return;
            }

            List<ReferenceGroup> groups;

            // ── LSP path (preferred when a language server is running) ─────────
            if (_lspClient?.IsInitialized == true && _currentFilePath is not null)
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                IReadOnlyList<WpfHexEditor.Editor.Core.LSP.LspLocation> locations;
                try
                {
                    locations = await _lspClient.ReferencesAsync(
                        _currentFilePath, line, column, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    StatusMessage?.Invoke(this, "Find References timed out — falling back to local scan.");
                    locations = Array.Empty<WpfHexEditor.Editor.Core.LSP.LspLocation>();
                }

                if (locations.Count > 0)
                {
                    groups = BuildGroupsFromLspLocations(locations, symbol);
                    ShowReferencesPopup(groups, symbol, locations.Count, source: "LSP", line: line, column: column);
                    return;
                }
                // Fall through to local scan when LSP returns no results.
            }

            // ── Local/workspace scan fallback ────────────────────────────────
            // When a solution is loaded, search across all files of the same
            // extension; otherwise fall back to the current document only.
            groups = BuildGroupsFromWorkspaceScan(symbol);
            int total = groups.Sum(g => g.Items.Count);

            if (total == 0)
            {
                StatusMessage?.Invoke(this, $"No occurrences of '{symbol}' found.");
                return;
            }

            ShowReferencesPopup(groups, symbol, total, source: "workspace", line: line, column: column);
        }

        /// <summary>
        /// Scans the current in-memory document AND all solution files of the same
        /// file extension for whole-word occurrences of <paramref name="symbol"/>.
        /// Falls back to current-document-only scan when no solution is loaded.
        /// </summary>
        private List<ReferenceGroup> BuildGroupsFromWorkspaceScan(string symbol)
        {
            // Step 1 — always scan the current in-memory document.
            var groups = BuildGroupsFromLocalScan(symbol);

            // Step 2 — scan workspace files of matching extension.
            var ext = Path.GetExtension(_currentFilePath ?? string.Empty);
            if (string.IsNullOrEmpty(ext)) return groups;

            var workspacePaths = WorkspaceFileCache.GetPathsForExtensions([ext]);
            foreach (var path in workspacePaths)
            {
                if (path.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
                    continue; // already covered by the in-memory scan above

                var lines = WorkspaceFileCache.GetLines(path);
                if (lines is null) continue;

                var items = new List<ReferenceItem>();
                for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
                {
                    var lineText = lines[lineIdx];
                    if (string.IsNullOrEmpty(lineText)) continue;

                    int col = 0;
                    while (true)
                    {
                        int pos = lineText.IndexOf(symbol, col, StringComparison.Ordinal);
                        if (pos < 0) break;

                        bool leftOk  = pos == 0                       || !IsWordChar(lineText[pos - 1]);
                        bool rightOk = pos + symbol.Length >= lineText.Length
                                       || !IsWordChar(lineText[pos + symbol.Length]);

                        if (leftOk && rightOk)
                        {
                            var snippet = lineText.Length > 200 ? lineText[..200] : lineText;
                            items.Add(new ReferenceItem
                            {
                                Line    = lineIdx,
                                Column  = pos,
                                Snippet = snippet.TrimStart()
                            });
                        }

                        col = pos + symbol.Length;
                    }
                }

                if (items.Count > 0)
                    groups.Add(new ReferenceGroup
                    {
                        FilePath     = path,
                        DisplayLabel = Path.GetFileName(path),
                        Items        = items
                    });
            }

            return groups;
        }

        /// <summary>
        /// Converts LSP location results into <see cref="ReferenceGroup"/> list,
        /// reading snippets from the in-memory document or disk.
        /// </summary>
        private List<ReferenceGroup> BuildGroupsFromLspLocations(
            IReadOnlyList<WpfHexEditor.Editor.Core.LSP.LspLocation> locations,
            string symbol)
        {
            var byFile = new Dictionary<string, List<WpfHexEditor.Editor.Core.LSP.LspLocation>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var loc in locations)
            {
                var path = UriToFilePath(loc.Uri);
                if (!byFile.TryGetValue(path, out var list))
                    byFile[path] = list = new List<WpfHexEditor.Editor.Core.LSP.LspLocation>();
                list.Add(loc);
            }

            var groups = new List<ReferenceGroup>(byFile.Count);
            foreach (var (filePath, locs) in byFile)
            {
                string[]? lines = null;
                if (filePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
                    lines = _document.Lines.Select(l => l.Text).ToArray();
                else if (File.Exists(filePath))
                    lines = File.ReadAllLines(filePath);

                var items = new List<ReferenceItem>(locs.Count);
                foreach (var loc in locs)
                {
                    var raw = (lines != null && loc.StartLine < lines.Length)
                        ? lines[loc.StartLine]
                        : string.Empty;
                    if (raw.Length > 200) raw = raw[..200];

                    items.Add(new ReferenceItem
                    {
                        Line    = loc.StartLine,
                        Column  = loc.StartColumn,
                        Snippet = raw.TrimStart()
                    });
                }

                groups.Add(new ReferenceGroup
                {
                    FilePath     = filePath,
                    DisplayLabel = Path.GetFileName(filePath),
                    Items        = items
                });
            }

            return groups;
        }

        /// <summary>
        /// Scans the current in-memory document for all whole-word occurrences of
        /// <paramref name="symbol"/> and returns them as a single-file
        /// <see cref="ReferenceGroup"/>. Used when no LSP client is available.
        /// </summary>
        private List<ReferenceGroup> BuildGroupsFromLocalScan(string symbol)
        {
            var items = new List<ReferenceItem>();

            for (int lineIdx = 0; lineIdx < _document.Lines.Count; lineIdx++)
            {
                var lineText = _document.Lines[lineIdx].Text;
                if (string.IsNullOrEmpty(lineText)) continue;

                int col = 0;
                while (true)
                {
                    int pos = lineText.IndexOf(symbol, col, StringComparison.Ordinal);
                    if (pos < 0) break;

                    // Whole-word boundary check — skip if adjacent chars are word chars.
                    bool leftOk  = pos == 0                       || !IsWordChar(lineText[pos - 1]);
                    bool rightOk = pos + symbol.Length >= lineText.Length
                                   || !IsWordChar(lineText[pos + symbol.Length]);

                    if (leftOk && rightOk)
                    {
                        var snippet = lineText.Length > 200 ? lineText[..200] : lineText;
                        items.Add(new ReferenceItem
                        {
                            Line    = lineIdx,
                            Column  = pos,
                            Snippet = snippet.TrimStart()
                        });
                    }

                    col = pos + symbol.Length;
                }
            }

            if (items.Count == 0) return new List<ReferenceGroup>();

            return new List<ReferenceGroup>
            {
                new ReferenceGroup
                {
                    FilePath     = _currentFilePath ?? string.Empty,
                    DisplayLabel = _currentFilePath is not null
                                   ? Path.GetFileName(_currentFilePath)
                                   : "(unsaved document)",
                    Items        = items
                }
            };
        }

        /// <summary>
        /// Computes the anchor Point, shows the popup and updates the status bar.
        /// </summary>
        private void ShowReferencesPopup(
            List<ReferenceGroup> groups, string symbol, int total, string source,
            int line = -1, int column = -1)
        {
            // Defaults: Shift+F12 path supplies no overrides, reads cursor fields.
            if (line   < 0) line   = _cursorLine;
            if (column < 0) column = _cursorColumn;

            int visLineOffset = line - _firstVisibleLine;
            double lh = _lineHeight > 0 ? _lineHeight : 16.0;
            double cw = _charWidth  > 0 ? _charWidth  : 8.0;
            double x  = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + column * cw;

            // Prefer the actual rendered hit-zone Top as anchor Y.
            // _lineYLookup can accumulate rounding errors when many InlineHints lines appear
            // above the declaration, causing the popup to float too high.
            double anchorY = -1;
            foreach (var (hz, hzLine, _) in _hintsHitZones)
            {
                if (hzLine == line) { anchorY = hz.Top; break; }
            }
            if (anchorY < 0)
            {
                // Shift+F12 / hint not currently rendered — fall back to _lineYLookup.
                double codeY = _lineYLookup.TryGetValue(line, out double ly)
                    ? ly : TopMargin + visLineOffset * lh;
                anchorY = codeY - HintLineHeight;
            }

            var anchor = new Point(x, anchorY);

            // Resolve kind icon from lens data (null-safe: Shift+F12 invocations have no lens entry).
            string iconGlyph = "\uE8A5";
            System.Windows.Media.Brush iconBrush = System.Windows.Media.Brushes.Gray;
            if (_hintsData.TryGetValue(line, out var lensEntry))
            {
                iconGlyph = lensEntry.IconGlyph;
                iconBrush = lensEntry.IconBrush;
            }

            // Store latest results so pin handler can forward them to the dock host.
            _lastReferenceGroups = groups;
            _lastReferenceSymbol = symbol;

            _referencesPopup ??= new ReferencesPopup();
            _referencesPopup.NavigationRequested -= OnReferencesNavigationRequested;
            _referencesPopup.NavigationRequested += OnReferencesNavigationRequested;
            _referencesPopup.RefreshRequested    -= OnPopupRefreshRequested;
            _referencesPopup.RefreshRequested    += OnPopupRefreshRequested;
            _referencesPopup.PinRequested        -= OnPopupPinRequested;
            _referencesPopup.PinRequested        += OnPopupPinRequested;

            _referencesPopup.Show(this, groups, symbol, anchor, lh, iconGlyph, iconBrush);
            StatusMessage?.Invoke(this,
                $"{total} occurrence{(total != 1 ? "s" : "")} of '{symbol}' ({source}).");
        }

        /// <summary>Converts a <c>file:///</c> URI to a local file-system path.</summary>
        private static string UriToFilePath(string uri)
        {
            if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(uri[8..]).Replace('/', Path.DirectorySeparatorChar);
            if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(uri[7..]).Replace('/', Path.DirectorySeparatorChar);
            return uri;
        }

        /// <summary>
        /// Routes a reference-popup navigation event: same-file → local scroll;
        /// different file → propagates to the IDE host.
        /// </summary>
        private void OnReferencesNavigationRequested(
            object? sender, ReferencesNavigationEventArgs e)
        {
            _referencesPopup?.Close();

            if (e.FilePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
                NavigateToLine(e.Line);
            else
                ReferenceNavigationRequested?.Invoke(this, e);
        }

        private async void OnPopupRefreshRequested(object? sender, EventArgs e)
            => await FindAllReferencesAsync();

        private void OnPopupPinRequested(object? sender, EventArgs e)
        {
            _referencesPopup?.Close();
            FindAllReferencesDockRequested?.Invoke(this, new FindAllReferencesDockEventArgs
            {
                Groups     = _lastReferenceGroups,
                SymbolName = _lastReferenceSymbol
            });
        }

        /// <summary>
        /// Returns true when <paramref name="originalSource"/> lives in the same Win32 HWND
        /// (PresentationSource) as the references popup — i.e., the click originated inside
        /// the popup's own layered window, not in the CodeEditor window.
        /// <para>
        /// VisualTreeHelper.GetParent cannot cross HWND boundaries, so an HWND-aware check
        /// via <see cref="PresentationSource.FromVisual"/> is required for AllowsTransparency
        /// popups that live in their own HwndSource.
        /// </para>
        /// </summary>
        private bool IsEventFromInsidePopup(object? originalSource)
        {
            if (_referencesPopup?.IsOpen != true || _referencesPopup.Child is null)
                return false;
            if (originalSource is not Visual visual)
                return false;

            // Compare HwndSource instances: equal ⟹ same HWND ⟹ click is from the popup.
            var clickSource = PresentationSource.FromVisual(visual);
            var popupSource = PresentationSource.FromVisual(_referencesPopup.Child);
            return clickSource is not null && ReferenceEquals(clickSource, popupSource);
        }

        /// <summary>
        /// Returns true when the click position (relative to this editor) maps to a screen
        /// point that falls within the references popup's bounding rectangle.
        /// Belt-and-suspenders fallback for the Win32 click-through scenario where
        /// <see cref="IsEventFromInsidePopup"/> cannot detect the popup source.
        /// </summary>
        private bool IsClickInsidePopupBounds(Point posRelativeToThis)
        {
            if (_referencesPopup?.IsOpen != true || _referencesPopup.Child is not UIElement child)
                return false;
            try
            {
                var screenPt     = PointToScreen(posRelativeToThis);
                var popupTopLeft = child.PointToScreen(new Point(0, 0));
                return new Rect(popupTopLeft, child.RenderSize).Contains(screenPt);
            }
            catch { return false; }
        }

        // -- Public methods (IDocumentEditor) -----------------------------

        void IDocumentEditor.Copy() => CopyToClipboard();
        void IDocumentEditor.Cut() => CutToClipboard();
        void IDocumentEditor.Paste() => PasteFromClipboard();
        void IDocumentEditor.Delete() => DeleteSelection();
        void IDocumentEditor.SelectAll() => SelectAll();

        public void Close()
        {
            // Notify the LSP server before clearing the path (Phase 4).
            if (_lspClient?.IsInitialized == true && _currentFilePath is not null)
                _lspClient.CloseDocument(_currentFilePath);

            _quickInfoPopup?.Hide();
            _hoverQuickInfoService?.Dispose();
            _hoverQuickInfoService = null;
            DismissEndBlockHint();
            _endBlockHintPopup?.Dispose();
            _endBlockHintPopup = null;
            _ctrlClickService?.Dispose();
            _ctrlClickService = null;

            // Do NOT replace _document with a new empty instance here.
            // Replacing it causes an immediate blank re-render via InvalidateVisual(),
            // which persists whenever Close() is called before OpenAsync() completes
            // (e.g. during tab-reload or rapid file switching). The existing document
            // content stays visible until OpenAsync() loads the next file.
            _currentFilePath = null;
            if (_smartCompletePopup is not null) _smartCompletePopup.CurrentFilePath = null;
            _isDirty = false;
            _cursorLine = 0;
            _cursorColumn = 0;
            _selection.Clear();
            _undoEngine.Reset();
            _validationErrors.Clear();
            // Do NOT call InvalidateVisual() — the tab is either being removed from the
            // visual tree (no render needed) or OpenAsync() will trigger the next render.
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, BuildTitle());
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        }

        // -- Events --------------------------------------------------------

        /// <summary>Current caret line (0-based). Updated after every cursor movement.</summary>
        public int CursorLine => _cursorLine;

        /// <summary>Fired when the caret moves to a different line (debounced to line-level changes).</summary>
        public event EventHandler? CaretMoved;

        /// <summary>
        /// Exposes the folding engine so external adapters (e.g. EditorEventAdapter) can
        /// subscribe to <see cref="Folding.FoldingEngine.RegionsChanged"/> without coupling
        /// to the internal field.
        /// </summary>
        public Folding.FoldingEngine? FoldingEngine => _foldingEngine;

        /// <summary>
        /// Currently selected text, bounded to 4096 characters.
        /// Returns <see cref="string.Empty"/> when nothing is selected.
        /// </summary>
        public string SelectedText
        {
            get
            {
                if (_selection.IsEmpty) return string.Empty;
                var raw = _document.GetText(_selection.NormalizedStart, _selection.NormalizedEnd);
                return raw.Length > 4096 ? raw[..4096] : raw;
            }
        }

        /// <summary>
        /// Raised when the user navigates to a reference in a different file via the
        /// Find All References popup. The host should open the target file and move
        /// the caret to the specified position.
        /// </summary>
        public event EventHandler<ReferencesNavigationEventArgs>? ReferenceNavigationRequested;

        /// <summary>
        /// Raised when the user pins the References popup into a docked panel.
        /// The host should open (or activate) a <see cref="FindReferencesPanel"/>
        /// and call <c>Refresh</c> with the supplied groups.
        /// </summary>
        public event EventHandler<FindAllReferencesDockEventArgs>? FindAllReferencesDockRequested;

        public event EventHandler? ModifiedChanged;
        public event EventHandler? CanUndoChanged;
        public event EventHandler? CanRedoChanged;
        public event EventHandler<string>? TitleChanged;
        public event EventHandler<string>? StatusMessage;
        public event EventHandler<string>? OutputMessage;
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Raised when Ctrl+Click targets an external symbol (e.g. BCL / NuGet assembly).
        /// The IDE host should route this to AssemblyExplorer or open a decompiled-source tab.
        /// </summary>
        public event EventHandler<GoToExternalDefinitionEventArgs>? GoToExternalDefinitionRequested;

        // -- Long-running operations (no-op: CodeEditor has no async operations) --
        public bool IsBusy => false;
        public void CancelOperation() { }
        public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
        public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
        public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

        // -- Helpers -------------------------------------------------------

        private string BuildTitle()
        {
            var name = !string.IsNullOrEmpty(_currentFilePath)
                ? Path.GetFileName(_currentFilePath)
                : "untitled.json";
            return _isDirty ? name + " *" : name;
        }

        // ═══════════════════════════════════════════════════════════════════
        // IBufferAwareEditor
        // ═══════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public void AttachBuffer(IDocumentBuffer buffer)
        {
            if (_buffer is not null) DetachBuffer();
            _buffer = buffer;

            // Push current editor content into the buffer (editor is authoritative on attach).
            _suppressBufferSync = true;
            try   { buffer.SetText(GetText(), source: this); }
            finally { _suppressBufferSync = false; }

            buffer.Changed += OnBufferChanged;
        }

        /// <inheritdoc/>
        public void DetachBuffer()
        {
            if (_buffer is null) return;
            _buffer.Changed -= OnBufferChanged;
            _buffer = null;
        }

        private void OnBufferChanged(object? sender, DocumentBufferChangedEventArgs e)
        {
            // Ignore changes we originated to prevent feedback loops.
            if (_suppressBufferSync || ReferenceEquals(e.Source, this)) return;

            // Another editor updated the buffer — sync our content.
            _suppressBufferSync = true;
            try   { _document.LoadFromString(e.NewText); }
            finally { _suppressBufferSync = false; }

            _undoEngine.Reset();
            InvalidateVisual();
        }

        // -- IPropertyProviderSource -------------------------------------------
        private WpfHexEditor.Editor.CodeEditor.CodeEditorPropertyProvider? _propertyProvider;
        public IPropertyProvider? GetPropertyProvider()
            => _propertyProvider ??= new WpfHexEditor.Editor.CodeEditor.CodeEditorPropertyProvider(this);

        // ═══════════════════════════════════════════════════════════════════
        // IRefreshTimeReporter
        // ═══════════════════════════════════════════════════════════════════

        /// <inheritdoc />
        public StatusBarItem? RefreshTimeStatusBarItem => _sbRefreshTime;

        // ═══════════════════════════════════════════════════════════════════
        // IStatusBarContributor
        // ═══════════════════════════════════════════════════════════════════

        private ObservableCollection<StatusBarItem>? _jsonStatusBarItems;
        private StatusBarItem _sbLanguage  = null!;
        private StatusBarItem _sbPosition  = null!;
        private StatusBarItem _sbZoom      = null!;
        private StatusBarItem _sbSelection = null!;

        /// <summary>Current caret column (0-based). Companion to <see cref="CursorLine"/>.</summary>
        public int CursorColumn => _cursorColumn;

        public ObservableCollection<StatusBarItem> StatusBarItems
            => _jsonStatusBarItems ??= BuildJsonStatusBarItems();

        private ObservableCollection<StatusBarItem> BuildJsonStatusBarItems()
        {
            _sbLanguage  = new StatusBarItem { Label = "Language", Tooltip = "Detected syntax language" };
            _sbPosition  = new StatusBarItem { Label = "Position", Tooltip = "Caret line and column" };
            _sbZoom      = new StatusBarItem { Label = "Zoom",     Tooltip = "Editor zoom level" };
            _sbSelection = new StatusBarItem { Label = "Sel",      Tooltip = "Number of selected characters", IsVisible = false };

            // Zoom preset choices — selecting one applies the zoom level immediately.
            foreach (var (pct, factor) in new (string, double)[] { ("50%", 0.5), ("75%", 0.75), ("100%", 1.0), ("125%", 1.25), ("150%", 1.5), ("200%", 2.0) })
            {
                var capture = factor;
                _sbZoom.Choices.Add(new StatusBarChoice
                {
                    DisplayName = pct,
                    Command     = new JsonRelayCommand(_ => ZoomLevel = capture),
                });
            }

            // Wire live-update events once (lazy-init guard ensures single subscription).
            CaretMoved       += (_, _) => RefreshJsonStatusBarItems();
            ZoomLevelChanged += (_, _) => RefreshJsonStatusBarItems();
            SelectionChanged += (_, _) => RefreshJsonStatusBarItems();

            RefreshJsonStatusBarItems();
            return new ObservableCollection<StatusBarItem> { _sbLanguage, _sbPosition, _sbZoom, _sbSelection };
        }

        void IStatusBarContributor.RefreshStatusBarItems() => RefreshJsonStatusBarItems();

        internal void RefreshJsonStatusBarItems()
        {
            if (_jsonStatusBarItems is null) return;

            _sbLanguage.Value = ExternalHighlighter?.LanguageName ?? _highlighter.LanguageName ?? "JSON";
            _sbPosition.Value = $"Ln {_cursorLine + 1}, Col {_cursorColumn + 1}";
            _sbZoom.Value     = $"{(int)(ZoomLevel * 100)}%";

            bool hasSelection = !_selection.IsEmpty;
            _sbSelection.IsVisible = hasSelection;
            if (hasSelection)
            {
                int charCount = _selection.IsMultiLine
                    ? (_document?.GetText(_selection.NormalizedStart, _selection.NormalizedEnd).Length ?? 0)
                    : Math.Abs(_selection.NormalizedEnd.Column - _selection.NormalizedStart.Column);
                _sbSelection.Value = charCount.ToString();
            }

            // Keep zoom choice checkmarks in sync.
            string zoomLabel = $"{(int)(ZoomLevel * 100)}%";
            foreach (var choice in _sbZoom.Choices)
                choice.IsActive = choice.DisplayName == zoomLabel;
        }

        // ═══════════════════════════════════════════════════════════════════
        // IEditorPersistable
        // Persists caret position, scroll offset, and syntax language id.
        // Binary changeset (byte-level) is not applicable to a text editor —
        // GetChangesetSnapshot returns Empty and ApplyChangeset is a no-op.
        // ═══════════════════════════════════════════════════════════════════

        EditorConfigDto IEditorPersistable.GetEditorConfig()
        {
            var extra = new Dictionary<string, string>
            {
                ["wordWrap"] = IsWordWrapEnabled ? "1" : "0"
            };
            return new EditorConfigDto
            {
                CaretLine        = _cursorLine + 1,   // store 1-based
                CaretColumn      = _cursorColumn + 1,
                FirstVisibleLine = (int)(_verticalScrollOffset / Math.Max(1, _lineHeight)) + 1,
                SyntaxLanguageId = ExternalHighlighter is not null ? "external" : null,
                Extra            = extra,
            };
        }

        void IEditorPersistable.ApplyEditorConfig(EditorConfigDto config)
        {
            if (config.CaretLine > 0 && _document != null)
            {
                _cursorLine   = Math.Clamp(config.CaretLine - 1, 0, _document.Lines.Count - 1);
                _cursorColumn = Math.Max(0, config.CaretColumn - 1);
            }
            if (config.FirstVisibleLine > 0 && _lineHeight > 0)
            {
                _verticalScrollOffset = (config.FirstVisibleLine - 1) * _lineHeight;
            }
            if (config.Extra?.TryGetValue("wordWrap", out var ww) == true)
                IsWordWrapEnabled = ww == "1";
            InvalidateVisual();
        }

        // CodeEditor has no binary modifications — return null / no-op
        byte[]? IEditorPersistable.GetUnsavedModifications() => null;
        void IEditorPersistable.ApplyUnsavedModifications(byte[] data) { }

        // Binary changeset model is not applicable for a text editor
        ChangesetSnapshot IEditorPersistable.GetChangesetSnapshot() => ChangesetSnapshot.Empty;
        void IEditorPersistable.ApplyChangeset(ChangesetDto changeset) { }

        void IEditorPersistable.MarkChangesetSaved()
        {
            _undoEngine.MarkSaved();
            _isDirty = false;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
        }

        // No bookmark concept in CodeEditor yet
        IReadOnlyList<BookmarkDto>? IEditorPersistable.GetBookmarks() => null;
        void IEditorPersistable.ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks) { }

        // ═══════════════════════════════════════════════════════════════════
        // IDiagnosticSource
        // ═══════════════════════════════════════════════════════════════════

        public event EventHandler? DiagnosticsChanged;

        string IDiagnosticSource.SourceLabel
            => !string.IsNullOrEmpty(_currentFilePath) ? Path.GetFileName(_currentFilePath)! : "JSON Editor";

        IReadOnlyList<DiagnosticEntry> IDiagnosticSource.GetDiagnostics()
        {
            if (_validationErrors == null || _validationErrors.Count == 0)
                return [];

            var fileName = !string.IsNullOrEmpty(_currentFilePath) ? Path.GetFileName(_currentFilePath) : null;
            var filePath = _currentFilePath;

            return _validationErrors.Select(ve => new DiagnosticEntry(
                Severity:    ve.Severity switch
                {
                    ValidationSeverity.Warning => DiagnosticSeverity.Warning,
                    ValidationSeverity.Info    => DiagnosticSeverity.Message,
                    _                          => DiagnosticSeverity.Error,
                },
                Code:        !string.IsNullOrEmpty(ve.ErrorCode) ? ve.ErrorCode : ve.Layer.ToString(),
                Description: ve.Message ?? string.Empty,
                FileName:    fileName,
                FilePath:    filePath,
                Line:        ve.Line + 1,
                Column:      ve.Column + 1
            )).ToList();
        }

        /// <summary>
        /// Pushes error and warning line indices from the current validation state to
        /// <see cref="_codeScrollMarkerPanel"/> so red/amber ticks appear on the scrollbar.
        /// Called automatically whenever <see cref="DiagnosticsChanged"/> fires.
        /// </summary>
        private void UpdateDiagnosticScrollMarkers()
        {
            if (_codeScrollMarkerPanel == null) return;

            var errorLines   = new List<int>();
            var warningLines = new List<int>();

            foreach (var (line, errors) in _validationByLine)
            {
                bool hasError = errors.Any(e => e.Severity == Models.ValidationSeverity.Error);
                if (hasError)
                    errorLines.Add(line);
                else
                    warningLines.Add(line);
            }

            _codeScrollMarkerPanel.UpdateDiagnosticMarkers(errorLines, warningLines,
                _document?.Lines.Count ?? 1);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Quick Info — hover dispatch and popup management
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Evaluates whether a new Quick Info request should be dispatched for the
        /// given hover position, then calls <see cref="Services.HoverQuickInfoService.RequestAsync"/>.
        /// </summary>
        private void HandleQuickInfoHover(TextPosition hoverPos, Point pixelPos)
        {
            if (_hoverQuickInfoService is null) return;

            // Suppress if the mouse is already inside the popup itself.
            if (_quickInfoPopup?.IsMouseOverPopup == true) return;

            // Jitter guard: don't re-dispatch unless the text position actually changed.
            if (hoverPos == _lastHoverTextPos) return;
            _lastHoverTextPos = hoverPos;
            _lastHoverPixel   = pixelPos;

            if (_currentFilePath is null || _document is null) return;

            var lineText = hoverPos.Line >= 0 && hoverPos.Line < _document.Lines.Count
                ? _document.Lines[hoverPos.Line].Text ?? string.Empty
                : string.Empty;
            var (word, _) = GetWordAt(lineText, hoverPos.Column);

            if (string.IsNullOrEmpty(word))
            {
                _hoverQuickInfoService.Cancel();
                return;
            }

            // Snapshot diagnostics for the service (cross-thread safe copy).
            _hoverQuickInfoService.SetDiagnostics(_validationErrors.ToArray());

            var lineSnapshot = _document.Lines.ToArray();
            _hoverQuickInfoService.RequestAsync(
                _currentFilePath, hoverPos.Line, hoverPos.Column, word, lineSnapshot);
        }

        // ── End-of-Block Hint ─────────────────────────────────────────────────────

        /// <summary>
        /// Called on every mouse move. Starts/stops the end-of-block hint timer based on
        /// whether the hovered line is the EndLine of a folding region.
        /// </summary>
        private void HandleEndBlockHintHover(int hoverLine0)
        {
            if (!IsEndBlockHintEnabled()) return;
            if (hoverLine0 == _endBlockHintHoveredLine) return;

            _endBlockHintHoveredLine = hoverLine0;
            _endBlockHintTimer?.Stop();

            var region = FindRegionEndingAt(hoverLine0);
            if (region is not null && IsRegionKindAllowed(region))
            {
                _endBlockHintActiveRegion = region;
                _endBlockHintTimer?.Start();
            }
            else
            {
                DismissEndBlockHint();
            }
        }

        private bool IsEndBlockHintEnabled()
        {
            if (_foldingEngine is null || _document is null) return false;
            if (!ShowEndOfBlockHint) return false;
            return Language?.FoldingRules?.EndOfBlockHint?.IsEnabled ?? true;
        }

        private bool IsRegionKindAllowed(FoldingRegion r)
        {
            var hint = Language?.FoldingRules?.EndOfBlockHint;
            if (hint is null) return true;
            return r.Kind switch
            {
                WpfHexEditor.Editor.CodeEditor.Folding.FoldingRegionKind.Directive => hint.TriggerDirective,
                _                                                                   => hint.TriggerBrace,
            };
        }

        /// <summary>
        /// Returns the innermost (largest StartLine) non-collapsed region whose EndLine == line0.
        /// </summary>
        private FoldingRegion? FindRegionEndingAt(int line0)
        {
            if (_foldingEngine is null) return null;
            FoldingRegion? best = null;
            foreach (var r in _foldingEngine.Regions)
            {
                if (r.IsCollapsed) continue;
                if (r.EndLine != line0) continue;
                if (best is null || r.StartLine > best.StartLine) best = r;
            }
            return best;
        }

        private void DismissEndBlockHint()
        {
            _endBlockHintTimer?.Stop();
            _endBlockHintActiveRegion = null;
            _endBlockHintPopup?.Hide();
        }

        private void OnEndBlockHintTimerTick(object? sender, EventArgs e)
        {
            _endBlockHintTimer!.Stop();
            if (_endBlockHintActiveRegion is null || _foldingEngine is null || _document is null) return;

            var r = _endBlockHintActiveRegion;
            if (!_lineYLookup.TryGetValue(r.EndLine, out double closeY)) return;

            _endBlockHintPopup ??= new EndBlockHintPopup();
            _endBlockHintPopup.NavigationRequested -= OnEndBlockHintNavigate;
            _endBlockHintPopup.NavigationRequested += OnEndBlockHintNavigate;

            int maxCtx = Language?.FoldingRules?.EndOfBlockHint?.MaxContextLines ?? 3;
            _endBlockHintPopup.Show(
                this, r, _document.Lines, _typeface, _fontSize,
                new Rect(TextAreaLeftOffset, closeY, Math.Max(1, ActualWidth - TextAreaLeftOffset), _lineHeight),
                ExternalHighlighter,
                maxCtx);
        }

        private void OnEndBlockHintNavigate(int startLine0)
        {
            NavigateToLine(startLine0);
        }

        /// <summary>
        /// Called when <see cref="Services.HoverQuickInfoService"/> fires
        /// <see cref="Services.HoverQuickInfoService.QuickInfoResolved"/>.
        /// </summary>
        private void OnFoldPeekTimerTick(object? sender, EventArgs e)
        {
            _foldPeekTimer!.Stop();
            if (_foldPeekTargetLine < 0 || _foldingEngine == null || _document == null) return;

            var region = _foldingEngine.GetRegionAt(_foldPeekTargetLine);
            if (region == null || !region.IsCollapsed) return;

            // Find the label rect so we can anchor the popup beneath it.
            Rect labelRect = default;
            foreach (var (rect, ln) in _foldLabelHitZones)
                if (ln == _foldPeekTargetLine) { labelRect = rect; break; }

            _foldPeekPopup ??= new FoldPeekPopup();
            _foldPeekPopup.Show(this, region, _document.Lines, _typeface, _fontSize, labelRect,
                                ExternalHighlighter);
        }

        private void OnQuickInfoResolved(object? sender, WpfHexEditor.SDK.ExtensionPoints.QuickInfoResult? result)
        {
            if (result is null)
            {
                _quickInfoPopup?.Hide();
                return;
            }

            // Lazy-create popup on first use.
            if (_quickInfoPopup is null)
            {
                _quickInfoPopup = new QuickInfoPopup();
                _quickInfoPopup.ActionRequested += OnQuickInfoActionRequested;
            }

            // Compute anchor below the hovered line (InlineHints-aware Y via _lineYLookup).
            double anchorX = TextAreaLeftOffset + _lastHoverTextPos.Column * _charWidth
                             - _horizontalScrollOffset;
            double anchorY = _lineYLookup.TryGetValue(_lastHoverTextPos.Line, out double ly)
                ? ly + _lineHeight + 2
                : _lastHoverTextPos.Line * _lineHeight + _lineHeight + 2;
            var anchor = new Point(anchorX, anchorY);

            _quickInfoPopup.Show(this, result, anchor);
        }

        /// <summary>Routes action link clicks from the Quick Info popup.</summary>
        private void OnQuickInfoActionRequested(object? sender, QuickInfoActionEventArgs e)
        {
            switch (e.Command)
            {
                case "GoToDefinition":
                    if (_hoveredSymbolZone.HasValue)
                        _ = NavigateToDefinitionAsync(_hoveredSymbolZone.Value);
                    break;

                case "FindAllReferences":
                    _ = FindAllReferencesAsync();
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Ctrl+Click — symbol underline and definition navigation
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Updates <see cref="_hoveredSymbolZone"/> and dispatches an async resolution
        /// via <see cref="Services.CtrlClickNavigationService"/> when Ctrl is held.
        /// </summary>
        private void HandleCtrlHover(TextPosition hoverPos)
        {
            if (_document is null || _ctrlClickService is null) return;

            var lineText = hoverPos.Line >= 0 && hoverPos.Line < _document.Lines.Count
                ? _document.Lines[hoverPos.Line].Text ?? string.Empty
                : string.Empty;
            var (word, startCol) = GetWordAt(lineText, hoverPos.Column);

            if (string.IsNullOrEmpty(word))
            {
                // Keep the zone when the cursor is still within the word's column span.
                // WPF fires MouseMove just before MouseDown; a sub-pixel shift can land on
                // a column boundary where GetWordAt returns empty even though the pointer is
                // visually inside the underlined token. Clearing here would make OnMouseDown
                // see HasValue = false and silently skip navigation.
                if (_hoveredSymbolZone.HasValue
                    && hoverPos.Line   == _hoveredSymbolZone.Value.Line
                    && hoverPos.Column >= _hoveredSymbolZone.Value.StartCol
                    && hoverPos.Column <= _hoveredSymbolZone.Value.EndCol)
                    return;

                if (_hoveredSymbolZone.HasValue)
                {
                    _hoveredSymbolZone = null;
                    _ctrlClickService.Cancel();
                    InvalidateVisual();
                }
                return;
            }

            int endCol = startCol + word.Length;

            // Skip non-navigable token kinds (keywords, literals, comments, operators).
            // Only Identifier, Type, Attribute, and Default (unclassified) tokens are navigable.
            var tokenKind = GetTokenKindAtColumn(hoverPos.Line, startCol);
            if (tokenKind is SyntaxTokenKind.Keyword
                          or SyntaxTokenKind.Comment
                          or SyntaxTokenKind.String
                          or SyntaxTokenKind.Number
                          or SyntaxTokenKind.Operator
                          or SyntaxTokenKind.Bracket)
            {
                if (_hoveredSymbolZone.HasValue)
                {
                    _hoveredSymbolZone = null;
                    _ctrlClickService.Cancel();
                    // Cursor stays Hand — Ctrl is still held.
                    InvalidateVisual();
                }
                return;
            }

            // Skip if zone is identical to the current one (avoid redundant invalidate + async call).
            if (_hoveredSymbolZone.HasValue
                && _hoveredSymbolZone.Value.Line     == hoverPos.Line
                && _hoveredSymbolZone.Value.StartCol == startCol
                && _hoveredSymbolZone.Value.EndCol   == endCol)
                return;

            // Create provisional zone; TargetFilePath will be filled in by OnCtrlClickTargetResolved.
            _hoveredSymbolZone = new SymbolHitZone(
                hoverPos.Line, startCol, endCol, word,
                string.Empty, 0, 0, false);

            InvalidateVisual();

            if (_currentFilePath is null) return;
            var lineSnapshot = _document.Lines.ToArray();
            _ctrlClickService.RequestAsync(
                _currentFilePath, hoverPos.Line, hoverPos.Column,
                startCol, endCol, word, lineSnapshot);
        }

        /// <summary>
        /// Called when <see cref="Services.CtrlClickNavigationService"/> fires
        /// <see cref="Services.CtrlClickNavigationService.TargetResolved"/>.
        /// Updates the hovered zone with the resolved target location.
        /// </summary>
        private void OnCtrlClickTargetResolved(
            object? sender, Services.CtrlClickTarget? target)
        {
            if (target is null || !_hoveredSymbolZone.HasValue) return;

            // Only apply if the resolution still matches the zone currently under the cursor.
            var current = _hoveredSymbolZone.Value;
            if (current.Line     != target.Line
                || current.StartCol != target.StartCol
                || current.EndCol   != target.EndCol)
                return;

            _hoveredSymbolZone = new SymbolHitZone(
                target.Line, target.StartCol, target.EndCol, target.SymbolName,
                target.TargetFilePath, target.TargetLine, target.TargetColumn,
                target.IsExternal);
        }

        /// <summary>Navigates to the definition of the given symbol zone.</summary>
        private async Task NavigateToDefinitionAsync(SymbolHitZone zone)
        {
            _quickInfoPopup?.Hide();
            PushNavigation(_currentFilePath, _cursorLine, _cursorColumn);

            // 1. Already resolved to an in-project file — navigate directly.
            if (!string.IsNullOrEmpty(zone.TargetFilePath) && !zone.IsExternal)
            {
                if (zone.TargetFilePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
                    NavigateToLine(zone.TargetLine);
                else
                    ReferenceNavigationRequested?.Invoke(this, new ReferencesNavigationEventArgs
                    {
                        FilePath = zone.TargetFilePath,
                        Line     = zone.TargetLine + 1,
                        Column   = zone.TargetColumn + 1
                    });
                return;
            }

            // 2. Ask LSP for definition.
            if (_lspClient?.IsInitialized == true && _currentFilePath is not null)
            {
                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var locations = await _lspClient.DefinitionAsync(
                        _currentFilePath, zone.Line, zone.StartCol, cts.Token)
                        .ConfigureAwait(true);

                    if (locations.Count > 0)
                    {
                        await HandleDefinitionLocationsAsync(locations, zone.SymbolName)
                            .ConfigureAwait(true);
                        return;
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception) { }
            }

            // 3. Local CodeStructureParser scan — finds declarations within the current document.
            //    Runs synchronously so a Ctrl+Click always produces feedback even without LSP.
            if (_document.Lines.Count > 0)
            {
                var snapshot = CodeStructureParser.Parse(_document.Lines);
                var all      = snapshot.Types.Concat(snapshot.Members);
                var decl     = all.FirstOrDefault(item =>
                    string.Equals(item.Name, zone.SymbolName, StringComparison.Ordinal)
                    && item.Line != zone.Line);

                if (decl is not null)
                {
                    NavigateToLine(decl.Line);
                    return;
                }
            }

            // 3b. Workspace-wide declaration scan — searches all solution files of the same
            //     extension using CodeStructureParser.  Handles cross-file navigation when
            //     no LSP is running (e.g. Ctrl+Click on a type defined in another project file).
            //     Runs on a background thread to avoid blocking the UI with large solutions.
            {
                var ext = Path.GetExtension(_currentFilePath ?? string.Empty);
                if (!string.IsNullOrEmpty(ext))
                {
                    var symbolName     = zone.SymbolName;
                    var currentPath    = _currentFilePath;
                    var workspacePaths = WorkspaceFileCache.GetPathsForExtensions([ext]);

                    var workspaceResult = await System.Threading.Tasks.Task.Run(() =>
                    {
                        foreach (var path in workspacePaths)
                        {
                            if (path.Equals(currentPath, StringComparison.OrdinalIgnoreCase))
                                continue;

                            var fileLines = WorkspaceFileCache.GetLines(path);
                            if (fileLines is null) continue;

                            try
                            {
                                var codeLines = fileLines
                                    .Select((t, i) => new CodeLine(t, i))
                                    .ToList();

                                var snap  = CodeStructureParser.Parse(codeLines);
                                var found = snap.Types.Concat(snap.Members).FirstOrDefault(item =>
                                    string.Equals(item.Name, symbolName, StringComparison.Ordinal));

                                if (found is not null)
                                    return (path, found.Line);
                            }
                            catch { /* skip files that fail to parse */ }
                        }
                        return ((string?)null, 0);
                    }).ConfigureAwait(true);

                    if (workspaceResult.Item1 is not null)
                    {
                        ReferenceNavigationRequested?.Invoke(this, new ReferencesNavigationEventArgs
                        {
                            FilePath = workspaceResult.Item1,
                            Line     = workspaceResult.Item2 + 1,
                            Column   = 1
                        });
                        return;
                    }
                }
            }

            // 4. External / no LSP fallback — symbol not found in any solution file.
            HandleExternalDefinitionAsync(zone.SymbolName);
        }

        /// <summary>
        /// Processes a list of LSP definition locations: navigates in-project targets
        /// directly; routes external/metadata targets via <see cref="GoToExternalDefinitionRequested"/>.
        /// </summary>
        private async Task HandleDefinitionLocationsAsync(
            IReadOnlyList<WpfHexEditor.Editor.Core.LSP.LspLocation> locations,
            string symbolName)
        {
            // Multiple definition locations (e.g. interface + implementation) — show popup
            // so the user can pick the target rather than silently navigating to the first.
            if (locations.Count > 1)
            {
                var groups = BuildGroupsFromLspLocations(locations, symbolName);
                ShowReferencesPopup(groups, symbolName, locations.Count,
                    source: "definition", line: _cursorLine, column: _cursorColumn);
                return;
            }

            var loc = locations[0];
            bool isMetadata = loc.Uri.StartsWith("metadata:",            StringComparison.OrdinalIgnoreCase)
                           || loc.Uri.StartsWith("omnisharp-metadata:",  StringComparison.OrdinalIgnoreCase)
                           || loc.Uri.StartsWith("csharp-metadata:",     StringComparison.OrdinalIgnoreCase)
                           || loc.Uri.StartsWith("dotnet://metadata",    StringComparison.OrdinalIgnoreCase)
                           || loc.Uri.Contains("?assembly=",             StringComparison.OrdinalIgnoreCase);

            if (isMetadata)
            {
                // Pass the raw URI + target line so the host can parse assembly/type and
                // scroll directly to the declaration after decompilation.
                HandleExternalDefinitionAsync(symbolName, loc.Uri,
                    targetLine:   loc.StartLine + 1,
                    targetColumn: loc.StartColumn + 1);
                return;
            }

            string? localPath = null;
            try { localPath = new Uri(loc.Uri).LocalPath; }
            catch { /* malformed URI — treat as external */ }

            if (localPath is null || !System.IO.File.Exists(localPath))
            {
                HandleExternalDefinitionAsync(symbolName, loc.Uri);
                return;
            }

            // In-project navigation.
            if (localPath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
                NavigateToLine(loc.StartLine);
            else
                ReferenceNavigationRequested?.Invoke(this, new ReferencesNavigationEventArgs
                {
                    FilePath = localPath,
                    Line     = loc.StartLine + 1,
                    Column   = loc.StartColumn + 1
                });

            await Task.CompletedTask.ConfigureAwait(true);
        }

        /// <summary>
        /// Fires <see cref="GoToExternalDefinitionRequested"/> so the IDE host can route
        /// to AssemblyExplorer or open a decompiled-source tab.
        /// </summary>
        private void HandleExternalDefinitionAsync(
            string symbolName,
            string? metadataUri  = null,
            int     targetLine   = 0,
            int     targetColumn = 0)
        {
            GoToExternalDefinitionRequested?.Invoke(this,
                new GoToExternalDefinitionEventArgs(
                    symbolName, _currentFilePath, metadataUri, targetLine, targetColumn));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Ctrl+Hover underline rendering
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws a single-pixel underline beneath the symbol token currently hovered
        /// while Ctrl is held.  Called from <see cref="OnRender"/> inside the
        /// H-scroll transform so coordinates are in document space.
        /// </summary>
        private void RenderCtrlHoverUnderline(DrawingContext dc)
        {
            if (!_ctrlDown || !_hoveredSymbolZone.HasValue) return;

            var zone = _hoveredSymbolZone.Value;

            // Lazy-create pen from theme resource; recreated each render pass so
            // theme switches are reflected immediately (same pattern as urlPen).
            var brush = TryFindResource("CE_CtrlHover_Underline") as Brush ?? Brushes.Cyan;
            var pen   = new Pen(brush, 1.0);
            pen.Freeze();

            double y        = _lineYLookup.TryGetValue(zone.Line, out double ly)
                ? ly + _lineHeight - 2
                : zone.Line * _lineHeight + _lineHeight - 2;
            double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double x1       = textLeft + zone.StartCol * _charWidth;
            double x2       = textLeft + zone.EndCol   * _charWidth;

            // Add the symbol to the hit-zone list so OnMouseMove can detect it even
            // when the async resolution hasn't completed yet.
            _symbolHitZones.Add(zone);

            dc.DrawLine(pen, new Point(x1, y), new Point(x2, y));
        }

        // -- URL hit-zone (per-render, rebuilt in RenderTextContent) ---------------

        /// <summary>
        /// Represents a single URL token position for hit-testing (cursor + Ctrl+Click).
        /// The list of active zones is rebuilt in <see cref="RenderTextContent"/> on each render.
        /// </summary>
        private readonly record struct UrlHitZone(int Line, int StartCol, int EndCol, string Url);

        // -- Symbol hit-zone (per-render when Ctrl is held) ────────────────────

        /// <summary>
        /// Identifier token position for Ctrl+hover underline and Ctrl+Click navigation.
        /// TargetFilePath/Line/Column are set by <see cref="Services.CtrlClickNavigationService"/>
        /// after async resolution. IsExternal = true requires decompilation fallback.
        /// </summary>
        private readonly record struct SymbolHitZone(
            int    Line,
            int    StartCol,
            int    EndCol,
            string SymbolName,
            string TargetFilePath,
            int    TargetLine,
            int    TargetColumn,
            bool   IsExternal);

        // -- Shared relay command (accessible from all partial-class files) --------
        private sealed class JsonRelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
            : System.Windows.Input.ICommand
        {
            public bool CanExecute(object? p)  => canExecute?.Invoke(p) ?? true;
            public void Execute(object? p)     => execute(p);
            public event EventHandler? CanExecuteChanged;
        }
    }

    // -- GoToExternalDefinition event args ─────────────────────────────────────

    /// <summary>
    /// Carries the symbol name and originating file path for a
    /// <see cref="CodeEditor.GoToExternalDefinitionRequested"/> event.
    /// The IDE host should route this to AssemblyExplorer or open a decompiled tab.
    /// </summary>
    public sealed class GoToExternalDefinitionEventArgs : EventArgs
    {
        /// <summary>Symbol name (type, method, property, etc.) to navigate to.</summary>
        public string SymbolName { get; }

        /// <summary>
        /// Full path of the source file that triggered the navigation request.
        /// May be null when the editor has no current file (untitled buffer).
        /// </summary>
        public string? SourceFilePath { get; }

        /// <summary>
        /// Raw LSP URI that identified this symbol as external (e.g.
        /// "omnisharp-metadata:?assembly=System.Console&amp;type=System.Console&amp;...").
        /// The IDE host can parse <c>assembly=</c> and <c>type=</c> query parameters to
        /// locate and decompile the assembly. Null when the symbol was not resolved via LSP.
        /// </summary>
        public string? MetadataUri { get; }

        /// <summary>
        /// 1-based line number within the decompiled source to navigate to after opening.
        /// 0 means unknown — host should call <c>FindSymbolLineInSource</c> as fallback.
        /// </summary>
        public int TargetLine { get; }

        /// <summary>1-based column within <see cref="TargetLine"/>. 0 means unknown.</summary>
        public int TargetColumn { get; }

        internal GoToExternalDefinitionEventArgs(
            string  symbolName,
            string? sourceFilePath,
            string? metadataUri  = null,
            int     targetLine   = 0,
            int     targetColumn = 0)
        {
            SymbolName     = symbolName;
            SourceFilePath = sourceFilePath;
            MetadataUri    = metadataUri;
            TargetLine     = targetLine;
            TargetColumn   = targetColumn;
        }
    }

}
