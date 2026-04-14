// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/InlineHintsService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Background service that builds a 0-based-line → reference-count map
//     for InlineHints hint rendering.
//     Uses CodeStructureParser to locate declaration lines, then performs
//     a whole-word scan of all document lines to count usages.
//
// Architecture Notes:
//     Pattern: Service / Observer.
//     Debounced 800 ms to avoid O(n²) work on every keystroke.
//     Lines snapshot is taken on the UI thread (ObservableCollection is
//     not thread-safe); actual counting runs on a Task.Run thread.
//     Result is marshalled back to the WPF Dispatcher before firing
//     HintsDataRefreshed so callers can safely update UI state.
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Editor.CodeEditor.Models;
using WpfHexEditor.Editor.CodeEditor.NavigationBar;

namespace WpfHexEditor.Editor.CodeEditor.Services;

internal sealed class InlineHintsService : IDisposable
{
    // ── State ─────────────────────────────────────────────────────────────────

    private CodeDocument?            _document;
    private string?                  _currentFilePath;
    private CancellationTokenSource  _cts      = new();
    private readonly DispatcherTimer _debounce;
    private bool                     _disposed;

    // Roslyn reference-count provider — null when LSP client is not Roslyn.
    private IReferenceCountProvider? _roslynProvider;
    private int                      _inlineHintsSource; // 0=Auto, 1=RoslynOnly, 2=RegexAlways

    // Retry counter: when Roslyn is available but workspace not yet compiled,
    // CountReferencesAsync returns null for every symbol → roslynData.Count == 0.
    // We retry up to 2 times (4 s, 8 s) so hints upgrade automatically once ready.
    private int _roslynRetryCount;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Current hints data: 0-based line index → (reference count, symbol name, MDL2 icon glyph, icon brush, symbol kind, isRoslyn).
    /// <c>IsRoslyn = true</c> when the count came from Roslyn semantic analysis; <c>false</c> = regex fallback.
    /// </summary>
    public IReadOnlyDictionary<int, (int Count, string Symbol, string IconGlyph, Brush IconBrush, InlineHintsSymbolKinds Kind, bool IsRoslyn)> HintsData
        { get; private set; } = new Dictionary<int, (int, string, string, Brush, InlineHintsSymbolKinds, bool)>();

