//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.ScriptEditor.Controls;

namespace WpfHexEditor.Editor.ScriptEditor;

/// <summary>
/// Factory that registers the <see cref="ScriptEditor"/> with the
/// <see cref="IEditorRegistry"/> so the host application can open game script
/// files automatically by extension.
/// </summary>
public sealed class ScriptEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new ScriptEditorDescriptor();

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return !string.IsNullOrEmpty(ext) && _descriptor.SupportedExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    public IDocumentEditor Create() => new Controls.ScriptEditor();
}

file sealed class ScriptEditorDescriptor : IEditorDescriptor
{
    public string Id          => "script-editor";
    public string DisplayName => "Script Editor";
    public string Description => "Game script editor stub. Extends TextEditor with TBL encoding support. Planned for a future sprint.";

    public IReadOnlyList<string> SupportedExtensions =>
    [
        ".scr", ".msg", ".evt", ".script", ".dec",
    ];
}
