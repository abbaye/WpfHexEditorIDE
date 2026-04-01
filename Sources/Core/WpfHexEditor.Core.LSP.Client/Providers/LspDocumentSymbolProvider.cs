// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspDocumentSymbolProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Sends textDocument/documentSymbol and parses the response into a flat
//     list of LspDocumentSymbol regardless of whether the server returns the
//     hierarchical DocumentSymbol[] or the legacy SymbolInformation[] shape.
//
// Architecture Notes:
//     Follows the same provider pattern as LspDefinitionProvider.
//     Hierarchical DocumentSymbol trees are recursively flattened so callers
//     receive a simple ordered list usable for breadcrumb resolution.
// ==========================================================

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspDocumentSymbolProvider
{
    private static readonly IReadOnlyList<LspDocumentSymbol> s_empty = Array.Empty<LspDocumentSymbol>();

    private readonly LspJsonRpcChannel _channel;
    internal Action<string>? _log;

    internal LspDocumentSymbolProvider(LspJsonRpcChannel channel)
        => _channel = channel;

    internal async Task<IReadOnlyList<LspDocumentSymbol>> GetAsync(
        string filePath, CancellationToken ct)
    {
        var uri     = LspDocumentSync.ToUri(filePath);
        var @params = new { textDocument = new { uri } };

        JsonNode? result;
        try
        {
            result = await _channel.CallAsync("textDocument/documentSymbol", @params, ct)
                                   .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[SymbolProvider] CallAsync failed: {ex.GetType().Name}: {ex.Message}");
            return s_empty;
        }

        if (result is not JsonArray arr || arr.Count == 0)
        {
            _log?.Invoke($"[SymbolProvider] Empty result for {System.IO.Path.GetFileName(filePath)}");
            return s_empty;
        }

        // Detect shape from first element:
        //   DocumentSymbol  → has "range" key
        //   SymbolInformation → has "location" key
        bool isDocumentSymbol = (arr[0] as JsonObject)?.ContainsKey("range") == true;

        var list = new List<LspDocumentSymbol>(arr.Count);
        if (isDocumentSymbol)
        {
            foreach (var item in arr)
                if (item is JsonObject obj) FlattenDocumentSymbol(obj, null, list);
        }
        else
        {
            foreach (var item in arr)
                if (item is JsonObject obj) ParseSymbolInfo(obj, list);
        }

        return list;
    }

    // ── Hierarchical DocumentSymbol ───────────────────────────────────────────

    private static void FlattenDocumentSymbol(JsonObject obj, string? container, List<LspDocumentSymbol> list)
    {
        var name  = obj["name"]?.GetValue<string>();
        var kind  = KindFromInt(obj["kind"]?.GetValue<int>() ?? 0);
        var range = obj["range"];
        var start = range?["start"];
        var end   = range?["end"];
        if (name is null || start is null) return;

        list.Add(new LspDocumentSymbol
        {
            Name          = name,
            Kind          = kind,
            StartLine     = start["line"]?.GetValue<int>()      ?? 0,
            StartColumn   = start["character"]?.GetValue<int>() ?? 0,
            EndLine       = end?["line"]?.GetValue<int>()       ?? 0,
            EndColumn     = end?["character"]?.GetValue<int>()  ?? 0,
            ContainerName = container,
        });

        if (obj["children"] is JsonArray children)
            foreach (var child in children)
                if (child is JsonObject childObj) FlattenDocumentSymbol(childObj, name, list);
    }

    // ── Legacy SymbolInformation ──────────────────────────────────────────────

    private static void ParseSymbolInfo(JsonObject obj, List<LspDocumentSymbol> list)
    {
        var name     = obj["name"]?.GetValue<string>();
        var kind     = KindFromInt(obj["kind"]?.GetValue<int>() ?? 0);
        var location = obj["location"];
        var range    = location?["range"];
        var start    = range?["start"];
        var end      = range?["end"];
        if (name is null || start is null) return;

        list.Add(new LspDocumentSymbol
        {
            Name          = name,
            Kind          = kind,
            StartLine     = start["line"]?.GetValue<int>()      ?? 0,
            StartColumn   = start["character"]?.GetValue<int>() ?? 0,
            EndLine       = end?["line"]?.GetValue<int>()       ?? 0,
            EndColumn     = end?["character"]?.GetValue<int>()  ?? 0,
            ContainerName = obj["containerName"]?.GetValue<string>(),
        });
    }

    // ── LSP SymbolKind integer → canonical lowercase string ───────────────────

    private static string KindFromInt(int k) => k switch
    {
        1  => "file",          2  => "module",        3  => "namespace",
        4  => "package",       5  => "class",         6  => "method",
        7  => "property",      8  => "field",         9  => "constructor",
        10 => "enum",          11 => "interface",     12 => "function",
        13 => "variable",      14 => "constant",      15 => "string",
        16 => "number",        17 => "boolean",       18 => "array",
        19 => "object",        20 => "key",           21 => "null",
        22 => "enummember",    23 => "struct",        24 => "event",
        25 => "operator",      26 => "typeparameter",
        _  => "unknown",
    };
}
