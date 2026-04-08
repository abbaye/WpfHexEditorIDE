// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Model/RelationshipKind.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Enumerates the UML relationship kinds supported by the class
//     diagram engine: Inheritance, Association, Dependency,
//     Aggregation, and Composition.
//
// Architecture Notes:
//     Pure BCL enum — no WPF or platform dependencies.
//     Maps 1-to-1 with DSL arrow tokens and Mermaid export syntax.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Model;

/// <summary>Classifies the UML relationship type between two diagram nodes.</summary>
public enum RelationshipKind
{
    /// <summary>Is-a relationship rendered with a hollow arrowhead (<c>&lt;|--</c>).</summary>
    Inheritance,

    /// <summary>Has-a or uses-a navigable link rendered with an open arrow (<c>--&gt;</c>).</summary>
    Association,

    /// <summary>Uses-at-runtime (dashed arrow) rendered with a dashed line (<c>..&gt;</c>).</summary>
    Dependency,

    /// <summary>Whole-part relationship where the part can outlive the whole (<c>o--</c>).</summary>
    Aggregation,

    /// <summary>Strong whole-part ownership where the part cannot outlive the whole (<c>*--</c>).</summary>
    Composition,

    /// <summary>Interface implementation rendered with dashed line + hollow triangle (<c>..|&gt;</c>).</summary>
    Realization,

    /// <summary>General usage relationship (caller → callee) rendered with open dashed arrow.</summary>
    Uses,

    /// <summary>Factory/builder relationship: this type creates instances of the target.</summary>
    Creates
}
