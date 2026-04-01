// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: HighlightPipelineService.cs
// Author: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Background syntax-highlight pipeline for CodeEditor.
//     Computes token spans for the visible viewport + a ±20-line buffer
//     on a background thread. Visible lines are prioritised over buffer lines.
//     Raises HighlightsComputed on the UI thread when new results are ready.
//
// Architecture Notes:
//     Strategy Pattern — pluggable ISyntaxHighlighter injected at call time.
//     CancellationToken cancels the in-flight task on every scroll/edit event.
//     Thread safety: CodeLine.TokensCache is a reference-typed field; atomic
//     assignment is guaranteed by the CLR for reference types on all .NET GC heaps.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Services
{
    /// <summary>
    /// Computes syntax tokens for visible and nearby lines on a background thread.
    /// Cancels and restarts whenever <see cref="ScheduleAsync"/> is called.
    /// </summary>
    internal sealed class HighlightPipelineService : IDisposable
    {
        private CancellationTokenSource? _cts;
        private readonly SynchronizationContext? _syncContext = SynchronizationContext.Current;

        /// <summary>
        /// Raised on the UI thread when at least the visible range has been highlighted.
        /// Args: (firstLine, lastLine) of the highlighted range.
        /// </summary>
        public event Action<int, int>? HighlightsComputed;

        /// <summary>
        /// Schedules background highlighting for the visible viewport + a ±20-line buffer.
        /// Any in-flight task is cancelled immediately before the new one starts.
        /// </summary>
        public void ScheduleAsync(
            IList<CodeLine> lines,
            int firstVisible,
            int lastVisible,
            ISyntaxHighlighter? highlighter,
            ISyntaxHighlighter? externalHighlighter)
        {
            var activeHighlighter = externalHighlighter ?? highlighter;
            if (activeHighlighter is null || lines.Count == 0) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            int bufStart = Math.Max(0, firstVisible - 20);
            int bufEnd   = Math.Min(lines.Count - 1, lastVisible + 20);
            if (bufEnd < bufStart) return;

            // Snapshot: capture index + text on the UI thread (safe — UI thread owns the list)
            int count  = bufEnd - bufStart + 1;
            var idxArr = new int[count];
            var txtArr = new string[count];
            var needArr = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int li    = bufStart + i;
                idxArr[i] = li;
                var line  = lines[li];
                needArr[i] = line.IsCacheDirty || line.TokensCache.Count == 0;
                txtArr[i]  = needArr[i] ? line.Text : string.Empty;
            }

            // Fast-path: nothing dirty in the buffer — skip thread pool work entirely.
            bool anyDirty = false;
            for (int i = 0; i < count; i++) { if (needArr[i]) { anyDirty = true; break; } }
            if (!anyDirty) return;

            var syncCtx = _syncContext;

            Task.Run(() =>
            {
                int highlighted = 0;

                // Pass 1: visible range first (lowest latency)
                for (int i = 0; i < count && !token.IsCancellationRequested; i++)
                {
                    int li = idxArr[i];
                    if (!needArr[i] || li < firstVisible || li > lastVisible) continue;

                    activeHighlighter.Reset(); // stateful highlighters reset per-line pass
                    var tokens = activeHighlighter.Highlight(txtArr[i], li);

                    if (li < lines.Count && !token.IsCancellationRequested)
                    {
                        // OPT-F: reuse existing TokensCache list to avoid Gen-0 allocation per line.
                        AssignTokensCache(lines[li], tokens);
                        lines[li].IsCacheDirty      = false;
                        lines[li].IsGlyphCacheDirty = true; // force GlyphRun rebuild on next render
                        lines[li].GlyphRunCache     = null;
                        highlighted++;
                    }
                }

                // Pass 2: buffer range pre-fetch
                for (int i = 0; i < count && !token.IsCancellationRequested; i++)
                {
                    int li = idxArr[i];
                    if (!needArr[i] || (li >= firstVisible && li <= lastVisible)) continue;

                    activeHighlighter.Reset();
                    var tokens = activeHighlighter.Highlight(txtArr[i], li);

                    if (li < lines.Count && !token.IsCancellationRequested)
                    {
                        // OPT-F: reuse existing TokensCache list to avoid Gen-0 allocation per line.
                        AssignTokensCache(lines[li], tokens);
                        lines[li].IsCacheDirty      = false;
                        lines[li].IsGlyphCacheDirty = true;
                        lines[li].GlyphRunCache     = null;
                        highlighted++;
                    }
                }

                // Only notify the UI when at least one visible line was actually re-highlighted,
                // preventing a render-loop: OnRender → ScheduleAsync → HighlightsComputed → InvalidateVisual → OnRender.
                if (highlighted > 0 && !token.IsCancellationRequested && syncCtx is not null)
                {
                    int completedFirst = firstVisible;
                    int completedLast  = Math.Min(lastVisible, bufEnd);
                    syncCtx.Post(_ => HighlightsComputed?.Invoke(completedFirst, completedLast), null);
                }
            }, token);
        }

        // OPT-F: writes tokens into the line's existing TokensCache list rather than
        // allocating a new List<> on every highlight pass.  In steady-state editing, the
        // list capacity is already correct, so Clear+AddRange produces zero heap allocation.
        private static void AssignTokensCache(
            WpfHexEditor.Editor.CodeEditor.Models.CodeLine line,
            System.Collections.Generic.IEnumerable<WpfHexEditor.Editor.CodeEditor.Helpers.SyntaxHighlightToken> tokens)
        {
            if (line.TokensCache is null)
            {
                line.TokensCache = new System.Collections.Generic.List<WpfHexEditor.Editor.CodeEditor.Helpers.SyntaxHighlightToken>(tokens);
            }
            else
            {
                line.TokensCache.Clear();
                line.TokensCache.AddRange(tokens);
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
