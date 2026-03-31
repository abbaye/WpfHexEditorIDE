// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: LSP/ILspClient.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Public SDK contract for a standard JSON-RPC Language Server Protocol client.
//     Plugins or IDE modules that need LSP completions/diagnostics/hover/go-to-def
//     consume this interface, injected via IIDEHostContext.LspClient.
//
// Architecture Notes:
//     Interface Segregation — only the methods/events relevant to editor integration
//     are exposed. The concrete WpfHexEditor.Core.LSP.Client implementation handles
//     process lifecycle, JSON-RPC framing, and capability negotiation internally.
//
//     All async methods accept a CancellationToken so callers can cancel inflight
//     requests when the document changes or the editor loses focus.
// ==========================================================

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WpfHexEditor.Editor.Core.LSP;

// ── LSP Data Transfer Objects ─────────────────────────────────────────────────

/// <summary>A single completion item returned by the language server.</summary>
public sealed class LspCompletionItem
{
    /// <summary>Text displayed in the completion list.</summary>
    public required string Label { get; init; }

    /// <summary>Optional kind (e.g. "Method", "Field", "Keyword"). May be null.</summary>
    public string? Kind { get; init; }

    /// <summary>Optional detail line shown to the right of the label.</summary>
    public string? Detail { get; init; }

    /// <summary>Text actually inserted when the item is committed. Defaults to <see cref="Label"/>.</summary>
    public string? InsertText { get; init; }

    /// <summary>Markdown documentation for the item.</summary>
    public string? Documentation { get; init; }
}

/// <summary>A diagnostic (error / warning / info / hint) reported by the language server.</summary>
public sealed class LspDiagnostic
{
    public required int StartLine   { get; init; }   // 0-based
    public required int StartColumn { get; init; }   // 0-based
    public required int EndLine     { get; init; }
    public required int EndColumn   { get; init; }
    public required string Message  { get; init; }

    /// <summary>"error", "warning", "information", "hint".</summary>
    public required string Severity { get; init; }

    /// <summary>Optional diagnostic code or rule name.</summary>
    public string? Code { get; init; }
}

/// <summary>Hover information returned by the language server.</summary>
public sealed class LspHoverResult
{
    /// <summary>Markdown-formatted hover content.</summary>
    public required string Contents { get; init; }

    // Range is optional in the spec; omitted for simplicity.
}

/// <summary>A file location returned by definition/references requests.</summary>
public sealed class LspLocation
{
    public required string Uri        { get; init; }
    public required int    StartLine  { get; init; }
    public required int    StartColumn{ get; init; }
}

/// <summary>Event args carrying diagnostics received from the language server for a document.</summary>
public sealed class LspDiagnosticsReceivedEventArgs : EventArgs
{
    public required string                  DocumentUri { get; init; }
    public required IReadOnlyList<LspDiagnostic> Diagnostics { get; init; }
}

/// <summary>A single text replacement within a document (0-based coordinates).</summary>
public sealed class LspTextEdit
{
    public required int    StartLine   { get; init; }
    public required int    StartColumn { get; init; }
    public required int    EndLine     { get; init; }
    public required int    EndColumn   { get; init; }
    public required string NewText     { get; init; }
}

/// <summary>
/// A workspace-wide set of text edits keyed by file path.
/// Apply edits bottom-up (reverse line order) within each file to avoid offset drift.
/// </summary>
public sealed class LspWorkspaceEdit
{
    public required IReadOnlyDictionary<string, IReadOnlyList<LspTextEdit>> Changes { get; init; }
}

/// <summary>A code action (quick fix or refactoring) returned by the language server.</summary>
public sealed class LspCodeAction
{
    public required string   Title       { get; init; }
    /// <summary>"quickfix", "refactor.rewrite", etc. May be null.</summary>
    public          string?  Kind        { get; init; }
    public          bool     IsPreferred { get; init; }
    /// <summary>Null for Command-only actions (server-side execution — not supported).</summary>
    public LspWorkspaceEdit? Edit        { get; init; }
}

/// <summary>A symbol within a document (used for breadcrumb / outline).</summary>
public sealed class LspDocumentSymbol
{
    public required string Name        { get; init; }
    /// <summary>Kind string e.g. "class", "method", "namespace".</summary>
    public required string Kind        { get; init; }
    public required int    StartLine   { get; init; }
    public required int    StartColumn { get; init; }
    public required int    EndLine     { get; init; }
    public required int    EndColumn   { get; init; }
    public          string? ContainerName { get; init; }
}

