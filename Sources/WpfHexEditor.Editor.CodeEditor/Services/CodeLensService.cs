// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/CodeLensService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Background service that builds a 0-based-line → reference-count map
//     for CodeLens hint rendering.
//     Uses CodeStructureParser to locate declaration lines, then performs
//     a whole-word scan of all document lines to count usages.
//
// Architecture Notes:
//     Pattern: Service / Observer.
//     Debounced 800 ms to avoid O(n²) work on every keystroke.
//     Lines snapshot is taken on the UI thread (ObservableCollection is
//     not thread-safe); actual counting runs on a Task.Run thread.
//     Result is marshalled back to the WPF Dispatcher before firing
//     LensDataRefreshed so callers can safely update UI state.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WpfHexEditor.Editor.CodeEditor.Models;
using WpfHexEditor.Editor.CodeEditor.NavigationBar;

namespace WpfHexEditor.Editor.CodeEditor.Services;

internal sealed class CodeLensService : IDisposable
{
    // ── State ─────────────────────────────────────────────────────────────────

    private CodeDocument?            _document;
    private CancellationTokenSource  _cts      = new();
    private readonly DispatcherTimer _debounce;
    private bool                     _disposed;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Current lens data: 0-based line index → (reference count, symbol name).
    /// </summary>
    public IReadOnlyDictionary<int, (int Count, string Symbol)> LensData { get; private set; }
        = new Dictionary<int, (int, string)>();

    /// <summary>Raised on the UI thread when <see cref="LensData"/> has been refreshed.</summary>
    public event EventHandler? LensDataRefreshed;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal CodeLensService()
    {
        _debounce       = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _debounce.Tick += OnDebounceTick;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches to <paramref name="document"/> and schedules an immediate refresh.
    /// Safe to call multiple times — the previous document is detached first.
    /// </summary>
    internal void Attach(CodeDocument document)
    {
        Detach();
        _document              = document;
        _document.TextChanged += OnDocumentTextChanged;
        ScheduleRefresh();
    }

    /// <summary>Detaches from the current document and cancels any pending work.</summary>
    internal void Detach()
    {
        _debounce.Stop();

        if (_document is not null)
        {
            _document.TextChanged -= OnDocumentTextChanged;
            _document              = null;
        }

        CancelPending();
        LensData = new Dictionary<int, (int, string)>();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
        _cts.Dispose();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnDocumentTextChanged(object sender, TextChangedEventArgs e)
        => ScheduleRefresh();

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        _ = RefreshAsync();
    }

    // ── Core logic ────────────────────────────────────────────────────────────

    private void ScheduleRefresh()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void CancelPending()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    private async Task RefreshAsync()
    {
        if (_document is null) return;

        CancelPending();
        var ct = _cts.Token;

        // Snapshot lines on the UI thread — ObservableCollection is not thread-safe.
        var lineSnapshot = _document.Lines.ToList();

        try
        {
            // ConfigureAwait(true) resumes on the UI (Dispatcher) thread so we can
            // safely update LensData and fire the event without an Invoke call.
            var data = await Task.Run(() => ComputeLensData(lineSnapshot, ct), ct)
                                 .ConfigureAwait(true);

            if (ct.IsCancellationRequested) return;

            LensData = data;
            LensDataRefreshed?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) { /* expected on rapid editing */ }
    }

    /// <summary>
    /// Pure background worker: parse declarations then count whole-word references.
    /// Must not touch any WPF objects — only BCL + snapshoted CodeLine data.
    /// </summary>
    private static IReadOnlyDictionary<int, (int Count, string Symbol)> ComputeLensData(
        IReadOnlyList<CodeLine> lines, CancellationToken ct)
    {
        var snapshot = CodeStructureParser.Parse(lines);

        // Collect declaration items — Types + Members; skip Namespaces (too broad).
        var items = snapshot.Types.Concat(snapshot.Members).ToList();
        if (items.Count == 0) return new Dictionary<int, (int, string)>();

        var result = new Dictionary<int, (int, string)>(items.Count);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            string symbol = item.Name;
            if (string.IsNullOrEmpty(symbol)) continue;

            int count = CountWholeWordOccurrences(lines, symbol, ct);

            // A count of 0 can happen for generated/private symbols with no callers.
            // Store anyway so the UI can show "0 références" if desired, but we skip
            // 0-count entries to avoid cluttering the lens layer.
            if (count > 0)
                result[item.Line] = (count, item.Name);
        }

        return result;
    }

    private static int CountWholeWordOccurrences(
        IReadOnlyList<CodeLine> lines, string symbol, CancellationToken ct)
    {
        int count = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            string text = lines[i].Text;
            if (string.IsNullOrEmpty(text)) continue;

            int col = 0;
            while (col < text.Length)
            {
                int idx = text.IndexOf(symbol, col, StringComparison.Ordinal);
                if (idx < 0) break;

                bool leftOk  = idx == 0               || !IsWordChar(text[idx - 1]);
                bool rightOk = idx + symbol.Length >= text.Length
                               || !IsWordChar(text[idx + symbol.Length]);

                if (leftOk && rightOk)
                    count++;

                col = idx + 1;
            }
        }

        return count;
    }

    private static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch) || ch == '_';
}
