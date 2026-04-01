// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspDefinitionProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Sends textDocument/definition and textDocument/references requests
//     and normalises the various LSP response shapes (Location, LocationLink[])
//     into the flat LspLocation list.
// ==========================================================

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspDefinitionProvider
{
    private static readonly IReadOnlyList<LspLocation> s_empty = Array.Empty<LspLocation>();

    private readonly LspJsonRpcChannel _channel;

    internal LspDefinitionProvider(LspJsonRpcChannel channel)
    {
        _channel = channel;
    }

    internal Task<IReadOnlyList<LspLocation>> GetDefinitionAsync(
        string filePath, int line, int column, CancellationToken ct)
        => QueryLocationsAsync("textDocument/definition", filePath, line, column, ct);

    internal Task<IReadOnlyList<LspLocation>> GetImplementationAsync(
        string filePath, int line, int column, CancellationToken ct)
        => QueryLocationsAsync("textDocument/implementation", filePath, line, column, ct);

    internal Task<IReadOnlyList<LspLocation>> GetTypeDefinitionAsync(
        string filePath, int line, int column, CancellationToken ct)
        => QueryLocationsAsync("textDocument/typeDefinition", filePath, line, column, ct);

    internal Task<IReadOnlyList<LspLocation>> GetReferencesAsync(
        string filePath, int line, int column, CancellationToken ct)
    {
        var uri     = LspDocumentSync.ToUri(filePath);
        var @params = new
        {
            textDocument = new { uri },
            position     = new { line, character = column },
            context      = new { includeDeclaration = false },
        };
        return QueryLocationsRawAsync("textDocument/references", @params, ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<LspLocation>> QueryLocationsAsync(
        string method, string filePath, int line, int column, CancellationToken ct)
    {
        var uri     = LspDocumentSync.ToUri(filePath);
        var @params = new
        {
            textDocument = new { uri },
            position     = new { line, character = column },
        };
        return await QueryLocationsRawAsync(method, @params, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<LspLocation>> QueryLocationsRawAsync(
        string method, object @params, CancellationToken ct)
    {
        JsonNode? result;
        try
        {
            result = await _channel.CallAsync(method, @params, ct).ConfigureAwait(false);
        }
        catch { return s_empty; }

        if (result is null) return s_empty;

        // Response is Location | Location[] | LocationLink[] | null
        if (result is JsonObject single)
            return ParseSingleLocation(single) is { } loc
                ? new[] { loc }
                : s_empty;

        if (result is not JsonArray arr) return s_empty;

        var list = new List<LspLocation>(arr.Count);
        foreach (var item in arr)
        {
            if (item is not JsonObject obj) continue;
            var l = ParseSingleLocation(obj);
            if (l is not null) list.Add(l);
        }
        return list;
    }

    private static LspLocation? ParseSingleLocation(JsonObject obj)
    {
        // Location: { uri, range: { start: { line, character } } }
        // LocationLink: { targetUri, targetRange: { start: ... } }
        var uri   = (obj["uri"] ?? obj["targetUri"])?.GetValue<string>();
        var range = obj["range"] ?? obj["targetRange"];
        var start = range?["start"];

        if (uri is null || start is null) return null;

        return new LspLocation
        {
            Uri         = uri,
            StartLine   = start["line"]?.GetValue<int>()      ?? 0,
            StartColumn = start["character"]?.GetValue<int>() ?? 0,
        };
    }
}
