//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.JsonEditor.Controls;

namespace WpfHexEditor.Editor.JsonEditor;

/// <summary>
/// <see cref="IEditorFactory"/> for the JSON / WHJSON editor.
/// Register at application startup:
/// <code>EditorRegistry.Instance.Register(new JsonEditorFactory());</code>
/// </summary>
public sealed class JsonEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new JsonEditorDescriptor();

    public IEditorDescriptor Descriptor => _descriptor;

    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return ext is ".json" or ".whjson";
    }

    public IDocumentEditor Create() => new Controls.JsonEditor();
}

file sealed class JsonEditorDescriptor : IEditorDescriptor
{
    public string Id          => "json-editor";
    public string DisplayName => "JSON Editor";
    public string Description => "JSON / WhjSON format definition editor";
    public IReadOnlyList<string> SupportedExtensions => [".json", ".whjson"];
}
