// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Model/ClassRelationship.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Immutable record representing a directed UML relationship between
//     two class nodes in a diagram document.
//
// Architecture Notes:
//     Implemented as a record for value-equality semantics.
//     SourceId and TargetId reference ClassNode.Id — not ClassNode.Name —
//     to support future rename refactoring without breaking relationships.
//     Label is optional and rendered as a mid-edge annotation.
//     SourceMultiplicity / TargetMultiplicity carry UML multiplicity notation
//     (e.g. "1", "*", "0..1", "1..*"). Waypoints store user-defined bend
//     points for the arrow path (persisted in DSL, ignored by auto-routing).
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Model;

/// <summary>
/// Represents a directed UML relationship between two nodes in a
/// <see cref="DiagramDocument"/>.
/// </summary>
public sealed record ClassRelationship
{
    /// <summary>
    /// The <see cref="ClassNode.Id"/> of the relationship source (tail end).
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// The <see cref="ClassNode.Id"/> of the relationship target (arrow head end).
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>The UML relationship kind that determines the arrow style.</summary>
    public RelationshipKind Kind { get; init; } = RelationshipKind.Association;

    /// <summary>
    /// Optional mid-edge label shown on the connector.
    /// <see langword="null"/> when no label is specified in the DSL.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// UML multiplicity at the source end (e.g. "1", "*", "0..1").
    /// <see langword="null"/> when not specified.
    /// </summary>
    public string? SourceMultiplicity { get; init; }

    /// <summary>
    /// UML multiplicity at the target end (e.g. "1", "1..*").
    /// <see langword="null"/> when not specified.
    /// </summary>
    public string? TargetMultiplicity { get; init; }

    /// <summary>
    /// Optional role name at the source end of the relationship.
    /// </summary>
    public string? SourceRole { get; init; }

    /// <summary>
    /// Optional role name at the target end of the relationship.
    /// </summary>
    public string? TargetRole { get; init; }

    /// <summary>
    /// User-defined intermediate bend points for the arrow path.
    /// Stored as alternating X/Y pairs. Empty = auto-routed.
    /// </summary>
    public List<(double X, double Y)> Waypoints { get; init; } = [];
}
