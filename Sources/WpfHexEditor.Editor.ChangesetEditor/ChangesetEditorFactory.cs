// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Windows;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.ChangesetEditor.Controls;

namespace WpfHexEditor.Editor.ChangesetEditor;

/// <summary>
/// Factory that registers this editor for <c>.whchg</c> companion files.
/// </summary>
public sealed class ChangesetEditorFactory : IEditorFactory
{
    public bool CanOpen(string filePath)
        => filePath.EndsWith(".whchg", StringComparison.OrdinalIgnoreCase);

    public UIElement Create(string filePath)
    {
        var editor = new ChangesetEditorControl();
        editor.OpenFile(filePath);
        return editor;
    }
}