    /// <summary>Raised on the UI thread when <see cref="HintsData"/> has been refreshed.</summary>
    public event EventHandler? HintsDataRefreshed;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal InlineHintsService()
    {
        _debounce       = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _debounce.Tick += OnDebounceTick;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches to <paramref name="document"/> and schedules an immediate refresh.
    /// <paramref name="currentFilePath"/> is used to filter workspace files by
    /// extension and to avoid double-counting the currently open file.
    /// Safe to call multiple times — the previous document is detached first.
    /// </summary>
    internal void Attach(CodeDocument document, string? currentFilePath)
    {
        Detach();
        _currentFilePath       = currentFilePath;
        _document              = document;
        _document.TextChanged += OnDocumentTextChanged;
        WorkspaceFileCache.WorkspaceChanged += OnWorkspaceChanged;
        ScheduleRefresh();
    }

    /// <summary>Detaches from the current document and cancels any pending work.</summary>
    internal void Detach()
    {
        WorkspaceFileCache.WorkspaceChanged -= OnWorkspaceChanged;
        _debounce.Stop();

        if (_document is not null)
        {
            _document.TextChanged -= OnDocumentTextChanged;
            _document              = null;
        }

        _currentFilePath = null;
        CancelPending();
        HintsData = new Dictionary<int, (int, string, string, Brush, InlineHintsSymbolKinds, bool)>();
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
    {
        ShiftHintsForLineChange(e);
        ScheduleRefresh();
    }

    // Shifts HintsData line keys immediately when a line is inserted or deleted,
    // so hints stay visible and correctly positioned while the debounce recomputes.
    private void ShiftHintsForLineChange(TextChangedEventArgs e)
    {
        if (e.ChangeType != TextChangeType.NewLine && e.ChangeType != TextChangeType.DeleteLine)
            return;
        if (HintsData.Count == 0) return;

        int pivot = e.Position.Line;
        int delta = e.ChangeType == TextChangeType.NewLine ? +1 : -1;

        var shifted = new Dictionary<int, (int Count, string Symbol, string IconGlyph, Brush IconBrush, InlineHintsSymbolKinds Kind, bool IsRoslyn)>(HintsData.Count);
        foreach (var (key, val) in HintsData)
        {
            if (key == pivot && delta == -1) continue;   // deleted line's hint is dropped
            shifted[key > pivot ? key + delta : key] = val;
        }
        HintsData = shifted;
        HintsDataRefreshed?.Invoke(this, EventArgs.Empty);
    }

    // WorkspaceChanged fires on an arbitrary thread — marshal to the UI dispatcher.
    private void OnWorkspaceChanged()
        => _debounce.Dispatcher.InvokeAsync(ScheduleRefresh);

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        _ = RefreshAsync();
    }

    // ── Roslyn provider ───────────────────────────────────────────────────────

    /// <summary>
    /// Updates the Roslyn provider and source strategy, then schedules a refresh.
    /// Call from <c>CodeEditor.SetLspClient</c> and the settings-changed handler.
    /// </summary>
    internal void SetReferenceCountProvider(IReferenceCountProvider? provider, int source)
    {
        if (ReferenceEquals(_roslynProvider, provider) && _inlineHintsSource == source) return;
        _roslynProvider    = provider;
        _inlineHintsSource = source;
        if (provider is not null)
            _roslynRetryCount = 0;
        ScheduleRefresh();
    }

    private bool UseRoslyn() =>
        _inlineHintsSource != 2
        && _roslynProvider is not null
        && _currentFilePath is not null
        && _roslynProvider.CanProvide(_currentFilePath);

    // ── Core logic ────────────────────────────────────────────────────────────

    private void ScheduleRefresh()
    {
        _debounce.Stop();
        _debounce.Interval = TimeSpan.FromMilliseconds(800);
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

        // Snapshot lines and file path on the UI thread — ObservableCollection is not thread-safe.
        var lineSnapshot = _document.Lines.ToList();
        var filePath     = _currentFilePath;

        try
        {
            // ── Step 1 : Regex — always runs first for immediate feedback (800 ms debounce).
            // ConfigureAwait(true) resumes on the UI (Dispatcher) thread so HintsData
            // and the event can be touched without an explicit Invoke call.
            var regexData = await Task.Run(() => ComputeHintsData(lineSnapshot, filePath, ct), ct)
                                      .ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            HintsData = regexData;
            HintsDataRefreshed?.Invoke(this, EventArgs.Empty);

            // ── Step 2 : Roslyn upgrade — runs after regex results are visible.
            // Only attempted when the provider is available and not overridden to regex.
            if (_inlineHintsSource == 2 || _roslynProvider is null || filePath is null) return;
            if (!_roslynProvider.CanProvide(filePath)) return;

            var roslynData = await ComputeHintsDataRoslynAsync(lineSnapshot, filePath, _roslynProvider, ct)
                                   .ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            // Only publish the Roslyn upgrade if it produced at least one result —
            // an empty Roslyn result (e.g. workspace not fully loaded) keeps the regex hints visible.
            if (roslynData.Count > 0)
            {
                _roslynRetryCount = 0;
                HintsData = roslynData;
                HintsDataRefreshed?.Invoke(this, EventArgs.Empty);
            }
            else if (HintsData.Count > 0 && _roslynRetryCount < 2)
            {
                // Roslyn provider is available but returned nothing (workspace still compiling).
                // Retry with increasing delays (4 s, 8 s) so the popup upgrades once ready.
                _roslynRetryCount++;
                _debounce.Interval = TimeSpan.FromSeconds(4 * _roslynRetryCount);
                _debounce.Start();
            }
        }
        catch (OperationCanceledException) { /* expected on rapid editing */ }
    }

    /// <summary>
    /// Roslyn-backed computation: uses <see cref="IReferenceCountProvider"/> for
    /// semantically-accurate counts (no false positives in strings/comments).
    /// Falls back to regex count per symbol if the provider returns null.
    /// </summary>
    private static async Task<IReadOnlyDictionary<int, (int Count, string Symbol, string IconGlyph, Brush IconBrush, InlineHintsSymbolKinds Kind, bool IsRoslyn)>>
        ComputeHintsDataRoslynAsync(
            IReadOnlyList<CodeLine> lines,
            string filePath,
            IReferenceCountProvider provider,
            CancellationToken ct)
    {
        var snapshot = CodeStructureParser.Parse(lines);
        var items    = snapshot.Types.Concat(snapshot.Members).ToList();
        if (items.Count == 0)
            return new Dictionary<int, (int, string, string, Brush, InlineHintsSymbolKinds, bool)>();

        var result = new Dictionary<int, (int, string, string, Brush, InlineHintsSymbolKinds, bool)>(items.Count);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(item.Name)) continue;

            // Roslyn gives the true count across the whole solution.
            var count = await provider.CountReferencesAsync(filePath, item.Line, item.Name, ct)
                                      .ConfigureAwait(false);

            // null means Roslyn failed for this symbol — skip rather than show a stale count.
            if (count is null or 0) continue;

            result[item.Line] = (count.Value, item.Name, item.IconGlyph, item.IconBrush, MapToInlineHintsKind(item), true);
        }

