// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspCompletionProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Sends textDocument/completion requests and maps the JSON response to
//     the strongly-typed LspCompletionItem list expected by CodeEditor's
//     SmartCompleteEngine.
// ==========================================================

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspCompletionProvider
{
    private static readonly IReadOnlyList<LspCompletionItem> s_empty = Array.Empty<LspCompletionItem>();

    private readonly LspJsonRpcChannel _channel;

    internal LspCompletionProvider(LspJsonRpcChannel channel)
    {
        _channel = channel;
    }

    internal async Task<IReadOnlyList<LspCompletionItem>> GetAsync(
        string filePath, int line, int column, char? triggerChar, CancellationToken ct)
    {
        var uri     = LspDocumentSync.ToUri(filePath);
        // triggerKind: 1 = Invoked (Ctrl+Space), 2 = TriggerCharacter (e.g. '.')
        object context = triggerChar.HasValue
            ? new { triggerKind = 2, triggerCharacter = triggerChar.Value.ToString() }
            : new { triggerKind = 1 };
        var @params = new
        {
            textDocument = new { uri },
            position     = new { line, character = column },
            context,
        };

        JsonNode? result;
        try
        {
            result = await _channel.CallAsync("textDocument/completion", @params, ct)
                                    .ConfigureAwait(false);
        }
        catch { return s_empty; }

        if (result is null) return s_empty;

        // The response is either CompletionList { items: [...] } or just an array.
        var items = result is JsonObject obj
            ? obj["items"]?.AsArray()
            : result.AsArray();

        if (items is null) return s_empty;

        var list = new List<LspCompletionItem>(items.Count);
        foreach (var item in items)
        {
            if (item is null) continue;
            var label      = item["label"]?.GetValue<string>();
            if (label is null) continue;

            list.Add(new LspCompletionItem
            {
                Label            = label,
                Kind             = KindToString(item["kind"]?.GetValue<int?>()),
                Detail           = item["detail"]?.GetValue<string>(),
                InsertText       = item["insertText"]?.GetValue<string>(),
                Documentation    = ExtractDocumentation(item["documentation"]),
                RawJson          = item.ToJsonString(),
                CommitCharacters = ParseCommitChars(item["commitCharacters"]),
            });
        }
        return list;
    }

    internal async Task<LspCompletionItem?> ResolveAsync(LspCompletionItem item, CancellationToken ct)
    {
        if (item.RawJson is null) return null;

        JsonNode? rawItem;
        try { rawItem = JsonNode.Parse(item.RawJson); }
        catch { return null; }

        JsonNode? result;
        try
        {
            result = await _channel.CallAsync("completionItem/resolve", rawItem, ct)
                                    .ConfigureAwait(false);
        }
        catch { return null; }

        if (result is null) return null;

        return new LspCompletionItem
        {
            Label         = result["label"]?.GetValue<string>() ?? item.Label,
            Kind          = item.Kind,
            Detail        = result["detail"]?.GetValue<string>() ?? item.Detail,
            InsertText    = result["insertText"]?.GetValue<string>() ?? item.InsertText,
            Documentation = ExtractDocumentation(result["documentation"]) ?? item.Documentation,
            RawJson       = item.RawJson,
        };
    }

    private static string? KindToString(int? kind) => kind switch
    {
        1  => "Text",
        2  => "Method",
        3  => "Function",
        4  => "Constructor",
        5  => "Field",
        6  => "Variable",
        7  => "Class",
        8  => "Interface",
        9  => "Module",
        10 => "Property",
        14 => "Keyword",
        15 => "Snippet",
        17 => "File",
        18 => "Reference",
        _  => null,
    };

    private static IReadOnlyList<string>? ParseCommitChars(JsonNode? node)
    {
        if (node is not JsonArray arr || arr.Count == 0) return null;
        var list = new List<string>(arr.Count);
        foreach (var c in arr)
            if (c?.GetValue<string>() is { Length: > 0 } s) list.Add(s);
        return list.Count > 0 ? list : null;
    }

    private static string? ExtractDocumentation(JsonNode? doc)
    {
        if (doc is null) return null;
        if (doc is JsonValue) return doc.GetValue<string>();   // plain string
        return doc["value"]?.GetValue<string>();               // MarkupContent
    }
}
