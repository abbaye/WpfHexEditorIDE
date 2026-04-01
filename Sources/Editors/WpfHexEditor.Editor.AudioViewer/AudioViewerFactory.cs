//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.AudioViewer.Controls;

namespace WpfHexEditor.Editor.AudioViewer;

/// <summary>
/// Factory that registers the <see cref="AudioViewer"/> with the
/// <see cref="IEditorRegistry"/> so the host application can open audio files
/// automatically by extension.
/// </summary>
public sealed class AudioViewerFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new AudioViewerDescriptor();

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return !string.IsNullOrEmpty(ext) && _descriptor.SupportedExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    public IDocumentEditor Create() => new Controls.AudioViewer();
}

file sealed class AudioViewerDescriptor : IEditorDescriptor
{
    public string Id          => "audio-viewer";
    public string DisplayName => "Audio Viewer";
    public string Description => "Read-only audio viewer stub. Planned for a future sprint (requires NAudio).";

    public IReadOnlyList<string> SupportedExtensions =>
    [
        ".wav", ".mp3", ".ogg", ".flac", ".xm",
        ".mod", ".it", ".s3m", ".aiff",
    ];
}
