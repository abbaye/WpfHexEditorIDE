// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspInlayHintsProvider.cs
// Description:
//     Sends textDocument/inlayHint requests and decodes the JSON response
//     into LspInlayHint records for display by LspInlayHintsLayer.
//
// Architecture Notes:
//     LSP 3.17 — label may be a plain string OR an array of InlayHintLabelPart { value }.
//     kind: 1 = Type, 2 = Parameter.
// ==========================================================

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspInlayHintsProvider
{
    private readonly LspJsonRpcChannel _channel;

    internal LspInlayHintsProvider(LspJsonRpcChannel channel)
        => _channel = channel;

    internal async Task<IReadOnlyList<LspInlayHint>> GetAsync(
        string filePath, int startLine, int endLine, CancellationToken ct)
    {
        var uri     = LspDocumentSync.ToUri(filePath);
        var @params = new
        {
            textDocument = new { uri },
            range = new
            {
                start = new { line = startLine, character = 0 },
                end   = new { line = endLine + 10, character = 0 },
            },
        };

        JsonNode? result;
        try
        {
            result = await _channel.CallAsync("textDocument/inlayHint", @params, ct)
                                    .ConfigureAwait(false);
        }
        catch { return System.Array.Empty<LspInlayHint>(); }

        if (result is not JsonArray arr) return System.Array.Empty<LspInlayHint>();

        var hints = new List<LspInlayHint>(arr.Count);
        foreach (var item in arr)
        {
            if (item is null) continue;

            var pos    = item["position"];
            var line   = pos?["line"]?.GetValue<int>()      ?? -1;
            var col    = pos?["character"]?.GetValue<int>() ?? 0;
            var kind   = item["kind"]?.GetValue<int>() ?? 0;
            var label  = ParseLabel(item["label"]);

            if (line < 0 || string.IsNullOrEmpty(label)) continue;

            hints.Add(new LspInlayHint
            {
                Line   = line,
                Column = col,
                Label  = label,
                Kind   = kind == 2 ? "parameter" : "type",
            });
        }
        return hints;
    }

    private static string? ParseLabel(JsonNode? node)
    {
        if (node is null) return null;

        // Plain string
        if (node is JsonValue) return node.GetValue<string>();

        // Array of InlayHintLabelPart { value, ... }
        if (node is JsonArray parts)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var part in parts)
                sb.Append(part?["value"]?.GetValue<string>() ?? string.Empty);
            return sb.Length > 0 ? sb.ToString() : null;
        }
        return null;
    }
}
