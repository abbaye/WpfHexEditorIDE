// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Services/ClassToolboxRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Registry of predefined toolbox entry templates for the diagram
//     toolbox panel. Provides 8 starter templates covering common OOP
//     patterns: Class, Interface, Abstract, Enum, Struct, Entity,
//     Repository, Service.
//
// Architecture Notes:
//     Pattern: Registry / Catalog.
//     All entries are immutable records. Entries list is pre-built once
//     in the static constructor and exposed as IReadOnlyList.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Services;

/// <summary>
/// A predefined type template available in the diagram toolbox.
/// </summary>
/// <param name="Name">Display name shown in the toolbox panel.</param>
/// <param name="Kind">Default structural kind for the generated node.</param>
/// <param name="DefaultMembers">Member stubs pre-populated when the template is dropped.</param>
public sealed record ToolboxEntry(
    string Name,
    ClassKind Kind,
    IReadOnlyList<ClassMember> DefaultMembers);

/// <summary>
/// Provides the set of predefined toolbox templates available in the class diagram editor.
/// </summary>
public sealed class ClassToolboxRegistry
{
    private static readonly IReadOnlyList<ToolboxEntry> _entries = BuildEntries();

    /// <summary>All available toolbox templates.</summary>
    public IReadOnlyList<ToolboxEntry> Entries => _entries;

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    private static IReadOnlyList<ToolboxEntry> BuildEntries() =>
    [
        // Plain class — no members
        new ToolboxEntry("Class", ClassKind.Class, []),

        // Interface with one template method
        new ToolboxEntry("Interface", ClassKind.Interface,
        [
            new ClassMember
            {
                Name = "Execute",
                Kind = MemberKind.Method,
                TypeName = "void",
                Visibility = MemberVisibility.Public
            }
        ]),

        // Abstract class with one abstract method
        new ToolboxEntry("Abstract Class", ClassKind.Abstract,
        [
            new ClassMember
            {
                Name = "OnExecute",
                Kind = MemberKind.Method,
                TypeName = "void",
                Visibility = MemberVisibility.Protected,
                IsAbstract = true
            }
        ]),

        // Enum — no members (values are added by the user)
        new ToolboxEntry("Enum", ClassKind.Enum, []),

        // Struct — no members
        new ToolboxEntry("Struct", ClassKind.Struct, []),

        // Entity: Id field + Name property + ToString method
        new ToolboxEntry("Entity", ClassKind.Class,
        [
            new ClassMember { Name = "Id",   Kind = MemberKind.Field,    TypeName = "int",    Visibility = MemberVisibility.Private },
            new ClassMember { Name = "Name", Kind = MemberKind.Property, TypeName = "string", Visibility = MemberVisibility.Public },
            new ClassMember { Name = "ToString", Kind = MemberKind.Method, TypeName = "string", Visibility = MemberVisibility.Public }
        ]),

        // Repository interface with CRUD stubs
        new ToolboxEntry("Repository", ClassKind.Interface,
        [
            new ClassMember { Name = "GetAll",    Kind = MemberKind.Method, TypeName = "IEnumerable<T>", Visibility = MemberVisibility.Public },
            new ClassMember { Name = "GetById",   Kind = MemberKind.Method, TypeName = "T",              Visibility = MemberVisibility.Public, Parameters = ["int id"] },
            new ClassMember { Name = "Add",       Kind = MemberKind.Method, TypeName = "void",           Visibility = MemberVisibility.Public, Parameters = ["T entity"] },
            new ClassMember { Name = "Remove",    Kind = MemberKind.Method, TypeName = "void",           Visibility = MemberVisibility.Public, Parameters = ["int id"] }
        ]),

        // Service class with a dependency field and Execute method
        new ToolboxEntry("Service", ClassKind.Class,
        [
            new ClassMember { Name = "_dependency", Kind = MemberKind.Field,  TypeName = "IDependency", Visibility = MemberVisibility.Private },
            new ClassMember { Name = "Execute",     Kind = MemberKind.Method, TypeName = "void",        Visibility = MemberVisibility.Public }
        ])
    ];
}
