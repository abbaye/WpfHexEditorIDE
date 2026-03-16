// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Models/TypeModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Updated: 2026-03-16 — Phase 2: added GenericParameters, XmlDocComment.
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Immutable model representing a single .NET type definition.
//     Enhanced from the plugin stub: adds BaseTypeName, InterfaceNames,
//     and CustomAttributes populated by AssemblyAnalysisEngine.
//
// Architecture Notes:
//     Pattern: Immutable data model (init-only properties).
//     PeOffset = 0 means the offset was not resolved.
// ==========================================================

namespace WpfHexEditor.Core.AssemblyAnalysis.Models;

/// <summary>Distinguishes the kind of a .NET type definition.</summary>
public enum TypeKind
{
    Class,
    Struct,
    Interface,
    Enum,
    Delegate
}

/// <summary>
/// Immutable model for a .NET TypeDefinition metadata row.
/// Contains its members as child lists.
/// </summary>
public sealed class TypeModel
{
    /// <summary>CLR namespace, e.g. "System.Collections.Generic". Empty for global namespace.</summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>Simple type name without namespace, e.g. "List`1".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Fully qualified name: "{Namespace}.{Name}" or just "{Name}" for global ns.</summary>
    public string FullName =>
        string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";

    /// <summary>Class, Struct, Interface, Enum, or Delegate.</summary>
    public TypeKind Kind { get; init; }

    /// <summary>True when the type has public visibility.</summary>
    public bool IsPublic { get; init; }

    /// <summary>True when the type is abstract.</summary>
    public bool IsAbstract { get; init; }

    /// <summary>True when the type is sealed.</summary>
    public bool IsSealed { get; init; }

    /// <summary>
    /// Raw PE file offset of the TypeDef metadata row, or 0 if not yet resolved.
    /// Used by the HexEditor sync feature to navigate to the byte in the loaded file.
    /// </summary>
    public long PeOffset { get; init; }

    /// <summary>ECMA-335 metadata token (0x02xxxxxx for TypeDef rows).</summary>
    public int MetadataToken { get; init; }

    /// <summary>
    /// Resolved name of the base type, e.g. "System.Object", "System.Enum".
    /// Null when there is no explicit base type (e.g. interfaces, or Object itself).
    /// </summary>
    public string? BaseTypeName { get; init; }

    /// <summary>
    /// Names of explicitly implemented interfaces, e.g. ["IDisposable", "IEnumerable`1"].
    /// Empty when the type implements no interfaces.
    /// </summary>
    public IReadOnlyList<string> InterfaceNames { get; init; } = [];

    /// <summary>
    /// Simple names of custom attributes applied to this type, e.g. ["Serializable", "Obsolete"].
    /// Attribute suffix is stripped for brevity (e.g. "ObsoleteAttribute" → "Obsolete").
    /// </summary>
    public IReadOnlyList<string> CustomAttributes { get; init; } = [];

    /// <summary>
    /// Generic type parameter names declared on this type,
    /// e.g. ["T", "TKey", "TValue"]. Empty for non-generic types.
    /// </summary>
    public IReadOnlyList<string> GenericParameters { get; init; } = [];

    /// <summary>
    /// Summary sentence from the companion XML documentation file (e.g. MyLib.xml).
    /// Null when no .xml file exists or the type has no doc entry.
    /// </summary>
    public string? XmlDocComment { get; init; }

    public IReadOnlyList<MemberModel> Methods    { get; init; } = [];
    public IReadOnlyList<MemberModel> Fields     { get; init; } = [];
    public IReadOnlyList<MemberModel> Properties { get; init; } = [];
    public IReadOnlyList<MemberModel> Events     { get; init; } = [];
}
