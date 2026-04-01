// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Collections.Generic;
using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.ChangesetEditor.Controls;

namespace WpfHexEditor.Editor.ChangesetEditor;

/// <summary>
/// Factory that registers this editor for <c>.whchg</c> companion files.
/// </summary>
public sealed class ChangesetEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new ChangesetEditorDescriptor();

    public IEditorDescriptor Descriptor => _descriptor;

    public bool CanOpen(string filePath)
        => Path.GetExtension(filePath).Equals(".whchg", StringComparison.OrdinalIgnoreCase);

    public IDocumentEditor Create() => new ChangesetEditorControl();
}

file sealed class ChangesetEditorDescriptor : IEditorDescriptor
{
    public string Id          => "changeset-editor";
    public string DisplayName => "Changeset Editor";
    public string Description => "Viewer and editor for .whchg companion changeset files";
    public IReadOnlyList<string> SupportedExtensions => [".whchg"];
}
