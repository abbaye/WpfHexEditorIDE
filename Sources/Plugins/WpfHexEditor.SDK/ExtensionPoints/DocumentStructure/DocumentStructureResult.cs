// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/DocumentStructure/DocumentStructureResult.cs
// Created: 2026-04-05
// Description:
//     Result container returned by IDocumentStructureProvider.GetStructureAsync().
//     Contains the root-level nodes plus metadata about the parse.
//
// Architecture Notes:
//     Immutable. IsPartial supports progressive loading for large documents.
// ==========================================================

namespace WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

/// <summary>
/// Result of a document structure parse, containing the root-level nodes.
/// </summary>
public sealed class DocumentStructureResult
{
    /// <summary>Root-level structure nodes (the top of the hierarchy).</summary>
    public IReadOnlyList<DocumentStructureNode> Nodes { get; init; } = [];

    /// <summary>Absolute path of the parsed file.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Language identifier (e.g. "csharp", "json", "markdown"), or null if unknown.</summary>
    public string? Language { get; init; }

    /// <summary>UTC timestamp when the structure was produced.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>True when this result is partial (more data loading). False when complete.</summary>
    public bool IsPartial { get; init; }
}
