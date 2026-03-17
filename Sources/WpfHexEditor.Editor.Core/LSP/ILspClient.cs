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
//     are exposed. The concrete WpfHexEditor.LSP.Client implementation handles
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

    // ── Signature Help ────────────────────────────────────────────────────────

    /// <summary>
    /// Requests textDocument/signatureHelp (called after '(').
    /// Returns null when the server does not support it or no signature matches.
    /// </summary>
    Task<string?> SignatureHelpAsync(
        string filePath, int line, int column, CancellationToken ct = default);

    // ── Push Notifications ────────────────────────────────────────────────────

    /// <summary>
    /// Raised on the UI thread whenever the server pushes
    /// textDocument/publishDiagnostics for any open document.
    /// </summary>
    event EventHandler<LspDiagnosticsReceivedEventArgs> DiagnosticsReceived;
}
