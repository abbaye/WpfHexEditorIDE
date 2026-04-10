// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/HoverQuickInfoService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Background service that resolves Quick Info hover data for a given
//     (filePath, line, column) position.  Used by CodeEditor to populate
//     the QuickInfoPopup after the hover dwell timer fires.
//
// Architecture Notes:
//     Pattern: Service / Observer — mirrors InlineHintsService.
//     Resolution priority:
//       1. Diagnostics snapshot covering the position  (instant, no I/O)
//       2. ILspClient.HoverAsync                       (network, 10-s timeout)
//       3. IQuickInfoProvider plugin contributors      (pluggable)
//       4. Local CodeStructureParser fallback          (BCL-only)
//     Debounce: 400 ms (shorter than InlineHints 800 ms — hover is user-driven).
//     Threading: UI-thread snapshot → Task.Run background → ConfigureAwait(true)
//     marshal back → fire QuickInfoResolved on UI thread.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WpfHexEditor.Editor.CodeEditor.Models;
using WpfHexEditor.Editor.CodeEditor.NavigationBar;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.SDK.ExtensionPoints;

namespace WpfHexEditor.Editor.CodeEditor.Services;

internal sealed class HoverQuickInfoService : IDisposable
{
    // ── State ─────────────────────────────────────────────────────────────────

    private readonly DispatcherTimer              _debounce;
    private CancellationTokenSource               _cts      = new();
    private ILspClient?                           _lspClient;
    private IReadOnlyList<IQuickInfoProvider>     _providers = [];
    private IReadOnlyList<ValidationError>        _diagnostics = [];
    private bool                                  _disposed;

    // Pending hover request parameters (set by RequestAsync, consumed by OnDebounceTick)
    private string _pendingFilePath  = string.Empty;
    private int    _pendingLine;
    private int    _pendingColumn;
    private string _pendingWord      = string.Empty;

