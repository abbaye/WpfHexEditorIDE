// ==========================================================
// Project: WpfHexEditor.Core.Roslyn
// File: RoslynLanguageClient.cs
// Contributors: Claude Opus 4.6
// Created: 2026-04-01
// Description:
//     In-process ILspClient implementation backed by Roslyn APIs.
//     Replaces OmniSharp for C# and VB.NET — zero external process.
//     All LSP DTO mapping is internal; consumers see only ILspClient.
//
// Architecture Notes:
//     Adapter Pattern — wraps RoslynWorkspaceManager + provider classes
//     behind the existing ILspClient contract. Thread-safe via immutable
//     Roslyn Solution snapshots. Background analysis fires DiagnosticsReceived
//     on the WPF dispatcher thread.
// ==========================================================

using System.Windows.Threading;
using WpfHexEditor.Core.Roslyn.Providers;
using WpfHexEditor.Core.Roslyn.Services;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Core.Roslyn;

/// <summary>
/// In-process Roslyn-based language client for C# and VB.NET.
/// Implements <see cref="ILspClient"/> — drop-in replacement for OmniSharp.
/// Also implements <see cref="IReferenceCountProvider"/> so <c>InlineHintsService</c>
/// can use semantic reference counts for C#/VB.NET files.
/// </summary>
public sealed class RoslynLanguageClient : ILspClient, IReferenceCountProvider, IInlineHintsOptionsClient, IDiagnosticsModeClient
{
    private readonly Dispatcher _dispatcher;
    private readonly RoslynWorkspaceManager _workspace;
    private readonly BackgroundAnalysisService _analysisService;
    private readonly MetadataAsSourceCache _metadataCache = new();
    private readonly RoslynReferenceCountProvider _refCountProvider;
    private string? _lastCompletionFilePath;
    private bool _initialized;
    private bool _fullyLoaded;

    // Suppresses diagnostics until the solution/project is loaded.
    // Pre-solution batches contain unresolved-namespace garbage — discard them.
    // Set to true by LoadSolutionAsync/LoadProjectsAsync, or after standalone analysis starts.
    private bool _solutionReady;
    private DispatcherTimer? _standaloneFallbackTimer;

    // InlineHints sub-options (configurable from outside)
    private bool _showVarTypeHints = true;
    private bool _showLambdaReturnTypeHints = true;

    /// <summary>Sets var-type and lambda-return InlineHints options.</summary>
    public void SetInlineHintsOptions(bool showVarTypeHints, bool showLambdaReturnTypeHints)
    {
        _showVarTypeHints = showVarTypeHints;
        _showLambdaReturnTypeHints = showLambdaReturnTypeHints;
    }

