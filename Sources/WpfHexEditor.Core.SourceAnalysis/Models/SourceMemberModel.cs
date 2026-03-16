// ==========================================================
// Project: WpfHexEditor.Core.SourceAnalysis
// File: Models/SourceMemberModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-16
// Description:
//     Immutable model for a member declaration found inside a source type.
//     Represents methods, properties, fields, events, and constructors.
// ==========================================================

namespace WpfHexEditor.Core.SourceAnalysis.Models;

/// <summary>Kind of a source-level member declaration.</summary>
public enum SourceMemberKind
{
    Constructor,
    Method,
    Property,
    Field,
    Event
}

/// <summary>
/// Represents a member declaration found inside a type by the regex source scanner.
/// BCL-only — produced without Roslyn.
/// </summary>
public sealed class SourceMemberModel
{
    /// <summary>Member name, e.g. "OnLoaded", "IsVisible", "_count".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Return type string as written in source, e.g. "void", "Task&lt;string&gt;", "bool".
    /// Empty for constructors.
    /// </summary>
    public string ReturnType { get; init; } = string.Empty;

    /// <summary>Constructor, Method, Property, Field, or Event.</summary>
    public SourceMemberKind Kind { get; init; }

    /// <summary>1-based line number of the member declaration in the source file.</summary>
    public int LineNumber { get; init; }

    /// <summary>True when the "public" modifier is present on the declaration line.</summary>
    public bool IsPublic { get; init; }

    /// <summary>True when the "static" modifier is present.</summary>
    public bool IsStatic { get; init; }

    /// <summary>True when the "override" modifier is present.</summary>
    public bool IsOverride { get; init; }

    /// <summary>True when the "async" modifier is present.</summary>
    public bool IsAsync { get; init; }
}
