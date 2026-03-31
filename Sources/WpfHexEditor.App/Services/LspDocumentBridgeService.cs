// ==========================================================
// Project: WpfHexEditor.App
// File: Services/LspDocumentBridgeService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Wires DocumentManager document lifecycle events to LspBufferBridge instances.
//     For each registered document whose buffer has a known LanguageId,
//     the service obtains (or reuses) an initialized ILspClient from the registry
//     and creates a bridge so the LSP server is notified of didOpen/didChange/didClose.
//
// Architecture Notes:
//     Pattern: Service / Coordinator
//     - One LspClient per language ID (lazy, initialized once, shared across documents).
//     - One LspBufferBridge per (filePath × languageId) pair.
//     - Best-effort: LSP failures are caught, logged, and do not block the editor.
//     - Must be created on and operate on the WPF Dispatcher thread.
// ==========================================================

using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Services;

namespace WpfHexEditor.App.Services;

// ── LSP server state ─────────────────────────────────────────────────────────

/// <summary>Lifecycle states of a Language Server Process.</summary>
public enum LspServerState { Idle, Connecting, Ready, Error }

/// <summary>Event args published when an LSP server changes state.</summary>
public sealed class LspServerStateChangedEventArgs : EventArgs
{
    public required string        LanguageId { get; init; }
    public required LspServerState State      { get; init; }
    /// <summary>Human-readable server name (e.g. "OmniSharp"). Null when Idle.</summary>
    public          string?       ServerName { get; init; }
    /// <summary>Error message when State == Error.</summary>
    public          string?       ErrorMessage { get; init; }
}

/// <summary>
/// Coordinates <see cref="LspBufferBridge"/> creation/disposal in response to
/// <see cref="IDocumentManager"/> document lifecycle events.
/// </summary>
internal sealed class LspDocumentBridgeService : IDisposable
{
    private readonly IDocumentManager              _documentManager;
    private readonly ILspServerRegistry            _registry;
    private readonly Action<string>                _log;
    private readonly Func<string?>                 _workspacePathProvider;

    // One initialized LSP client per language ID (created lazily, shared across documents).
    private readonly Dictionary<string, ILspClient>       _clients = new(StringComparer.OrdinalIgnoreCase);

    // One bridge per open document file path.
    private readonly Dictionary<string, LspBufferBridge>  _bridges = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// The most recently initialized LSP client, or null when no server is active.
    /// Used by the workspace-symbols popup (Ctrl+T) to issue workspace/symbol requests.
    /// </summary>
    public ILspClient? ActiveClient => _clients.Values.FirstOrDefault(c => c.IsInitialized);

    // ── Public events ─────────────────────────────────────────────────────────

    /// <summary>
    /// Raised on the WPF dispatcher thread when a language server's connection state changes.
    /// Consumers (e.g. <c>LspStatusBarAdapter</c>) can subscribe to update UI indicators.
    /// </summary>
    public event EventHandler<LspServerStateChangedEventArgs>? ServerStateChanged;

    // ── Construction ──────────────────────────────────────────────────────────

