// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Model/CodeTypeKind.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Language-agnostic type kind used by the IR. Decoupled from
//     ClassKind (diagram model) so a single IR can target multiple
//     languages without leaking diagram concerns.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

/// <summary>Identifies the structural kind of an IR type declaration.</summary>
public enum CodeTypeKind
{
    /// <summary>Concrete reference type.</summary>
    Class,

    /// <summary>Reference type marked as abstract — cannot be instantiated.</summary>
    AbstractClass,

    /// <summary>Sealed reference type — cannot be inherited.</summary>
    SealedClass,

    /// <summary>Static class — only static members, no instances.</summary>
    StaticClass,

    /// <summary>Value type (struct).</summary>
    Struct,

    /// <summary>Read-only value type.</summary>
    ReadOnlyStruct,

    /// <summary>Contract type with no implementation (interface).</summary>
    Interface,

    /// <summary>Named-constant set (enum).</summary>
    Enum,

    /// <summary>C# 9+ reference-type record.</summary>
    Record,

    /// <summary>C# 10+ value-type record.</summary>
    RecordStruct,

    /// <summary>Delegate type declaration.</summary>
    Delegate
}

/// <summary>Identifies the kind of a member inside an IR type.</summary>
public enum CodeMemberKind
{
    /// <summary>Instance or static field.</summary>
    Field,

    /// <summary>Property (auto- or full).</summary>
    Property,

    /// <summary>Method, constructor, or operator.</summary>
    Method,

    /// <summary>Constructor — kept distinct from Method to ease IR consumption.</summary>
    Constructor,

    /// <summary>CLR event.</summary>
    Event,

    /// <summary>Indexer (this[...]).</summary>
    Indexer,

    /// <summary>Enum-member literal.</summary>
    EnumMember
}