/// <summary>A workspace-level symbol returned by workspace/symbol.</summary>
public sealed class LspWorkspaceSymbol
{
    public required string Name       { get; init; }
    public required string Kind       { get; init; }
    public required string Uri        { get; init; }
    public required int    StartLine  { get; init; }
    public required int    StartColumn{ get; init; }
    public          string? ContainerName { get; init; }
}

/// <summary>A single inlay hint (parameter name label) from textDocument/inlayHint.</summary>
public sealed class LspInlayHint
{
    public required int    Line       { get; init; }   // 0-based
    public required int    Column     { get; init; }   // 0-based
    public required string Label      { get; init; }
    /// <summary>"type" or "parameter".</summary>
    public required string Kind       { get; init; }
}

/// <summary>Decoded semantic tokens for a document.</summary>
public sealed class LspSemanticTokensResult
{
    /// <summary>
    /// Flat list of decoded tokens. Each token gives the line, column, length,
    /// token-type string, and optional modifiers.
    /// </summary>
    public required IReadOnlyList<LspSemanticToken> Tokens { get; init; }
}

/// <summary>A single decoded semantic token.</summary>
public sealed class LspSemanticToken
{
    public required int    Line       { get; init; }   // 0-based
    public required int    Column     { get; init; }
    public required int    Length     { get; init; }
    /// <summary>e.g. "type", "method", "variable", "parameter", "namespace", "keyword", "string", "number".</summary>
    public required string TokenType  { get; init; }
    /// <summary>Modifier flags e.g. "static", "readonly", "deprecated".</summary>
    public required IReadOnlyList<string> Modifiers { get; init; }
}

/// <summary>
/// A single item in a call hierarchy (e.g. a method, function, or constructor).
/// Returned by textDocument/prepareCallHierarchy and used as the key for
/// callHierarchy/incomingCalls and callHierarchy/outgoingCalls.
/// </summary>
public sealed class LspCallHierarchyItem
{
    public required string Name       { get; init; }
    /// <summary>LSP SymbolKind as string e.g. "method", "function", "class".</summary>
    public required string Kind       { get; init; }
    public required string Uri        { get; init; }   // file:/// URI → convert via LspDocumentSync.FromUri
    public required int    StartLine  { get; init; }   // 0-based
    public required int    StartColumn{ get; init; }
    public          string? ContainerName { get; init; }
}

/// <summary>
/// One incoming call — the location that calls into a <see cref="LspCallHierarchyItem"/>.
/// Returned by callHierarchy/incomingCalls.
/// </summary>
public sealed class LspIncomingCall
{
    public required LspCallHierarchyItem From       { get; init; }
    /// <summary>Call-site ranges within <see cref="From"/>; may be empty.</summary>
    public required IReadOnlyList<(int StartLine, int StartColumn, int EndLine, int EndColumn)> FromRanges { get; init; }
}

/// <summary>
/// One outgoing call — a callee invoked from inside a <see cref="LspCallHierarchyItem"/>.
/// Returned by callHierarchy/outgoingCalls.
/// </summary>
public sealed class LspOutgoingCall
{
    public required LspCallHierarchyItem To         { get; init; }
    public required IReadOnlyList<(int StartLine, int StartColumn, int EndLine, int EndColumn)> FromRanges { get; init; }
}

/// <summary>
/// A single item in a type hierarchy (class, interface, struct).
/// Returned by textDocument/prepareTypeHierarchy and used as the key for
/// typeHierarchy/supertypes and typeHierarchy/subtypes.
/// </summary>
public sealed class LspTypeHierarchyItem
{
    public required string Name       { get; init; }
    public required string Kind       { get; init; }
    public required string Uri        { get; init; }
    public required int    StartLine  { get; init; }
    public required int    StartColumn{ get; init; }
    public          string? ContainerName { get; init; }
}

/// <summary>
/// A range within a document that participates in a linked editing session.
/// All ranges in the list contain the same text; editing one synchronises the others.
/// Coordinates are 0-based (LSP convention).
/// </summary>
public sealed class LspLinkedRange
{
    public required int StartLine   { get; init; }
    public required int StartColumn { get; init; }
    public required int EndLine     { get; init; }
    public required int EndColumn   { get; init; }
}

