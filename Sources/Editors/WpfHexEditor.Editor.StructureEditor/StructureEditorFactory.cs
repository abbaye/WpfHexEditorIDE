//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.StructureEditor.Controls;

namespace WpfHexEditor.Editor.StructureEditor;

/// <summary>
/// Factory that registers the <see cref="StructureEditor"/> with the
/// <see cref="IEditorRegistry"/> so the host application can open .whfmt
/// structure definition files automatically by extension.
/// </summary>
public sealed class StructureEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new StructureEditorDescriptor();

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return !string.IsNullOrEmpty(ext) && _descriptor.SupportedExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    public IDocumentEditor Create() => new Controls.StructureEditor();
}

file sealed class StructureEditorDescriptor : IEditorDescriptor
{
    public string Id          => "structure-editor";
    public string DisplayName => "Structure Editor";
    public string Description => "Visual editor for .whfmt structure definition files. Edit blocks, types, offsets, lengths and colors in a DataGrid.";

    public IReadOnlyList<string> SupportedExtensions =>
    [
        ".whfmt",
    ];
}
