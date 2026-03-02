//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.TileEditor.Controls;

namespace WpfHexEditor.Editor.TileEditor;

/// <summary>
/// Factory that registers the <see cref="TileEditor"/> with the
/// <see cref="IEditorRegistry"/> so the host application can open tile graphics
/// files automatically by extension.
/// </summary>
public sealed class TileEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new TileEditorDescriptor();

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return !string.IsNullOrEmpty(ext) && _descriptor.SupportedExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    public IDocumentEditor Create() => new Controls.TileEditor();
}

file sealed class TileEditorDescriptor : IEditorDescriptor
{
    public string Id          => "tile-editor";
    public string DisplayName => "Tile Editor";
    public string Description => "NES/GBA/GB tile graphics editor stub. Planned for a future sprint.";

    public IReadOnlyList<string> SupportedExtensions =>
    [
        ".chr", ".til", ".gfx",
    ];
}
