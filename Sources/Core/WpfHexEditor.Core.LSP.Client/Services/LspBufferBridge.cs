// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Services/LspBufferBridge.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Bridges one IDocumentBuffer to one ILspClient.
//     Sends textDocument/didOpen on construction, debounced
//     textDocument/didChange on every buffer mutation, and
//     textDocument/didClose on Dispose.
//
// Architecture Notes:
//     Pattern: Adapter / Event Bridge
//     - One bridge per (buffer × lspClient) pair, owned by LspDocumentBridgeService.
//     - 300 ms DispatcherTimer debounce prevents flooding the LSP server on rapid
//       keystrokes; the last version at debounce expiry is always sent.
//     - Must be created and used on the WPF Dispatcher thread.
// ==========================================================

using System.Windows.Threading;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Core.LSP.Client.Services;

/// <summary>
/// Bridges one <see cref="IDocumentBuffer"/> to one <see cref="ILspClient"/>.
/// Dispose to send textDocument/didClose and stop the debounce timer.
/// </summary>
public sealed class LspBufferBridge : IDisposable
{
    private readonly ILspClient      _client;
    private readonly IDocumentBuffer _buffer;
    private readonly DispatcherTimer _debounce;
    private          string          _pendingText  = string.Empty;
    private          int             _pendingVersion;
    private          bool            _disposed;

    public LspBufferBridge(ILspClient client, IDocumentBuffer buffer)
    {
        _client = client;
        _buffer = buffer;

        // 300 ms debounce — same as the old _lspChangeTimer inside CodeEditor.
        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounce.Tick += OnDebounce;

        // Send initial didOpen with current buffer content.
        _client.OpenDocument(buffer.FilePath, buffer.LanguageId, buffer.Text);

        _buffer.Changed += OnBufferChanged;
    }

    // -- Event handler ---------------------------------------------------------

    private void OnBufferChanged(object? sender, DocumentBufferChangedEventArgs e)
    {
        // Capture latest version; the debounce timer will send it.
        _pendingText    = e.NewText;
        _pendingVersion = e.NewVersion;

        _debounce.Stop();
        _debounce.Start();
    }

    private void OnDebounce(object? sender, EventArgs e)
    {
        _debounce.Stop();
        if (!_disposed)
            _client.DidChange(_buffer.FilePath, _pendingVersion, _pendingText);
    }

    // -- IDisposable -----------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _debounce.Stop();
        _debounce.Tick -= OnDebounce;

        _buffer.Changed -= OnBufferChanged;
        _client.CloseDocument(_buffer.FilePath);
    }
}
