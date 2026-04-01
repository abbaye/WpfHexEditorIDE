// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/IQuickInfoProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Extension point contract for plugins that contribute Quick Info hover
//     tooltip content. When the user hovers over a symbol, the CodeEditor
//     queries all registered IQuickInfoProvider contributors and uses the
//     first non-null result to populate the QuickInfoPopup.
//
// Architecture Notes:
//     Pattern: Extension Point / Strategy.
//     Register in plugin manifest: "extensions": { "QuickInfo": "MyPlugin.MyProvider" }
//     The IDE queries providers in registration order; first non-null wins.
//     Providers run on a background Task.Run thread — must be thread-safe and
//     must not touch WPF objects.
// ==========================================================

namespace WpfHexEditor.SDK.ExtensionPoints;

// ── Result DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// A single actionable link shown at the bottom of the Quick Info popup.
/// </summary>
public sealed class QuickInfoActionLink
{
    /// <summary>Display label shown in the popup (e.g. "Go to Definition (F12)").</summary>
    public required string Label { get; init; }

    /// <summary>
    /// Well-known command identifier routed back to the CodeEditor.
    /// Supported values: <c>"GoToDefinition"</c>, <c>"FindAllReferences"</c>.
    /// </summary>
    public required string Command { get; init; }
}

/// <summary>
/// Data returned by an <see cref="IQuickInfoProvider"/> for a specific position.
/// All properties except <see cref="SymbolName"/> and <see cref="SymbolKind"/> are optional.
/// </summary>
public sealed class QuickInfoResult
{
    /// <summary>Short symbol name (e.g. "WriteLine", "MyClass").</summary>
    public required string SymbolName { get; init; }

    /// <summary>
    /// Human-readable kind label (e.g. "method", "class", "field", "property", "keyword").
    /// Used to select the Segoe MDL2 glyph shown in the header.
    /// </summary>
    public required string SymbolKind { get; init; }

    /// <summary>Full type signature line (e.g. "void Console.WriteLine(string value)").</summary>
    public string? TypeSignature { get; init; }

    /// <summary>
    /// Documentation text (plain text or lightweight Markdown).
    /// Line breaks are respected; full Markdown rendering is not guaranteed.
    /// </summary>
    public string? DocumentationMarkdown { get; init; }

    /// <summary>
    /// Diagnostic error/warning message to display in a tinted section.
    /// Non-null only when the hover position overlaps a diagnostic squiggly.
    /// </summary>
    public string? DiagnosticMessage { get; init; }

    /// <summary>
    /// Severity of the diagnostic: <c>"error"</c>, <c>"warning"</c>, or <c>"information"</c>.
    /// Ignored when <see cref="DiagnosticMessage"/> is null.
    /// </summary>
    public string? DiagnosticSeverity { get; init; }

    /// <summary>Action links shown at the bottom of the popup (e.g. Go to Definition).</summary>
    public IReadOnlyList<QuickInfoActionLink> ActionLinks { get; init; } = [];
}

// ── Extension point interface ──────────────────────────────────────────────────

/// <summary>
/// Extension point: contribute hover Quick Info content to the CodeEditor.
/// <para>
/// Register in plugin manifest:
/// <code>"extensions": { "QuickInfo": "MyPlugin.MyQuickInfoProvider" }</code>
/// </para>
/// </summary>
public interface IQuickInfoProvider
{
    /// <summary>
    /// Returns Quick Info data for the symbol at <paramref name="line"/>/<paramref name="column"/>,
    /// or <see langword="null"/> if this provider does not handle the position.
    /// </summary>
    /// <param name="filePath">Absolute path of the open file.</param>
    /// <param name="line">0-based line index.</param>
    /// <param name="column">0-based column index (cursor position).</param>
    /// <param name="ct">Cancellation token — honour promptly to avoid stalling hover.</param>
    Task<QuickInfoResult?> GetQuickInfoAsync(
        string filePath, int line, int column, CancellationToken ct);
}
