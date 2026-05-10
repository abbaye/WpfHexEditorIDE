// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Model/CodeGenericParameter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Immutable IR descriptor for a generic type parameter and its
//     associated constraints.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

/// <summary>Variance annotation for a generic parameter.</summary>
public enum CodeVariance
{
    /// <summary>No variance annotation.</summary>
    None,

    /// <summary>Covariant (<c>out T</c>).</summary>
    Covariant,

    /// <summary>Contravariant (<c>in T</c>).</summary>
    Contravariant
}

/// <summary>Immutable IR descriptor for a generic type parameter.</summary>
public sealed record CodeGenericParameter
{
    /// <summary>The parameter identifier (e.g. <c>T</c>, <c>TKey</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Variance annotation; only valid on interface and delegate parameters.</summary>
    public CodeVariance Variance { get; init; } = CodeVariance.None;

    /// <summary>
    /// Constraint clauses rendered verbatim (e.g. <c>class</c>, <c>new()</c>,
    /// <c>IDisposable</c>, <c>notnull</c>).
    /// </summary>
    public IReadOnlyList<string> Constraints { get; init; } = [];
}
