// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: RoundTrip/Abstractions/MemberEdit.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Discriminated union describing every kind of edit the diagram
//     canvas can request back into a source file. Implementations of
//     ILanguageRoundTripEditor consume one MemberEdit at a time and
//     produce a RoundTripResult.
//
// Architecture Notes:
//     Sealed inheritance hierarchy with a private constructor on the
//     abstract base prevents external extension — the union is closed.
//     Records give value-based equality used in tests and undo entries.
//     No I/O, no Roslyn references — pure data DTO.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

/// <summary>
/// Closed discriminated union over every round-trip edit kind the diagram
/// surface can emit. Consumed by <see cref="ILanguageRoundTripEditor"/>.
/// </summary>
public abstract record MemberEdit
{
    private protected MemberEdit() { }

    /// <summary>Stable identifier of the type the edit targets (fully-qualified name).</summary>
    public required string TargetTypeFullName { get; init; }
}

// ── Type-level edits ─────────────────────────────────────────────────────────

public sealed record AddType(string Snippet) : MemberEdit;
public sealed record RemoveType() : MemberEdit;
public sealed record RenameType(string NewName) : MemberEdit;
public sealed record ChangeBaseType(string? NewBaseType) : MemberEdit;
public sealed record AddInterface(string InterfaceName) : MemberEdit;
public sealed record RemoveInterface(string InterfaceName) : MemberEdit;

// ── Member-level edits ───────────────────────────────────────────────────────

public sealed record AddMember(string Snippet) : MemberEdit;
public sealed record RemoveMember(string MemberName) : MemberEdit;
public sealed record RenameMember(string OldName, string NewName) : MemberEdit;
public sealed record ChangeVisibility(string MemberName, MemberVisibilityKind NewVisibility) : MemberEdit;
public sealed record ChangeMemberType(string MemberName, string NewType) : MemberEdit;

/// <summary>Visibility kinds expressible across C# and VB.</summary>
public enum MemberVisibilityKind
{
    Public,
    Internal,
    Protected,
    ProtectedInternal,
    PrivateProtected,
    Private
}
