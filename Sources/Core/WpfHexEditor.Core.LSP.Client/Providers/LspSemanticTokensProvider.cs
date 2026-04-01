// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspSemanticTokensProvider.cs
// Description:
//     Sends textDocument/semanticTokens/full requests and decodes the
//     delta-encoded data[] array into LspSemanticToken records.
//
// Architecture Notes:
//     LSP semantic tokens use a 5-tuple delta encoding:
//       [deltaLine, deltaStart, length, tokenType, tokenModifiers]
//     Token type names come from ServerCapabilities.SemanticTokenTypesLegend.
//     Modifiers are a bitmask into SemanticTokenModifiersLegend.
// ==========================================================

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspSemanticTokensProvider
{
    private readonly LspJsonRpcChannel          _channel;
    private readonly IReadOnlyList<string>      _tokenTypesLegend;
    private readonly IReadOnlyList<string>      _tokenModifiersLegend;

    internal LspSemanticTokensProvider(
        LspJsonRpcChannel     channel,
        IReadOnlyList<string> tokenTypesLegend,
        IReadOnlyList<string> tokenModifiersLegend)
    {
        _channel              = channel;
        _tokenTypesLegend     = tokenTypesLegend;
        _tokenModifiersLegend = tokenModifiersLegend;
    }

    internal async Task<LspSemanticTokensResult?> GetAsync(
        string filePath, CancellationToken ct)
    {
        var uri     = LspDocumentSync.ToUri(filePath);
        var @params = new { textDocument = new { uri } };

        JsonNode? result;
        try
        {
            result = await _channel.CallAsync("textDocument/semanticTokens/full", @params, ct)
                                    .ConfigureAwait(false);
        }
        catch { return null; }

        var dataNode = result?["data"];
        if (dataNode is not JsonArray dataArr || dataArr.Count == 0) return null;

        // Decode 5-tuples: [deltaLine, deltaStart, length, tokenType, tokenModifiers]
        var raw = new int[dataArr.Count];
        for (var i = 0; i < dataArr.Count; i++)
            raw[i] = dataArr[i]?.GetValue<int>() ?? 0;

        var tokens  = new List<LspSemanticToken>(raw.Length / 5);
        var absLine = 0;
        var absChar = 0;

        for (var i = 0; i + 4 < raw.Length; i += 5)
        {
            var deltaLine  = raw[i];
            var deltaStart = raw[i + 1];
            var length     = raw[i + 2];
            var typeIndex  = raw[i + 3];
            var modifiers  = raw[i + 4];

            absLine += deltaLine;
            absChar  = deltaLine == 0 ? absChar + deltaStart : deltaStart;

            var typeName = typeIndex < _tokenTypesLegend.Count
                ? _tokenTypesLegend[typeIndex]
                : null;

            string[]? modNames = null;
            if (modifiers != 0 && _tokenModifiersLegend.Count > 0)
            {
                var mods = new List<string>(4);
                for (var bit = 0; bit < _tokenModifiersLegend.Count; bit++)
                {
                    if ((modifiers & (1 << bit)) != 0)
                        mods.Add(_tokenModifiersLegend[bit]);
                }
                if (mods.Count > 0) modNames = mods.ToArray();
            }

            tokens.Add(new LspSemanticToken
            {
                Line      = absLine,
                Column    = absChar,
                Length    = length,
                TokenType = typeName ?? string.Empty,
                Modifiers = (IReadOnlyList<string>?)modNames ?? System.Array.Empty<string>(),
            });
        }

        return new LspSemanticTokensResult { Tokens = tokens };
    }
}
