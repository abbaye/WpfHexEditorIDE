//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.EntropyViewer.Controls;

namespace WpfHexEditor.Editor.EntropyViewer;

/// <summary>
/// Factory that registers <see cref="EntropyViewer"/> with the
/// <see cref="IEditorRegistry"/>. Opens any binary/data file for entropy analysis.
/// </summary>
public sealed class EntropyViewerFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new EntropyViewerDescriptor();

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return !string.IsNullOrEmpty(ext) && _descriptor.SupportedExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    public IDocumentEditor Create() => new Controls.EntropyViewer();
}

file sealed class EntropyViewerDescriptor : IEditorDescriptor
{
    public string Id          => "entropy-viewer";
    public string DisplayName => "Entropy Viewer";
    public string Description => "Entropy and byte-distribution analysis for binary files.";

    public IReadOnlyList<string> SupportedExtensions => [];
    // Entropy viewer is typically opened from Tools menu, not by extension.
    // Leave empty so it does not intercept files intended for HexEditor.
}
