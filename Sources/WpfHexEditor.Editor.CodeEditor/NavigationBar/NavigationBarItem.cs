// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: NavigationBarItem.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Immutable data model for a single entry in the VS-like
//     navigation bar (namespace / type / member).
// ==========================================================

namespace WpfHexEditor.Editor.CodeEditor.NavigationBar;

public enum NavigationItemKind { Namespace, Type, Member }
public enum TypeKind            { Class, Interface, Struct, Enum, Record, Delegate, Unknown }
public enum MemberKind          { Method, Constructor, Property, Indexer, Field, Event, Unknown }

/// <summary>
/// Represents one navigation item shown in a navigation-bar ComboBox.
/// </summary>
public sealed record NavigationBarItem(
    NavigationItemKind Kind,
    string             Name,
    string             FullName,
    int                Line,
    TypeKind           TypeKind   = TypeKind.Unknown,
    MemberKind         MemberKind = MemberKind.Unknown)
{
    public override string ToString() => Name;
}

/// <summary>
/// Snapshot produced by <see cref="CodeStructureParser"/> for one parse pass.
/// </summary>
public sealed class CodeStructureSnapshot
{
    public static readonly CodeStructureSnapshot Empty = new();

    public IReadOnlyList<NavigationBarItem> Namespaces { get; init; } = [];
    public IReadOnlyList<NavigationBarItem> Types      { get; init; } = [];
    public IReadOnlyList<NavigationBarItem> Members    { get; init; } = [];
}