    public RoslynLanguageClient(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _workspace = new RoslynWorkspaceManager();
        _analysisService = new BackgroundAnalysisService(_workspace, dispatcher);
        _analysisService.DiagnosticsReady += (s, e) =>
        {
            // Suppress diagnostics until the solution/project context is loaded.
            // Pre-solution batches are garbage (CS0246 unresolved namespaces, etc.).
            if (!_solutionReady) return;

            if (!_fullyLoaded)
            {
                _fullyLoaded = true;
                FullyLoaded?.Invoke();
            }
            DiagnosticsReceived?.Invoke(this, e);
        };
        _refCountProvider = new RoslynReferenceCountProvider(_workspace);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    public bool IsInitialized => _initialized;

    /// <inheritdoc/>
    public bool IsFullyLoaded => _fullyLoaded;

    /// <inheritdoc cref="IDiagnosticsModeClient.UsesPullDiagnostics"/>
    /// Roslyn uses push-mode diagnostics via <see cref="BackgroundAnalysisService.DiagnosticsReady"/>.
    public bool UsesPullDiagnostics => false;

    /// <inheritdoc/>
    public event Action? FullyLoaded;

    /// <summary>Number of projects in the loaded MSBuild solution (0 if standalone).</summary>
    public int LoadedProjectCount => _workspace.CurrentSolution.ProjectIds.Count;

    public Task InitializeAsync(CancellationToken ct = default)
    {
        _initialized = true;

        // Standalone fallback: allow diagnostics after 8s if no solution/project is loaded.
        // Covers loose .cs files opened without a .sln context.
        // Cancelled immediately by LoadSolutionAsync/LoadProjectsAsync when a solution context arrives.
        _standaloneFallbackTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(8)
        };
        _standaloneFallbackTimer.Tick += (_, _) =>
        {
            _standaloneFallbackTimer.Stop();
            _standaloneFallbackTimer = null;
            _solutionReady = true;
        };
        _standaloneFallbackTimer.Start();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads a .sln or .csproj into the Roslyn workspace for full project-graph analysis.
    /// Called from MainWindow when the IDE solution is opened.
    /// </summary>
    public async Task LoadSolutionAsync(string solutionPath, CancellationToken ct = default)
    {
        // Cancel standalone fallback — a solution context is coming, don't allow pre-solution diagnostics.
        _dispatcher.Invoke(() => { _standaloneFallbackTimer?.Stop(); _standaloneFallbackTimer = null; });
        await _workspace.LoadSolutionAsync(solutionPath, ct).ConfigureAwait(false);
        _solutionReady = true;
        // Re-analyze all open documents with the new project context.
        foreach (var filePath in _workspace.OpenDocumentPaths)
            _analysisService.NotifyChanged(filePath);
    }

    /// <summary>
    /// Loads individual .csproj / .vbproj files into Roslyn.
    /// Format-agnostic alternative to <see cref="LoadSolutionAsync"/>: works for any IDE
    /// solution format (.sln, .slnx, .whsln, …) because it is driven by project paths
    /// extracted from the already-loaded <c>ISolution</c>, not from the solution file.
    /// </summary>
    public async Task LoadProjectsAsync(IEnumerable<string> projectPaths, CancellationToken ct = default)
    {
        _dispatcher.Invoke(() => { _standaloneFallbackTimer?.Stop(); _standaloneFallbackTimer = null; });
        await _workspace.LoadProjectsAsync(projectPaths, ct).ConfigureAwait(false);
        _solutionReady = true;
        foreach (var filePath in _workspace.OpenDocumentPaths)
            _analysisService.NotifyChanged(filePath);
    }

    /// <summary>Unloads the MSBuild solution, reverting to standalone file analysis.</summary>
    public void UnloadSolution()
    {
        _workspace.UnloadSolution();
    }

    public ValueTask DisposeAsync()
    {
        _analysisService.Dispose();
        _workspace.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Document Synchronization ──────────────────────────────────────────────

    public void OpenDocument(string filePath, string languageId, string text)
    {
        _workspace.OpenDocument(filePath, languageId, text);
        _analysisService.NotifyChanged(filePath);
    }

    public void DidChange(string filePath, int version, string newText)
    {
        _workspace.UpdateDocument(filePath, newText);
        _analysisService.NotifyChanged(filePath);
    }

    // Roslyn manages its own in-process workspace; incremental range sync is not
    // applicable — the workspace is always updated via the full-text DidChange path.
    // This method intentionally does nothing; callers check IIncrementalSyncClient
    // before choosing the incremental code path.
    public void DidChangeIncremental(string filePath, int version,
        int startLine, int startCol, int endLine, int endCol,
        int rangeLength, string newText) { }

    public void CloseDocument(string filePath) => _workspace.CloseDocument(filePath);

    public void SaveDocument(string filePath, string? text = null)
    {
        if (text is not null)
            _workspace.UpdateDocument(filePath, text);
    }

    // ── Completions ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<LspCompletionItem>> CompletionAsync(
        string filePath, int line, int column, char? triggerChar = null, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        _lastCompletionFilePath = filePath;
        return await RoslynCompletionProvider.GetCompletionsAsync(doc, line, column, triggerChar, ct)
            .ConfigureAwait(false);
    }

    public async Task<LspCompletionItem?> ResolveCompletionItemAsync(
        LspCompletionItem item, CancellationToken ct = default)
    {
        if (_lastCompletionFilePath is null) return item;
        var doc = _workspace.GetDocument(_lastCompletionFilePath);
        if (doc is null) return item;
        return await RoslynCompletionProvider.ResolveAsync(doc, item, ct).ConfigureAwait(false);
    }

    // ── Hover ─────────────────────────────────────────────────────────────────

    public async Task<LspHoverResult?> HoverAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return null;
        return await RoslynHoverProvider.GetHoverAsync(doc, line, column, ct).ConfigureAwait(false);
    }

    // ── Definition / References ───────────────────────────────────────────────

    public async Task<IReadOnlyList<LspLocation>> DefinitionAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        return await RoslynNavigationProvider.GetDefinitionAsync(doc, line, column, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LspLocation>> ReferencesAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        return await RoslynNavigationProvider.GetReferencesAsync(doc, line, column, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LspLocation>> ImplementationAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        return await RoslynNavigationProvider.GetImplementationAsync(doc, line, column, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LspLocation>> TypeDefinitionAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        return await RoslynNavigationProvider.GetTypeDefinitionAsync(doc, line, column, ct)
            .ConfigureAwait(false);
    }

    // ── Signature Help ────────────────────────────────────────────────────────

    public async Task<LspSignatureHelpResult?> SignatureHelpAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return null;
        return await RoslynSignatureHelpProvider.GetSignatureHelpAsync(doc, line, column, ct)
            .ConfigureAwait(false);
    }

    // ── Code Actions ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<LspCodeAction>> CodeActionAsync(
        string filePath,
        int startLine, int startColumn,
        int endLine, int endColumn,
        CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        return await RoslynCodeActionProvider.GetCodeActionsAsync(
            doc, startLine, startColumn, endLine, endColumn, ct).ConfigureAwait(false);
    }

