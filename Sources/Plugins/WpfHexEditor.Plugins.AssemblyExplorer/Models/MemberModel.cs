// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Models/MemberModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Immutable model representing a single .NET member:
//     a method, field, property, or event definition.
//
// Architecture Notes:
//     Pattern: Immutable data model (init-only properties).
//     Signature is a pre-decoded string for display only; IL decoding
//     is deferred to the DecompilerService (future phase).
// ==========================================================

namespace WpfHexEditor.Plugins.AssemblyExplorer.Models;

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

    /// <summary>
    /// Raw PE file offset of the metadata row, or 0 if not yet resolved.
    /// Used by the HexEditor sync feature.
    /// </summary>
    public long PeOffset { get; init; }

    /// <summary>ECMA-335 metadata token (table-specific row token).</summary>
    public int MetadataToken { get; init; }

    /// <summary>
    /// Human-readable decoded signature for display, e.g. "int Add(int a, int b)".
    /// Null if signature decoding was skipped (stub phase).
    /// </summary>
    public string? Signature { get; init; }
}
