// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlDesignerFactory.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     IEditorFactory implementation for the XAML Designer.
//     Creates XamlDesignerSplitHost instances for .xaml files.
//     Syntax highlighting is injected lazily in OpenAsync via the
//     CodeEditorSplitHost's own language-registry lookup.
//
// Architecture Notes:
//     Factory Pattern. Register at application startup via
//     _editorRegistry.Register(new XamlDesignerFactory()).
// ==========================================================

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.XamlDesigner.Controls;

namespace WpfHexEditor.Editor.XamlDesigner;

/// <summary>
/// <see cref="IEditorFactory"/> that creates <see cref="XamlDesignerSplitHost"/> instances
/// for <c>.xaml</c> files.
/// </summary>
public sealed class XamlDesignerFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new XamlDesignerDescriptor();

    public IEditorDescriptor Descriptor => _descriptor;

    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return ext == ".xaml";
    }

    /// <summary>
    /// Creates a new <see cref="XamlDesignerSplitHost"/>.
    /// Syntax highlighting is resolved from the LanguageRegistry in
    /// <see cref="IOpenableDocument.OpenAsync"/> when the file path is known.
    /// </summary>
    public IDocumentEditor Create() => new XamlDesignerSplitHost();
}

file sealed class XamlDesignerDescriptor : IEditorDescriptor
{
    public string Id          => "xaml-designer";
    public string DisplayName => "XAML Designer";
    public string Description => "Visual XAML designer with live split-pane design surface and property inspector.";
    public IReadOnlyList<string> SupportedExtensions => [".xaml"];
}
