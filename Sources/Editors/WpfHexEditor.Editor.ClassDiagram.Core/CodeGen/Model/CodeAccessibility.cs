// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Model/CodeAccessibility.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Language-agnostic accessibility level used by the IR. Mirrors
//     Roslyn's Accessibility enum so the C# generator can map 1:1.
//
// Architecture Notes:
//     Independent from MemberVisibility (which is the diagram-level
//     enum) so future visibility levels (file, protected internal,
//     private protected) can be added without touching the diagram
//     model.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

/// <summary>Language-agnostic accessibility level for an IR member or type.</summary>
public enum CodeAccessibility
{
    /// <summary>No explicit accessibility — language-default applies.</summary>
    NotApplicable,

    /// <summary>Visible from anywhere.</summary>
    Public,

    /// <summary>Visible only within the declaring type.</summary>
    Private,

    /// <summary>Visible to the declaring type and its derived types.</summary>
    Protected,

    /// <summary>Visible within the declaring assembly.</summary>
    Internal,

    /// <summary>Visible to the declaring assembly OR derived types.</summary>
    ProtectedInternal,

    /// <summary>Visible to the declaring type AND derived types within the same assembly.</summary>
    PrivateProtected
}
