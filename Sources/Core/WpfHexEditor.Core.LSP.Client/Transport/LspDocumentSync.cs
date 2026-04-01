// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Transport;

/// <summary>
/// Manages open-document state and sends LSP textDocument sync notifications.
/// </summary>
internal sealed class LspDocumentSync
{
    private readonly LspJsonRpcChannel               _channel;
    private readonly Dictionary<string, int>          _versions = new();     // uri → version

    /// <summary>
    /// Optional callback invoked (with the document URI) after each textDocument/didChange.
    /// Used by <see cref="LspClientImpl"/> in pull-diagnostics mode to restart the pull timer.
    /// </summary>
    internal Action<string>? OnDocumentChanged;

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

        // Notify pull-diagnostics coordinator that the document changed.
        OnDocumentChanged?.Invoke(uri);
    }

    /// <summary>
    /// Sends textDocument/didSave.
    /// If <paramref name="text"/> is non-null the full text is included
    /// (required when the server's saveOptions.includeText = true).
    /// </summary>
    internal async Task DidSaveAsync(string filePath, string? text, CancellationToken ct)
    {
        var uri = PathToUri(filePath);
        object p = text is not null
            ? new { textDocument = new { uri }, text }
            : new { textDocument = new { uri } };
        await _channel.NotifyAsync("textDocument/didSave", p, ct).ConfigureAwait(false);
    }

    internal async Task DidCloseAsync(string filePath, CancellationToken ct)
    {
        var uri = PathToUri(filePath);
        _versions.Remove(uri);

        var p = new { textDocument = new { uri } };
        await _channel.NotifyAsync("textDocument/didClose", p, ct).ConfigureAwait(false);
    }

    internal static string ToUri(string filePath) => PathToUri(filePath);

    /// <summary>Converts an LSP file:/// URI back to a local file system path.</summary>
    internal static string FromUri(string uri)
    {
        if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri[8..].Replace('/', System.IO.Path.DirectorySeparatorChar);
            return Uri.UnescapeDataString(path);
        }
        return Uri.UnescapeDataString(uri);
    }
}
