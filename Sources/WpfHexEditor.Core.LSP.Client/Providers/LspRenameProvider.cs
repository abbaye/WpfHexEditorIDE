// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspRenameProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Sends textDocument/rename requests and converts the WorkspaceEdit response
//     to an LspWorkspaceEdit for application by the CodeEditor rename service.
//
// Architecture Notes:
//     Pattern: Provider — same pattern as LspHoverProvider.
//     Edit parsing delegated to shared LspEditParser.
// ==========================================================

using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspRenameProvider
{
    private readonly LspJsonRpcChannel _channel;

    internal LspRenameProvider(LspJsonRpcChannel channel) => _channel = channel;

    internal async Task<LspWorkspaceEdit?> GetAsync(
        string filePath, int line, int column, string newName, CancellationToken ct)
    {
        var uri     = LspDocumentSync.ToUri(filePath);
        var @params = new
        {
            textDocument = new { uri },
            position     = new { line, character = column },
            newName,
        };

        JsonNode? result;
        try
        {
            result = await _channel.CallAsync("textDocument/rename", @params, ct)
                                    .ConfigureAwait(false);
        }
        catch { return null; }

        return LspEditParser.ParseWorkspaceEdit(result);
    }
}
