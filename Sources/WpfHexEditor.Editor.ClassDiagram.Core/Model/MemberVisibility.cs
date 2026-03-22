// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Model/MemberVisibility.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Enumerates access-modifier levels for class diagram members,
//     mirroring the four standard C# accessibility levels.
//
// Architecture Notes:
//     Pure BCL enum — no WPF or platform dependencies.
//     Consumed by ClassMember and the C# skeleton generator.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Model;

/// <summary>Represents the C# accessibility level of a class member.</summary>
public enum MemberVisibility
{
    /// <summary>Accessible from any assembly (<c>public</c>).</summary>
    Public,

    /// <summary>Accessible only within the declaring type (<c>private</c>).</summary>
    Private,

    /// <summary>Accessible within the declaring type and its derived types (<c>protected</c>).</summary>
    Protected,

    /// <summary>Accessible within the same assembly (<c>internal</c>).</summary>
    Internal
}
