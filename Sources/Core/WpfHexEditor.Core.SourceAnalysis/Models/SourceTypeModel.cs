// ==========================================================
// Project: WpfHexEditor.Core.SourceAnalysis
// File: Models/SourceTypeModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-16
// Description:
//     Immutable model for a type declaration found by the regex source scanner.
//     Represents class, struct, interface, enum, record, or record struct.
// ==========================================================

namespace WpfHexEditor.Core.SourceAnalysis.Models;

/// <summary>Kind of a source-level type declaration.</summary>
public enum SourceTypeKind
{
    Class,
    Struct,
    Interface,
    Enum,
    Record,
    RecordStruct
}

/// <summary>
/// Represents a type declaration found by the regex source scanner inside a .cs file.
/// BCL-only — produced without Roslyn.
/// </summary>
public sealed class SourceTypeModel
{
    /// <summary>Simple type name (no namespace), e.g. "MyViewModel".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Class, Struct, Interface, Enum, Record, or RecordStruct.</summary>
    public SourceTypeKind Kind { get; init; }

    /// <summary>1-based line number of the declaration keyword in the source file.</summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// True when the declaration has the "public" access modifier.
    /// Approximated from the raw declaration line — not fully semantic.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>True when the type has the "abstract" modifier.</summary>
    public bool IsAbstract { get; init; }

    /// <summary>True when the type has the "static" modifier.</summary>
    public bool IsStatic { get; init; }

    /// <summary>Members (methods, properties, fields, events, constructors) declared within this type.</summary>
    public IReadOnlyList<SourceMemberModel> Members { get; init; } = [];
}
