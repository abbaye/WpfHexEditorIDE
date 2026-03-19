// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: ClassDiagramFactory.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     IEditorFactory implementation that registers the class diagram
//     editor with the WpfHexEditor IDE's editor registry.
//     Opens .classdiagram files and instantiates ClassDiagramSplitHost.
//
// Architecture Notes:
//     Pattern: Factory Method.
//     Descriptor is a file-scoped class to keep the factory self-contained.
//     CanOpen uses Path.GetExtension for fast extension-only matching.
// ==========================================================

using System.IO;
using WpfHexEditor.Editor.ClassDiagram.Controls;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.ClassDiagram;

/// <summary>
/// Registers the class diagram editor with the IDE's <see cref="IEditorRegistry"/>.
/// </summary>
public sealed class ClassDiagramFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new ClassDiagramDescriptor();

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        string? ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return !string.IsNullOrEmpty(ext) && _descriptor.SupportedExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    public IDocumentEditor Create() => new ClassDiagramSplitHost();
}

file sealed class ClassDiagramDescriptor : IEditorDescriptor
{
    public string Id          => "class-diagram-editor";
    public string DisplayName => "Class Diagram Editor";
    public string Description =>
        "VS-Like interactive class diagram editor. Supports .classdiagram DSL files with a " +
        "bidirectional split view (DSL text pane + rendered canvas), undo/redo, snap-to-grid, " +
        "and C# / Mermaid / SVG / PNG export.";

    public IReadOnlyList<string> SupportedExtensions => [".classdiagram"];
}