    internal LspDocumentBridgeService(
        IDocumentManager   documentManager,
        ILspServerRegistry registry,
        Action<string>     log,
        Func<string?>?     workspacePathProvider = null)
    {
        _documentManager       = documentManager ?? throw new ArgumentNullException(nameof(documentManager));
        _registry              = registry        ?? throw new ArgumentNullException(nameof(registry));
        _log                   = log             ?? (_ => { });
        _workspacePathProvider = workspacePathProvider ?? (() => null);

        _documentManager.DocumentRegistered   += OnDocumentRegistered;
        _documentManager.DocumentUnregistered += OnDocumentUnregistered;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnDocumentRegistered(object? sender, DocumentModel doc)
    {
        // Buffer is attached slightly after Register, via AttachEditor — check asynchronously.
        // Use a short background dispatch so the buffer is already set by the time we check.
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(
            () => TryCreateBridge(doc),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void TryCreateBridge(DocumentModel doc)
    {
        if (_disposed || doc.Buffer is not { } buffer) return;
        if (string.IsNullOrEmpty(buffer.LanguageId))   return;
        if (_bridges.ContainsKey(buffer.FilePath))     return;  // already bridged

        var entry = _registry.FindByLanguage(buffer.LanguageId);
        if (entry is null || !entry.IsEnabled) return;

        OutputLogger.LspDebug($"TryCreateBridge: {System.IO.Path.GetFileName(buffer.FilePath)} [{buffer.LanguageId}]");
        _ = InitBridgeAsync(entry, buffer);
    }

    private async Task InitBridgeAsync(LspServerEntry entry, IDocumentBuffer buffer)
    {
        try
        {
            if (!_clients.TryGetValue(entry.LanguageId, out var client))
            {
                RaiseState(entry, LspServerState.Connecting);
                var workspacePath = _workspacePathProvider()
                    ?? System.IO.Path.GetDirectoryName(buffer.FilePath);
                OutputLogger.LspDebug($"CreateClient [{entry.LanguageId}] workspace={workspacePath ?? "null"}");
                client = _registry.CreateClient(entry, workspacePath);
                if (client is WpfHexEditor.Core.LSP.Client.LspClientImpl impl)
                    impl.Logger = msg => OutputLogger.LspDebug(msg);
                await client.InitializeAsync().ConfigureAwait(true);
                _clients[entry.LanguageId] = client;
                OutputLogger.LspInfo($"Server ready: {entry.LanguageId}");
                RaiseState(entry, LspServerState.Ready);
            }

            if (_disposed || _bridges.ContainsKey(buffer.FilePath)) return;

            var bridge = new LspBufferBridge(client, buffer);
            _bridges[buffer.FilePath] = bridge;
            OutputLogger.LspInfo($"Bridge: {System.IO.Path.GetFileName(buffer.FilePath)} → {entry.LanguageId}");

            // Inject the LSP client into any ILspAwareEditor so it can invoke
            // Code Actions and Rename directly (e.g. CodeEditor via Ctrl+. / F2).
            var doc = _documentManager.FindDocumentByBuffer(buffer);
            if (doc?.AssociatedEditor is ILspAwareEditor lspEditor)
            {
                if (lspEditor is WpfHexEditor.Editor.CodeEditor.Controls.CodeEditorSplitHost splitHost)
                    splitHost.BreadcrumbLogger = msg => OutputLogger.LspDebug(msg);

                lspEditor.SetLspClient(client);
                lspEditor.SetDocumentManager(_documentManager);
            }
        }
        catch (Exception ex)
        {
            OutputLogger.LspError($"Bridge init failed for '{System.IO.Path.GetFileName(buffer.FilePath)}': {ex.Message}");
            RaiseState(entry, LspServerState.Error, ex.Message);
        }
    }

    private void RaiseState(LspServerEntry entry, LspServerState state, string? error = null)
    {
        var args = new LspServerStateChangedEventArgs
        {
            LanguageId   = entry.LanguageId,
            State        = state,
            ServerName   = entry.LanguageId,   // use language ID as display name
            ErrorMessage = error,
        };
        ServerStateChanged?.Invoke(this, args);
    }

    private void OnDocumentUnregistered(object? sender, DocumentModel doc)
    {
        if (doc.Buffer is not { } buffer) return;
        if (_bridges.TryGetValue(buffer.FilePath, out var bridge))
        {
            bridge.Dispose();
            _bridges.Remove(buffer.FilePath);
        }

        // Clear the LSP client reference on the editor so it no longer tries to use it.
        if (doc.AssociatedEditor is ILspAwareEditor lspEditor)
            lspEditor.SetLspClient(null);
    }

    // ── Public helpers ────────────────────────────────────────────────────────

    /// <summary>Returns the active LSP client for the given language ID, or null if none.</summary>
    public ILspClient? TryGetClient(string languageId)
        => _clients.TryGetValue(languageId, out var client) ? client : null;

    /// <summary>
    /// Re-tries bridging all currently open documents.
    /// Call after the LSP service is created to catch documents opened before initialization.
    /// </summary>
    public void RetryOpenDocuments()
    {
        foreach (var doc in _documentManager.OpenDocuments)
            TryCreateBridge(doc);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _documentManager.DocumentRegistered   -= OnDocumentRegistered;
        _documentManager.DocumentUnregistered -= OnDocumentUnregistered;

        foreach (var bridge in _bridges.Values)
            bridge.Dispose();
        _bridges.Clear();

        foreach (var client in _clients.Values)
            client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _clients.Clear();
    }
}
