//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DisassemblyViewer.Controls;

namespace WpfHexEditor.Editor.DisassemblyViewer;

/// <summary>
/// Factory that registers the <see cref="DisassemblyViewer"/> with the
/// <see cref="IEditorRegistry"/> so the host application can open binary/executable
/// files automatically by extension.
/// </summary>
public sealed class DisassemblyViewerFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new DisassemblyViewerDescriptor();

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return !string.IsNullOrEmpty(ext) && _descriptor.SupportedExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    public IDocumentEditor Create() => new Controls.DisassemblyViewer();
}

file sealed class DisassemblyViewerDescriptor : IEditorDescriptor
{
    public string Id          => "disassembly-viewer";
    public string DisplayName => "Disassembly Viewer";
    public string Description => "Read-only disassembly viewer stub. Planned for a future sprint (requires Iced/Capstone.NET).";

    /// <summary>
    /// Empty while DisassemblyViewer is a stub — binary/ROM files fall through to HexEditor.
    /// Populate once Iced/Capstone.NET integration is complete.
    /// </summary>
    public IReadOnlyList<string> SupportedExtensions => [];
}