// ── ILspClient interface ───────────────────────────────────────────────────────

/// <summary>
/// Abstraction over a JSON-RPC Language Server Protocol client process.
/// Obtain a server-specific instance from <see cref="ILspServerRegistry"/>;
/// the active client for the current document is exposed via
/// <c>IIDEHostContext.LspClient</c> (may be null when no server is configured
/// for the current language).
/// </summary>
public interface ILspClient : IAsyncDisposable
{
    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the language server process and performs the LSP initialize/initialized
    /// handshake. Must be awaited before calling any other method.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Whether <see cref="InitializeAsync"/> has completed successfully.</summary>
    bool IsInitialized { get; }

    // ── Document Synchronization ──────────────────────────────────────────────

    /// <summary>Sends textDocument/didOpen.</summary>
    void OpenDocument(string filePath, string languageId, string text);

    /// <summary>
    /// Sends textDocument/didChange with a full-text replacement.
    /// Callers are responsible for debouncing (recommended ≥ 300 ms).
    /// </summary>
    void DidChange(string filePath, int version, string newText);

    /// <summary>Sends textDocument/didClose.</summary>
    void CloseDocument(string filePath);

    // ── Completions ───────────────────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/completion at the given (0-based) caret position.
    /// Returns an empty list when completions are unavailable or the server does
    /// not support them.
    /// </summary>
    Task<IReadOnlyList<LspCompletionItem>> CompletionAsync(
        string filePath, int line, int column, CancellationToken ct = default);

    // ── Hover ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/hover. Returns null when nothing is available.
    /// </summary>
    Task<LspHoverResult?> HoverAsync(
        string filePath, int line, int column, CancellationToken ct = default);

    // ── Definition / References ───────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/definition. Returns an empty list when the server
    /// does not support go-to-definition or no definition is found.
    /// </summary>
    Task<IReadOnlyList<LspLocation>> DefinitionAsync(
        string filePath, int line, int column, CancellationToken ct = default);

    /// <summary>
    /// Requests textDocument/references.
    /// </summary>
    Task<IReadOnlyList<LspLocation>> ReferencesAsync(
        string filePath, int line, int column, CancellationToken ct = default);

    /// <summary>
    /// Requests textDocument/implementation. Returns an empty list when the server
    /// does not support it or no implementations are found.
    /// </summary>
    Task<IReadOnlyList<LspLocation>> ImplementationAsync(
        string filePath, int line, int column, CancellationToken ct = default);

    /// <summary>
    /// Requests textDocument/typeDefinition. Returns an empty list when the server
    /// does not support it or no type definition is found.
    /// </summary>
    Task<IReadOnlyList<LspLocation>> TypeDefinitionAsync(
        string filePath, int line, int column, CancellationToken ct = default);

    // ── Signature Help ────────────────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/signatureHelp (called after '(').
    /// Returns null when the server does not support it or no signature matches.
    /// </summary>
    Task<string?> SignatureHelpAsync(
        string filePath, int line, int column, CancellationToken ct = default);

    // ── Code Actions ──────────────────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/codeAction at the given (0-based) range.
    /// Returns an empty list when actions are unavailable or the server does not support them.
    /// </summary>
    Task<IReadOnlyList<LspCodeAction>> CodeActionAsync(
        string filePath,
        int startLine, int startColumn,
        int endLine,   int endColumn,
        CancellationToken ct = default);

    // ── Rename ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/rename. Returns null when rename is unavailable or rejected.
    /// </summary>
    Task<LspWorkspaceEdit?> RenameAsync(
        string filePath, int line, int column, string newName,
        CancellationToken ct = default);

    // ── Document Symbols (breadcrumb / outline) ───────────────────────────────

    /// <summary>
    /// Requests textDocument/documentSymbol to get the symbol tree for the document.
    /// Returns a flat list ordered by appearance. Empty list when not supported.
    /// </summary>
    Task<IReadOnlyList<LspDocumentSymbol>> DocumentSymbolsAsync(
        string filePath, CancellationToken ct = default);

    // ── Workspace Symbols (Ctrl+T) ────────────────────────────────────────────

    /// <summary>
    /// Requests workspace/symbol. Returns an empty list when not supported.
    /// </summary>
    Task<IReadOnlyList<LspWorkspaceSymbol>> WorkspaceSymbolsAsync(
        string query, CancellationToken ct = default);

