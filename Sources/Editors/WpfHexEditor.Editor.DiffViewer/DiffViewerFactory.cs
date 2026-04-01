//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DiffViewer.Controls;

namespace WpfHexEditor.Editor.DiffViewer;

/// <summary>
/// Factory for <see cref="DiffViewer"/>.
/// The diff viewer is typically opened from <c>Tools &gt; Compare Files…</c>
/// rather than by file extension, so <see cref="SupportedExtensions"/> is empty.
/// </summary>
public sealed class DiffViewerFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new DiffViewerDescriptor();

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath) => false;  // opened from menu, not by extension

    /// <inheritdoc/>
    public IDocumentEditor Create() => new Controls.DiffViewer();
}

file sealed class DiffViewerDescriptor : IEditorDescriptor
{
    public string Id          => "diff-viewer";
    public string DisplayName => "Diff Viewer";
    public string Description => "Side-by-side binary diff comparison. Open from Tools > Compare Files.";

    public IReadOnlyList<string> SupportedExtensions => [];
}
