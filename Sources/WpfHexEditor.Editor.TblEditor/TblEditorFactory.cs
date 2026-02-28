using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.TblEditor.Controls;

namespace WpfHexEditor.TblEditor;

/// <summary>
/// Optional <see cref="IEditorFactory"/> for the TBL editor.
/// Register this at application startup when plug-in auto-discovery is desired:
/// <code>EditorRegistry.Instance.Register(new TblEditorFactory());</code>
/// When plug-in support is not needed, simply instantiate <see cref="TblEditorControl"/> directly.
/// </summary>
public sealed class TblEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new TblEditorDescriptor();

    public IEditorDescriptor Descriptor => _descriptor;

    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return ext is ".tbl" or ".tblx";
    }

    public IDocumentEditor Create() => new TblEditorControl();
}

file sealed class TblEditorDescriptor : IEditorDescriptor
{
    public string Id          => "tbl-editor";
    public string DisplayName => "TBL Editor";
    public string Description => "Character table editor for game ROM translation projects";
    public IReadOnlyList<string> SupportedExtensions => [".tbl", ".tblx"];
}
