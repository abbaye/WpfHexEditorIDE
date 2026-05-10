// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Model/CodeUsing.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Immutable IR descriptor for a using/import directive.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

/// <summary>Immutable IR descriptor for a single using/import directive.</summary>
public sealed record CodeUsing
{
    /// <summary>Imported namespace name (e.g. <c>System.Collections.Generic</c>).</summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Optional alias when the using is a renaming directive
    /// (e.g. <c>using Foo = Bar.Baz;</c>).
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>Whether this is a static import (<c>using static</c>).</summary>
    public bool IsStatic { get; init; }

    /// <summary>Whether the import is global (C# 10+ <c>global using</c>).</summary>
    public bool IsGlobal { get; init; }
}
