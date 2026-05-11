// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Model/GenericParameter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Phase 2A — one entry in ClassNode.TypeParameters. Represents a
//     single generic type parameter (e.g. T) and its constraints
//     (`class`, `struct`, `new()`, or a base-type/interface bound).
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Model;

/// <summary>
/// A single generic type parameter declared on a <see cref="ClassNode"/>.
/// </summary>
/// <param name="Name">Parameter name (e.g. "T", "TKey", "TValue").</param>
/// <param name="Constraints">
/// Raw constraint strings as they appear in source (e.g. "class", "new()",
/// "IComparable&lt;T&gt;"). Empty when the parameter is unconstrained.
/// </param>
/// <param name="Variance">Variance keyword (in/out) or empty.</param>
public sealed record GenericParameter(
    string                Name,
    IReadOnlyList<string> Constraints,
    string                Variance = "")
{
    /// <summary>Convenience factory for the common no-constraint case.</summary>
    public static GenericParameter Of(string name) => new(name, [], string.Empty);
}
