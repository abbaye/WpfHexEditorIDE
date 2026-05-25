// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspDiagnosticPullProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-05-20
// Description:
//     LSP 3.18 pull-diagnostics provider.
//     Implements textDocument/diagnostic (single-file) and
//     workspace/diagnostic (full workspace) as defined in ADR-041.
//
// Architecture Notes:
//     Provider pattern — stateless per-request, delegates to LspJsonRpcChannel.
//     Result identifiers from incremental responses are stored per-URI so the
//     next request sends the previousResultId and the server can return
//     DocumentDiagnosticReportKind.Unchanged to avoid re-sending full lists.
//     Capability guard: only activate when ServerCapabilities.diagnosticProvider != null.
// ==========================================================

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using WpfHexEditor.Core.LSP.Client.Transport;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Core.LSP.Client.Providers;

/// <summary>Diagnostic entry paired with its source document URI (returned by pull methods).</summary>
internal sealed record LspPulledDiagnostic(string DocumentUri, LspDiagnostic Diagnostic);

/// <summary>
/// LSP 3.18 pull-diagnostics client (ADR-041).
/// Supplements the push-based <c>textDocument/publishDiagnostics</c> for servers
/// that only send diagnostics on explicit request (e.g. rust-analyzer, clangd 16+).
/// </summary>
internal sealed class LspDiagnosticPullProvider
{
    private readonly LspJsonRpcChannel _channel;

    // Per-URI result identifier for incremental workspace/diagnostic requests.
    private readonly ConcurrentDictionary<string, string> _resultIds
        = new(StringComparer.OrdinalIgnoreCase);

    internal LspDiagnosticPullProvider(LspJsonRpcChannel channel)
    {
        _channel = channel;
    }

    // ── Single-file pull (textDocument/diagnostic) ────────────────────────────

    /// <summary>
    /// Requests diagnostics for a single document.
    /// Returns an empty list on error or when the server responds with "unchanged".
    /// </summary>
    internal async Task<IReadOnlyList<LspPulledDiagnostic>> PullDocumentDiagnosticsAsync(
        string filePath, CancellationToken ct)
    {
        var uri = LspDocumentSync.ToUri(filePath);

        _resultIds.TryGetValue(uri, out var previousResultId);

        var @params = new
        {
            textDocument     = new { uri },
            previousResultId = previousResultId,
        };

        JsonNode? result;
        try
        {
            result = await _channel
                .CallAsync("textDocument/diagnostic", @params, ct)
                .ConfigureAwait(false);
        }
        catch { return []; }

        return ParseDocumentReport(result, uri)
            .Select(d => new LspPulledDiagnostic(uri, d))
            .ToList();
    }

    // ── Workspace pull (workspace/diagnostic) ─────────────────────────────────

    /// <summary>
    /// Requests diagnostics for the entire workspace.
    /// Incremental: sends previousResultIds for all known documents so the server
    /// can respond with "unchanged" for unmodified files.
    /// </summary>
    internal async Task<IReadOnlyList<LspPulledDiagnostic>> PullWorkspaceDiagnosticsAsync(
        CancellationToken ct)
    {
        var previousResultIds = _resultIds
            .Select(kv => new { uri = kv.Key, value = kv.Value })
            .ToArray();

        var @params = new { previousResultIds };

        JsonNode? result;
        try
        {
            result = await _channel
                .CallAsync("workspace/diagnostic", @params, ct)
                .ConfigureAwait(false);
        }
        catch { return []; }

        if (result is not JsonObject obj) return [];

        var items = obj["items"] as JsonArray;
        if (items is null || items.Count == 0) return [];

        var all = new List<LspPulledDiagnostic>();
        foreach (var item in items)
        {
            if (item is not JsonObject reportObj) continue;
            var uri = reportObj["uri"]?.GetValue<string>() ?? string.Empty;
            foreach (var d in ParseDocumentReport(reportObj, uri))
                all.Add(new LspPulledDiagnostic(uri, d));
        }
        return all;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IReadOnlyList<LspDiagnostic> ParseDocumentReport(JsonNode? node, string uri)
    {
        if (node is not JsonObject obj) return [];

        var kind = obj["kind"]?.GetValue<string>();

        // "unchanged" → server has no new diagnostics; return empty (caller keeps previous set).
        if (string.Equals(kind, "unchanged", StringComparison.OrdinalIgnoreCase))
            return [];

        // Store result identifier for future incremental requests.
        var resultId = obj["resultId"]?.GetValue<string>();
        if (resultId is not null && !string.IsNullOrEmpty(uri))
            _resultIds[uri] = resultId;

        var items = obj["items"] as JsonArray;
        if (items is null || items.Count == 0) return [];

        var diagnostics = new List<LspDiagnostic>(items.Count);
        foreach (var item in items)
        {
            var d = ParseDiagnostic(item);
            if (d is not null) diagnostics.Add(d);
        }
        return diagnostics;
    }

    private static LspDiagnostic? ParseDiagnostic(JsonNode? node)
    {
        if (node is not JsonObject obj) return null;

        var message = obj["message"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(message)) return null;

        var range    = obj["range"];
        var start    = range?["start"];
        var severity = obj["severity"]?.GetValue<int>() ?? 3;

        return new LspDiagnostic
        {
            Message     = message,
            Code        = obj["code"]?.ToString(),
            Severity    = LspSeverity.ToString(severity),
            StartLine   = start?["line"]?.GetValue<int>()               ?? 0,
            StartColumn = start?["character"]?.GetValue<int>()          ?? 0,
            EndLine     = range?["end"]?["line"]?.GetValue<int>()       ?? 0,
            EndColumn   = range?["end"]?["character"]?.GetValue<int>()  ?? 0,
        };
    }

    /// <summary>Clears cached result identifiers (call when server restarts).</summary>
    internal void Reset() => _resultIds.Clear();
}
