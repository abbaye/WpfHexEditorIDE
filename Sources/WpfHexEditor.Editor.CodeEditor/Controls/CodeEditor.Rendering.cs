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
        #region Rendering - OnRender Override

        /// <summary>
        /// Returns the Y pixel position (relative to the control top) for the n-th visible
        /// (non-hidden) line in the current viewport.  Preserves the sub-pixel smooth-scroll
        /// fraction from the virtualization engine so lines align correctly during smooth scroll.
        /// </summary>
        /// <param name="visIdx">0-based index among non-hidden visible lines.</param>
        #region Scope Guide Lines

        /// <summary>
        /// Draws vertical scope guide lines for each non-collapsed fold region.
        /// Each line is placed at the indentation column of the body content,
        /// running from the bottom of the opening brace line to the top of the closing brace line
        /// (VS Code style — the guide covers only the body, without touching either brace).
        /// </summary>
        private void RenderScopeGuides(DrawingContext dc)
        {
            if (!ShowScopeGuides || _foldingEngine == null || _document == null || _lineHeight <= 0)
                return;

            double textX = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // Determine the innermost block containing the cursor for active-guide highlight.
            var activeRegion = FindInnermostContainingRegion(_cursorLine);

            // OPT-PERF-03: iterate pre-filtered _visibleRegions (populated in CalculateVisibleLines)
            // instead of all regions — avoids O(total-regions) scan every frame.
            foreach (var region in _visibleRegions)
            {
                double guideX = ComputeScopeGuideX(textX, region);
                if (guideX < textX) continue; // never draw before text area origin

                // For Allman-style code the BraceFoldingStrategy sets StartLine = method-header line,
                // so StartLine+1 is the standalone { line — skip it to start the guide after the {.
                int bodyStart = region.StartLine + 1;
                if (bodyStart < _document!.Lines.Count && _document.Lines[bodyStart].Text.Trim() == "{")
                    bodyStart++;

                // Draw from bottom of { (first real body line) to top of } — VS Code exact behavior.
                double yTop    = ScopeLineIndexToY(bodyStart);
                double yBottom = ScopeLineIndexToY(region.EndLine);

                // Use the active pen for the innermost block containing the cursor (VS Code style).
                var pen = (activeRegion != null && ReferenceEquals(region, activeRegion))
                    ? s_scopeGuideActivePen
                    : s_scopeGuidePen;

                dc.DrawLine(pen, new Point(guideX, yTop), new Point(guideX, yBottom));
            }
        }

        /// <summary>
        /// Returns the X position for the scope guide of <paramref name="region"/> by
        /// finding the leading whitespace of the first non-empty line inside the block.
        /// </summary>
        /// <summary>
        /// Returns the innermost non-collapsed <see cref="FoldingRegion"/> whose body contains
        /// <paramref name="cursorLine"/>, or <c>null</c> if none does.
        /// "Innermost" is defined as the region with the largest <c>StartLine</c> (most nested block).
        /// </summary>
        private FoldingRegion? FindInnermostContainingRegion(int cursorLine)
        {
            FoldingRegion? best = null;
            foreach (var r in _foldingEngine!.Regions)
            {
                if (r.IsCollapsed) continue;
                if (cursorLine > r.StartLine && cursorLine <= r.EndLine)
                    if (best == null || r.StartLine > best.StartLine)
                        best = r;
            }
            return best;
        }

        private double ComputeScopeGuideX(double textX, FoldingRegion region)
        {
            // Use the opening tag / declaration line's own indentation as the guide X.
            // Previously this used startLine+1 (first content line), which placed XML/XAML
            // guides at the attribute-alignment indent instead of the tag's column (ADR-054).
            if (_document is null || region.StartLine >= _document.Lines.Count)
                return textX;

            var startText = _document.Lines[region.StartLine].Text ?? string.Empty;
            int spaces = 0;
            foreach (char c in startText)
            {
                if      (c == ' ')  spaces++;
                else if (c == '\t') spaces += IndentSize;
                else break;
            }
            return textX + spaces * _charWidth;
        }

        /// <summary>
        /// Converts a document line index to its Y pixel position in the viewport,
        /// accounting for fold-collapsed lines (mirrors <see cref="GetFoldAwareLineY"/>
        /// but takes an absolute line index instead of a visible-line counter).
        /// </summary>
        private double ScopeLineIndexToY(int lineIndex)
        {
            int visIdx = 0;
            for (int i = _firstVisibleLine; i < lineIndex && i < _document!.Lines.Count; i++)
                if (_foldingEngine == null || !_foldingEngine.IsLineHidden(i)) visIdx++;
            return GetFoldAwareLineY(visIdx);
        }

        #endregion

        #region Sticky Scroll (#160)

        /// <summary>
        /// Builds the ordered scope-chain visible in the sticky header for the current
        /// first-visible line.  Returns scopes that contain <paramref name="firstLine"/>
        /// ordered from outermost to innermost, limited to <see cref="_stickyScrollMaxLines"/>
        /// and filtered by <see cref="_stickyScrollMinScopeLines"/>.
        /// </summary>
        private List<FoldingRegion> FindScopeChainAt(int firstLine)
        {
            if (_foldingEngine is null || _document is null) return [];

            // O(N) single pass: insertion-sort into a small bounded list (max _stickyScrollMaxLines).
            // Avoids the O(N log N) LINQ .OrderBy() that ran on every scroll frame.
            var result = new List<FoldingRegion>(_stickyScrollMaxLines + 1);
            foreach (var r in _foldingEngine.Regions)
            {
                if (r.IsCollapsed || r.StartLine >= firstLine || r.EndLine < firstLine) continue;
                if ((r.EndLine - r.StartLine) < _stickyScrollMinScopeLines) continue;

                // Binary-search insertion point to keep list sorted by StartLine ascending.
                int ins = result.Count;
                while (ins > 0 && result[ins - 1].StartLine > r.StartLine) ins--;
                result.Insert(ins, r);

                // Trim to window: keep only the innermost MaxLines entries.
                if (result.Count > _stickyScrollMaxLines)
                    result.RemoveAt(0);
            }
            return result;
        }

        /// <summary>
        /// Counts the number of qualifying scopes that contain <paramref name="line"/>
        /// without allocating a list.  Used by the fixed-point stickyLine resolver.
        /// </summary>
        private int CountScopeDepthAt(int line)
        {
            if (_foldingEngine is null) return 0;
            int count = 0;
            foreach (var r in _foldingEngine.Regions)
            {
                if (!r.IsCollapsed && r.StartLine < line && r.EndLine >= line
                    && (r.EndLine - r.StartLine) >= _stickyScrollMinScopeLines)
                    if (++count >= _stickyScrollMaxLines) break;
            }
            return count;
        }

        /// <summary>
        /// Recomputes the sticky header entries from the current viewport and pushes them
        /// to <see cref="_stickyScrollHeader"/>.  Called from <see cref="OnRender"/> and
        /// after scroll/option changes.
        /// </summary>
        private void UpdateStickyScrollHeader()
        {
            if (_stickyScrollHeader is null) return;

            if (!_stickyScrollEnabled || _document is null || _foldingEngine is null
                || _lineHeight <= 0 || _typeface is null)
            {
                _stickyScrollHeader.Update(
                    Array.Empty<StickyScrollEntry>(), 0, 0, _typeface ?? new Typeface("Consolas"),
                    _fontSize, 0, 1.0, false, false,
                    false, 0, 0, null, null);
                return;
            }

            // Fixed-point resolution: find the smallest stickyLine such that
            // CountScopeDepthAt(stickyLine) == stickyLine - rawLine.
            // This avoids the cross-frame feedback loop introduced by using the previous
            // frame's RequiredHeight as an additive offset (ADR-SS-FP-01).
            int rawLine = (_lineHeight > 0)
                ? Math.Max(0, (int)(_verticalScrollOffset / _lineHeight))
                : _firstVisibleLine;
            int stickyLine = rawLine;
            for (int pass = 0; pass <= _stickyScrollMaxLines; pass++)
            {
                int next = rawLine + CountScopeDepthAt(stickyLine);
                if (next == stickyLine) break;
                stickyLine = next;
            }
            var chain   = FindScopeChainAt(stickyLine);
            var entries = new List<StickyScrollEntry>(chain.Count);
            double ppdip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            var activeHighlighter = (ISyntaxHighlighter?)ExternalHighlighter ?? _highlighter;

            foreach (var region in chain)
            {
                if (region.StartLine >= _document.Lines.Count) continue;
                var line = _document.Lines[region.StartLine];
                var text = line.Text ?? string.Empty;

                IReadOnlyList<SyntaxHighlightToken> tokens = Array.Empty<SyntaxHighlightToken>();
                if (_stickyScrollSyntaxHighlight && activeHighlighter != null)
                {
                    try
                    {
                        tokens = activeHighlighter.Highlight(text, region.StartLine)
                            .Select(t => t with
                            {
                                Foreground = ResolveBrushForKind(t.Kind) ?? t.Foreground
                            })
                            .ToList<SyntaxHighlightToken>();
                    }
                    catch { /* highlighter not ready — fall back to plain */ }
                }

                entries.Add(new StickyScrollEntry(region.StartLine, tokens, text));
            }

            double textX = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            _stickyScrollHeader.Update(
                entries, _lineHeight, _charWidth, _typeface, _fontSize,
                textX, ppdip, _stickyScrollSyntaxHighlight, _stickyScrollClickToNavigate,
                ShowLineNumbers, LineNumberWidth, LineNumberMargin,
                _lineNumberTypeface, LineNumberForeground);
        }

        #endregion

        #region Outlining Commands (Ctrl+M chord)

        /// <summary>Toggle the fold region that starts on the cursor line (Ctrl+M, Ctrl+M).</summary>
        private void OutlineToggleCurrent()
        {
            if (_foldingEngine == null || !IsFoldingEnabled) return;
            _foldingEngine.ToggleRegion(_cursorLine);
            InvalidateVisual();
        }

        /// <summary>
        /// Collapse all regions if none are collapsed; otherwise expand all (Ctrl+M, Ctrl+L).
        /// </summary>
        private void OutlineToggleAll()
        {
            if (_foldingEngine == null || !IsFoldingEnabled) return;
            bool anyCollapsed = _foldingEngine.Regions.Any(r => r.IsCollapsed);
            if (anyCollapsed) _foldingEngine.ExpandAll();
            else              _foldingEngine.CollapseAll();
            InvalidateVisual();
        }

        /// <summary>Expand all regions and disable outlining (Ctrl+M, Ctrl+P).</summary>
        private void OutlineStop()
        {
            _foldingEngine?.ExpandAll();
            IsFoldingEnabled = false;
            InvalidateVisual();
        }

        /// <summary>
        /// Expand the innermost collapsed region that contains the cursor (Ctrl+M, Ctrl+U).
        /// </summary>
        private void OutlineStopHidingCurrent()
        {
            if (_foldingEngine == null || !IsFoldingEnabled) return;
            // Find the innermost collapsed region containing the cursor.
            FoldingRegion? innermost = null;
            foreach (var r in _foldingEngine.Regions)
            {
                if (!r.IsCollapsed) continue;
                if (_cursorLine < r.StartLine || _cursorLine > r.EndLine) continue;
                if (innermost == null || (r.EndLine - r.StartLine) < (innermost.EndLine - innermost.StartLine))
                    innermost = r;
            }
            if (innermost != null)
                _foldingEngine.ToggleRegion(innermost.StartLine);
            InvalidateVisual();
        }

        /// <summary>Collapse all regions (Ctrl+M, Ctrl+O).</summary>
        private void OutlineCollapseToDefinitions()
        {
            if (_foldingEngine == null || !IsFoldingEnabled) return;
            _foldingEngine.CollapseAll();
            InvalidateVisual();
        }

        #endregion

        /// <summary>
        /// Returns the Y coordinate where the code text for visible-index
        /// <paramref name="visIdx"/> should be drawn.
        /// For declaration lines with InlineHints active the Y is pushed down by
        /// <see cref="HintLineHeight"/>; for all other lines it sits at the slot top.
        /// Falls back to the uniform formula when the precomputed list is unavailable.
        /// </summary>
        private double GetFoldAwareLineY(int visIdx)
        {
            if (visIdx < _visLinePositions.Count)
                return _visLinePositions[visIdx].Y;

            // Fallback: uniform layout (no InlineHints offset).
            double scrollFraction = (EnableVirtualScrolling && _virtualizationEngine != null)
                ? _virtualizationEngine.GetLineYPosition(_firstVisibleLine)
                : 0.0;
            return TopMargin + scrollFraction + visIdx * _lineHeight;
        }

        /// <summary>
        /// Returns the top Y of the lens hint zone for visible-index <paramref name="visIdx"/>
        /// (the HintLineHeight zone immediately above the code text).
        /// </summary>
        private double GetLensZoneY(int visIdx) => GetFoldAwareLineY(visIdx) - HintLineHeight;

        /// <summary>
        /// Rebuilds the per-line visual-row arrays used when <see cref="IsWordWrapEnabled"/> is true.
        /// O(n) over logical line count. (ADR-049)
        /// </summary>
        private void RebuildWrapMap()
        {
            if (!IsWordWrapEnabled || _document is null || _charWidth <= 0)
            {
                _wrapHeights      = Array.Empty<int>();
                _wrapOffsets      = Array.Empty<int>();
                _totalVisualRows  = 0;
                _charsPerVisualLine = 0;
                return;
            }

            double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double vBarW    = _vScrollBar?.Visibility == Visibility.Visible ? ScrollBarThickness : 0;
            double availW   = ActualWidth - textLeft - vBarW;
            _charsPerVisualLine = Math.Max(1, (int)(availW / _charWidth));

            var lines = _document.Lines;
            int n     = lines.Count;
            _wrapHeights = new int[n];
            _wrapOffsets = new int[n];
            int total = 0;
            for (int i = 0; i < n; i++)
            {
                _wrapOffsets[i] = total;
                int len          = lines[i].Text?.Length ?? 0;
                int h            = len == 0 ? 1 : (int)Math.Ceiling((double)len / _charsPerVisualLine);
                _wrapHeights[i]  = h;
                total           += h;
            }
            _totalVisualRows = total;
        }

        /// <summary>
        /// Binary-searches <see cref="_wrapOffsets"/> to find the logical line that owns
        /// <paramref name="visualRow"/>. Returns (logLine, subRow). (ADR-049)
        /// </summary>
        private (int logLine, int subRow) WrapVisualRowToLogical(int visualRow)
        {
            if (_wrapOffsets.Length == 0) return (Math.Max(0, visualRow), 0);
            int lo = 0, hi = _wrapOffsets.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_wrapOffsets[mid] <= visualRow) lo = mid;
                else hi = mid - 1;
            }
            return (lo, visualRow - _wrapOffsets[lo]);
        }

        /// <summary>
        /// Precomputes per-visible-line Y positions, adding <see cref="HintLineHeight"/>
        /// only for lines that have a InlineHints entry.  Must be called in OnRender immediately
        /// after <see cref="CalculateVisibleLines"/>.
        /// </summary>
        private void ComputeVisibleLinePositions()
        {
            _visLinePositions.Clear();
            _visLineSubRows.Clear();
            _lineYLookup.Clear();

            if (IsWordWrapEnabled && _wrapOffsets.Length > 0)
            {
                // ---- Word-wrap path ----
                // _verticalScrollOffset is in pixels; convert to first visual row.
                int firstVisRow = Math.Max(0, (int)(_verticalScrollOffset / _lineHeight));
                double y        = TopMargin + firstVisRow * _lineHeight - _verticalScrollOffset;
                bool hasHBar    = _hScrollBar?.Visibility == Visibility.Visible;
                double viewportH = ActualHeight - TopMargin - (hasHBar ? ScrollBarThickness : 0);
                int lastVisRow  = Math.Min(_totalVisualRows - 1,
                    firstVisRow + (int)(viewportH / _lineHeight) + 1);

                int vr = firstVisRow;
                while (vr <= lastVisRow)
                {
                    var (logLine, subRow) = WrapVisualRowToLogical(vr);
                    if (logLine >= _document!.Lines.Count) break;
                    double codeY = y;
                    if (subRow == 0 && ShowInlineHints && IsHintEntryVisible(logLine))
                    {
                        codeY = y + HintLineHeight;   // hint zone sits above the first sub-row
                        _lineYLookup[logLine] = codeY;
                    }
                    else if (subRow == 0)
                    {
                        _lineYLookup[logLine] = y;
                    }
                    _visLinePositions.Add((logLine, codeY));
                    _visLineSubRows.Add(subRow);
                    y = codeY + _lineHeight;
                    vr++;
                }
                return;
            }

            // ---- Normal path ----
            double scrollFraction = (EnableVirtualScrolling && _virtualizationEngine != null)
                ? _virtualizationEngine.GetLineYPosition(_firstVisibleLine)
                : 0.0;
            {
                double y = TopMargin + scrollFraction;
                for (int i = _firstVisibleLine; i <= _lastVisibleLine; i++)
                {
                    if (_foldingEngine?.IsLineHidden(i) == true) continue;

                    if (ShowInlineHints && IsHintEntryVisible(i))
                    {
                        double codeY = y + HintLineHeight;
                        _visLinePositions.Add((i, codeY));
                        _visLineSubRows.Add(0);
                        _lineYLookup[i] = codeY;
                        y += _lineHeight + HintLineHeight;
                    }
                    else
                    {
                        _visLinePositions.Add((i, y));
                        _visLineSubRows.Add(0);
                        _lineYLookup[i] = y;
                        y += _lineHeight;
                    }
                }
            }
        }

        /// <summary>
        /// Draws "N références" hints in the lens zone (top <see cref="HintLineHeight"/> px of
        /// each line slot) for lines that have declaration items in <see cref="_hintsData"/>.
        /// Hit zones are stored in <see cref="_hintsHitZones"/> for mouse interaction.
        /// Called from OnRender after the text-area clip but before the H-scroll transform,
        /// so hints are clipped to the text column yet not scrolled horizontally.
        /// </summary>
        private void RenderInlineHints(DrawingContext dc)
        {
            if (!ShowInlineHints || _visibleHintsCount == 0 || _document == null) return;

            _hintsHitZones.Clear();

            var normalBrush = (Brush?)TryFindResource("CE_Lens")       ?? Brushes.Gray;
            var hoverBrush  = (Brush?)TryFindResource("CE_Lens_Hover") ?? Brushes.Silver;
            var bgBrush     = (Brush?)TryFindResource("CE_Lens_Bg")    ?? Brushes.Transparent;
            double fontSize  = HintLineHeight * 0.72;   // ~11.5 px for a 16-px slot
            double baseX     = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double pixelsPerDip = _renderPixelsPerDip; // cached once per OnRender (OPT-PERF-03)

            for (int i = _firstVisibleLine; i <= _lastVisibleLine; i++)
            {
                if (_foldingEngine?.IsLineHidden(i) == true) continue;

                if (IsHintEntryVisible(i) && _hintsData.TryGetValue(i, out var entry) && entry.Count > 0)
                {
                    // Indent hint to match the leading whitespace of the declaration line.
                    string lineText = _document.Lines[i].Text ?? string.Empty;
                    int indent = 0;
                    while (indent < lineText.Length && (lineText[indent] == ' ' || lineText[indent] == '\t'))
                        indent++;
                    double x = baseX + _glyphRenderer.ComputeVisualX(lineText, indent);

                    string label  = entry.Count == 1 ? "1 reference" : $"{entry.Count} references";
                    var    brush  = i == _hoveredHintsLine ? hoverBrush : normalBrush;
                    var ft = new System.Windows.Media.FormattedText(
                        label,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        _typeface,
                        fontSize,
                        brush,
                        pixelsPerDip);

                    // Use _lineYLookup (code-text Y) minus HintLineHeight — works in both normal
                    // and word-wrap modes without relying on a visIdx counter.
                    double hintZoneY = _lineYLookup.TryGetValue(i, out double codeY)
                        ? codeY - HintLineHeight
                        : GetLensZoneY(0);   // defensive fallback
                    double y = hintZoneY + 1.0;  // 1 px top padding
                    // Subtle pill background — makes the hint visually distinct from code/comments.
                    dc.DrawRoundedRectangle(bgBrush, null, new Rect(x - 3, y, ft.Width + 6, ft.Height + 1), 3, 3);
                    dc.DrawText(ft, new Point(x, y));
                    _hintsHitZones.Add((new Rect(x - 3, y, ft.Width + 6, HintLineHeight - 2), i, entry.Symbol));
                }
            }
        }

        /// <summary>
        /// Returns the 0-based column of the first whole-word occurrence of
        /// <paramref name="symbol"/> in line <paramref name="lineIdx"/>.
        /// Falls back to 0 if not found (cursor lands at line start).
        /// </summary>
        private int FindSymbolColumnInLine(int lineIdx, string symbol)
        {
            if (string.IsNullOrEmpty(symbol) || _document == null || lineIdx >= _document.Lines.Count)
                return 0;

            string text = _document.Lines[lineIdx].Text;
            if (string.IsNullOrEmpty(text)) return 0;

            int idx = text.IndexOf(symbol, StringComparison.Ordinal);
            if (idx < 0) return 0;

            bool leftOk  = idx == 0             || !IsWordChar(text[idx - 1]);
            bool rightOk = idx + symbol.Length >= text.Length || !IsWordChar(text[idx + symbol.Length]);
            return (leftOk && rightOk) ? idx : 0;
        }

        /// <summary>
        /// Draws a "[…]" badge after the text of a collapsed fold-opener line and
        /// registers the badge rect in <see cref="_foldLabelHitZones"/> for click-to-toggle.
        /// </summary>
        private void RenderFoldCollapseLabel(DrawingContext dc, int lineIndex, double textX, double y)
        {
            if (_foldingEngine == null) return;
            var region = _foldingEngine.GetRegionAt(lineIndex);
            if (region == null || !region.IsCollapsed) return;

            // Resolve theme-aware brushes at render time so theme switches are reflected immediately.
            // Hover state: use brighter tokens when mouse is over this label.
            bool isHovered = lineIndex == _foldPeekTargetLine;
            var borderBrush = (isHovered
                ? TryFindResource("CE_FoldLabelBorderHover") as Brush
                : null)
                ?? TryFindResource("CE_FoldLabelBorder") as Brush
                ?? s_foldLabelPenBrush;
            var bgBrush = (isHovered
                ? TryFindResource("CE_FoldLabelBgHover") as Brush
                : null)
                ?? TryFindResource("CE_FoldLabelBg") as Brush
                ?? s_foldLabelBgBrush;
            var textBrush = TryFindResource("CE_FoldLabelFg") as Brush ?? s_foldLabelTextBrush;

            // Cache Pen against brush reference — rebuilt only on theme change (OPT-PERF-03).
            Pen pen;
            if (isHovered)
            {
                if (_cachedFoldLabelHoverPen == null || !ReferenceEquals(_cachedFoldLabelHoverBrush, borderBrush))
                {
                    _cachedFoldLabelHoverBrush = borderBrush;
                    _cachedFoldLabelHoverPen = new Pen(borderBrush, 1.5);
                }
                pen = _cachedFoldLabelHoverPen;
            }
            else
            {
                if (_cachedFoldLabelPen == null || !ReferenceEquals(_cachedFoldLabelBorderBrush, borderBrush))
                {
                    _cachedFoldLabelBorderBrush = borderBrush;
                    _cachedFoldLabelPen = new Pen(borderBrush, 1.0);
                }
                pen = _cachedFoldLabelPen;
            }

            double labelX;
            string labelText;

            if (region.Kind == FoldingRegionKind.Directive)
            {
                // For directive regions: blank the opening "#region ..." text by drawing a
                // background-colored rect over it (drawn after text, so it renders on top),
                // then place the label box at the original indentation level of the #region line.
                var editorBg = TryFindResource("CE_Background") as Brush;
                if (editorBg != null)
                    dc.DrawRectangle(editorBg, null,
                        new Rect(textX, y, Math.Max(0, ActualWidth - textX), _lineHeight));

                string dirText  = _document.Lines[lineIndex].Text ?? string.Empty;
                int    indentLen = dirText.Length - dirText.TrimStart().Length;
                double indentX   = _glyphRenderer?.ComputeVisualX(dirText, indentLen)
                                   ?? indentLen * _charWidth;
                labelX    = textX + indentX;
                labelText = string.IsNullOrEmpty(region.Name) ? "#region" : region.Name;
            }
            else
            {
                // For brace regions: label appears after the opening line text (e.g. after '{').
                var codeLine = _document.Lines[lineIndex];
                double textLen = (codeLine.Text?.TrimEnd().Length ?? 0) * _charWidth;
                labelX    = textX + textLen + _charWidth * 0.5;
                labelText = "{ \u2026 }";
            }

            double FontSize = _fontSize;
            const double PaddingH = 8.0;

            var ft = new FormattedText(
                labelText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                s_foldLabelTypeface, FontSize,
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Box spans the full line height so it aligns flush with the visible line boundaries.
            double boxW   = ft.Width + PaddingH * 2;
            double boxH   = _lineHeight;
            double labelY = y;

            var rect = new Rect(labelX, labelY, boxW, boxH);
            dc.DrawRoundedRectangle(bgBrush, pen, rect, 2.0, 2.0);
            dc.DrawText(ft, new Point(labelX + PaddingH, labelY + (boxH - ft.Height) / 2.0));
            _foldLabelHitZones.Add((rect, lineIndex));
        }

        /// <summary>
        /// Main rendering method - draws all visual elements
        /// Called by WPF when visual update is needed
        /// </summary>
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_document == null || _document.Lines.Count == 0)
                return;

            _refreshStopwatch.Restart();

            // Cache DPI once per render pass; used by RenderInlineHints and RenderLineNumbers (OPT-PERF-03).
            _renderPixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Cursor-change detection: covers every code path that moves the caret
            // (mouse click, keyboard, undo/redo, NavigateToLine, etc.) without requiring
            // a ScheduleWordHighlightUpdate() call at each individual site.
            if (_cursorLine != _wordHighlightTrackedLine || _cursorColumn != _wordHighlightTrackedCol)
            {
                _wordHighlightTrackedLine = _cursorLine;
                _wordHighlightTrackedCol  = _cursorColumn;
                ScheduleWordHighlightUpdate();
            }


            bool hasVBar = _vScrollBar?.Visibility == Visibility.Visible;
            bool hasHBar = _hScrollBar?.Visibility == Visibility.Visible;
            double contentW = ActualWidth  - (hasVBar ? ScrollBarThickness : 0);
            double contentH = ActualHeight - (hasHBar ? ScrollBarThickness : 0);
            double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // Calculate visible line range
            int prevFirstVisible = _firstVisibleLine, prevLastVisible = _lastVisibleLine;
            CalculateVisibleLines();
            // Pre-compute per-visible-line Y positions; only declaration lines get +HintLineHeight.
            ComputeVisibleLinePositions();

            // OPT-D: rebuild per-line Y positions only when the visible range, InlineHints, or
            // folding state changed — not on every caret-blink render frame.
            if (_firstVisibleLine != prevFirstVisible || _lastVisibleLine != prevLastVisible)
                _linePositionsDirty = true;

            if (_linePositionsDirty)
            {
                ComputeVisibleLinePositions();
                _linePositionsDirty = false;
            }

            // Sticky scroll header: refresh only when the true scroll-line changes.
            // Guard: never call InvalidateArrange() unconditionally inside OnRender —
            // it creates a WPF layout → render → layout cycle (infinite loop).
            // Use rawLine (_verticalScrollOffset / _lineHeight) instead of _firstVisibleLine
            // so the guard fires on every integer line boundary regardless of the render
            // buffer offset (ADR-IH-PERF-02, ADR-SS-FP-01).
            int stickyRawLine = _lineHeight > 0
                ? Math.Max(0, (int)(_verticalScrollOffset / _lineHeight + 1e-9))
                : _firstVisibleLine;
            if (stickyRawLine != _lastStickyFirstLine)
            {
                _lastStickyFirstLine = stickyRawLine;
                UpdateStickyScrollHeader();
                int newCount = _stickyScrollHeader?.RequiredHeight > 0
                    ? (int)(_stickyScrollHeader.RequiredHeight / _lineHeight) : 0;
                if (newCount != _stickyScrollLastEntryCount)
                {
                    _stickyScrollLastEntryCount = newCount;
                    InvalidateArrange(); // re-position header at its new height
                }
            }

            // -- Clip to content area (prevent drawing over scrollbars) --
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, contentW, contentH)));

            // 1. Editor background
            dc.DrawRectangle(EditorBackground, null, new Rect(0, 0, contentW, contentH));

            // 2. Line number gutter background (fixed — no H offset)
            if (ShowLineNumbers)
                dc.DrawRectangle(LineNumberBackground, null, new Rect(0, 0, LineNumberWidth, contentH));

            // 3. Current line highlight (spans visible text area, no H offset)
            RenderCurrentLineHighlight(dc, contentW, contentH);

            // 3a. Execution line highlight — yellow tint across the full text area when debugger is paused.
            if (_executionLineOneBased.HasValue)
            {
                int execLine0 = _executionLineOneBased.Value - 1;
                if (_lineYLookup.TryGetValue(execLine0, out double execY))
                {
                    var execBrush = TryFindResource("DB_ExecutionLineBackgroundBrush") as System.Windows.Media.Brush
                                    ?? new System.Windows.Media.SolidColorBrush(
                                           System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xDD, 0x00));
                    dc.DrawRectangle(execBrush, null,
                        new Rect(textLeft, execY, Math.Max(0, contentW - textLeft), _lineHeight));
                }
            }

            // -- Text area clip + horizontal translate -------------------
            dc.PushClip(new RectangleGeometry(new Rect(textLeft, 0, Math.Max(0, contentW - textLeft), contentH)));

            // 3b. InlineHints hints — drawn inside the text-area clip but WITHOUT the
            //     H-scroll transform so they stay anchored at the left edge.
            RenderInlineHints(dc);

            dc.PushTransform(new System.Windows.Media.TranslateTransform(-_horizontalScrollOffset, 0));

            // 4. Find result highlights
            RenderFindResults(dc);

            // 4b. Word-under-caret highlights (rendered below selection so selection stays visible)
            RenderWordHighlights(dc);

            // 4c. Ctrl+hover symbol underline (above word highlights, below selection)
            _symbolHitZones.Clear();
            RenderCtrlHoverUnderline(dc);

            // 5. Selection
            RenderSelection(dc);

            // 5a. Rectangular (block/column) selection overlay — Feature A
            RenderRectSelection(dc);

            // 5b. Drag-and-drop insertion caret — Feature B
            RenderDragDropCaret(dc);

            // 6a. Scope guides (drawn behind text so they don't obscure characters)
            RenderScopeGuides(dc);

            // 6. Text content
            RenderTextContent(dc);

            // 7. Validation errors (Phase 5)
            if (EnableValidation)
                RenderValidationErrors(dc);

            // 8. Bracket matching (Phase 6)
            RenderBracketMatching(dc);

            // 9. Cursor
            RenderCursor(dc);

            dc.Pop(); // H translate transform
            dc.Pop(); // text area clip

            // 10. Line numbers (no H offset — drawn on top of gutter background)
            if (ShowLineNumbers)
                RenderLineNumbers(dc);

            dc.Pop(); // content clip

            // 11. Corner background (intersection of V + H scrollbars)
            if (hasVBar && hasHBar)
                dc.DrawRectangle(LineNumberBackground ?? Brushes.Transparent, null,
                    new Rect(contentW, contentH, ScrollBarThickness, ScrollBarThickness));

            // Phase 11.4: Periodically cleanup token cache
            if (_frameCount++ % 60 == 0)
                _document.CleanupTokenCache(MaxCachedLines);

            _refreshStopwatch.Stop();
            _sbRefreshTime.Value = $"{_refreshStopwatch.ElapsedMilliseconds} ms";

            // Schedule background highlighting only when the visible range changed or dirty lines exist.
            // Never re-schedule from a render triggered by the pipeline itself (breaks render loop).
            bool rangeChanged = _firstVisibleLine != _lastHighlightFirst || _lastVisibleLine != _lastHighlightLast;
            bool hasDirty     = false;
            if (!rangeChanged)
            {
                int lo = Math.Max(0, _firstVisibleLine);
                int hi = Math.Min(_document.Lines.Count - 1, _lastVisibleLine);
                for (int i = lo; i <= hi; i++)
                {
                    if (_document.Lines[i].IsCacheDirty) { hasDirty = true; break; }
                }
            }
            // Skip highlight scheduling while the smooth-scroll animation is running.
            // The visible range changes every ~16 ms during scroll; scheduling on every frame
            // triggers background work + HighlightsComputed → extra InvalidateVisual per frame.
            // A final pass is triggered by SmoothScrollTimer_Tick when the animation settles.
            if ((rangeChanged || hasDirty) && !_smoothScrollTimer.IsEnabled)
            {
                _lastHighlightFirst = _firstVisibleLine;
                _lastHighlightLast  = _lastVisibleLine;
                _highlightPipeline.ScheduleAsync(
                    _document.Lines,
                    _firstVisibleLine,
                    _lastVisibleLine,
                    _highlighter,
                    ExternalHighlighter);
            }

            // Pan mode indicator — drawn last (on top of all content, no clipping)
            _panMode?.Render(dc);
        }

        private int _frameCount = 0; // Frame counter for periodic cache cleanup
        private readonly Stopwatch   _refreshStopwatch = new();
        private readonly StatusBarItem _sbRefreshTime  = new() { Label = "Refresh", Tooltip = "Render frame time in milliseconds", Value = "—" };

        /// <summary>
        /// Measure: update cached max content width; scrollbars manage their own layout.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            // Update cached max content width using incremental tracker (P1-CE-02 — O(1))
            _maxContentWidth = _document != null && _document.Lines.Count > 0
                ? _cachedMaxLineLength * _charWidth + 20
                : 0;

            // Measure scrollbar children
            _vScrollBar?.Measure(new Size(ScrollBarThickness, double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : Math.Max(0, availableSize.Height)));
            _hScrollBar?.Measure(new Size(double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, availableSize.Width), ScrollBarThickness));

            // Fill all available space (scrolling is internal)
            double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double w = double.IsInfinity(availableSize.Width)
                ? Math.Max(400, textLeft + _maxContentWidth + ScrollBarThickness)
                : availableSize.Width;
            int logicalOrVisualRows = IsWordWrapEnabled ? _totalVisualRows : (_document?.Lines.Count ?? 0);
            double h = double.IsInfinity(availableSize.Height)
                ? Math.Max(300, TopMargin + logicalOrVisualRows * _lineHeight + ScrollBarThickness)
                : availableSize.Height;
            return new Size(w, h);
        }

        /// <summary>
        /// Arrange: position scrollbars and update their ranges.
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            double textLeft    = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            int    hiddenLines = _foldingEngine?.TotalHiddenLineCount ?? 0;
            double totalH      = TopMargin + ((_document?.Lines.Count ?? 0) - hiddenLines) * _lineHeight
                             + (ShowInlineHints ? _visibleHintsCount * HintLineHeight : 0);
            double totalTW     = textLeft + _maxContentWidth;

            // Determine which scrollbars are needed (check for mutual dependency)
            bool needsV = totalH  > finalSize.Height;
            bool needsH = totalTW > finalSize.Width;
            if (needsV) needsH = totalTW > (finalSize.Width  - ScrollBarThickness);
            if (needsH) needsV = totalH  > (finalSize.Height - ScrollBarThickness);

            double contentW = needsV ? finalSize.Width  - ScrollBarThickness : finalSize.Width;
            double contentH = needsH ? finalSize.Height - ScrollBarThickness : finalSize.Height;

            // Word wrap: always hide horizontal scrollbar and rebuild map when width changes.
            if (IsWordWrapEnabled)
            {
                needsH = false;
                if (Math.Abs(finalSize.Width - _lastWrapArrangedWidth) > 0.5)
                {
                    _lastWrapArrangedWidth = finalSize.Width;
                    RebuildWrapMap();
                }
            }

            _vScrollBar.Visibility = needsV ? Visibility.Visible : Visibility.Hidden;
            _hScrollBar.Visibility = needsH ? Visibility.Visible : Visibility.Hidden;

            var vScrollRect = needsV ? new Rect(contentW, 0, ScrollBarThickness, contentH) : new Rect(0, 0, 0, 0);
            _vScrollBar.Arrange(vScrollRect);
            _hScrollBar.Arrange(needsH ? new Rect(0, contentH, contentW, ScrollBarThickness) : new Rect(0, 0, 0, 0));

            // Overlay scroll marker panel on top of the vertical scrollbar (click-through).
            _codeScrollMarkerPanel?.Arrange(vScrollRect);

            // Breakpoint gutter: leftmost strip at x=0.
            if (_breakpointGutterControl != null)
            {
                bool showBp = ShowLineNumbers;
                _breakpointGutterControl.Visibility = showBp ? Visibility.Visible : Visibility.Collapsed;
                _breakpointGutterControl.Arrange(showBp
                    ? new Rect(0, 0, BreakpointGutterControl.GutterWidth, contentH)
                    : new Rect(0, 0, 0, 0));
            }

            // Arrange the folding gutter immediately right of the breakpoint gutter (no overlap).
            if (_gutterControl != null)
            {
                bool showGutter = IsFoldingEnabled && ShowLineNumbers;
                _gutterControl.Visibility = showGutter ? Visibility.Visible : Visibility.Collapsed;
                _gutterControl.Arrange(showGutter
                    ? new Rect(BreakpointGutterControl.GutterWidth, 0, _gutterControl.Width, contentH)
                    : new Rect(0, 0, 0, 0));
            }

            // Sticky scroll header: spans the full width above the text area (y=0, x=0).
            if (_stickyScrollHeader != null)
            {
                if (_stickyScrollEnabled)
                {
                    double headerH = _stickyScrollHeader.RequiredHeight;
                    _stickyScrollHeader.Visibility = headerH > 0 ? Visibility.Visible : Visibility.Collapsed;
                    _stickyScrollHeader.Arrange(new Rect(0, 0, contentW, headerH > 0 ? headerH : 0));
                }
                else
                {
                    _stickyScrollHeader.Visibility = Visibility.Collapsed;
                    _stickyScrollHeader.Arrange(new Rect(0, 0, 0, 0));
                }
            }

            UpdateScrollBars(contentW, contentH);
            return finalSize;
        }

        #region ScrollBar Management

        /// <summary>
        /// Sync scrollbar ranges and values with current scroll/content state.
        /// Called from ArrangeOverride and after document/viewport changes.
        /// </summary>
        private void UpdateScrollBars(double contentW, double contentH)
        {
            if (_vScrollBar == null || _hScrollBar == null) return;

            _updatingScrollBar = true;
            try
            {
                double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

                // -- Vertical --------------------------------------------
                double totalH;
                if (IsWordWrapEnabled)
                    totalH = TopMargin + _totalVisualRows * _lineHeight;
                else
                {
                    int foldHidden = _foldingEngine?.TotalHiddenLineCount ?? 0;
                    totalH = TopMargin + ((_document?.Lines.Count ?? 0) - foldHidden) * _lineHeight
                        + (ShowInlineHints ? _visibleHintsCount * HintLineHeight : 0);
                }
                double maxV = Math.Max(0, totalH - contentH);

                // Clamp internal offset (e.g. file got shorter after edit)
                _verticalScrollOffset = Math.Min(_verticalScrollOffset, maxV);
                _currentScrollOffset  = _verticalScrollOffset;
                _targetScrollOffset   = _verticalScrollOffset;
                if (_virtualizationEngine != null)
                    _virtualizationEngine.ScrollOffset = _verticalScrollOffset;

                _vScrollBar.Minimum     = 0;
                _vScrollBar.Maximum     = maxV;
                _vScrollBar.ViewportSize = contentH;
                _vScrollBar.SmallChange = _lineHeight;
                _vScrollBar.LargeChange = contentH;
                _vScrollBar.Value       = _verticalScrollOffset;

                // -- Horizontal ------------------------------------------
                double maxH;
                if (IsWordWrapEnabled)
                {
                    maxH = 0;
                    _horizontalScrollOffset = 0;
                }
                else
                {
                    double totalTW = textLeft + _maxContentWidth;
                    maxH = Math.Max(0, totalTW - contentW);
                    _horizontalScrollOffset = Math.Min(_horizontalScrollOffset, maxH);
                }

                _hScrollBar.Minimum      = 0;
                _hScrollBar.Maximum      = maxH;
                _hScrollBar.ViewportSize = Math.Max(0, contentW - textLeft);
                _hScrollBar.SmallChange  = _charWidth * 3;
                _hScrollBar.LargeChange  = Math.Max(contentW - textLeft, _charWidth);
                _hScrollBar.Value        = _horizontalScrollOffset;
            }
            finally
            {
                _updatingScrollBar = false;
            }
        }

        private void SyncVScrollBar()
        {
            if (_vScrollBar == null || _updatingScrollBar) return;
            _updatingScrollBar = true;
            _vScrollBar.Value  = _verticalScrollOffset;
            _updatingScrollBar = false;
        }

        private void SyncHScrollBar()
        {
            if (_hScrollBar == null || _updatingScrollBar) return;
            _updatingScrollBar = true;
            _hScrollBar.Value  = _horizontalScrollOffset;
            _updatingScrollBar = false;
        }

        private void VScrollBar_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingScrollBar) return;
            _verticalScrollOffset = e.NewValue;
            _currentScrollOffset  = e.NewValue;
            _targetScrollOffset   = e.NewValue;
            if (_virtualizationEngine != null)
            {
                _virtualizationEngine.ScrollOffset = e.NewValue;
                _virtualizationEngine.CalculateVisibleRange();
            }
            InvalidateVisual();
        }

        private void HScrollBar_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingScrollBar) return;
            _horizontalScrollOffset = e.NewValue;
            InvalidateVisual();
        }

        /// <summary>
        /// Ensure the cursor column is visible in the text area (horizontal auto-scroll).
        /// </summary>
        private void EnsureCursorColumnVisible()
        {
            if (_hScrollBar == null || IsWordWrapEnabled) return;
            double textLeft  = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double contentW  = ActualWidth - (_vScrollBar?.Visibility == Visibility.Visible ? ScrollBarThickness : 0);
            double textAreaW = Math.Max(0, contentW - textLeft);
            if (textAreaW <= 0) return;

            double cursorX = _cursorColumn * _charWidth;        // position in the text area (no offset)
            double rightEdge = cursorX + _charWidth;

            if (cursorX < _horizontalScrollOffset)
            {
                _horizontalScrollOffset = Math.Max(0, cursorX);
                SyncHScrollBar();
                InvalidateVisual();
            }
            else if (rightEdge > _horizontalScrollOffset + textAreaW)
            {
                _horizontalScrollOffset = rightEdge - textAreaW;
                SyncHScrollBar();
                InvalidateVisual();
            }
        }

        #endregion

        /// <summary>
        /// Calculate which lines are visible in the viewport
        /// Phase 1: Simple calculation (no virtual scrolling yet)
        /// </summary>
        private void CalculateVisibleLines()
        {
            bool hasHBar = _hScrollBar?.Visibility == Visibility.Visible;
            double viewportH = ActualHeight - TopMargin - (hasHBar ? ScrollBarThickness : 0);

            if (IsWordWrapEnabled && _wrapOffsets.Length > 0)
            {
                // Word wrap: compute first/last visible logical lines from wrap map.
                int firstVisRow  = Math.Max(0, (int)(_verticalScrollOffset / _lineHeight));
                int lastVisRow   = Math.Min(_totalVisualRows - 1,
                    firstVisRow + (int)(viewportH / _lineHeight) + RenderBuffer + 1);
                _firstVisibleLine = WrapVisualRowToLogical(firstVisRow).logLine;
                _lastVisibleLine  = WrapVisualRowToLogical(lastVisRow).logLine;
                _firstVisibleLine = Math.Max(0, Math.Min(_firstVisibleLine, _document.Lines.Count - 1));
                _lastVisibleLine  = Math.Max(0, Math.Min(_lastVisibleLine,  _document.Lines.Count - 1));
                _gutterControl?.Update(_lineHeight, _firstVisibleLine, _lastVisibleLine,
                                       TopMargin, 0.0, _lineYLookup);
                return;
            }

            // Phase 11: Use VirtualizationEngine if enabled
            if (EnableVirtualScrolling && _virtualizationEngine != null)
            {
                // Update virtualization state
                _virtualizationEngine.ViewportHeight = viewportH;
                _virtualizationEngine.LineHeight = _lineHeight;
                _virtualizationEngine.ScrollOffset = _verticalScrollOffset;

                // Calculate visible range with render buffer
                var (first, last) = _virtualizationEngine.CalculateVisibleRange();
                _firstVisibleLine = first;
                _lastVisibleLine = last;
            }
            else
            {
                // Phase 1 fallback: Show all lines that fit in viewport (no virtualization)
                _firstVisibleLine = 0;
                _lastVisibleLine = Math.Min(_document.Lines.Count - 1,
                    (int)(viewportH / _lineHeight));
            }

            // Forward-scan: count visible (non-hidden) lines from _firstVisibleLine until the
            // viewport + render buffer is filled.  Single-pass; correctly handles folds whose
            // hidden range spans the initial VirtualizationEngine window.
            if (_foldingEngine != null && _foldingEngine.TotalHiddenLineCount > 0)
            {
                int needed  = (int)(viewportH / _lineHeight) + RenderBuffer + 1;
                int visible = 0;
                int i       = _firstVisibleLine;
                while (i < _document.Lines.Count)
                {
                    if (!_foldingEngine.IsLineHidden(i))
                    {
                        visible++;
                        if (visible >= needed) break;
                    }
                    i++;
                }
                _lastVisibleLine = Math.Min(_document.Lines.Count - 1, i);
            }

            // Rebuild visible-region cache for RenderScopeGuides (OPT-PERF-03).
            _visibleRegions.Clear();
            if (_foldingEngine != null)
            {
                foreach (var r in _foldingEngine.Regions)
                {
                    if (r.IsCollapsed || r.Kind == FoldingRegionKind.Directive) continue;
                    if (r.EndLine < _firstVisibleLine || r.StartLine + 1 > _lastVisibleLine) continue;
                    _visibleRegions.Add(r);
                }
            }

            // Sync gutter layout with the newly computed visible range.
            // Pass scroll fraction so gutter markers follow smooth-scroll sub-pixel offset.
            double gutterScrollFraction = (EnableVirtualScrolling && _virtualizationEngine != null)
                ? _virtualizationEngine.GetLineYPosition(_firstVisibleLine)
                : 0.0;
            _gutterControl?.Update(_lineHeight, _firstVisibleLine, _lastVisibleLine,
                                   TopMargin, gutterScrollFraction, _lineYLookup);

            // Sync breakpoint gutter with same visible range + gutter background brush.
            var bpBg = TryFindResource("LineNumberBackground") as System.Windows.Media.Brush
                    ?? System.Windows.Media.Brushes.Transparent;
            _breakpointGutterControl?.Update(
                _lineHeight, _firstVisibleLine, _lastVisibleLine, TopMargin, _lineYLookup, bpBg);
        }

        /// <summary>
        /// Render line numbers in left gutter
        /// </summary>
        private void RenderLineNumbers(DrawingContext dc)
        {
            // Flush line-number FormattedText cache when font parameters change (P1-CE-03)
            if (_cachedLineNumberFontSize != LineNumberFontSize ||
                !Equals(_cachedLineNumberTypeface, _lineNumberTypeface))
            {
                _lineNumberCache.Clear();
                _cachedLineNumberFontSize = LineNumberFontSize;
                _cachedLineNumberTypeface = _lineNumberTypeface;
            }

            double dpi = _renderPixelsPerDip; // cached once per OnRender (OPT-PERF-03)

            // Word-wrap path: iterate _visLinePositions; show line number only for sub-row 0.
            if (IsWordWrapEnabled && _visLinePositions.Count > 0)
            {
                for (int visPos = 0; visPos < _visLinePositions.Count; visPos++)
                {
                    int subRow = visPos < _visLineSubRows.Count ? _visLineSubRows[visPos] : 0;
                    if (subRow != 0) continue; // only draw number on first visual row of each logical line

                    var (i, y) = _visLinePositions[visPos];
                    if (i >= _document.Lines.Count) break;

                    if (!_lineNumberCache.TryGetValue(i + 1, out var ft))
                    {
                        ft = new FormattedText((i + 1).ToString(),
                            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                            _lineNumberTypeface, LineNumberFontSize, LineNumberForeground, dpi);
                        _lineNumberCache[i + 1] = ft;
                    }
                    double lnX   = LineNumberWidth - ft.Width - LineNumberMargin;
                    double lineY = _glyphRenderer != null
                        ? y + _glyphRenderer.Baseline - ft.Baseline
                        : y + (_lineHeight - ft.Height) / 2;
                    dc.DrawText(ft, new Point(lnX, lineY));
                    RenderValidationGlyph(dc, i, y);
                }
                dc.DrawLine(s_lineNumberSeparatorPen, new Point(LineNumberWidth, 0), new Point(LineNumberWidth, ActualHeight));
                return;
            }

            int visIdx = 0;
            for (int i = _firstVisibleLine; i <= _lastVisibleLine && i < _document.Lines.Count; i++)
            {
                // Skip lines hidden inside a collapsed fold region.
                if (_foldingEngine != null && _foldingEngine.IsLineHidden(i)) continue;

                double y = GetFoldAwareLineY(visIdx);
                visIdx++;

                // Cache FormattedText per line number — eliminates 2,400 allocations/s (P1-CE-03)
                if (!_lineNumberCache.TryGetValue(i + 1, out var formattedText))
                {
                    formattedText = new FormattedText(
                        (i + 1).ToString(),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        _lineNumberTypeface,
                        LineNumberFontSize,
                        LineNumberForeground,
                        dpi);
                    _lineNumberCache[i + 1] = formattedText;
                }

                // Right-align line numbers.
                // Align the FormattedText baseline to the GlyphRun baseline so the line
                // number sits on the same optical baseline as the code text on the same row.
                // Fallback: vertical center when GlyphRunRenderer is not yet initialised.
                double x     = LineNumberWidth - formattedText.Width - LineNumberMargin;
                double lineY = _glyphRenderer != null
                    ? y + _glyphRenderer.Baseline - formattedText.Baseline
                    : y + (_lineHeight - formattedText.Height) / 2;

                dc.DrawText(formattedText, new Point(x, lineY));

                // Render validation glyphs (error/warning icons) in left margin
                RenderValidationGlyph(dc, i, y);
            }

            // Draw separator line between line numbers and text (cached frozen pen — OPT-PERF-02)
            dc.DrawLine(s_lineNumberSeparatorPen, new Point(LineNumberWidth, 0), new Point(LineNumberWidth, ActualHeight));
        }

        /// <summary>
        /// Render validation glyph (error/warning icon) for a line if it has validation errors
        /// </summary>
        private void RenderValidationGlyph(DrawingContext dc, int line, double y)
        {
            // OPT-PERF-01: O(1) dictionary lookup instead of O(n) LINQ scan per visible line.
            if (!EnableValidation || _validationByLine.Count == 0) return;
            if (!_validationByLine.TryGetValue(line, out var lineErrors)) return;

            // Worst severity drives the glyph color (Error > Warning > Info).
            ValidationSeverity worstSeverity = lineErrors[0].Severity;
            for (int i = 1; i < lineErrors.Count; i++)
                if (lineErrors[i].Severity > worstSeverity) worstSeverity = lineErrors[i].Severity;

            if (worstSeverity == ValidationSeverity.Info) return; // No glyph for Info

            // Use themed CE_GutterError / CE_GutterWarning when available; fall back to static pens.
            Brush glyphBrush = worstSeverity == ValidationSeverity.Error
                ? (TryFindResource("CE_GutterError")   as Brush ?? s_squigglyError.Brush)
                : (TryFindResource("CE_GutterWarning") as Brush ?? s_squigglyWarning.Brush);

            double glyphSize = Math.Min(_lineHeight * 0.6, 12);
            double glyphX    = 5;
            double glyphY    = y + (_lineHeight - glyphSize) / 2;

            dc.DrawEllipse(glyphBrush, null, new Point(glyphX + glyphSize / 2, glyphY + glyphSize / 2), glyphSize / 2, glyphSize / 2);

            if (worstSeverity == ValidationSeverity.Error)
            {
                double offset = glyphSize * 0.25;
                dc.DrawLine(s_glyphInnerPen, new Point(glyphX + offset, glyphY + offset),             new Point(glyphX + glyphSize - offset, glyphY + glyphSize - offset));
                dc.DrawLine(s_glyphInnerPen, new Point(glyphX + glyphSize - offset, glyphY + offset), new Point(glyphX + offset, glyphY + glyphSize - offset));
            }
            else
            {
                double centerX = glyphX + glyphSize / 2;
                dc.DrawLine(s_glyphInnerPen, new Point(centerX, glyphY + glyphSize * 0.2), new Point(centerX, glyphY + glyphSize * 0.6));
                dc.DrawEllipse(Brushes.White, null, new Point(centerX, glyphY + glyphSize * 0.8), 1, 1);
            }
        }

        /// <summary>
        /// Render current line highlight
        /// </summary>
        private void RenderCurrentLineHighlight(DrawingContext dc, double contentW, double contentH)
        {
            if (_cursorLine < _firstVisibleLine || _cursorLine > _lastVisibleLine)
                return;

            // Do not highlight a line that is hidden inside a collapsed fold region.
            if (_foldingEngine != null && _foldingEngine.IsLineHidden(_cursorLine))
                return;

            double y;
            double highlightH;

            if (IsWordWrapEnabled)
            {
                // Word wrap: Y = first visual row of logical line; height covers all visual sub-rows.
                y = _lineYLookup.TryGetValue(_cursorLine, out double wy) ? wy : TopMargin;
                int wrapRows = (_wrapHeights.Length > _cursorLine) ? _wrapHeights[_cursorLine] : 1;
                highlightH = wrapRows * _lineHeight;
            }
            else
            {
                // Count non-hidden lines before _cursorLine to compute the correct visual Y.
                int visIdx = 0;
                for (int i = _firstVisibleLine; i < _cursorLine; i++)
                    if (_foldingEngine == null || !_foldingEngine.IsLineHidden(i)) visIdx++;
                y = GetFoldAwareLineY(visIdx);
                highlightH = _lineHeight;
            }

            double x = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // Draw background highlight (spans visible text area width — no H offset needed)
            if (ShowCurrentLineHighlight)
            {
                dc.DrawRectangle(CurrentLineBackground, null,
                    new Rect(x, y, contentW - x, highlightH));
            }

            // Draw border if enabled
            if (ShowCurrentLineBorder)
            {
                // Cache pen; rebuilt only when CurrentLineBorderColor DP changes (OPT-PERF-03).
                if (_cachedCurrentLineBorderPen == null || _cachedCurrentLineBorderColor != CurrentLineBorderColor)
                {
                    _cachedCurrentLineBorderColor = CurrentLineBorderColor;
                    _cachedCurrentLineBorderPen = new Pen(new SolidColorBrush(_cachedCurrentLineBorderColor), 1);
                    _cachedCurrentLineBorderPen.Freeze();
                }
                dc.DrawRectangle(null, _cachedCurrentLineBorderPen,
                    new Rect(x, y, contentW - x, highlightH));
            }
        }

        /// <summary>
        /// Render text selection overlay — word wrap path: each logical line is split into
        /// visual sub-rows; only the portion of the selected column range that falls within
        /// each sub-row is highlighted. (ADR-049)
        /// </summary>
        private void RenderSelectionWrapped(DrawingContext dc, Brush selectionBrush)
        {
            var start = _selection.NormalizedStart;
            var end   = _selection.NormalizedEnd;
            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            int cpr = Math.Max(1, _charsPerVisualLine);

            int firstLine = Math.Max(start.Line, _firstVisibleLine);
            int lastLine  = Math.Min(end.Line,   _lastVisibleLine);

            for (int line = firstLine; line <= lastLine; line++)
            {
                if (!_lineYLookup.TryGetValue(line, out double lineFirstRowY)) continue;

                int lineLen     = (line < _document.Lines.Count) ? _document.Lines[line].Length : 0;
                int selStartCol = (line == start.Line) ? start.Column : 0;
                int selEndCol   = (line == end.Line)   ? end.Column   : lineLen;
                if (selEndCol <= selStartCol) continue;

                int wrapRows = (_wrapHeights.Length > line) ? _wrapHeights[line] : 1;

                for (int s = 0; s < wrapRows; s++)
                {
                    int subStart  = s * cpr;
                    int subEnd    = subStart + cpr;
                    int bandStart = Math.Max(selStartCol, subStart);
                    int bandEnd   = Math.Min(selEndCol,   subEnd);
                    if (bandEnd <= bandStart) continue;

                    double y  = lineFirstRowY + s * _lineHeight;
                    double x1 = leftEdge + (bandStart - subStart) * _charWidth;
                    double x2 = leftEdge + (bandEnd   - subStart) * _charWidth;
                    if (x2 <= x1) x2 = x1 + _charWidth;

                    dc.DrawRoundedRectangle(selectionBrush, null,
                        new Rect(x1, y, x2 - x1, _lineHeight),
                        SelectionCornerRadius, SelectionCornerRadius);
                }
            }
        }

        /// <summary>
        /// Render text selection overlay (Phase 3 - Enhanced with multi-line support)
        /// </summary>
        private void RenderSelection(DrawingContext dc)
        {
            if (_selection.IsEmpty)
                return;

            // Use InactiveSelectionBackground when the editor (or any child) has no keyboard focus.
            // IsKeyboardFocusWithin is used rather than IsFocused so that interacting with
            // the scrollbars (child visuals) does not incorrectly dim the selection.
            Brush selectionBrush = IsKeyboardFocusWithin ? SelectionBackground : InactiveSelectionBackground;

            if (IsWordWrapEnabled)
            {
                RenderSelectionWrapped(dc, selectionBrush);
                return;
            }

            var start = _selection.NormalizedStart;
            var end = _selection.NormalizedEnd;

            // Single-line selection
            if (start.Line == end.Line)
            {
                if (start.Line >= _firstVisibleLine && start.Line <= _lastVisibleLine)
                {
                    double y = _lineYLookup.TryGetValue(start.Line, out double sy) ? sy
                        : (EnableVirtualScrolling && _virtualizationEngine != null
                            ? TopMargin + _virtualizationEngine.GetLineYPosition(start.Line)
                            : TopMargin + (start.Line - _firstVisibleLine) * _lineHeight);

                    double x1 = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + (start.Column * _charWidth);
                    double x2 = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + (end.Column * _charWidth);

                    dc.DrawRoundedRectangle(selectionBrush, null, new Rect(x1, y, x2 - x1, _lineHeight), SelectionCornerRadius, SelectionCornerRadius);
                }
            }
            else // Multi-line selection (Phase 3)
            {
                double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

                // Collect overlapping segments then union them so the brush is applied once,
                // preventing double-alpha darkening at junctions with semi-transparent selection brushes.
                var segments = new List<Geometry>();

                // First line — extend bottom by CornerRadius so the rounded tail merges with next segment
                if (start.Line >= _firstVisibleLine && start.Line <= _lastVisibleLine)
                {
                    double y  = _lineYLookup.TryGetValue(start.Line, out double fsy) ? fsy
                        : TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
                            ? _virtualizationEngine.GetLineYPosition(start.Line)
                            : (start.Line - _firstVisibleLine) * _lineHeight);
                    double x1 = leftEdge + (start.Column * _charWidth);
                    double x2 = leftEdge + (_document.Lines[start.Line].Length * _charWidth);
                    segments.Add(new RectangleGeometry(new Rect(x1, y, Math.Max(x2 - x1, _charWidth), _lineHeight + SelectionCornerRadius), SelectionCornerRadius, SelectionCornerRadius));
                }

                // Middle lines — extend top and bottom by CornerRadius to merge with neighbours.
                // Clamp to visible viewport so the loop is O(visible_lines) rather than O(selected_lines).
                int middleFirst = Math.Max(start.Line + 1, _firstVisibleLine);
                int middleLast  = Math.Min(end.Line   - 1, _lastVisibleLine);
                for (int line = middleFirst; line <= middleLast; line++)
                {
                    double lineBaseY = _lineYLookup.TryGetValue(line, out double mly) ? mly
                        : TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
                            ? _virtualizationEngine.GetLineYPosition(line)
                            : (line - _firstVisibleLine) * _lineHeight);
                    double y = lineBaseY - SelectionCornerRadius;
                    double width = _document.Lines[line].Length * _charWidth;
                    segments.Add(new RectangleGeometry(new Rect(leftEdge, y, Math.Max(width, _charWidth), _lineHeight + SelectionCornerRadius * 2), SelectionCornerRadius, SelectionCornerRadius));
                }

                // Last line — extend top by CornerRadius so the rounded head merges with previous segment
                if (end.Line >= _firstVisibleLine && end.Line <= _lastVisibleLine)
                {
                    double y  = (_lineYLookup.TryGetValue(end.Line, out double ely) ? ely
                        : TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
                            ? _virtualizationEngine.GetLineYPosition(end.Line)
                            : (end.Line - _firstVisibleLine) * _lineHeight)) - SelectionCornerRadius;
                    double x2 = leftEdge + (end.Column * _charWidth);
                    segments.Add(new RectangleGeometry(new Rect(leftEdge, y, x2 - leftEdge, _lineHeight + SelectionCornerRadius), SelectionCornerRadius, SelectionCornerRadius));
                }

                if (segments.Count > 0)
                {
                    // Cache combined geometry — avoid O(n²) Geometry.Combine when selection is stable (OPT-PERF-04).
                    if (_cachedSelectionGeometry == null
                        || !_cachedSelGeomStart.Equals(start) || !_cachedSelGeomEnd.Equals(end)
                        || _cachedSelGeomFirstLine != _firstVisibleLine || _cachedSelGeomLastLine != _lastVisibleLine)
                    {
                        Geometry combined = segments[0];
                        for (int i = 1; i < segments.Count; i++)
                            combined = Geometry.Combine(combined, segments[i], GeometryCombineMode.Union, null);
                        combined.Freeze();
                        _cachedSelectionGeometry = combined;
                        _cachedSelGeomStart      = start;
                        _cachedSelGeomEnd        = end;
                        _cachedSelGeomFirstLine  = _firstVisibleLine;
                        _cachedSelGeomLastLine   = _lastVisibleLine;
                    }
                    dc.DrawGeometry(selectionBrush, null, _cachedSelectionGeometry);
                }
            }
        }

        /// <summary>
        /// Renders the rectangular (block/column) selection overlay as a single seamless rectangle
        /// spanning the full vertical extent of the selection. Drawing one rectangle eliminates
        /// the anti-aliasing seams that appear when drawing one rect per line.
        /// Uses _lineYLookup for InlineHints-aware Y offsets (mandatory).
        /// </summary>
        private void RenderRectSelection(DrawingContext dc)
        {
            if (_rectSelection.IsEmpty) return;

            // Clamp selection range to the visible viewport.
            int visTop    = Math.Max(_rectSelection.TopLine,    _firstVisibleLine);
            int visBottom = Math.Min(_rectSelection.BottomLine, _lastVisibleLine);
            if (visTop > visBottom) return; // selection entirely outside viewport

            Brush selBrush = IsKeyboardFocusWithin ? SelectionBackground : InactiveSelectionBackground;

            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            var (leftCol, rightCol) = _rectSelection.GetColumnRange();
            double x1    = leftEdge + leftCol  * _charWidth;
            double x2    = leftEdge + rightCol * _charWidth;
            double width = Math.Max(x2 - x1, 1.0); // at least 1px when collapsed

            // Mandatory: use _lineYLookup for InlineHints-aware Y offset.
            double yTop = _lineYLookup.TryGetValue(visTop, out double lt) ? lt
                : TopMargin + (visTop - _firstVisibleLine) * _lineHeight;
            double yBottom = (_lineYLookup.TryGetValue(visBottom, out double lb) ? lb
                : TopMargin + (visBottom - _firstVisibleLine) * _lineHeight) + _lineHeight;

            dc.DrawRectangle(selBrush, null, new Rect(x1, yTop, width, yBottom - yTop));
        }

        /// <summary>
        /// Renders a 2px wide vertical insertion-caret bar at the drag-drop target position.
        /// Orange by default (VS convention for drag insertion points).
        /// </summary>
        private void RenderDragDropCaret(DrawingContext dc)
        {
            if (_dragDrop.Phase != DragPhase.Dragging) return;

            var drop = _dragDrop.DropPosition;
            if (drop.Line < _firstVisibleLine || drop.Line > _lastVisibleLine) return;

            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;
            double y = _lineYLookup.TryGetValue(drop.Line, out double ly) ? ly
                : TopMargin + (drop.Line - _firstVisibleLine) * _lineHeight;
            double x = leftEdge + drop.Column * _charWidth;

            var caretBrush = TryFindResource("CE_DragCaret") as Brush ?? Brushes.Orange;
            dc.DrawRectangle(caretBrush, null, new Rect(x - 1, y, 2, _lineHeight));
        }

        /// <summary>
        /// Render find/replace results highlighting
        /// </summary>
        private void RenderFindResults(DrawingContext dc)
        {
            if (_findResults == null || _findResults.Count == 0)
                return;

            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // Render all find results with FindResultColor
            for (int i = 0; i < _findResults.Count; i++)
            {
                var result = _findResults[i];

                if (result.Line < _firstVisibleLine || result.Line > _lastVisibleLine)
                    continue;

                // Y: use _lineYLookup so InlineHints hint rows are accounted for
                double y = _lineYLookup.TryGetValue(result.Line, out double ry) ? ry
                    : TopMargin + (result.Line - _firstVisibleLine) * _lineHeight;

                // X: expand tabs correctly via ComputeVisualX instead of raw column * charWidth
                var lineText = result.Line < _document.Lines.Count ? _document.Lines[result.Line].Text : string.Empty;
                double x1 = leftEdge + _glyphRenderer.ComputeVisualX(lineText, result.Column);
                double x2 = leftEdge + _glyphRenderer.ComputeVisualX(lineText, result.Column + _findMatchLength);

                // Use HighlightMatchColor for current match, FindResultColor for others
                Brush highlightBrush = (i == _currentFindMatchIndex)
                    ? HighlightMatchColor
                    : FindResultColor;
                if (highlightBrush.IsFrozen == false)
                    highlightBrush.Freeze();

                dc.DrawRoundedRectangle(highlightBrush, null, new Rect(x1, y, x2 - x1, _lineHeight), SelectionCornerRadius, SelectionCornerRadius);
            }
        }

        /// <summary>
        /// Renders a subtle highlight box on every occurrence of the word currently under the caret.
        /// Called from OnRender after RenderFindResults and before RenderSelection.
        /// </summary>
        private void RenderWordHighlights(DrawingContext dc)
        {
            if (_wordHighlights.Count == 0 || _wordHighlightLen == 0)
                return;

            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            foreach (var pos in _wordHighlights)
            {
                if (pos.Line < _firstVisibleLine || pos.Line > _lastVisibleLine)
                    continue;

                // Word highlights on InlineHints declaration lines are distracting:
                // the hint zone sits above the code text and the rectangle would
                // overlap it.  Skip these lines entirely.
                if (ShowInlineHints && IsHintEntryVisible(pos.Line))
                    continue;

                // Skip collapsed directive opener lines — text is blanked; a rect outline
                // would appear as stray top/bottom horizontal strokes over the fold label.
                if (_foldingEngine?.GetRegionAt(pos.Line) is { IsCollapsed: true, Kind: FoldingRegionKind.Directive })
                    continue;

                double y = _lineYLookup.TryGetValue(pos.Line, out double wy) ? wy
                    : (EnableVirtualScrolling && _virtualizationEngine != null
                        ? TopMargin + _virtualizationEngine.GetLineYPosition(pos.Line)
                        : TopMargin + (pos.Line - _firstVisibleLine) * _lineHeight);

                double x1 = leftEdge + pos.Column * _charWidth;
                double x2 = x1 + _wordHighlightLen * _charWidth;

                dc.DrawRectangle(s_wordHighlightBg, s_wordHighlightPen,
                    new Rect(x1, y, x2 - x1, _lineHeight));
            }
        }

        /// <summary>
        /// Extracts the identifier word at <paramref name="col"/> within <paramref name="text"/>.
        /// Returns an empty string if the character at col is not a word character or the word is shorter than 2 chars.
        /// </summary>
        private (string Word, int StartCol) GetWordAt(string text, int col)
        {
            if (string.IsNullOrEmpty(text) || col < 0 || col >= text.Length)
                return (string.Empty, col);

            if (!IsWordChar(text[col]))
                return (string.Empty, col);

            int start = col;
            while (start > 0 && IsWordChar(text[start - 1]))
                start--;

            int end = col;
            while (end < text.Length - 1 && IsWordChar(text[end + 1]))
                end++;

            string word = text.Substring(start, end - start + 1);
            return word.Length >= 2 ? (word, start) : (string.Empty, start);
        }

        /// <summary>
        /// Returns the <see cref="SyntaxTokenKind"/> of the cached syntax token that covers
        /// <paramref name="column"/> on <paramref name="lineIndex"/>.
        /// Returns <see cref="SyntaxTokenKind.Default"/> when the cache is not yet populated
        /// or no token covers that column (uncached lines are treated as potentially navigable).
        /// </summary>
        private SyntaxTokenKind GetTokenKindAtColumn(int lineIndex, int column)
        {
            if (lineIndex < 0 || lineIndex >= _document.Lines.Count) return SyntaxTokenKind.Default;

            var cache = _document.Lines[lineIndex].TokensCache;
            if (cache is null) return SyntaxTokenKind.Default;

            foreach (var token in cache)
            {
                if (column >= token.StartColumn && column < token.StartColumn + token.Length)
                    return token.Kind;
            }
            return SyntaxTokenKind.Default;
        }

        /// <summary>
        /// Rescans the document for all occurrences of the word under the caret and
        /// updates both the viewport highlight list and the scroll marker panel.
        /// Called by the debounce timer; runs on the UI thread.
        /// </summary>
        private void UpdateWordHighlights()
        {
            _wordHighlights.Clear();
            _wordHighlightWord = string.Empty;
            _wordHighlightLen  = 0;

            string word = ResolveHighlightWord();

            if (word.Length >= 2)
            {
                _wordHighlightWord = word;
                _wordHighlightLen  = word.Length;

                // Whole-word scan across all lines.
                for (int li = 0; li < _document.Lines.Count; li++)
                {
                    string lineText = _document.Lines[li].Text ?? string.Empty;
                    int idx = 0;
                    while ((idx = lineText.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
                    {
                        bool leftOk  = idx == 0                          || !IsWordChar(lineText[idx - 1]);
                        bool rightOk = idx + word.Length >= lineText.Length || !IsWordChar(lineText[idx + word.Length]);

                        if (leftOk && rightOk)
                            _wordHighlights.Add(new TextPosition(li, idx));

                        idx += word.Length;
                    }
                }
            }

            InvalidateVisual();

            // Update scroll bar tick marks.
            if (_codeScrollMarkerPanel != null)
            {
                if (_wordHighlights.Count == 0)
                    _codeScrollMarkerPanel.ClearWordMarkers();
                else
                {
                    var distinctLines = _wordHighlights
                        .Select(p => p.Line)
                        .Distinct()
                        .ToList();
                    _codeScrollMarkerPanel.UpdateWordMarkers(distinctLines,
                        Math.Max(1, _document?.TotalLines ?? 1));
                }

                // Sync caret + selection markers every render pass.
                if (_document != null)
                {
                    int visibleLines = Math.Max(1, _document.Lines.Count - (_foldingEngine?.TotalHiddenLineCount ?? 0));
                    bool hasSelection = !_selection.IsEmpty && _selection.NormalizedStart.Line != _selection.NormalizedEnd.Line;
                    _codeScrollMarkerPanel.UpdateCaretAndSelection(
                        _cursorLine,
                        hasSelection ? _selection.NormalizedStart.Line : -1,
                        hasSelection ? _selection.NormalizedEnd.Line   : -1,
                        visibleLines);
                }
            }
        }

        /// <summary>
        /// Returns the word to highlight: the selected text (single-line, ≥2 chars) if a
        /// selection is active, otherwise the identifier word at the current caret position.
        /// Returns <see cref="string.Empty"/> when no suitable word is found.
        /// </summary>
        private string ResolveHighlightWord()
        {
            if (!EnableWordHighlight || _document == null || _document.Lines.Count == 0)
                return string.Empty;

            // Single-line selection takes priority.
            if (!_selection.IsEmpty && !_selection.IsMultiLine)
            {
                int li = _selection.NormalizedStart.Line;
                int s  = _selection.NormalizedStart.Column;
                int e  = _selection.NormalizedEnd.Column;
                if (li < _document.Lines.Count && e > s)
                {
                    string lt = _document.Lines[li].Text ?? string.Empty;
                    if (e <= lt.Length)
                        return lt.Substring(s, e - s);
                }
            }

            // Fall back to word at caret.
            if (_cursorLine < _document.Lines.Count)
                return GetWordAt(_document.Lines[_cursorLine].Text ?? string.Empty, _cursorColumn).Word;

            return string.Empty;
        }

        /// <summary>
        /// Arms (or re-arms) the 250 ms debounce timer that fires <see cref="UpdateWordHighlights"/>.
        /// Safe to call on every keystroke / caret move.
        /// </summary>
        private void ScheduleWordHighlightUpdate()
        {
            _wordHighlightTimer?.Stop();
            _wordHighlightTimer?.Start();
        }

        /// <summary>
        /// Render text content with syntax highlighting (Phase 2)
        /// </summary>
        /// <summary>
        /// Word-wrap path for <see cref="RenderTextContent"/>. Iterates <see cref="_visLinePositions"/>
        /// (which has one entry per visible sub-row) and renders each sub-line segment. (ADR-049)
        /// </summary>
        private void RenderTextContentWrapped(DrawingContext dc, double x)
        {
            if (_document is null) return;
            double dpi       = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var    defaultFg = (Brush?)TryFindResource("CE_Foreground") ?? Brushes.White;

            ExternalHighlighter?.Reset();

            // Cache fresh tokens for the current logical line so continuation sub-rows
            // reuse the same UI-thread-resolved brushes instead of the pipeline-cached ones.
            IReadOnlyList<Helpers.SyntaxHighlightToken>? lineTokensCache = null;
            int lastCachedLine = -1;

            for (int visPos = 0; visPos < _visLinePositions.Count; visPos++)
            {
                var (logLine, y) = _visLinePositions[visPos];
                int subRow       = visPos < _visLineSubRows.Count ? _visLineSubRows[visPos] : 0;
                if (logLine >= _document.Lines.Count) break;

                var lineText = _document.Lines[logLine].Text ?? string.Empty;
                int startCol = subRow * _charsPerVisualLine;
                int endCol   = Math.Min(startCol + _charsPerVisualLine, lineText.Length);
                if (startCol > 0 && startCol >= lineText.Length) continue;
                if (string.IsNullOrEmpty(lineText)) continue;

                // Highlight the logical line once (subRow == 0) and cache for continuation rows.
                // All sub-rows of the same logical line share one UI-thread highlight call so
                // brushes are resolved correctly and the block-comment state advances once.
                if (ExternalHighlighter is { } ext && (subRow == 0 || logLine != lastCachedLine))
                {
                    lineTokensCache = ext.Highlight(lineText, logLine)
                        .Select(t => t with { Foreground = ResolveBrushForKind(t.Kind) ?? t.Foreground })
                        .ToList();
                    lastCachedLine = logLine;
                }

                IEnumerable<Helpers.SyntaxHighlightToken> rawTokens =
                    lineTokensCache ?? (IEnumerable<Helpers.SyntaxHighlightToken>)[];

                double baselineY = _glyphRenderer != null
                    ? y + _glyphRenderer.Baseline
                    : y + _charHeight * 0.8;

                // Base pass: paint the sub-line in the default foreground so characters not
                // covered by any token remain visible — mirrors the non-wrap base pass.
                if (ExternalHighlighter is not null && endCol > startCol)
                {
                    var subLine   = lineText.Substring(startCol, endCol - startCol);
                    var baseToken = new Helpers.SyntaxHighlightToken(startCol, endCol - startCol, subLine, defaultFg);
                    if (_glyphRenderer != null)
                        _glyphRenderer.RenderToken(dc, baseToken, x, y, baselineY);
                    else
                    {
                        var ftBase = new FormattedText(subLine, System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight, _typeface, _fontSize, defaultFg, dpi);
                        dc.DrawText(ftBase, new Point(x, y));
                    }
                }

                foreach (var token in rawTokens.OrderBy(t => t.StartColumn))
                {
                    int tokenEnd = token.StartColumn + token.Length;
                    if (tokenEnd <= startCol) continue;
                    if (token.StartColumn >= endCol) break;

                    int ss = Math.Max(token.StartColumn, startCol);
                    int se = Math.Min(tokenEnd, endCol);
                    if (se <= ss) continue;

                    var    span   = lineText.Substring(ss, se - ss);
                    var    brush  = token.Foreground ?? defaultFg;
                    double tokenX = x + (ss - startCol) * _charWidth;

                    // Use GlyphRunRenderer when available — same sharp ClearType rendering
                    // as the non-wrap path; correctly applies IsBold / IsItalic flags.
                    if (_glyphRenderer != null)
                    {
                        var sliced = token with { Text = span, StartColumn = ss, Length = se - ss };
                        _glyphRenderer.RenderToken(dc, sliced, tokenX, y, baselineY);
                    }
                    else
                    {
                        var typeface = token.IsBold ? _boldTypeface : _typeface;
                        var ft = new FormattedText(span, System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight, typeface, _fontSize, brush, dpi);
                        if (token.IsItalic)
                            ft.SetFontStyle(FontStyles.Italic);
                        dc.DrawText(ft, new Point(tokenX, y));
                    }
                }
            }
        }

        private void RenderTextContent(DrawingContext dc)
        {
            double x = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            var context = new JsonParserContext();

            // Rebuild URL hit-zones each render pass (document may have changed).
            _urlHitZones.Clear();
            _foldLabelHitZones.Clear();

            // Cache URL pen; rebuilt only when SyntaxUrlColor DP reference changes (OPT-PERF-03).
            if (_cachedUrlPen == null || !ReferenceEquals(_cachedUrlBrush, SyntaxUrlColor))
            {
                _cachedUrlBrush = SyntaxUrlColor;
                _cachedUrlPen = new Pen(_cachedUrlBrush, 1.0);
                _cachedUrlPen.Freeze();
            }
            var urlPen = _cachedUrlPen;

            // Reset external highlighter state before a full render pass.
            ExternalHighlighter?.Reset();

            // Word wrap: delegate to dedicated method that iterates visual sub-rows.
            if (IsWordWrapEnabled && _charsPerVisualLine > 0 && _visLinePositions.Count > 0)
            {
                RenderTextContentWrapped(dc, x);
                return;
            }

            int visIdx = 0;
            for (int i = _firstVisibleLine; i <= _lastVisibleLine && i < _document.Lines.Count; i++)
            {
                // Skip lines hidden inside a collapsed fold region.
                if (_foldingEngine != null && _foldingEngine.IsLineHidden(i)) continue;

                double y = GetFoldAwareLineY(visIdx);
                visIdx++;

                var line = _document.Lines[i];

                if (!string.IsNullOrEmpty(line.Text))
                {
                    // ── P1-CE-05: GlyphRun cache fast path ───────────────────────────
                    // When the line text has not changed since the last render and a
                    // GlyphRun cache exists, skip token generation entirely and draw
                    // the pre-built runs via a single PushTransform.  URL hit-zones are
                    // restored from the per-line cache so click detection stays intact.
                    if (_glyphRenderer != null
                        && !line.IsGlyphCacheDirty
                        && line.GlyphRunCache is { Count: > 0 } cachedRuns)
                    {
                        // Restore URL hit-zones (built when the cache was first populated).
                        if (line.CachedUrlZones is { } zones)
                            foreach (var z in zones)
                                _urlHitZones.Add(new UrlHitZone(i, z.StartCol, z.EndCol, z.Url));

                        // Translate once per line → draw all cached GlyphRuns.
                        dc.PushTransform(new System.Windows.Media.TranslateTransform(x, y));
                        foreach (var entry in cachedRuns)
                            dc.DrawGlyphRun(entry.Foreground, entry.Run);
                        dc.Pop();

                        // URL hover underline (changes per mouse-move without dirtying the cache).
                        if (_hoveredUrlZone.HasValue && _hoveredUrlZone.Value.Line == i)
                        {
                            foreach (var entry in cachedRuns)
                            {
                                if (entry.IsUrlToken
                                    && entry.StartColumn >= _hoveredUrlZone.Value.StartCol
                                    && entry.StartColumn <  _hoveredUrlZone.Value.EndCol)
                                {
                                    double underlineY = y + _glyphRenderer.Baseline + 2;
                                    double tokenX     = x + entry.Run.BaselineOrigin.X;
                                    dc.DrawLine(urlPen,
                                        new Point(tokenX, underlineY),
                                        new Point(tokenX + entry.TokenLength * _charWidth, underlineY));
                                }
                            }
                        }

                        // Draw fold-collapse label at end of line if this is a collapsed region opener.
                        RenderFoldCollapseLabel(dc, i, x, y);

                        // Advance stateful highlighter even when using the glyph cache,
                        // so block-comment tracking (_inBlockComment) stays correct for
                        // subsequent lines that may not be cached.
                        ExternalHighlighter?.Highlight(line.Text, i);

                        continue; // skip slow path
                    }
                    // ── end fast path ─────────────────────────────────────────────────

                    // ── OPT-A Fast path B: optimistic stale-cache rendering ───────────
                    // The background pipeline has not yet refreshed this line (IsCacheDirty=true)
                    // but a GlyphRun cache from the previous frame is still available.
                    // Render the stale frame immediately so the caret stays instant, and let
                    // the pipeline trigger a clean frame within ~100 ms when it finishes.
                    if (_glyphRenderer != null
                        && line.IsCacheDirty
                        && line.IsGlyphCacheDirty
                        && line.GlyphRunCache is { Count: > 0 } staleRuns)
                    {
                        if (line.CachedUrlZones is { } zones)
                            foreach (var z in zones)
                                _urlHitZones.Add(new UrlHitZone(i, z.StartCol, z.EndCol, z.Url));

                        dc.PushTransform(new System.Windows.Media.TranslateTransform(x, y));
                        foreach (var entry in staleRuns)
                            dc.DrawGlyphRun(entry.Foreground, entry.Run);
                        dc.Pop();

                        // Advance stateful block-comment tracking even when using stale cache
                        // so subsequent (non-cached) lines in the same frame have correct state.
                        ExternalHighlighter?.Highlight(line.Text, i);
                        RenderFoldCollapseLabel(dc, i, x, y);
                        continue;
                    }
                    // ── end OPT-A Fast path B ─────────────────────────────────────────

                    // Use external (language-pluggable) highlighter when available,
                    // otherwise fall back to the built-in JSON highlighter.
                    bool hasExternalHighlighter = ExternalHighlighter is not null;
                    IEnumerable<Helpers.SyntaxHighlightToken> rawTokens;

                    if (ExternalHighlighter is { } ext)
                    {
                        // OPT-A Fast path C: background pipeline has refreshed this line
                        // (IsCacheDirty=false) and fresh tokens are cached.  Use them directly
                        // instead of re-running the regex highlighter on the UI thread.
                        // ext.Highlight() is still called (result discarded) to keep the stateful
                        // block-comment tracker in sync for subsequent lines in this frame.
                        if (!line.IsCacheDirty && line.TokensCache is { Count: > 0 } freshTokens)
                        {
                            ext.Highlight(line.Text, i); // state tracking only — result discarded
                            rawTokens = freshTokens.Select(t => t with
                            {
                                Foreground = ResolveBrushForKind(t.Kind) ?? t.Foreground
                            });
                        }
                        else
                        {
                            // Resolve brushes at render time from live CodeEditor DPs (CE_* keys).
                            // This ensures correct colors even when the theme changes after file open,
                            // and avoids the timing issue of baking brushes at file-open time.
                            rawTokens = ext.Highlight(line.Text, i)
                                .Select(t => t with
                                {
                                    Foreground = ResolveBrushForKind(t.Kind) ?? t.Foreground
                                });
                        }
                    }
                    else
                    {
                        var jsonTokens = _highlighter.HighlightLine(line, context);
                        rawTokens = jsonTokens.Select(t => new Helpers.SyntaxHighlightToken(
                            t.StartColumn, t.Length, t.Text ?? string.Empty,
                            t.Foreground ?? EditorForeground, t.IsBold, t.IsItalic));
                    }

                    // URL post-pass: overlay URL tokens on top of the highlighter output.
                    // URLs are detected regardless of which highlighter is active and always
                    // rendered with SyntaxUrlColor + underline so they are visually distinct.
                    // Materialise to list so we can both render and cache in one pass.
                    var renderTokens = OverlayUrlTokens(line.Text, i, rawTokens).ToList();

                    // Pre-compute baseline Y once per line (GlyphRun requires it).
                    double baselineY = _glyphRenderer != null
                        ? y + _glyphRenderer.Baseline
                        : y + _charHeight * 0.8;

                    // Base pass (external highlighter only): draw the entire line in EditorForeground
                    // so unmatched spans (identifiers, punctuation not covered by any regex rule)
                    // remain visible in the default text color instead of being invisible.
                    if (hasExternalHighlighter)
                    {
                        var baseToken = new Helpers.SyntaxHighlightToken(
                            0, line.Text.Length, line.Text, EditorForeground);
                        if (_glyphRenderer != null)
                            _glyphRenderer.RenderToken(dc, baseToken, x, y, baselineY);
                        else
                        {
                            var ft = new FormattedText(
                                line.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                _typeface, _fontSize, EditorForeground,
                                VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            dc.DrawText(ft, new Point(x, y));
                        }
                    }

                    foreach (var token in renderTokens)
                    {
                        // Use tab-aware X so tokens on tab-indented lines are not shifted left.
                        double tokenX = x + (_glyphRenderer?.ComputeVisualX(line.Text, token.StartColumn)
                                             ?? token.StartColumn * _charWidth);

                        if (_glyphRenderer != null)
                        {
                            _glyphRenderer.RenderToken(dc, token, tokenX, y, baselineY);
                        }
                        else
                        {
                            // Safety fallback: FormattedText (e.g. before first measure pass).
                            var typeface = token.IsBold ? _boldTypeface : _typeface;
                            var ft = new FormattedText(
                                token.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                typeface, _fontSize, token.Foreground,
                                VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            if (token.IsItalic)
                                ft.SetFontStyle(FontStyles.Italic);
                            dc.DrawText(ft, new Point(tokenX, y));
                        }

                        // Draw underline only on the URL currently hovered by the mouse.
                        // This avoids permanent underlines on xmlns/href URIs in XML/XAML files.
                        if (ReferenceEquals(token.Foreground, SyntaxUrlColor)
                            && _hoveredUrlZone.HasValue
                            && _hoveredUrlZone.Value.Line     == i
                            && token.StartColumn              >= _hoveredUrlZone.Value.StartCol
                            && token.StartColumn              <  _hoveredUrlZone.Value.EndCol)
                        {
                            // Place the underline 2px below the text baseline (tight, VS-style).
                            double underlineY = baselineY + 2;
                            dc.DrawLine(urlPen,
                                new Point(tokenX, underlineY),
                                new Point(tokenX + token.Length * _charWidth, underlineY));
                        }
                    }

                    // ── P1-CE-05: Build GlyphRun cache after first render ─────────────
                    // Cache for the base-pass token too when using external highlighter.
                    if (_glyphRenderer != null)
                    {
                        var allCacheTokens = hasExternalHighlighter
                            ? Enumerable.Concat(
                                new[] { new Helpers.SyntaxHighlightToken(
                                    0, line.Text.Length, line.Text, EditorForeground) },
                                renderTokens)
                            : (IEnumerable<Helpers.SyntaxHighlightToken>)renderTokens;

                        line.GlyphRunCache     = _glyphRenderer.BuildLineGlyphRuns(allCacheTokens, SyntaxUrlColor, line.Text);
                        line.IsGlyphCacheDirty = false;

                        // Cache URL zones for GlyphRun-hit renders (no re-run of OverlayUrlTokens).
                        line.CachedUrlZones = _urlHitZones
                            .Where(z => z.Line == i)
                            .Select(z => (z.StartCol, z.EndCol, z.Url))
                            .ToList();
                    }
                    // ── end cache build ───────────────────────────────────────────────
                }

                // Draw fold-collapse label (handles both non-empty and empty opener lines).
                RenderFoldCollapseLabel(dc, i, x, y);
            }
        }

        /// <summary>
        /// Scans <paramref name="lineText"/> for HTTP/HTTPS URLs using <see cref="s_urlRegex"/>.
        /// For each match, replaces any existing tokens at the URL's columns with a new token
        /// colored with <see cref="SyntaxUrlColor"/>, and registers a <see cref="UrlHitZone"/>
        /// for cursor and Ctrl+Click handling.
        /// </summary>
        private IEnumerable<SyntaxHighlightToken> OverlayUrlTokens(
            string lineText, int lineIndex, IEnumerable<SyntaxHighlightToken> source)
        {
            // Fast-path: cheap string contains check avoids regex allocation on lines without URLs (OPT-PERF-04).
            if (!lineText.Contains("http", StringComparison.OrdinalIgnoreCase)) return source;
            var matches = s_urlRegex.Matches(lineText);
            if (matches.Count == 0) return source;

            // Materialise the source so we can splice URL tokens in.
            var result = source.ToList();
            var urlBrush = SyntaxUrlColor;

            foreach (Match m in matches)
            {
                // Register hit-zone for mouse interaction.
                _urlHitZones.Add(new UrlHitZone(lineIndex, m.Index, m.Index + m.Length, m.Value));

                // Remove any tokens that overlap the URL range (they'll be replaced).
                result.RemoveAll(t => t.StartColumn < m.Index + m.Length
                                   && t.StartColumn + t.Length > m.Index);

                // Add the URL token with SyntaxUrlColor (used as a sentinel in RenderTextContent).
                result.Add(new SyntaxHighlightToken(m.Index, m.Length, m.Value, urlBrush));
            }

            // Re-sort by start column so tokens render left-to-right.
            result.Sort(static (a, b) => a.StartColumn.CompareTo(b.StartColumn));
            return result;
        }

        /// <summary>
        /// Render cursor (simple rectangle for Phase 1)
        /// Phase 1: Static cursor, blinking will be added later
        /// </summary>
        private void RenderCursor(DrawingContext dc)
        {
            // Show cursor even without focus (but dimmed)
            bool hasFocus = IsFocused;

            // Check caret visibility for blinking effect (only blink when focused)
            if (hasFocus && !_caretVisible)
                return;

            if (_cursorLine < _firstVisibleLine || _cursorLine > _lastVisibleLine)
                return;

            double x, y;
            double textLeft = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            if (IsWordWrapEnabled && _wrapOffsets.Length > _cursorLine && _charsPerVisualLine > 0)
            {
                int caretVisRow = _wrapOffsets[_cursorLine] + _cursorColumn / _charsPerVisualLine;
                int caretVisCol = _cursorColumn % _charsPerVisualLine;
                // Use the Y stored in _lineYLookup for the first visual row of the logical line,
                // then add the sub-row offset.
                double firstRowY = _lineYLookup.TryGetValue(_cursorLine, out double lky)
                    ? lky
                    : TopMargin + (_wrapOffsets[_cursorLine] - (int)(_verticalScrollOffset / _lineHeight)) * _lineHeight;
                int subRow = _cursorColumn / _charsPerVisualLine;
                y = firstRowY + subRow * _lineHeight;
                x = textLeft + caretVisCol * _charWidth;
            }
            else
            {
                // Use per-line Y lookup so the caret sits at the code-text Y on InlineHints lines.
                y = _lineYLookup.TryGetValue(_cursorLine, out double cy) ? cy
                    : (EnableVirtualScrolling && _virtualizationEngine != null
                        ? TopMargin + _virtualizationEngine.GetLineYPosition(_cursorLine)
                        : TopMargin + (_cursorLine - _firstVisibleLine) * _lineHeight);
                x = textLeft + (_cursorColumn * _charWidth);
            }

            // Draw cursor as vertical line using DPs for color and width
            // When not focused, use 50% opacity to show inactive cursor
            Color caretColor = CaretColor;
            if (!hasFocus)
                caretColor = Color.FromArgb(128, caretColor.R, caretColor.G, caretColor.B);

            // Cache caret pens; rebuilt only when CaretColor or focus state changes (OPT-PERF-03).
            if (_cachedCaretPen == null || _cachedCaretColor != caretColor)
            {
                _cachedCaretColor = caretColor;
                _cachedCaretPen = new Pen(new SolidColorBrush(caretColor), CaretWidth);
                _cachedCaretPen.Freeze();
                var secondaryColor = Color.FromArgb((byte)(caretColor.A * 0.6),
                    caretColor.R, caretColor.G, caretColor.B);
                _cachedCaretSecondaryPen = new Pen(new SolidColorBrush(secondaryColor), CaretWidth);
                _cachedCaretSecondaryPen.Freeze();
            }
            var cursorPen = _cachedCaretPen;

            dc.DrawLine(cursorPen,
                new Point(x, y),
                new Point(x, y + _lineHeight - 2));

            // Secondary carets (multi-caret editing) — drawn at 60% opacity (pen already cached above).
            if (_caretManager.IsMultiCaret)
            {
                var carets       = _caretManager.Carets;
                var secondaryPen = _cachedCaretSecondaryPen!;
                double textLeftSec = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

                for (int ci = 1; ci < carets.Count; ci++)
                {
                    var c = carets[ci];
                    if (c.Line < _firstVisibleLine || c.Line > _lastVisibleLine) continue;

                    double sx = textLeftSec + c.Column * _charWidth;
                    double sy = _lineYLookup.TryGetValue(c.Line, out double scy) ? scy
                        : TopMargin + (c.Line - _firstVisibleLine) * _lineHeight;
                    dc.DrawLine(secondaryPen, new Point(sx, sy), new Point(sx, sy + _lineHeight - 2));
                }
            }
        }

        /// <summary>
        /// Render validation errors as squiggly lines (Phase 5)
        /// </summary>
        private void RenderValidationErrors(DrawingContext dc)
        {
            if (_validationErrors == null || _validationErrors.Count == 0)
                return;

            double leftEdge = ShowLineNumbers ? TextAreaLeftOffset : LeftMargin;

            // OPT-PERF-03: use _validationByLine index — O(viewport) instead of O(all errors).
            for (int i = _firstVisibleLine; i <= _lastVisibleLine; i++)
            {
                if (!_validationByLine.TryGetValue(i, out var lineErrors)) continue;
                foreach (var error in lineErrors)
                {
                    double y = TopMargin + (EnableVirtualScrolling && _virtualizationEngine != null
                        ? _virtualizationEngine.GetLineYPosition(error.Line)
                        : (error.Line - _firstVisibleLine) * _lineHeight) + _lineHeight - 3;
                    double x1 = leftEdge + (error.Column * _charWidth);
                    double x2 = x1 + (error.Length * _charWidth);

                    Pen squigglyPen = error.Severity switch
                    {
                        ValidationSeverity.Warning => s_squigglyWarning,
                        ValidationSeverity.Info    => s_squigglyInfo,
                        _                          => s_squigglyError,
                    };
                    DrawSquigglyLine(dc, x1, x2, y, squigglyPen);
                }
            }
        }

        /// <summary>
        /// Draws a squiggly (wavy) underline using direct dc.DrawLine calls with a pre-cached pen.
        /// Avoids StreamGeometry + Pen allocations per error per frame (OPT-PERF-02).
        /// </summary>
        private static void DrawSquigglyLine(DrawingContext dc, double x1, double x2, double y, Pen pen)
        {
            double x  = x1;
            bool   up = true;
            while (x + 2 <= x2)
            {
                double xNext = x + 2;
                dc.DrawLine(pen, new Point(x, y), new Point(xNext, y + (up ? -2 : 2)));
                x  = xNext;
                up = !up;
            }
        }

        /// <summary>
        /// Render bracket matching highlights (Phase 6)
        /// </summary>
        private void RenderBracketMatching(DrawingContext dc)
        {
            if (_cursorColumn < 0 || _cursorLine < 0 || _cursorLine >= _document.Lines.Count)
                return;

            // No bracket matching on collapsed directive opener lines (text is blanked).
            if (_foldingEngine?.GetRegionAt(_cursorLine) is { IsCollapsed: true, Kind: FoldingRegionKind.Directive })
                return;

            // Cache FindMatchingBracket result — avoid O(distance) scan every frame (OPT-PERF-03).
            TextPosition? matchPos  = null;
            int           bracketColumn = -1;

            if (_cachedBracketCursorLine != _cursorLine || _cachedBracketCursorCol != _cursorColumn)
            {
                var line = _document.Lines[_cursorLine];
                char? charBeforeCursor = null;
                int charBeforePos = _cursorColumn - 1;
                if (charBeforePos >= 0 && charBeforePos < line.Text.Length)
                    charBeforeCursor = line.Text[charBeforePos];
                char? charAtCursor = _cursorColumn < line.Text.Length ? line.Text[_cursorColumn] : (char?)null;

                if (charAtCursor.HasValue && IsBracket(charAtCursor.Value))
                {
                    bracketColumn = _cursorColumn;
                    matchPos      = FindMatchingBracket(_cursorLine, _cursorColumn, charAtCursor.Value);
                }
                else if (charBeforeCursor.HasValue && IsBracket(charBeforeCursor.Value))
                {
                    bracketColumn = charBeforePos;
                    matchPos      = FindMatchingBracket(_cursorLine, charBeforePos, charBeforeCursor.Value);
                }

                _cachedBracketCursorLine  = _cursorLine;
                _cachedBracketCursorCol   = _cursorColumn;
                _cachedBracketColumn      = bracketColumn;
                _cachedBracketMatchResult = matchPos;
            }
            else
            {
                bracketColumn = _cachedBracketColumn;
                matchPos      = _cachedBracketMatchResult;
            }

            // Highlight both brackets if match found — static cached pens, no allocation (OPT-PERF-03).
            if (matchPos.HasValue && bracketColumn >= 0)
            {
                HighlightBracket(dc, _cursorLine, bracketColumn, s_bracketHighlightBrush, s_bracketBorderPen);
                HighlightBracket(dc, matchPos.Value.Line, matchPos.Value.Column, s_bracketHighlightBrush, s_bracketBorderPen);
            }
        }

        /// <summary>
        /// Highlight a single bracket
        /// </summary>
        private void HighlightBracket(DrawingContext dc, int line, int column, Brush background, Pen border)
        {
            if (line < _firstVisibleLine || line > _lastVisibleLine)
                return;

            // Use _lineYLookup to account for InlineHints hint zone height offset (same pattern as RenderCursor/RenderSelection/RenderWordHighlights)
            double y = _lineYLookup.TryGetValue(line, out double by) ? by
                : (EnableVirtualScrolling && _virtualizationEngine != null
                    ? TopMargin + _virtualizationEngine.GetLineYPosition(line)
                    : TopMargin + (line - _firstVisibleLine) * _lineHeight);
            double x = (ShowLineNumbers ? TextAreaLeftOffset : LeftMargin) + (column * _charWidth);

            // Draw background highlight
            dc.DrawRectangle(background, null, new Rect(x, y, _charWidth, _lineHeight));

            // Draw border
            dc.DrawRectangle(null, border, new Rect(x, y, _charWidth, _lineHeight));
        }

        /// <summary>
        /// Check if character is a bracket
        /// </summary>
        private bool IsBracket(char ch)
        {
            return ch == '(' || ch == ')' || ch == '[' || ch == ']' || ch == '{' || ch == '}';
        }

        /// <summary>
        /// Find matching bracket for given position
        /// </summary>
        private TextPosition? FindMatchingBracket(int line, int column, char bracket)
        {
            if (line < 0 || line >= _document.Lines.Count)
                return null;

            // Determine direction and matching bracket
            bool searchForward;
            char matchingBracket;

            switch (bracket)
            {
                case '(':
                    searchForward = true;
                    matchingBracket = ')';
                    break;
                case ')':
                    searchForward = false;
                    matchingBracket = '(';
                    break;
                case '[':
                    searchForward = true;
                    matchingBracket = ']';
                    break;
                case ']':
                    searchForward = false;
                    matchingBracket = '[';
                    break;
                case '{':
                    searchForward = true;
                    matchingBracket = '}';
                    break;
                case '}':
                    searchForward = false;
                    matchingBracket = '{';
                    break;
                default:
                    return null;
            }

            if (searchForward)
            {
                return FindMatchingBracketForward(line, column + 1, bracket, matchingBracket);
            }
            else
            {
                return FindMatchingBracketBackward(line, column - 1, bracket, matchingBracket);
            }
        }

        /// <summary>
        /// Search forward for matching bracket
        /// </summary>
        private TextPosition? FindMatchingBracketForward(int startLine, int startColumn, char openBracket, char closeBracket)
        {
            int depth = 1;
            bool inString = false;
            bool escaped = false;

            for (int lineIdx = startLine; lineIdx < _document.Lines.Count; lineIdx++)
            {
                var line = _document.Lines[lineIdx];
                int start = (lineIdx == startLine) ? startColumn : 0;

                for (int col = start; col < line.Text.Length; col++)
                {
                    char ch = line.Text[col];

                    // Handle escape sequences
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    // Handle strings (skip brackets inside strings)
                    if (ch == '"')
                    {
                        inString = !inString;
                        continue;
                    }

                    if (inString)
                        continue;

                    // Check brackets
                    if (ch == openBracket)
                    {
                        depth++;
                    }
                    else if (ch == closeBracket)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return new TextPosition(lineIdx, col);
                        }
                    }
                }
            }

            return null; // No match found
        }

        /// <summary>
        /// Search backward for matching bracket
        /// </summary>
        private TextPosition? FindMatchingBracketBackward(int startLine, int startColumn, char closeBracket, char openBracket)
        {
            int depth = 1;

            for (int lineIdx = startLine; lineIdx >= 0; lineIdx--)
            {
                var line = _document.Lines[lineIdx];
                int start = (lineIdx == startLine) ? startColumn : line.Text.Length - 1;

                for (int col = start; col >= 0; col--)
                {
                    char ch = line.Text[col];

                    // Simple check (doesn't handle strings perfectly in backward direction)
                    // This is acceptable for most cases
                    if (ch == closeBracket)
                    {
                        depth++;
                    }
                    else if (ch == openBracket)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return new TextPosition(lineIdx, col);
                        }
                    }
                }
            }

            return null; // No match found
        }

        #endregion

    }
}
