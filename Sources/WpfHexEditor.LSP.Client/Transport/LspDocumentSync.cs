// ==========================================================
// Project: WpfHexEditor.LSP.Client
// File: Transport/LspDocumentSync.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Tracks open documents and sends textDocument/didOpen,
//     textDocument/didChange (full-sync), and textDocument/didClose
//     notifications through the LspJsonRpcChannel.
//
// Architecture Notes:
//     Dictionary<uri, version> prevents sending duplicate opens and
//     ensures monotonically increasing version numbers per document.
// ==========================================================

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.LSP.Client.Transport;

namespace WpfHexEditor.LSP.Client.Transport;

/// <summary>
/// Manages open-document state and sends LSP textDocument sync notifications.
/// </summary>
internal sealed class LspDocumentSync
{
    private readonly LspJsonRpcChannel               _channel;
    private readonly Dictionary<string, int>          _versions = new();     // uri → version

    internal LspDocumentSync(LspJsonRpcChannel channel)
    {
        _channel = channel;
    }

    // ── Path → URI helper ──────────────────────────────────────────────────────

    private static string PathToUri(string filePath)
        => new Uri(filePath).AbsoluteUri;

    // ── API ────────────────────────────────────────────────────────────────────

    internal async Task DidOpenAsync(string filePath, string languageId, string text, CancellationToken ct)
    {
        var uri = PathToUri(filePath);
        _versions[uri] = 1;

        var p = new
        {
            textDocument = new
            {
                uri,
                languageId,
                version = 1,
                text,
            },
        };
        await _channel.NotifyAsync("textDocument/didOpen", p, ct).ConfigureAwait(false);
    }

    internal async Task DidChangeAsync(string filePath, int version, string newText, CancellationToken ct)
    {
        var uri = PathToUri(filePath);
        _versions[uri] = version;

        var p = new
        {
            textDocument = new { uri, version },
            contentChanges = new[] { new { text = newText } },   // full sync
        };
        await _channel.NotifyAsync("textDocument/didChange", p, ct).ConfigureAwait(false);
    }

    internal async Task DidCloseAsync(string filePath, CancellationToken ct)
    {
        var uri = PathToUri(filePath);
        _versions.Remove(uri);

        var p = new { textDocument = new { uri } };
        await _channel.NotifyAsync("textDocument/didClose", p, ct).ConfigureAwait(false);
    }

    internal static string ToUri(string filePath) => PathToUri(filePath);
}
