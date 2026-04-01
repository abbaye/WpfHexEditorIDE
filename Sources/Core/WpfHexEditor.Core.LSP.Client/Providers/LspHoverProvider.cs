// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspHoverProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Sends textDocument/hover requests and converts the JSON response
//     to LspHoverResult for display in the CodeEditor tooltip popup.
// ==========================================================

using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspHoverProvider
{
    private readonly LspJsonRpcChannel _channel;

    internal LspHoverProvider(LspJsonRpcChannel channel)
    {
        _channel = channel;
    }

    internal async Task<LspHoverResult?> GetAsync(
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
            result = await _channel.CallAsync("textDocument/hover", @params, ct)
                                    .ConfigureAwait(false);
        }
        catch { return null; }

        if (result is null) return null;

        // contents may be a string, a MarkupContent { kind, value }, or an array.
        var contentsNode = result["contents"];
        var text         = ExtractContents(contentsNode);
        return text is null ? null : new LspHoverResult { Contents = text };
    }

    private static string? ExtractContents(JsonNode? node)
    {
        if (node is null) return null;

        // Plain string
        if (node is JsonValue) return node.GetValue<string>();

        // MarkupContent { kind, value }
        if (node["value"] is JsonNode v) return v.GetValue<string>();

        // Array — join the first non-null text entry
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                var t = ExtractContents(item);
                if (t is not null) return t;
            }
        }
        return null;
    }
}
