// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspWorkspaceSymbolsProvider.cs
// Description:
//     Sends workspace/symbol requests and decodes the JSON response
//     into LspWorkspaceSymbol records for the Go-to-Symbol palette.
//
// Architecture Notes:
//     LSP 3.17 returns either SymbolInformation[] or WorkspaceSymbol[].
//     Both formats are handled: location.uri/range for SymbolInformation,
//     location.uri + location.range for WorkspaceSymbol (same structure).
// ==========================================================

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspWorkspaceSymbolsProvider
{
    private readonly LspJsonRpcChannel _channel;

    internal LspWorkspaceSymbolsProvider(LspJsonRpcChannel channel)
        => _channel = channel;

    internal async Task<IReadOnlyList<LspWorkspaceSymbol>> GetAsync(
        string query, CancellationToken ct)
    {
        var @params = new { query };

        JsonNode? result;
        try
        {
            result = await _channel.CallAsync("workspace/symbol", @params, ct)
                                    .ConfigureAwait(false);
        }
        catch { return System.Array.Empty<LspWorkspaceSymbol>(); }

        if (result is not JsonArray arr) return System.Array.Empty<LspWorkspaceSymbol>();

        var symbols = new List<LspWorkspaceSymbol>(arr.Count);
        foreach (var item in arr)
        {
            if (item is null) continue;

            var name      = item["name"]?.GetValue<string>() ?? string.Empty;
            var kind      = item["kind"]?.GetValue<int>()    ?? 0;
            var container = item["containerName"]?.GetValue<string>();

            // Location can be { uri, range } or just { uri } (WorkspaceSymbol LSP 3.17)
            var location = item["location"];
            var uri      = location?["uri"]?.GetValue<string>() ?? string.Empty;
            var range    = location?["range"];
            var start    = range?["start"];
            var line     = start?["line"]?.GetValue<int>()      ?? 0;
            var col      = start?["character"]?.GetValue<int>() ?? 0;

            var filePath = LspDocumentSync.FromUri(uri);

            symbols.Add(new LspWorkspaceSymbol
            {
                Name          = name,
                Kind          = KindToName(kind),
                Uri           = uri,
                StartLine     = line,
                StartColumn   = col,
                ContainerName = container,
            });
        }
        return symbols;
    }

    // LSP SymbolKind integer → lowercase name consumed by GoToSymbolItem.KindToGlyph
    private static string KindToName(int kind) => kind switch
    {
        1  => "file",
        2  => "module",
        3  => "namespace",
        4  => "package",
        5  => "class",
        6  => "method",
        7  => "property",
        8  => "field",
        9  => "constructor",
        10 => "enum",
        11 => "interface",
        12 => "function",
        13 => "variable",
        14 => "constant",
        15 => "string",
        16 => "number",
        17 => "boolean",
        18 => "array",
        19 => "object",
        20 => "key",
        21 => "null",
        22 => "enummember",
        23 => "struct",
        24 => "event",
        25 => "operator",
        26 => "typeparameter",
        _  => "unknown",
    };
}