        return result;
    }

    /// <summary>
    /// Pure background worker: parse declarations then count whole-word references
    /// across both the current document lines and all solution files of the same
    /// file extension via <see cref="WorkspaceFileCache"/>.
    /// Must not touch any WPF objects — only BCL + snapshotted CodeLine data.
    /// </summary>
    private static IReadOnlyDictionary<int, (int Count, string Symbol, string IconGlyph, Brush IconBrush, InlineHintsSymbolKinds Kind, bool IsRoslyn)> ComputeHintsData(
        IReadOnlyList<CodeLine> lines, string? currentFilePath, CancellationToken ct)
    {
        var snapshot = CodeStructureParser.Parse(lines);

        // Collect declaration items — Types + Members; skip Namespaces (too broad).
        var items = snapshot.Types.Concat(snapshot.Members).ToList();
        if (items.Count == 0) return new Dictionary<int, (int, string, string, Brush, InlineHintsSymbolKinds, bool)>();

        // Gather workspace files of matching extension once per ComputeHintsData call.
        IReadOnlyList<string> workspacePaths = [];
        var ext = Path.GetExtension(currentFilePath ?? string.Empty);
        if (!string.IsNullOrEmpty(ext))
            workspacePaths = WorkspaceFileCache.GetPathsForExtensions([ext]);

        var result = new Dictionary<int, (int, string, string, Brush, InlineHintsSymbolKinds, bool)>(items.Count);

        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) return result;

            string symbol = item.Name;
            if (string.IsNullOrEmpty(symbol)) continue;

            // Count in the current in-memory document.
            int count = CountWholeWordOccurrences(lines, symbol, ct);

            // ADR-004: External assembly ref-counting deferred (post Phase 6).
            // Requires IDecompilerService + per-assembly WorkspaceFileCache bucket keyed by assembly identity.
            // Unblock when AssemblyAnalysisEngine (CSharpSkeletonEmitter) is stable.

            // Count across all solution files of the same extension, skipping the
            // current file (already counted above via in-memory lines).
            foreach (var wPath in workspacePaths)
            {
                if (ct.IsCancellationRequested) return result;

                if (wPath.Equals(currentFilePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                var wLines = WorkspaceFileCache.GetLines(wPath);
                if (wLines is null) continue;

                count += CountWholeWordOccurrencesInText(wLines, symbol, ct);
            }

            // Skip 0-count entries to avoid cluttering the hints layer.
            if (count > 0)
                result[item.Line] = (count, item.Name, item.IconGlyph, item.IconBrush, MapToInlineHintsKind(item), false);
        }

        return result;
    }

    /// <summary>
    /// Maps a <see cref="NavigationBarItem"/>'s Kind/TypeKind/MemberKind to the
    /// corresponding <see cref="InlineHintsSymbolKinds"/> flag for render filtering.
    /// </summary>
    private static InlineHintsSymbolKinds MapToInlineHintsKind(NavigationBarItem item) =>
        (item.Kind, item.TypeKind, item.MemberKind) switch
        {
            (NavigationItemKind.Type,   TypeKind.Class,       _)                    => InlineHintsSymbolKinds.Class,
            (NavigationItemKind.Type,   TypeKind.Interface,   _)                    => InlineHintsSymbolKinds.Interface,
            (NavigationItemKind.Type,   TypeKind.Struct,      _)                    => InlineHintsSymbolKinds.Struct,
            (NavigationItemKind.Type,   TypeKind.Enum,        _)                    => InlineHintsSymbolKinds.Enum,
            (NavigationItemKind.Type,   TypeKind.Record,      _)                    => InlineHintsSymbolKinds.Record,
            (NavigationItemKind.Type,   TypeKind.Delegate,    _)                    => InlineHintsSymbolKinds.Delegate,
            (NavigationItemKind.Member, _,                    MemberKind.Method)    => InlineHintsSymbolKinds.Method,
            (NavigationItemKind.Member, _,                    MemberKind.Constructor) => InlineHintsSymbolKinds.Constructor,
            (NavigationItemKind.Member, _,                    MemberKind.Property)  => InlineHintsSymbolKinds.Property,
            (NavigationItemKind.Member, _,                    MemberKind.Indexer)   => InlineHintsSymbolKinds.Indexer,
            (NavigationItemKind.Member, _,                    MemberKind.Field)     => InlineHintsSymbolKinds.Field,
            (NavigationItemKind.Member, _,                    MemberKind.Event)     => InlineHintsSymbolKinds.Event,
            // Unknown/Namespace fall-through: include in All so they are never silently hidden.
            _                                                                       => InlineHintsSymbolKinds.All,
        };

    /// <summary>Counts whole-word occurrences of <paramref name="symbol"/> in a
    /// <see cref="CodeLine"/> list (current in-memory document).</summary>
    private static int CountWholeWordOccurrences(
        IReadOnlyList<CodeLine> lines, string symbol, CancellationToken ct)
    {
        int count = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            if (ct.IsCancellationRequested) return 0;

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

    /// <summary>Counts whole-word occurrences of <paramref name="symbol"/> in a
    /// plain <see cref="string"/> array (workspace file read from disk).</summary>
    private static int CountWholeWordOccurrencesInText(
        string[] lines, string symbol, CancellationToken ct)
    {
        int count = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (ct.IsCancellationRequested) return 0;

            string text = lines[i];
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
