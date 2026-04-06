// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/DocumentStructure/DocumentStructureNode.cs
// Created: 2026-04-05
// Description:
//     Immutable model representing a single node in a document structure tree.
//     Produced by IDocumentStructureProvider implementations.
//
// Architecture Notes:
//     Pure data record — no WPF dependency. Children form a recursive tree.
//     Supports both text-based (line/column) and binary (byte offset) documents.
// ==========================================================

namespace WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

/// <summary>
/// A single node in a document structure tree.
/// Produced by <see cref="IDocumentStructureProvider"/> implementations.
/// </summary>
public sealed class DocumentStructureNode
{
    /// <summary>Display name of the node (e.g. type name, heading text, element tag).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Semantic kind of the node. Standard values: "class", "struct", "interface", "enum",
    /// "method", "function", "constructor", "property", "field", "variable", "event",
    /// "constant", "namespace", "module", "heading", "section", "element", "key",
    /// "array", "object", "block", "region", "record", "enummember", "typeparameter", "file".
    /// </summary>
    public string Kind { get; init; } = "unknown";

    /// <summary>
    /// Optional Segoe MDL2 Assets glyph override. When null, the panel auto-resolves from Kind.
    /// </summary>
    public string? IconGlyph { get; init; }

    /// <summary>
    /// Optional detail text shown after the name (e.g. return type, type hint, value preview).
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>1-based start line in the source file. -1 if unknown or not applicable.</summary>
    public int StartLine { get; init; } = -1;

    /// <summary>1-based start column. -1 if unknown.</summary>
    public int StartColumn { get; init; } = -1;

    /// <summary>1-based end line. -1 if unknown.</summary>
    public int EndLine { get; init; } = -1;

    /// <summary>1-based end column. -1 if unknown.</summary>
    public int EndColumn { get; init; } = -1;

    /// <summary>Byte offset for binary documents. -1 if not applicable.</summary>
    public long ByteOffset { get; init; } = -1;

    /// <summary>Byte length for binary documents. 0 if not applicable.</summary>
    public long ByteLength { get; init; }

    /// <summary>True when this node has no children.</summary>
    public bool IsLeaf => Children.Count == 0;

    /// <summary>Child nodes forming the hierarchy.</summary>
    public IReadOnlyList<DocumentStructureNode> Children { get; init; } = [];
}
