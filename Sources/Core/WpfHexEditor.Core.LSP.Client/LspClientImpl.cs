// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
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
using WpfHexEditor.Core.LSP.Client.Providers;
using WpfHexEditor.Core.LSP.Client.Services;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client;

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
    private LspCompletionProvider?        _completion;
    private LspHoverProvider?             _hover;
    private LspDefinitionProvider?        _definition;
    private LspCodeActionProvider?        _codeAction;
    private LspSignatureHelpProvider?     _signatureHelp;
    private LspRenameProvider?            _rename;
    private LspLinkedEditingProvider?     _linkedEditing;
    private LspCallHierarchyProvider?     _callHierarchy;
    private LspTypeHierarchyProvider?     _typeHierarchy;
    private LspDocumentSymbolProvider?    _symbolProvider;
    private LspInlayHintsProvider?        _inlayHints;
    private LspSemanticTokensProvider?    _semanticTokens;
    private LspWorkspaceSymbolsProvider?  _workspaceSymbols;

    // Pull-diagnostics state (LSP 3.18) — used when server advertises diagnosticProvider.
    private System.Windows.Threading.DispatcherTimer? _pullDiagTimer;
    private string?                                    _pendingPullUri;    // URI of last changed doc

    // ── ILspClient: lifecycle ──────────────────────────────────────────────────

    public bool IsInitialized { get; private set; }

    /// <summary>Optional diagnostic logger injected by the host.</summary>
    public Action<string>? Logger { get; set; }

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
        _completion    = new LspCompletionProvider(channel);
        _hover         = new LspHoverProvider(channel);
        _definition    = new LspDefinitionProvider(channel);
        _codeAction     = new LspCodeActionProvider(channel);
        _signatureHelp  = new LspSignatureHelpProvider(channel);
        _rename         = new LspRenameProvider(channel);
        _linkedEditing = new LspLinkedEditingProvider(channel);
        _callHierarchy  = new LspCallHierarchyProvider(channel);
        _typeHierarchy  = new LspTypeHierarchyProvider(channel);
        _symbolProvider = new LspDocumentSymbolProvider(channel) { _log = Logger };

        Capabilities  = _process.Capabilities;

        _inlayHints      = new LspInlayHintsProvider(channel);
        _semanticTokens  = new LspSemanticTokensProvider(
            channel,
            Capabilities.SemanticTokenTypesLegend,
            Capabilities.SemanticTokenModifiersLegend);
        _workspaceSymbols = new LspWorkspaceSymbolsProvider(channel);

        if (Capabilities.HasDiagnosticProvider)
        {
            // Pull mode (LSP 3.18): server will NOT push publishDiagnostics.
            // Register a callback so DidChange restarts the debounce timer.
            _sync.OnDocumentChanged = uri =>
            {
                _pendingPullUri = uri;
                _dispatcher.BeginInvoke(() =>
                {
                    _pullDiagTimer?.Stop();
                    _pullDiagTimer?.Start();
                });
            };

            // 1-second debounce timer; pulls diagnostics for the last changed URI.
            _pullDiagTimer = new System.Windows.Threading.DispatcherTimer(
                System.TimeSpan.FromMilliseconds(1000),
                System.Windows.Threading.DispatcherPriority.Background,
                async (_, _) =>
                {
                    _pullDiagTimer!.Stop();
                    if (_pendingPullUri is not null)
                        await PullDiagnosticsInternalAsync(_pendingPullUri).ConfigureAwait(false);
                },
                _dispatcher);
            _pullDiagTimer.IsEnabled = false;
        }
        else
        {
            // Push mode: subscribe to textDocument/publishDiagnostics notifications.
            channel.NotificationReceived += OnNotificationReceived;
        }

        IsInitialized = true;
    }

    // ── ILspClient: document sync ──────────────────────────────────────────────

    public void OpenDocument(string filePath, string languageId, string text)
        => _ = _sync?.DidOpenAsync(filePath, languageId, text, CancellationToken.None);

    public void DidChange(string filePath, int version, string newText)
        => _ = _sync?.DidChangeAsync(filePath, version, newText, CancellationToken.None);

    public void CloseDocument(string filePath)
        => _ = _sync?.DidCloseAsync(filePath, CancellationToken.None);

    public void SaveDocument(string filePath, string? text = null)
        => _ = _sync?.DidSaveAsync(filePath, text, CancellationToken.None);

    // ── ILspClient: completions ────────────────────────────────────────────────

    public Task<IReadOnlyList<LspCompletionItem>> CompletionAsync(
        string filePath, int line, int column, char? triggerChar = null, CancellationToken ct = default)
    {
        if (_completion is null || !Capabilities.HasCompletionProvider)
            return Task.FromResult<IReadOnlyList<LspCompletionItem>>(Array.Empty<LspCompletionItem>());
        return _completion.GetAsync(filePath, line, column, triggerChar, ct);
    }

    public Task<LspCompletionItem?> ResolveCompletionItemAsync(
        LspCompletionItem item, CancellationToken ct = default)
    {
        if (_completion is null || !Capabilities.HasCompletionProvider || item.RawJson is null)
            return Task.FromResult<LspCompletionItem?>(null);
        return _completion.ResolveAsync(item, ct);
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

    public Task<IReadOnlyList<LspLocation>> ImplementationAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        if (_definition is null || !Capabilities.HasImplementationProvider)
            return Task.FromResult<IReadOnlyList<LspLocation>>(Array.Empty<LspLocation>());
        return _definition.GetImplementationAsync(filePath, line, column, ct);
    }

    public Task<IReadOnlyList<LspLocation>> TypeDefinitionAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        if (_definition is null || !Capabilities.HasTypeDefinitionProvider)
            return Task.FromResult<IReadOnlyList<LspLocation>>(Array.Empty<LspLocation>());
        return _definition.GetTypeDefinitionAsync(filePath, line, column, ct);
    }

    // ── ILspClient: signature help ─────────────────────────────────────────────

    public Task<LspSignatureHelpResult?> SignatureHelpAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        if (_signatureHelp is null || !Capabilities.HasSignatureHelpProvider)
            return Task.FromResult<LspSignatureHelpResult?>(null);
        return _signatureHelp.GetAsync(filePath, line, column, ct);
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
        => _symbolProvider is not null && IsInitialized
            ? _symbolProvider.GetAsync(filePath, ct)
            : Task.FromResult<IReadOnlyList<LspDocumentSymbol>>(Array.Empty<LspDocumentSymbol>());

    // ── ILspClient: workspace symbols ────────────────────────────────────────

    public Task<IReadOnlyList<LspWorkspaceSymbol>> WorkspaceSymbolsAsync(
        string query, CancellationToken ct = default)
    {
        if (_workspaceSymbols is null || !Capabilities.HasWorkspaceSymbolsProvider)
            return Task.FromResult<IReadOnlyList<LspWorkspaceSymbol>>(Array.Empty<LspWorkspaceSymbol>());
        return _workspaceSymbols.GetAsync(query, ct);
    }

    // ── ILspClient: inlay hints ───────────────────────────────────────────────

    public Task<IReadOnlyList<LspInlayHint>> InlayHintsAsync(
        string filePath, int startLine, int endLine, CancellationToken ct = default)
    {
        if (_inlayHints is null || !Capabilities.HasInlayHintsProvider)
            return Task.FromResult<IReadOnlyList<LspInlayHint>>(Array.Empty<LspInlayHint>());
        return _inlayHints.GetAsync(filePath, startLine, endLine, ct);
    }

    // ── ILspClient: semantic tokens ───────────────────────────────────────────

    public Task<LspSemanticTokensResult?> SemanticTokensAsync(
        string filePath, CancellationToken ct = default)
    {
        if (_semanticTokens is null || !Capabilities.HasSemanticTokensProvider)
            return Task.FromResult<LspSemanticTokensResult?>(null);
        return _semanticTokens.GetAsync(filePath, ct);
    }

    // ── ILspClient: formatting ────────────────────────────────────────────────

    public async Task<IReadOnlyList<LspTextEdit>> FormattingAsync(
        string filePath, int tabSize, bool insertSpaces, CancellationToken ct = default)
    {
        if (_process is null || !Capabilities.HasFormattingProvider)
            return Array.Empty<LspTextEdit>();

        var uri    = LspDocumentSync.ToUri(filePath);
        var @params = new
        {
            textDocument    = new { uri },
            options         = new { tabSize, insertSpaces },
        };

        var result = await _process.Channel
            .CallAsync("textDocument/formatting", @params, ct)
            .ConfigureAwait(false);

        return ParseTextEdits(result);
    }

    public async Task<IReadOnlyList<LspTextEdit>> RangeFormattingAsync(
        string filePath,
        int startLine, int startColumn,
        int endLine,   int endColumn,
        int tabSize,   bool insertSpaces,
        CancellationToken ct = default)
    {
        if (_process is null || !Capabilities.HasRangeFormattingProvider)
            return Array.Empty<LspTextEdit>();

        var uri    = LspDocumentSync.ToUri(filePath);
        var @params = new
        {
            textDocument = new { uri },
            range        = new
            {
                start = new { line = startLine, character = startColumn },
                end   = new { line = endLine,   character = endColumn },
            },
            options = new { tabSize, insertSpaces },
        };

        var result = await _process.Channel
            .CallAsync("textDocument/rangeFormatting", @params, ct)
            .ConfigureAwait(false);

        return ParseTextEdits(result);
    }

    private static IReadOnlyList<LspTextEdit> ParseTextEdits(System.Text.Json.Nodes.JsonNode? node)
    {
        var arr = node?.AsArray();
        if (arr is null || arr.Count == 0) return Array.Empty<LspTextEdit>();

        var edits = new List<LspTextEdit>(arr.Count);
        foreach (var item in arr)
        {
            if (item is null) continue;
            var range = item["range"];
            var start = range?["start"];
            var end   = range?["end"];
            edits.Add(new LspTextEdit
            {
                StartLine   = start?["line"]?.GetValue<int>()      ?? 0,
                StartColumn = start?["character"]?.GetValue<int>() ?? 0,
                EndLine     = end?["line"]?.GetValue<int>()        ?? 0,
                EndColumn   = end?["character"]?.GetValue<int>()   ?? 0,
                NewText     = item["newText"]?.GetValue<string>()  ?? string.Empty,
            });
        }
        return edits;
    }

    // ── ILspClient: linked editing ranges (LSP 3.16) ─────────────────────────

    public Task<IReadOnlyList<LspLinkedRange>> LinkedEditingRangesAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        if (_linkedEditing is null || !Capabilities.HasLinkedEditingRangeProvider)
            return Task.FromResult<IReadOnlyList<LspLinkedRange>>(Array.Empty<LspLinkedRange>());
        return _linkedEditing.GetAsync(filePath, line, column, ct);
    }

    // ── ILspClient: call hierarchy (LSP 3.16) ────────────────────────────────

    public Task<IReadOnlyList<LspCallHierarchyItem>> PrepareCallHierarchyAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        if (_callHierarchy is null || !Capabilities.HasCallHierarchyProvider)
            return Task.FromResult<IReadOnlyList<LspCallHierarchyItem>>(Array.Empty<LspCallHierarchyItem>());
        return _callHierarchy.PrepareAsync(filePath, line, column, ct);
    }

    public Task<IReadOnlyList<LspIncomingCall>> GetIncomingCallsAsync(
        LspCallHierarchyItem item, CancellationToken ct = default)
    {
        if (_callHierarchy is null || !Capabilities.HasCallHierarchyProvider)
            return Task.FromResult<IReadOnlyList<LspIncomingCall>>(Array.Empty<LspIncomingCall>());
        return _callHierarchy.GetIncomingCallsAsync(item, ct);
    }

    public Task<IReadOnlyList<LspOutgoingCall>> GetOutgoingCallsAsync(
        LspCallHierarchyItem item, CancellationToken ct = default)
    {
        if (_callHierarchy is null || !Capabilities.HasCallHierarchyProvider)
            return Task.FromResult<IReadOnlyList<LspOutgoingCall>>(Array.Empty<LspOutgoingCall>());
        return _callHierarchy.GetOutgoingCallsAsync(item, ct);
    }

    // ── ILspClient: type hierarchy (LSP 3.17) ─────────────────────────────────

    public Task<IReadOnlyList<LspTypeHierarchyItem>> PrepareTypeHierarchyAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        if (_typeHierarchy is null || !Capabilities.HasTypeHierarchyProvider)
            return Task.FromResult<IReadOnlyList<LspTypeHierarchyItem>>(Array.Empty<LspTypeHierarchyItem>());
        return _typeHierarchy.PrepareAsync(filePath, line, column, ct);
    }

    public Task<IReadOnlyList<LspTypeHierarchyItem>> GetSupertypesAsync(
        LspTypeHierarchyItem item, CancellationToken ct = default)
    {
        if (_typeHierarchy is null || !Capabilities.HasTypeHierarchyProvider)
            return Task.FromResult<IReadOnlyList<LspTypeHierarchyItem>>(Array.Empty<LspTypeHierarchyItem>());
        return _typeHierarchy.GetSupertypesAsync(item, ct);
    }

    public Task<IReadOnlyList<LspTypeHierarchyItem>> GetSubtypesAsync(
        LspTypeHierarchyItem item, CancellationToken ct = default)
    {
        if (_typeHierarchy is null || !Capabilities.HasTypeHierarchyProvider)
            return Task.FromResult<IReadOnlyList<LspTypeHierarchyItem>>(Array.Empty<LspTypeHierarchyItem>());
        return _typeHierarchy.GetSubtypesAsync(item, ct);
    }

    // ── Pull diagnostics (LSP 3.18) ───────────────────────────────────────────

    /// <summary>
    /// Sends textDocument/diagnostic for <paramref name="uri"/> and fires
    /// <see cref="DiagnosticsReceived"/> with the result.
    /// No-op when the server returns an "unchanged" report.
    /// </summary>
    private async System.Threading.Tasks.Task PullDiagnosticsInternalAsync(string uri)
    {
        if (_process is null) return;

        try
        {
            var @params = new { textDocument = new { uri } };
            var result  = await _process.Channel
                .CallAsync("textDocument/diagnostic", @params, System.Threading.CancellationToken.None)
                .ConfigureAwait(false);

            // Only process "full" reports; "unchanged" means use cached data.
            var kind = result?["kind"]?.GetValue<string>();
            if (!string.Equals(kind, "full", StringComparison.OrdinalIgnoreCase)) return;

            var items = result?["items"]?.AsArray();
            if (items is null) return;

            var diagnostics = new System.Collections.Generic.List<LspDiagnostic>(items.Count);
            foreach (var d in items)
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

            var filePath = LspDocumentSync.FromUri(uri);
            var args     = new LspDiagnosticsReceivedEventArgs
            {
                DocumentUri = filePath,
                Diagnostics = diagnostics,
            };
            _dispatcher.InvokeAsync(() => DiagnosticsReceived?.Invoke(this, args));
        }
        catch { /* Ignore transient errors — next DidChange will trigger a fresh pull */ }
    }

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
        _pullDiagTimer?.Stop();
        _pullDiagTimer = null;

        if (_process is not null)
        {
            await _process.ShutdownAsync().ConfigureAwait(false);
            await _process.DisposeAsync().ConfigureAwait(false);
        }
        IsInitialized = false;
    }
}
