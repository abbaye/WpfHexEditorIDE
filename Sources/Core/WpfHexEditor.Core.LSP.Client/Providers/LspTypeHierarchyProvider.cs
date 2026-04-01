// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspTypeHierarchyProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Implements the three type-hierarchy LSP methods:
//       - textDocument/prepareTypeHierarchy
//       - typeHierarchy/supertypes
//       - typeHierarchy/subtypes
//
// Architecture Notes:
//     Provider pattern — stateless, delegates JSON-RPC framing to LspJsonRpcChannel.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspTypeHierarchyProvider
{
    private readonly LspJsonRpcChannel _channel;

    internal LspTypeHierarchyProvider(LspJsonRpcChannel channel)
    {
        _channel = channel;
    }

    // ── Prepare ────────────────────────────────────────────────────────────────

    internal async Task<IReadOnlyList<LspTypeHierarchyItem>> PrepareAsync(
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
                .CallAsync("textDocument/prepareTypeHierarchy", @params, ct)
                .ConfigureAwait(false);
        }
        catch { return Array.Empty<LspTypeHierarchyItem>(); }

        if (result is not JsonArray arr || arr.Count == 0)
            return Array.Empty<LspTypeHierarchyItem>();

        return ParseItems(arr);
    }

    // ── Supertypes ─────────────────────────────────────────────────────────────

    internal async Task<IReadOnlyList<LspTypeHierarchyItem>> GetSupertypesAsync(
        LspTypeHierarchyItem item, CancellationToken ct)
    {
        var @params = new { item = SerializeItem(item) };

        JsonNode? result;
        try
        {
            result = await _channel
                .CallAsync("typeHierarchy/supertypes", @params, ct)
                .ConfigureAwait(false);
        }
        catch { return Array.Empty<LspTypeHierarchyItem>(); }

        if (result is not JsonArray arr || arr.Count == 0)
            return Array.Empty<LspTypeHierarchyItem>();

        return ParseItems(arr);
    }

    // ── Subtypes ───────────────────────────────────────────────────────────────

    internal async Task<IReadOnlyList<LspTypeHierarchyItem>> GetSubtypesAsync(
        LspTypeHierarchyItem item, CancellationToken ct)
    {
        var @params = new { item = SerializeItem(item) };

        JsonNode? result;
        try
        {
            result = await _channel
                .CallAsync("typeHierarchy/subtypes", @params, ct)
                .ConfigureAwait(false);
        }
        catch { return Array.Empty<LspTypeHierarchyItem>(); }

        if (result is not JsonArray arr || arr.Count == 0)
            return Array.Empty<LspTypeHierarchyItem>();

        return ParseItems(arr);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static IReadOnlyList<LspTypeHierarchyItem> ParseItems(JsonArray arr)
    {
        var items = new List<LspTypeHierarchyItem>(arr.Count);
        foreach (var node in arr)
        {
            var item = ParseItem(node);
            if (item is not null) items.Add(item);
        }
        return items;
    }

    private static LspTypeHierarchyItem? ParseItem(JsonNode? node)
    {
        if (node is null) return null;

        var name = node["name"]?.GetValue<string>();
        var uri  = node["uri"]?.GetValue<string>();
        if (name is null || uri is null) return null;

        var selRange = node["selectionRange"] ?? node["range"];
        var start    = selRange?["start"];

        return new LspTypeHierarchyItem
        {
            Name          = name,
            Kind          = SymbolKindToString(node["kind"]?.GetValue<int>() ?? 0),
            Uri           = LspDocumentSync.FromUri(uri),
            StartLine     = start?["line"]?.GetValue<int>()      ?? 0,
            StartColumn   = start?["character"]?.GetValue<int>() ?? 0,
            ContainerName = node["containerName"]?.GetValue<string>(),
        };
    }

    private static object SerializeItem(LspTypeHierarchyItem item) => new
    {
        name = item.Name,
        kind = SymbolKindFromString(item.Kind),
        uri  = new Uri(item.Uri).AbsoluteUri,
        range          = new { start = new { line = item.StartLine, character = item.StartColumn },
                               end   = new { line = item.StartLine, character = item.StartColumn } },
        selectionRange = new { start = new { line = item.StartLine, character = item.StartColumn },
                               end   = new { line = item.StartLine, character = item.StartColumn } },
    };

    private static string SymbolKindToString(int kind) => kind switch
    {
        1  => "file",        2  => "module",      3  => "namespace",  4  => "package",
        5  => "class",       6  => "method",       7  => "property",   8  => "field",
        9  => "constructor", 10 => "enum",         11 => "interface",  12 => "function",
        13 => "variable",    14 => "constant",     15 => "string",     26 => "operator",
        _  => "symbol",
    };

    private static int SymbolKindFromString(string kind) => kind switch
    {
        "file"        => 1,  "module"    => 2,  "namespace"   => 3,
        "package"     => 4,  "class"     => 5,  "method"      => 6,
        "property"    => 7,  "field"     => 8,  "constructor" => 9,
        "enum"        => 10, "interface" => 11, "function"    => 12,
        "variable"    => 13, "constant"  => 14, "operator"    => 26,
        _             => 5,  // default: class
    };
}
