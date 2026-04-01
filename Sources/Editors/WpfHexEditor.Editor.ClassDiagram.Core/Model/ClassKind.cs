// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Model/ClassKind.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Enumerates the structural kind of a class diagram node,
//     distinguishing between classes, interfaces, enums, structs,
//     and abstract classes.
//
// Architecture Notes:
//     Pure BCL enum — no WPF or platform dependencies.
//     Used by ClassNode and downstream generators/exporters.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Model;

/// <summary>Identifies the structural kind of a type node in the diagram.</summary>
public enum ClassKind
{
    /// <summary>A concrete or abstract reference type (<c>class</c>).</summary>
    Class,

    /// <summary>A contract type (<c>interface</c>).</summary>
    Interface,

    /// <summary>A named constant set (<c>enum</c>).</summary>
    Enum,

    /// <summary>A value type (<c>struct</c>).</summary>
    Struct,

    /// <summary>An explicitly abstract reference type (<c>abstract class</c>).</summary>
    Abstract
}