    // Snapshot of document lines taken on the UI thread before going background
    private IReadOnlyList<CodeLine> _lineSnapshot = [];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised on the UI thread after resolution completes.
    /// <see langword="null"/> result means: no info found, hide the popup.
    /// </summary>
    public event EventHandler<QuickInfoResult?>? QuickInfoResolved;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal HoverQuickInfoService()
    {
        _debounce       = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _debounce.Tick += OnDebounceTick;
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    internal void SetLspClient(ILspClient? client)
        => _lspClient = client;

    internal void SetProviders(IReadOnlyList<IQuickInfoProvider> providers)
        => _providers = providers;

    /// <summary>
    /// Snapshot the current diagnostics list (call before RequestAsync).
    /// Caller must pass an already-snapshotted copy — do NOT pass the live list.
    /// </summary>
    internal void SetDiagnostics(IReadOnlyList<ValidationError> snapshot)
        => _diagnostics = snapshot;

    /// <summary>Optional logging sink wired by the host (e.g. OutputLogger.Info).</summary>
    internal Action<string>? Logger { get; set; }

    internal void SetDebounceInterval(TimeSpan interval)
        => _debounce.Interval = interval;

    // ── Request / Cancel ──────────────────────────────────────────────────────

    /// <summary>
    /// Schedules a debounced resolution for the given position.
    /// Pass a pre-snapshotted line list (taken on the UI thread) for the local fallback.
    /// </summary>
    internal void RequestAsync(
        string filePath, int line, int column, string word,
        IReadOnlyList<CodeLine> lineSnapshot)
    {
        bool sameWord = string.Equals(word, _pendingWord, StringComparison.Ordinal)
                     && string.Equals(filePath, _pendingFilePath, StringComparison.Ordinal);

        _pendingFilePath = filePath;
        _pendingLine     = line;
        _pendingColumn   = column;
        _pendingWord     = word;
        _lineSnapshot    = lineSnapshot;

        // Skip if already resolving for the same word.
        if (sameWord) return;

        // Cancel previous in-flight request and resolve immediately.
        // The DispatcherTimer debounce was unreliable (Tick never fired in some scenarios).
        // Instead, cancel-and-resolve is fast enough — LSP hover has its own 10s timeout.
        _debounce.Stop();
        _ = ResolveAsync();
    }

    /// <summary>Cancels any pending debounce + in-flight request and fires null result.</summary>
    internal void Cancel()
    {
        _debounce.Stop();
        CancelPending();
        QuickInfoResolved?.Invoke(this, null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debounce.Stop();
        CancelPending();
        _cts.Dispose();
    }

    // ── Debounce tick ─────────────────────────────────────────────────────────

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        Controls.CodeEditor.DiagnosticLogger?.Invoke($"[QuickInfo] DebounceTick fired, word='{_pendingWord}'");
        _ = ResolveAsync();
    }

    // ── Core resolution ───────────────────────────────────────────────────────

    private void CancelPending()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    private async Task ResolveAsync()
    {
        CancelPending();
        var ct = _cts.Token;

        // Capture all state on the UI thread before going background
        string filePath     = _pendingFilePath;
        int    line         = _pendingLine;
        int    column       = _pendingColumn;
        string word         = _pendingWord;
        var    lineSnap     = _lineSnapshot;
        var    diagSnap     = _diagnostics;
        var    lsp          = _lspClient;
        var    providers    = _providers;

        Controls.CodeEditor.DiagnosticLogger?.Invoke($"[QuickInfo] ResolveAsync word='{word}' lsp={lsp?.GetType().Name ?? "null"}");

        try
        {
            var log = Logger;
            var result = await Task.Run(
                () => ComputeAsync(filePath, line, column, word, lineSnap, diagSnap,
                                   lsp, providers, log, ct),
                ct).ConfigureAwait(true);   // resume on UI thread

            Controls.CodeEditor.DiagnosticLogger?.Invoke($"[QuickInfo] ResolveAsync result={result is not null} cancelled={ct.IsCancellationRequested}");

            if (ct.IsCancellationRequested) return;
            QuickInfoResolved?.Invoke(this, result);
        }
        catch (OperationCanceledException)
        {
            Controls.CodeEditor.DiagnosticLogger?.Invoke("[QuickInfo] ResolveAsync cancelled");
        }
        catch (Exception ex)
        {
            Controls.CodeEditor.DiagnosticLogger?.Invoke($"[QuickInfo] ResolveAsync ERROR: {ex.Message}");
        }
    }

    // ── Background computation (Task.Run thread — no WPF objects) ────────────

    private static async Task<QuickInfoResult?> ComputeAsync(
        string filePath, int line, int column, string word,
        IReadOnlyList<CodeLine> lines,
        IReadOnlyList<ValidationError> diagnostics,
        ILspClient? lsp,
        IReadOnlyList<IQuickInfoProvider> providers,
        Action<string>? logger,
        CancellationToken ct)
    {
        void Log(string msg) => Controls.CodeEditor.DiagnosticLogger?.Invoke($"[QuickInfo] {msg}");
        Log($"Resolve word='{word}' line={line} col={column} lsp={lsp?.GetType().Name ?? "null"} init={lsp?.IsInitialized}");

        // 1. Diagnostic overlay — instant, covers squigglies ──────────────────
        var diagResult = TryBuildDiagnosticInfo(diagnostics, line, column, word);
        Log($"Step1 diag={diagResult is not null}");

        // 2. LSP hover — most authoritative source ─────────────────────────────
        if (lsp?.IsInitialized == true)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linked  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

            try
            {
                Log("Step2 LSP HoverAsync...");
                var hover = await lsp.HoverAsync(filePath, line, column, linked.Token)
                                     .ConfigureAwait(false);
                Log($"Step2 LSP hover={hover is not null}");
                if (hover is not null)
                    return MergeDiagnostic(BuildFromLsp(hover, word), diagResult);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Log("Step2 LSP timeout");
            }
        }
        else
        {
            Log("Step2 LSP skipped (null or not initialized)");
        }

        ct.ThrowIfCancellationRequested();

        // 3. Plugin contributors ──────────────────────────────────────────────
        Log($"Step3 providers.Count={providers.Count}");
        foreach (var provider in providers)
        {
            var pluginResult = await provider
                .GetQuickInfoAsync(filePath, line, column, ct)
                .ConfigureAwait(false);

            if (pluginResult is not null)
            {
                Log($"Step3 provider hit: {provider.GetType().Name}");
                return MergeDiagnostic(pluginResult, diagResult);
            }
        }

        ct.ThrowIfCancellationRequested();

        // 4. Local fallback — CodeStructureParser ─────────────────────────────
        var local = ComputeLocalFallback(word, line, lines);
        Log($"Step4 local={local is not null}");
        if (local is not null)
            return MergeDiagnostic(local, diagResult);

        // 5. Return diagnostic-only result if we have one, otherwise nothing ──
        Log($"Step5 final diagOnly={diagResult is not null}");
        return diagResult;
    }

    // ── LSP result builder ────────────────────────────────────────────────────

