// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/DocumentStructure/IDocumentStructureProvider.cs
// Created: 2026-04-05
// Description:
//     Extension point contract for document structure/outline providers.
//     Plugins or core assemblies implement this to supply hierarchical structure
//     for different document types (code, binary, markdown, JSON, XML, etc.).
//
// Architecture Notes:
//     Multiple providers can coexist. The resolver picks the highest-priority
//     provider that CanProvide for a given document. Priority scale:
//       LSP=1000, SourceOutline=500, Language-specific=300, Folding=100.
// ==========================================================

namespace WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

/// <summary>
/// Extension point: document structure / outline provider.
/// Implementations produce a hierarchical tree of <see cref="DocumentStructureNode"/>
/// for a given document file.
/// </summary>
public interface IDocumentStructureProvider
{
    /// <summary>Display name shown when multiple providers are available (e.g. "LSP Symbols").</summary>
    string DisplayName { get; }

    /// <summary>
    /// Priority for provider selection. Higher values are preferred.
    /// Convention: LSP=1000, SourceOutline=500, Language-specific=300, Folding=100.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Returns true if this provider can produce structure for the given document.
    /// Called on the UI thread — must be fast (no I/O).
    /// </summary>
    /// <param name="filePath">Absolute file path, or null if unavailable.</param>
    /// <param name="documentType">Document type from IDocument.DocumentType (e.g. "code", "hex", "text").</param>
    /// <param name="language">Language identifier from ICodeEditorService.CurrentLanguage (e.g. "csharp"), or null.</param>
    bool CanProvide(string? filePath, string? documentType, string? language);

    /// <summary>
    /// Produces the document structure tree. Called on a background thread.
    /// Returns null when the file cannot be parsed or is empty.
    /// </summary>
    Task<DocumentStructureResult?> GetStructureAsync(string filePath, CancellationToken ct = default);
}
