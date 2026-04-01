// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Model/DiagramDocument.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Root aggregate for a class diagram: holds the full collection of
//     class nodes and their relationships, plus the optional backing
//     file path for save/load operations.
//
// Architecture Notes:
//     Mutable collection properties allow incremental construction by
//     the parser and interactive diagram editor.
//     FindClass / FindRelationship are O(n) linear scans — acceptable
//     for diagrams up to a few hundred nodes; callers with hot-path
//     lookup needs should build their own dictionary indexes.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Model;

/// <summary>
/// Root aggregate that holds all <see cref="ClassNode"/> instances and
/// <see cref="ClassRelationship"/> edges of a single diagram document.
/// </summary>
public sealed class DiagramDocument
{
    /// <summary>All type nodes contained in this diagram.</summary>
    public List<ClassNode> Classes { get; init; } = [];

    /// <summary>All directed relationships between nodes.</summary>
    public List<ClassRelationship> Relationships { get; init; } = [];

    /// <summary>
    /// Absolute path of the DSL source file backing this document,
    /// or <see langword="null"/> when the document has not been saved yet.
    /// </summary>
    public string? FilePath { get; set; }

    // -------------------------------------------------------
    // Query helpers
    // -------------------------------------------------------

    /// <summary>
    /// Returns the first <see cref="ClassNode"/> whose <see cref="ClassNode.Name"/>
    /// matches <paramref name="name"/> (ordinal, case-sensitive), or
    /// <see langword="null"/> when not found.
    /// </summary>
    /// <param name="name">The type name to search for.</param>
    public ClassNode? FindClass(string name) =>
        Classes.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// Returns the first <see cref="ClassRelationship"/> whose source and target
    /// identifiers match <paramref name="srcId"/> and <paramref name="tgtId"/>
    /// (ordinal, case-sensitive), or <see langword="null"/> when not found.
    /// </summary>
    /// <param name="srcId">Source node identifier.</param>
    /// <param name="tgtId">Target node identifier.</param>
    public ClassRelationship? FindRelationship(string srcId, string tgtId) =>
        Relationships.FirstOrDefault(r =>
            string.Equals(r.SourceId, srcId, StringComparison.Ordinal) &&
            string.Equals(r.TargetId, tgtId, StringComparison.Ordinal));
}