    private static QuickInfoResult BuildFromLsp(LspHoverResult hover, string word)
    {
        string contents = hover.Contents;

        // Extract a type signature from the first non-blank line of the markdown
        string? sig  = null;
        string? docs = null;
        var     lines = contents.Split('\n');
        string  first = lines.FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? string.Empty;

        // Strip markdown fences: ```csharp ... ``` style
        if (first.StartsWith("```", StringComparison.Ordinal))
            first = first.TrimStart('`').TrimStart();

        if (first.Length > 0)
        {
            sig  = first.Length > 200 ? first[..200] : first;
            docs = lines.Length > 1
                ? string.Join("\n", lines.Skip(1)).Trim()
                : null;
        }
        else
        {
            docs = contents.Trim();
        }

        return new QuickInfoResult
        {
            SymbolName    = word,
            SymbolKind    = InferKindFromSignature(sig ?? word),
            TypeSignature = sig,
            DocumentationMarkdown = string.IsNullOrWhiteSpace(docs) ? null : docs,
            ActionLinks   = BuildDefaultActionLinks()
        };
    }

    // ── Local fallback ────────────────────────────────────────────────────────

    private static QuickInfoResult? ComputeLocalFallback(
        string word, int line, IReadOnlyList<CodeLine> lines)
    {
        if (string.IsNullOrEmpty(word) || lines.Count == 0) return null;

        var snapshot = CodeStructureParser.Parse(lines);
        var all      = snapshot.Types.Concat(snapshot.Members).ToList();

        // Find the declaration item whose name matches the hovered word
        var match = all.FirstOrDefault(item =>
            string.Equals(item.Name, word, StringComparison.Ordinal)
            && item.Line != line);   // skip the declaration line itself

        if (match is null) return null;

        string symbolKind = match.Kind switch
        {
            NavigationItemKind.Namespace => "namespace",
            NavigationItemKind.Type      => match.TypeKind.ToString().ToLowerInvariant(),
            NavigationItemKind.Member    => match.MemberKind.ToString().ToLowerInvariant(),
            _                            => "symbol"
        };

        return new QuickInfoResult
        {
            SymbolName    = match.Name,
            SymbolKind    = symbolKind,
            TypeSignature = null,
            ActionLinks   = BuildDefaultActionLinks()
        };
    }

    // ── Diagnostic helper ─────────────────────────────────────────────────────

    private static QuickInfoResult? TryBuildDiagnosticInfo(
        IReadOnlyList<ValidationError> diagnostics, int line, int column, string word)
    {
        // Find the first diagnostic that covers (line, column)
        var diag = diagnostics.FirstOrDefault(d =>
            d.Line == line && column >= d.Column);

        if (diag is null) return null;

        string severity = diag.Severity switch
        {
            ValidationSeverity.Warning     => "warning",
            ValidationSeverity.Info        => "information",
            _                              => "error"
        };

        // Return a diagnostic-only shell (SymbolName may be empty string)
        return new QuickInfoResult
        {
            SymbolName         = word,
            SymbolKind         = "error",
            DiagnosticMessage  = diag.Message,
            DiagnosticSeverity = severity,
            ActionLinks        = []
        };
    }

    /// <summary>Merges diagnostic fields from <paramref name="diag"/> into <paramref name="main"/>.</summary>
    private static QuickInfoResult MergeDiagnostic(QuickInfoResult main, QuickInfoResult? diag)
    {
        if (diag is null
            || main.DiagnosticMessage is not null)   // already has diagnostic info
            return main;

        return new QuickInfoResult
        {
            SymbolName            = main.SymbolName,
            SymbolKind            = main.SymbolKind,
            TypeSignature         = main.TypeSignature,
            DocumentationMarkdown = main.DocumentationMarkdown,
            ActionLinks           = main.ActionLinks,
            DiagnosticMessage     = diag.DiagnosticMessage,
            DiagnosticSeverity    = diag.DiagnosticSeverity
        };
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string InferKindFromSignature(string sig)
    {
        string s = sig.TrimStart();
        if (s.Contains("class "))     return "class";
        if (s.Contains("interface ")) return "interface";
        if (s.Contains("struct "))    return "struct";
        if (s.Contains("enum "))      return "enum";
        if (s.Contains('('))          return "method";
        if (s.Contains("=>"))         return "property";
        return "symbol";
    }

    private static IReadOnlyList<QuickInfoActionLink> BuildDefaultActionLinks() =>
    [
        new QuickInfoActionLink { Label = "Go to Definition (F12)",        Command = "GoToDefinition"     },
        new QuickInfoActionLink { Label = "Find All References (Shift+F12)", Command = "FindAllReferences" }
    ];
}
