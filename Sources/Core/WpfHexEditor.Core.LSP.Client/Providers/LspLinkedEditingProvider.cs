// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspLinkedEditingProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Sends textDocument/linkedEditingRange requests and converts the JSON
//     response to a flat list of LspLinkedRange for use by the CodeEditor's
//     simultaneous-edit synchronisation logic.
//
// Architecture Notes:
//     Provider pattern — stateless, one call per user keystroke (debounced by
//     the CodeEditor caller).  Returns an empty list when the server does not
//     support the capability or the caret is outside any linked region.
// ==========================================================

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspLinkedEditingProvider
{
    private readonly LspJsonRpcChannel _channel;

    internal LspLinkedEditingProvider(LspJsonRpcChannel channel)
    {
        _channel = channel;
    }

    internal async Task<IReadOnlyList<LspLinkedRange>> GetAsync(
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
                .CallAsync("textDocument/linkedEditingRange", @params, ct)
                .ConfigureAwait(false);
        }
        catch { return System.Array.Empty<LspLinkedRange>(); }

        if (result is null) return System.Array.Empty<LspLinkedRange>();

        var rangesNode = result["ranges"];
        if (rangesNode is not JsonArray arr || arr.Count == 0)
            return System.Array.Empty<LspLinkedRange>();

        var ranges = new List<LspLinkedRange>(arr.Count);
        foreach (var item in arr)
        {
            if (item is null) continue;
            var start = item["start"];
            var end   = item["end"];
            ranges.Add(new LspLinkedRange
            {
                StartLine   = start?["line"]?.GetValue<int>()      ?? 0,
                StartColumn = start?["character"]?.GetValue<int>() ?? 0,
                EndLine     = end?["line"]?.GetValue<int>()        ?? 0,
                EndColumn   = end?["character"]?.GetValue<int>()   ?? 0,
            });
        }
        return ranges;
    }
}
