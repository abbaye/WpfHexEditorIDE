// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Models/TypeModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Immutable model representing a single .NET type definition
//     (class, struct, interface, enum, or delegate).
//     Carries the raw PE file offset for HexEditor sync.
//
// Architecture Notes:
//     Pattern: Immutable data model (init-only properties).
//     PeOffset = 0 means the offset was not resolved (stub phase).
// ==========================================================

namespace WpfHexEditor.Plugins.AssemblyExplorer.Models;

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

    /// <summary>
    /// Raw PE file offset of the TypeDef metadata row, or 0 if not yet resolved.
    /// Used by the HexEditor sync feature to navigate to the byte in the loaded file.
    /// </summary>
    public long PeOffset { get; init; }

    /// <summary>ECMA-335 metadata token (0x02xxxxxx for TypeDef rows).</summary>
    public int MetadataToken { get; init; }

    public IReadOnlyList<MemberModel> Methods    { get; init; } = [];
    public IReadOnlyList<MemberModel> Fields     { get; init; } = [];
    public IReadOnlyList<MemberModel> Properties { get; init; } = [];
    public IReadOnlyList<MemberModel> Events     { get; init; } = [];
}
