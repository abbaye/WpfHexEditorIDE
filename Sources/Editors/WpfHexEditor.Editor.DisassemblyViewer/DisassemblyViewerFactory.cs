//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Reflection.PortableExecutable;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DisassemblyViewer.Controls;

namespace WpfHexEditor.Editor.DisassemblyViewer;

/// <summary>
/// Factory that registers the <see cref="DisassemblyViewer"/> with the
/// <see cref="IEditorRegistry"/> so the host application can open .NET assemblies
/// automatically by extension. Only opens managed PE files (has CLI metadata).
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
        if (string.IsNullOrEmpty(ext) || !_descriptor.SupportedExtensions.Contains(ext))
            return false;

        // Only claim managed PE files — native DLLs/EXEs should open in HexEditor
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var pe = new PEReader(fs);
            return pe.HasMetadata;
        }
        catch { return false; }
    }

    /// <inheritdoc/>
    public IDocumentEditor Create() => new Controls.DisassemblyViewer();
}

file sealed class DisassemblyViewerDescriptor : IEditorDescriptor
{
    public string Id          => "disassembly-viewer";
    public string DisplayName => "Disassembly Viewer";
    public string Description => "Read-only .NET assembly viewer. Reflects types, members, and metadata from managed PE files.";

    public IReadOnlyList<string> SupportedExtensions => [".dll", ".exe"];
}
