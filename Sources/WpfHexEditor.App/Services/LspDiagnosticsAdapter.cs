// ==========================================================
// Project: WpfHexEditor.App
// File: Services/LspDiagnosticsAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     IDiagnosticSource that bridges LSP push diagnostics into the
//     IDE ErrorPanel. Subscribes to each ILspClient as it becomes Ready
//     via LspDocumentBridgeService.ServerStateChanged, accumulates
//     per-document diagnostics, and raises DiagnosticsChanged so the
//     ErrorPanel refreshes automatically.
//
// Architecture Notes:
//     Pattern: Adapter + Observer
//     - One subscription per ILspClient (guarded by HashSet to avoid duplicates).
//     - Diagnostics keyed by document URI (replace-on-update — same as LSP spec).
//     - Always invoked on UI thread (LspClientImpl guarantee).
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Feeds LSP <c>textDocument/publishDiagnostics</c> notifications into the IDE ErrorPanel.
/// </summary>
internal sealed class LspDiagnosticsAdapter : IDiagnosticSource, IDisposable
{
    private readonly LspDocumentBridgeService _bridge;

    // Per-document diagnostics keyed by document URI.
    private readonly Dictionary<string, List<DiagnosticEntry>> _byUri
        = new(StringComparer.OrdinalIgnoreCase);

    // Guards against double-subscribing to the same ILspClient instance.
    private readonly HashSet<ILspClient> _subscribedClients = [];

    // ── IDiagnosticSource ─────────────────────────────────────────────────────

    public string SourceLabel => "LSP";

    public IReadOnlyList<DiagnosticEntry> GetDiagnostics()
        => _byUri.Values.SelectMany(x => x).ToList();

    public event EventHandler? DiagnosticsChanged;

    // ── Construction ──────────────────────────────────────────────────────────

    internal LspDiagnosticsAdapter(LspDocumentBridgeService bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _bridge.ServerStateChanged += OnServerStateChanged;
    }

    // ── Private handlers ─────────────────────────────────────────────────────

    private void OnServerStateChanged(object? sender, LspServerStateChangedEventArgs e)
    {
        if (e.State != LspServerState.Ready) return;

        var client = _bridge.TryGetClient(e.LanguageId);
        if (client is null || !_subscribedClients.Add(client)) return;

        client.DiagnosticsReceived += OnDiagnosticsReceived;
    }

    private void OnDiagnosticsReceived(
        object? sender,
        LspDiagnosticsReceivedEventArgs e)
    {
        // Always called on UI thread (guaranteed by LspClientImpl).
        var entries = e.Diagnostics
            .Select(d => new DiagnosticEntry(
                Severity:    MapSeverity(d.Severity),
                Code:        d.Code ?? "LSP",
                Description: d.Message,
                FileName:    TryGetFileName(e.DocumentUri),
                FilePath:    TryGetFilePath(e.DocumentUri),
                Line:        d.StartLine + 1,       // LSP is 0-based; ErrorPanel is 1-based
                Column:      d.StartColumn + 1))
            .ToList();

        _byUri[e.DocumentUri] = entries;
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DiagnosticSeverity MapSeverity(string severity) =>
        severity switch
        {
            "error"       => DiagnosticSeverity.Error,
            "warning"     => DiagnosticSeverity.Warning,
            _             => DiagnosticSeverity.Message,   // information / hint
        };

    private static string? TryGetFileName(string uri)
    {
        try { return System.IO.Path.GetFileName(new Uri(uri).LocalPath); }
        catch { return null; }
    }

    private static string? TryGetFilePath(string uri)
    {
        try { return new Uri(uri).LocalPath; }
        catch { return null; }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _bridge.ServerStateChanged -= OnServerStateChanged;
        foreach (var client in _subscribedClients)
            client.DiagnosticsReceived -= OnDiagnosticsReceived;
        _subscribedClients.Clear();
        _byUri.Clear();
    }
}
