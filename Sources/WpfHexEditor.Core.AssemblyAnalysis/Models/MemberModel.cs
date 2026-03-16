// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Models/MemberModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Updated: 2026-03-16 — Phase 2: added IsReadOnly, IsOverride, ConstantValue,
//     GenericParameters, XmlDocComment.
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Immutable model representing a single .NET member:
//     a method, field, property, or event definition.
//
// Architecture Notes:
//     Pattern: Immutable data model (init-only properties).
//     Signature is populated by SignatureDecoder during analysis;
//     no longer null for method/field/property rows.
// ==========================================================

namespace WpfHexEditor.Core.AssemblyAnalysis.Models;

/// <summary>Distinguishes the kind of a .NET member definition.</summary>
public enum MemberKind
{
    Method,
    Field,
    Property,
    Event
}

/// <summary>
/// Immutable model for a .NET member (method/field/property/event) row.
/// </summary>
public sealed class MemberModel
{
    /// <summary>Simple member name, e.g. "GetHashCode" or "_items".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Method, Field, Property, or Event.</summary>
    public MemberKind Kind { get; init; }

    /// <summary>True when the member has public visibility.</summary>
    public bool IsPublic { get; init; }

    /// <summary>True when the member is static.</summary>
    public bool IsStatic { get; init; }

    /// <summary>True when the member is abstract (interface member or abstract method).</summary>
    public bool IsAbstract { get; init; }

    /// <summary>True when the member is virtual (introduces a new virtual slot).</summary>
    public bool IsVirtual { get; init; }

    /// <summary>True when the method overrides an inherited virtual slot.</summary>
    public bool IsOverride { get; init; }

    /// <summary>True when the field is readonly (initonly in IL).</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// For const fields: the literal value formatted as a C# expression,
    /// e.g. "42", "\"hello\"", "true", "null". Null for non-const members.
    /// </summary>
    public string? ConstantValue { get; init; }

    /// <summary>
    /// Generic type parameter names declared on this member (methods only),
    /// e.g. ["T", "TResult"]. Empty for non-generic members.
    /// </summary>
    public IReadOnlyList<string> GenericParameters { get; init; } = [];

    /// <summary>
    /// Summary sentence from the companion XML documentation file (e.g. MyLib.xml).
    /// Null when no .xml file exists or the member has no doc entry.
    /// </summary>
    public string? XmlDocComment { get; init; }

    /// <summary>
    /// Raw PE file offset of the IL method body, or 0 if not yet resolved.
    /// Used by the HexEditor sync feature.
    /// </summary>
    public long PeOffset { get; init; }

    /// <summary>ECMA-335 metadata token (table-specific row token).</summary>
    public int MetadataToken { get; init; }

    /// <summary>
    /// Human-readable decoded signature for display, e.g. "int Add(int a, int b)".
    /// Populated by SignatureDecoder. Null only for Event rows (which have no blob signature).
    /// </summary>
    public string? Signature { get; init; }

    /// <summary>
    /// Raw byte length of the IL method body (header + code bytes).
    /// Populated for method definitions only; 0 for fields, properties, events,
    /// and abstract/extern/interface methods that have no body.
    /// Used by the hex editor highlight feature to mark the exact byte range.
    /// </summary>
    public int ByteLength { get; init; }

    /// <summary>
    /// Simple names of custom attributes applied to this member,
    /// e.g. ["Obsolete", "DllImport"]. Attribute suffix stripped.
    /// </summary>
    public IReadOnlyList<string> CustomAttributes { get; init; } = [];
}
