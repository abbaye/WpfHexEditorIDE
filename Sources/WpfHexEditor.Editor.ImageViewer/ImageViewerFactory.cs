//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.ImageViewer.Controls;

namespace WpfHexEditor.Editor.ImageViewer;

/// <summary>
/// Factory that registers the <see cref="ImageViewer"/> with the
/// <see cref="IEditorRegistry"/> so the host application can open image
/// files automatically by extension.
/// </summary>
public sealed class ImageViewerFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new ImageViewerDescriptor();

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return !string.IsNullOrEmpty(ext) && _descriptor.SupportedExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    public IDocumentEditor Create() => new Controls.ImageViewer();
}

file sealed class ImageViewerDescriptor : IEditorDescriptor
{
    public string Id          => "image-viewer";
    public string DisplayName => "Image Viewer";
    public string Description => "Read-only image viewer with zoom, pan and pixel inspection.";

    public IReadOnlyList<string> SupportedExtensions =>
    [
        ".png", ".bmp", ".jpg", ".jpeg", ".gif", ".ico",
        ".tiff", ".tif", ".webp", ".tga",
        // ".dds" — WPF BitmapImage cannot load DDS natively; falls back to HexEditor
    ];
}
