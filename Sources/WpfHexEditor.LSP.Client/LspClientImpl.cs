// ==========================================================
// Project: WpfHexEditor.LSP.Client
// File: LspClientImpl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Concrete implementation of ILspClient.
//     Composes LspProcess, LspDocumentSync, and the feature providers to deliver
//     a full LSP client consumable by CodeEditor and plugins.
//
// Architecture Notes:
//     Facade Pattern — single entry-point that delegates to Transport and Provider layers.
//     Thread-safety — DiagnosticsReceived always raised on the WPF Dispatcher (main thread).
// ==========================================================

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.LSP.Client.Providers;
using WpfHexEditor.LSP.Client.Services;
using WpfHexEditor.LSP.Client.Transport;

namespace WpfHexEditor.LSP.Client;

/// <summary>
/// Full <see cref="ILspClient"/> implementation backed by a language server process.
/// Created by <see cref="LspServerRegistry.CreateClient"/>.
/// </summary>
public sealed class LspClientImpl : ILspClient
{
    // ── Fields ─────────────────────────────────────────────────────────────────
    private readonly string           _executablePath;
    private readonly string?          _arguments;
    private readonly string?          _workspacePath;
    private readonly Dispatcher       _dispatcher;

    private LspProcess?               _process;
    private LspDocumentSync?          _sync;
    private LspCompletionProvider?    _completion;
    private LspHoverProvider?         _hover;
    private LspDefinitionProvider?    _definition;
    private LspCodeActionProvider?    _codeAction;
    private LspRenameProvider?        _rename;

    // ── ILspClient: lifecycle ──────────────────────────────────────────────────

    public bool IsInitialized { get; private set; }

    /// <summary>Server capability flags — valid after <see cref="InitializeAsync"/> completes.</summary>
    internal ServerCapabilities Capabilities { get; private set; } = ServerCapabilities.Parse(null);

    internal LspClientImpl(
        string  executablePath,
        string? arguments,
        string? workspacePath,
        Dispatcher dispatcher)
    {
        _executablePath = executablePath;
        _arguments      = arguments;
        _workspacePath  = workspacePath;
        _dispatcher     = dispatcher;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _process = new LspProcess();
        await _process.StartAsync(_executablePath, _arguments, _workspacePath, ct)
                       .ConfigureAwait(false);

        var channel = _process.Channel;
        _sync        = new LspDocumentSync(channel);
        _completion  = new LspCompletionProvider(channel);
        _hover       = new LspHoverProvider(channel);
        _definition  = new LspDefinitionProvider(channel);
        _codeAction  = new LspCodeActionProvider(channel);
        _rename      = new LspRenameProvider(channel);

        // Subscribe to push diagnostics.
        channel.NotificationReceived += OnNotificationReceived;

        Capabilities  = _process.Capabilities;
        IsInitialized = true;
    }

    // ── ILspClient: document sync ──────────────────────────────────────────────

    public void OpenDocument(string filePath, string languageId, string text)
        => _ = _sync?.DidOpenAsync(filePath, languageId, text, CancellationToken.None);

    public void DidChange(string filePath, int version, string newText)
        => _ = _sync?.DidChangeAsync(filePath, version, newText, CancellationToken.None);

    public void CloseDocument(string filePath)
        => _ = _sync?.DidCloseAsync(filePath, CancellationToken.None);

    // ── ILspClient: completions ────────────────────────────────────────────────