    // ── Rename ────────────────────────────────────────────────────────────────

    public async Task<LspWorkspaceEdit?> RenameAsync(
        string filePath, int line, int column, string newName,
        CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return null;
        return await RoslynRenameProvider.RenameAsync(doc, line, column, newName, ct)
            .ConfigureAwait(false);
    }

    // ── Document Symbols ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<LspDocumentSymbol>> DocumentSymbolsAsync(
        string filePath, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        return await RoslynSymbolProvider.GetDocumentSymbolsAsync(doc, ct).ConfigureAwait(false);
    }

    // ── Workspace Symbols ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<LspWorkspaceSymbol>> WorkspaceSymbolsAsync(
        string query, CancellationToken ct = default)
    {
        return await RoslynSymbolProvider.GetWorkspaceSymbolsAsync(
            _workspace.CurrentSolution, query, ct).ConfigureAwait(false);
    }

    // ── Inlay Hints ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<LspInlayHint>> InlayHintsAsync(
        string filePath, int startLine, int endLine, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        return await RoslynInlayHintsProvider.GetInlayHintsAsync(
                doc, startLine, endLine, ct,
                showVarTypeHints: _showVarTypeHints,
                showLambdaReturnTypeHints: _showLambdaReturnTypeHints)
            .ConfigureAwait(false);
    }

    // ── Semantic Tokens ───────────────────────────────────────────────────────

    public async Task<LspSemanticTokensResult?> SemanticTokensAsync(
        string filePath, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return null;
        return await RoslynSemanticTokensProvider.GetTokensAsync(doc, ct).ConfigureAwait(false);
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<LspTextEdit>> FormattingAsync(
        string filePath, int tabSize, bool insertSpaces, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        return await RoslynFormattingProvider.FormatDocumentAsync(doc, tabSize, insertSpaces, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LspTextEdit>> RangeFormattingAsync(
        string filePath,
        int startLine, int startColumn,
        int endLine, int endColumn,
        int tabSize, bool insertSpaces,
        CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        return await RoslynFormattingProvider.FormatRangeAsync(
            doc, startLine, startColumn, endLine, endColumn, tabSize, insertSpaces, ct)
            .ConfigureAwait(false);
    }

    // ── Call Hierarchy ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<LspCallHierarchyItem>> PrepareCallHierarchyAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        return await RoslynHierarchyProvider.PrepareCallHierarchyAsync(doc, line, column, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LspIncomingCall>> GetIncomingCallsAsync(
        LspCallHierarchyItem item, CancellationToken ct = default)
    {
        return await RoslynHierarchyProvider.GetIncomingCallsAsync(
            _workspace.CurrentSolution, item, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LspOutgoingCall>> GetOutgoingCallsAsync(
        LspCallHierarchyItem item, CancellationToken ct = default)
    {
        return await RoslynHierarchyProvider.GetOutgoingCallsAsync(
            _workspace.CurrentSolution, item, ct).ConfigureAwait(false);
    }

    // ── Type Hierarchy ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<LspTypeHierarchyItem>> PrepareTypeHierarchyAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        var doc = _workspace.GetDocument(filePath);
        if (doc is null) return [];
        return await RoslynHierarchyProvider.PrepareTypeHierarchyAsync(doc, line, column, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LspTypeHierarchyItem>> GetSupertypesAsync(
        LspTypeHierarchyItem item, CancellationToken ct = default)
    {
        return await RoslynHierarchyProvider.GetSupertypesAsync(
            _workspace.CurrentSolution, item, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LspTypeHierarchyItem>> GetSubtypesAsync(
        LspTypeHierarchyItem item, CancellationToken ct = default)
    {
        return await RoslynHierarchyProvider.GetSubtypesAsync(
            _workspace.CurrentSolution, item, ct).ConfigureAwait(false);
    }

    // ── Linked Editing Ranges ─────────────────────────────────────────────────

    public Task<IReadOnlyList<LspLinkedRange>> LinkedEditingRangesAsync(
        string filePath, int line, int column, CancellationToken ct = default)
    {
        // Not applicable for C#/VB.NET (used for HTML/XML tag renaming).
        return Task.FromResult<IReadOnlyList<LspLinkedRange>>([]);
    }

    // ── Diagnostics Push ──────────────────────────────────────────────────────

    public event EventHandler<LspDiagnosticsReceivedEventArgs>? DiagnosticsReceived;

    // ── IReferenceCountProvider ───────────────────────────────────────────────

    public bool CanProvide(string filePath)
        => _refCountProvider.CanProvide(filePath);

    public Task<int?> CountReferencesAsync(string filePath, int declarationLine, string symbolName, CancellationToken ct)
        => _refCountProvider.CountReferencesAsync(filePath, declarationLine, symbolName, ct);
}
