// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspCodeActionProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Sends textDocument/codeAction requests and converts the JSON response
//     to a list of LspCodeAction for display in the CodeEditor quick-fix popup.
//
// Architecture Notes:
//     Pattern: Provider — same pattern as LspHoverProvider.
//     Command-only actions (no `edit` node) are skipped — server-side execution
//     is out of scope for this client.
// ==========================================================

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspCodeActionProvider
{
    private readonly LspJsonRpcChannel _channel;

    internal LspCodeActionProvider(LspJsonRpcChannel channel) => _channel = channel;

    internal async Task<IReadOnlyList<LspCodeAction>> GetAsync(
        string filePath,
        int startLine, int startColumn,
        int endLine,   int endColumn,
        CancellationToken ct)
    {
        var uri     = LspDocumentSync.ToUri(filePath);
        var @params = new
        {
            textDocument = new { uri },
            range = new
            {
                start = new { line = startLine, character = startColumn },
                end   = new { line = endLine,   character = endColumn   },
            },
            context = new { diagnostics = System.Array.Empty<object>() },
        };

        JsonNode? result;
        try
        {
            result = await _channel.CallAsync("textDocument/codeAction", @params, ct)
                                    .ConfigureAwait(false);
        }
        catch { return System.Array.Empty<LspCodeAction>(); }

        if (result is not JsonArray arr) return System.Array.Empty<LspCodeAction>();

        var list = new List<LspCodeAction>(arr.Count);
        foreach (var item in arr)
        {
            if (item is null) continue;

            // Skip Command-only actions (server-side execution not supported).
            if (item["command"] is not null && item["edit"] is null) continue;

            list.Add(new LspCodeAction
            {
                Title       = item["title"]?.GetValue<string>() ?? string.Empty,
                Kind        = item["kind"]?.GetValue<string>(),
                IsPreferred = item["isPreferred"]?.GetValue<bool>() ?? false,
                Edit        = LspEditParser.ParseWorkspaceEdit(item["edit"]),
            });
        }
        return list;
    }
}
