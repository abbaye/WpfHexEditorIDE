//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.JsonEditor
// File: JsonEditorFactory.cs
// Description:
//     Factory that registers the JsonEditor with the IEditorRegistry
//     for .json and .jsonc files. Higher priority than CodeEditor for
//     these extensions due to JSON-specific toolbar commands.
// Architecture: IEditorFactory pattern — Create() returns a new JsonEditor instance.
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.JsonEditor.Controls;

namespace WpfHexEditor.Editor.JsonEditor;

public sealed class JsonEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new JsonEditorDescriptor();

    public IEditorDescriptor Descriptor => _descriptor;

    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return !string.IsNullOrEmpty(ext) && _descriptor.SupportedExtensions.Contains(ext);
    }

    public IDocumentEditor Create() => new JsonEditorControl();
}

file sealed class JsonEditorDescriptor : IEditorDescriptor
{
    public string Id          => "json-editor";
    public string DisplayName => "JSON Editor";
    public string Description => "Dedicated JSON/JSONC editor with format, minify, and validation.";

    public IReadOnlyList<string> SupportedExtensions =>
    [
        ".json",
        ".jsonc",
    ];
}