    public Task<IReadOnlyList<LspCompletionItem>> CompletionAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        if (_completion is null || !Capabilities.HasCompletionProvider)
            return Task.FromResult<IReadOnlyList<LspCompletionItem>>(Array.Empty<LspCompletionItem>());
        return _completion.GetAsync(filePath, line, column, ct);
    }

    // ── ILspClient: hover ──────────────────────────────────────────────────────

    public Task<LspHoverResult?> HoverAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        if (_hover is null || !Capabilities.HasHoverProvider)
            return Task.FromResult<LspHoverResult?>(null);
        return _hover.GetAsync(filePath, line, column, ct);
    }

    // ── ILspClient: definition / references ───────────────────────────────────

    public Task<IReadOnlyList<LspLocation>> DefinitionAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        if (_definition is null || !Capabilities.HasDefinitionProvider)
            return Task.FromResult<IReadOnlyList<LspLocation>>(Array.Empty<LspLocation>());
        return _definition.GetDefinitionAsync(filePath, line, column, ct);
    }

    public Task<IReadOnlyList<LspLocation>> ReferencesAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        if (_definition is null || !Capabilities.HasReferencesProvider)
            return Task.FromResult<IReadOnlyList<LspLocation>>(Array.Empty<LspLocation>());
        return _definition.GetReferencesAsync(filePath, line, column, ct);
    }

    // ── ILspClient: signature help ─────────────────────────────────────────────

    public async Task<string?> SignatureHelpAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        if (_process is null || !Capabilities.HasSignatureHelpProvider) return null;

        var uri    = LspDocumentSync.ToUri(filePath);
        var @params = new { textDocument = new { uri }, position = new { line, character = column } };

        JsonNode? result = await _process.Channel
            .CallAsync("textDocument/signatureHelp", @params, ct)
            .ConfigureAwait(false);

        // Return the label of the first active signature, or null.
        return result?["signatures"]?[0]?["label"]?.GetValue<string>();
    }

    // ── ILspClient: code actions ──────────────────────────────────────────────

    public Task<IReadOnlyList<LspCodeAction>> CodeActionAsync(
        string filePath, int startLine, int startColumn, int endLine, int endColumn,
        CancellationToken ct = default)
    {
        if (_codeAction is null || !Capabilities.HasCodeActionProvider)
            return Task.FromResult<IReadOnlyList<LspCodeAction>>(Array.Empty<LspCodeAction>());
        return _codeAction.GetAsync(filePath, startLine, startColumn, endLine, endColumn, ct);
    }

    // ── ILspClient: rename ────────────────────────────────────────────────────

    public Task<LspWorkspaceEdit?> RenameAsync(
        string filePath, int line, int column, string newName, CancellationToken ct = default)
    {
        if (_rename is null || !Capabilities.HasRenameProvider)
            return Task.FromResult<LspWorkspaceEdit?>(null);
        return _rename.GetAsync(filePath, line, column, newName, ct);
    }

    // ── ILspClient: document symbols ─────────────────────────────────────────

    public Task<IReadOnlyList<LspDocumentSymbol>> DocumentSymbolsAsync(
        string filePath, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LspDocumentSymbol>>(Array.Empty<LspDocumentSymbol>());

    // ── ILspClient: workspace symbols ────────────────────────────────────────

    public Task<IReadOnlyList<LspWorkspaceSymbol>> WorkspaceSymbolsAsync(
        string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LspWorkspaceSymbol>>(Array.Empty<LspWorkspaceSymbol>());

    // ── ILspClient: inlay hints ───────────────────────────────────────────────

    public Task<IReadOnlyList<LspInlayHint>> InlayHintsAsync(
        string filePath, int startLine, int endLine, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LspInlayHint>>(Array.Empty<LspInlayHint>());

    // ── ILspClient: semantic tokens ───────────────────────────────────────────

    public Task<LspSemanticTokensResult?> SemanticTokensAsync(
        string filePath, CancellationToken ct = default)
        => Task.FromResult<LspSemanticTokensResult?>(null);

    // ── ILspClient: diagnostics push ──────────────────────────────────────────

    public event EventHandler<LspDiagnosticsReceivedEventArgs>? DiagnosticsReceived;

    private void OnNotificationReceived(string method, JsonNode? p)
    {
        if (method != "textDocument/publishDiagnostics" || p is null) return;

        var uri  = p["uri"]?.GetValue<string>() ?? string.Empty;
        var list = p["diagnostics"]?.AsArray();
        if (list is null) return;

        var diagnostics = new List<LspDiagnostic>(list.Count);
        foreach (var d in list)
        {
            if (d is null) continue;
            var range    = d["range"];
            var start    = range?["start"];
            var end      = range?["end"];
            var severity = d["severity"]?.GetValue<int>() ?? 1;

            diagnostics.Add(new LspDiagnostic
            {
                StartLine   = start?["line"]?.GetValue<int>()      ?? 0,
                StartColumn = start?["character"]?.GetValue<int>() ?? 0,
                EndLine     = end?["line"]?.GetValue<int>()        ?? 0,
                EndColumn   = end?["character"]?.GetValue<int>()   ?? 0,
                Message     = d["message"]?.GetValue<string>()     ?? string.Empty,
                Severity    = SeverityToString(severity),
                Code        = d["code"]?.ToString(),
            });
        }

        var args = new LspDiagnosticsReceivedEventArgs
        {
            DocumentUri = uri,
            Diagnostics = diagnostics,
        };

        // Always raise on the UI thread.
        _dispatcher.InvokeAsync(() => DiagnosticsReceived?.Invoke(this, args));
    }

    private static string SeverityToString(int s) => s switch
    {
        1 => "error",
        2 => "warning",
        3 => "information",
        _ => "hint",
    };

    // ── IAsyncDisposable ───────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_process is not null)
        {
            await _process.ShutdownAsync().ConfigureAwait(false);
            await _process.DisposeAsync().ConfigureAwait(false);
        }
        IsInitialized = false;
    }
}
