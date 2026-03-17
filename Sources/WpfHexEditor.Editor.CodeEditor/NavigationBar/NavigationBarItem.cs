// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: NavigationBarItem.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Immutable data model for a single entry in the VS-like
//     navigation bar (namespace / type / member).
//     Includes icon glyph + colour matching VS2022 member icon palette.
// ==========================================================

using System.Windows.Media;

namespace WpfHexEditor.Editor.CodeEditor.NavigationBar;

public enum NavigationItemKind { Namespace, Type, Member }
public enum TypeKind            { Class, Interface, Struct, Enum, Record, Delegate, Unknown }
public enum MemberKind          { Method, Constructor, Property, Indexer, Field, Event, Unknown }

/// <summary>
/// Represents one navigation item shown in a navigation-bar ComboBox.
/// Exposes <see cref="IconGlyph"/> and <see cref="IconBrush"/> for the item DataTemplate.
/// </summary>
public sealed record NavigationBarItem(
    NavigationItemKind Kind,
    string             Name,
    string             FullName,
    int                Line,
    TypeKind           TypeKind   = TypeKind.Unknown,
    MemberKind         MemberKind = MemberKind.Unknown)
{
    // ── Icon glyph — Segoe MDL2 Assets codepoints matching the IDE Solution Explorer ──
    public string IconGlyph => (Kind, TypeKind, MemberKind) switch
    {
        (NavigationItemKind.Namespace, _, _)                    => "\uE8B7", // Folder
        (NavigationItemKind.Type, TypeKind.Class, _)            => "\uE943", // Class
        (NavigationItemKind.Type, TypeKind.Interface, _)        => "\uE8C1", // Interface
        (NavigationItemKind.Type, TypeKind.Struct, _)           => "\uE8A0", // Struct
        (NavigationItemKind.Type, TypeKind.Enum, _)             => "\uE945", // Enum
        (NavigationItemKind.Type, TypeKind.Record, _)           => "\uE943", // Record (same as Class)
        (NavigationItemKind.Type, TypeKind.Delegate, _)         => "\uE8F4", // Delegate (same as Method)
        (NavigationItemKind.Member, _, MemberKind.Method)       => "\uE8F4", // Method
        (NavigationItemKind.Member, _, MemberKind.Constructor)  => "\uE8F4", // Constructor (same as Method)
        (NavigationItemKind.Member, _, MemberKind.Property)     => "\uE8EC", // Property
        (NavigationItemKind.Member, _, MemberKind.Indexer)      => "\uE8EC", // Indexer (same as Property)
        (NavigationItemKind.Member, _, MemberKind.Field)        => "\uE8D2", // Field
        (NavigationItemKind.Member, _, MemberKind.Event)        => "\uE7FC", // Event
        _                                                       => "\uE8A5", // Fallback
    };

    // ── Icon colour — VS2022 member palette (same as AssemblyExplorer) ────────
    public Brush IconBrush => MakeBrush((Kind, TypeKind, MemberKind) switch
    {
        (NavigationItemKind.Namespace, _, _)                        => "#DCDCAA",
        (NavigationItemKind.Type, TypeKind.Class, _)                => "#4FC1FF",
        (NavigationItemKind.Type, TypeKind.Interface, _)            => "#B8D7A3",
        (NavigationItemKind.Type, TypeKind.Struct, _)               => "#4EC9B0",
        (NavigationItemKind.Type, TypeKind.Enum, _)                 => "#CE9178",
        (NavigationItemKind.Type, TypeKind.Record, _)               => "#4FC1FF",
        (NavigationItemKind.Type, TypeKind.Delegate, _)             => "#C586C0",
        (NavigationItemKind.Member, _, MemberKind.Method)           => "#C586C0",
        (NavigationItemKind.Member, _, MemberKind.Constructor)      => "#C586C0",
        (NavigationItemKind.Member, _, MemberKind.Property)         => "#9CDCFE",
        (NavigationItemKind.Member, _, MemberKind.Indexer)          => "#9CDCFE",
        (NavigationItemKind.Member, _, MemberKind.Field)            => "#9CDCFE",
        (NavigationItemKind.Member, _, MemberKind.Event)            => "#DCDCAA",
        _                                                           => "#9B9B9B",
    });

    public override string ToString() => Name;

    private static Brush MakeBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
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
