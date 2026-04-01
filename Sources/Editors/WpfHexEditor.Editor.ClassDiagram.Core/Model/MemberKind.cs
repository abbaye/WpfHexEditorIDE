// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Model/MemberKind.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Enumerates the kinds of members a class or interface can expose
//     in the class diagram model.
//
// Architecture Notes:
//     Pure BCL enum — no WPF or platform dependencies.
//     Used by ClassMember to classify each member for rendering and export.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Model;

/// <summary>Classifies the kind of a class or interface member.</summary>
public enum MemberKind
{
    /// <summary>A data field (instance or static variable).</summary>
    Field,

    /// <summary>An auto-property or full property declaration.</summary>
    Property,

    /// <summary>A method, constructor, or operator.</summary>
    Method,

    /// <summary>A CLR event declaration.</summary>
    Event
}