    // ── Inlay Hints ───────────────────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/inlayHint for a visible line range (0-based).
    /// Returns empty when the server does not support inlay hints (LSP 3.17+).
    /// </summary>
    Task<IReadOnlyList<LspInlayHint>> InlayHintsAsync(
        string filePath,
        int startLine, int endLine,
        CancellationToken ct = default);

    // ── Semantic Tokens ───────────────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/semanticTokens/full.
    /// Returns null when the server does not support semantic tokens.
    /// </summary>
    Task<LspSemanticTokensResult?> SemanticTokensAsync(
        string filePath, CancellationToken ct = default);

    // ── Formatting ────────────────────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/formatting for the whole document.
    /// Returns an empty list when the server does not support formatting or
    /// no changes are needed.  Apply edits bottom-up to avoid offset drift.
    /// </summary>
    Task<IReadOnlyList<LspTextEdit>> FormattingAsync(
        string filePath, int tabSize, bool insertSpaces,
        CancellationToken ct = default);

    /// <summary>
    /// Requests textDocument/rangeFormatting for a line range (0-based, inclusive).
    /// Returns an empty list when range formatting is unsupported or no changes are needed.
    /// </summary>
    Task<IReadOnlyList<LspTextEdit>> RangeFormattingAsync(
        string filePath,
        int startLine, int startColumn,
        int endLine,   int endColumn,
        int tabSize,   bool insertSpaces,
        CancellationToken ct = default);

    // ── Call Hierarchy (LSP 3.16) ────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/prepareCallHierarchy at the given caret position.
    /// Returns the call hierarchy items for the symbol under the cursor.
    /// Returns an empty list when the server does not support call hierarchy
    /// or the caret is not on a valid symbol.
    /// </summary>
    Task<IReadOnlyList<LspCallHierarchyItem>> PrepareCallHierarchyAsync(
        string filePath, int line, int column, CancellationToken ct = default);

    /// <summary>
    /// Requests callHierarchy/incomingCalls for the given item.
    /// Returns all callers of the item. Returns an empty list when unsupported.
    /// </summary>
    Task<IReadOnlyList<LspIncomingCall>> GetIncomingCallsAsync(
        LspCallHierarchyItem item, CancellationToken ct = default);

    /// <summary>
    /// Requests callHierarchy/outgoingCalls for the given item.
    /// Returns all callees invoked by the item. Returns an empty list when unsupported.
    /// </summary>
    Task<IReadOnlyList<LspOutgoingCall>> GetOutgoingCallsAsync(
        LspCallHierarchyItem item, CancellationToken ct = default);

    // ── Type Hierarchy (LSP 3.17) ────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/prepareTypeHierarchy at the given caret position.
    /// Returns an empty list when the server does not support it.
    /// </summary>
    Task<IReadOnlyList<LspTypeHierarchyItem>> PrepareTypeHierarchyAsync(
        string filePath, int line, int column, CancellationToken ct = default);

    /// <summary>
    /// Requests typeHierarchy/supertypes for the given item.
    /// Returns base classes / implemented interfaces.
    /// </summary>
    Task<IReadOnlyList<LspTypeHierarchyItem>> GetSupertypesAsync(
        LspTypeHierarchyItem item, CancellationToken ct = default);

    /// <summary>
    /// Requests typeHierarchy/subtypes for the given item.
    /// Returns derived classes / implementing types.
    /// </summary>
    Task<IReadOnlyList<LspTypeHierarchyItem>> GetSubtypesAsync(
        LspTypeHierarchyItem item, CancellationToken ct = default);

    // ── Linked Editing Ranges (LSP 3.16) ─────────────────────────────────────

    /// <summary>
    /// Requests textDocument/linkedEditingRange at the given caret position.
    /// Returns all ranges that must stay in sync (e.g. HTML open/close tag pairs).
    /// Returns an empty list when the server does not support this capability or
    /// the caret is not inside a linked region.
    /// </summary>
    Task<IReadOnlyList<LspLinkedRange>> LinkedEditingRangesAsync(
        string filePath, int line, int column, CancellationToken ct = default);

    // ── Push Notifications ────────────────────────────────────────────────────

    /// <summary>
    /// Raised on the UI thread whenever the server pushes
    /// textDocument/publishDiagnostics for any open document.
    /// </summary>
    event EventHandler<LspDiagnosticsReceivedEventArgs> DiagnosticsReceived;
}
