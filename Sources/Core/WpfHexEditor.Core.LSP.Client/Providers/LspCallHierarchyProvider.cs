// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspCallHierarchyProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Implements the three call-hierarchy LSP methods:
//       - textDocument/prepareCallHierarchy
//       - callHierarchy/incomingCalls
//       - callHierarchy/outgoingCalls
//
// Architecture Notes:
//     Provider pattern — stateless, delegates JSON-RPC framing to LspJsonRpcChannel.
//     SymbolKind is mapped from the LSP integer enum to a readable string (e.g. "method").
// ==========================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspCallHierarchyProvider
{
    private readonly LspJsonRpcChannel _channel;

    internal LspCallHierarchyProvider(LspJsonRpcChannel channel)
    {
        _channel = channel;
    }

    // ── Prepare ────────────────────────────────────────────────────────────────

    internal async Task<IReadOnlyList<LspCallHierarchyItem>> PrepareAsync(
        string filePath, int line, int column, CancellationToken ct)
    {
        var uri     = LspDocumentSync.ToUri(filePath);
        var @params = new
        {
            textDocument = new { uri },
            position     = new { line, character = column },
        };

        JsonNode? result;
        try
        {
            result = await _channel
                .CallAsync("textDocument/prepareCallHierarchy", @params, ct)
                .ConfigureAwait(false);
        }
        catch { return Array.Empty<LspCallHierarchyItem>(); }

        if (result is not JsonArray arr || arr.Count == 0)
            return Array.Empty<LspCallHierarchyItem>();

        return ParseItems(arr);
    }

    // ── Incoming ───────────────────────────────────────────────────────────────

    internal async Task<IReadOnlyList<LspIncomingCall>> GetIncomingCallsAsync(
        LspCallHierarchyItem item, CancellationToken ct)
    {
        var @params = new { item = SerializeItem(item) };

        JsonNode? result;
        try
        {
            result = await _channel
                .CallAsync("callHierarchy/incomingCalls", @params, ct)
                .ConfigureAwait(false);
        }
        catch { return Array.Empty<LspIncomingCall>(); }

        if (result is not JsonArray arr || arr.Count == 0)
            return Array.Empty<LspIncomingCall>();

        var calls = new List<LspIncomingCall>(arr.Count);
        foreach (var node in arr)
        {
            if (node is null) continue;
            var fromNode = node["from"];
            if (fromNode is null) continue;
            var from = ParseItem(fromNode);
            if (from is null) continue;
            var ranges = ParseRangeList(node["fromRanges"]);
            calls.Add(new LspIncomingCall { From = from, FromRanges = ranges });
        }
        return calls;
    }

    // ── Outgoing ───────────────────────────────────────────────────────────────

    internal async Task<IReadOnlyList<LspOutgoingCall>> GetOutgoingCallsAsync(
        LspCallHierarchyItem item, CancellationToken ct)
    {
        var @params = new { item = SerializeItem(item) };

        JsonNode? result;
        try
        {
            result = await _channel
                .CallAsync("callHierarchy/outgoingCalls", @params, ct)
                .ConfigureAwait(false);
        }
        catch { return Array.Empty<LspOutgoingCall>(); }

        if (result is not JsonArray arr || arr.Count == 0)
            return Array.Empty<LspOutgoingCall>();

        var calls = new List<LspOutgoingCall>(arr.Count);
        foreach (var node in arr)
        {
            if (node is null) continue;
            var toNode = node["to"];
            if (toNode is null) continue;
            var to = ParseItem(toNode);
            if (to is null) continue;
            var ranges = ParseRangeList(node["fromRanges"]);
            calls.Add(new LspOutgoingCall { To = to, FromRanges = ranges });
        }
        return calls;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static IReadOnlyList<LspCallHierarchyItem> ParseItems(JsonArray arr)
    {
        var items = new List<LspCallHierarchyItem>(arr.Count);
        foreach (var node in arr)
        {
            var item = ParseItem(node);
            if (item is not null) items.Add(item);
        }
        return items;
    }

    private static LspCallHierarchyItem? ParseItem(JsonNode? node)
    {
        if (node is null) return null;

        var name      = node["name"]?.GetValue<string>();
        var uri       = node["uri"]?.GetValue<string>();
        if (name is null || uri is null) return null;

        var selRange  = node["selectionRange"] ?? node["range"];
        var start     = selRange?["start"];

        return new LspCallHierarchyItem
        {
            Name          = name,
            Kind          = SymbolKindToString(node["kind"]?.GetValue<int>() ?? 0),
            Uri           = LspDocumentSync.FromUri(uri),
            StartLine     = start?["line"]?.GetValue<int>()      ?? 0,
            StartColumn   = start?["character"]?.GetValue<int>() ?? 0,
            ContainerName = node["containerName"]?.GetValue<string>(),
        };
    }

    private static object SerializeItem(LspCallHierarchyItem item) => new
    {
        name = item.Name,
        kind = SymbolKindFromString(item.Kind),
        uri  = new Uri(item.Uri).AbsoluteUri,
        range           = new { start = new { line = item.StartLine, character = item.StartColumn },
                                end   = new { line = item.StartLine, character = item.StartColumn } },
        selectionRange  = new { start = new { line = item.StartLine, character = item.StartColumn },
                                end   = new { line = item.StartLine, character = item.StartColumn } },
    };

    private static IReadOnlyList<(int, int, int, int)> ParseRangeList(JsonNode? node)
    {
        if (node is not JsonArray arr || arr.Count == 0)
            return Array.Empty<(int, int, int, int)>();

        var list = new List<(int, int, int, int)>(arr.Count);
        foreach (var r in arr)
        {
            if (r is null) continue;
            var start = r["start"];
            var end   = r["end"];
            list.Add((
                start?["line"]?.GetValue<int>()      ?? 0,
                start?["character"]?.GetValue<int>() ?? 0,
                end?["line"]?.GetValue<int>()        ?? 0,
                end?["character"]?.GetValue<int>()   ?? 0));
        }
        return list;
    }

    // LSP SymbolKind integer → display string (subset used in hierarchies).
    private static string SymbolKindToString(int kind) => kind switch
    {
        1  => "file",        2  => "module",   3  => "namespace", 4  => "package",
        5  => "class",       6  => "method",   7  => "property",  8  => "field",
        9  => "constructor", 10 => "enum",     11 => "interface", 12 => "function",
        13 => "variable",    14 => "constant", 15 => "string",    26 => "operator",
        _  => "symbol",
    };

    private static int SymbolKindFromString(string kind) => kind switch
    {
        "file"        => 1,  "module"     => 2,  "namespace"   => 3,
        "package"     => 4,  "class"      => 5,  "method"      => 6,
        "property"    => 7,  "field"      => 8,  "constructor" => 9,
        "enum"        => 10, "interface"  => 11, "function"    => 12,
        "variable"    => 13, "constant"   => 14, "operator"    => 26,
        _             => 6,  // default: method
    };
}
