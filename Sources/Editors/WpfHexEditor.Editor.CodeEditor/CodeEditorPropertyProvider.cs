//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Editor.Core;
using CodeEditorControl = WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor;

namespace WpfHexEditor.Editor.CodeEditor;

/// <summary>
/// <see cref="IPropertyProvider"/> for the JSON editor.
/// Surfaces document-level statistics in the Properties panel (F4).
/// Node-level path info is not yet exposed by the editor API.
/// </summary>
internal sealed class CodeEditorPropertyProvider : IPropertyProvider
{
    private readonly CodeEditorControl _editor;

    public CodeEditorPropertyProvider(CodeEditorControl editor)
    {
        _editor = editor;
        _editor.SelectionChanged += (_, _) =>
            PropertiesChanged?.Invoke(this, EventArgs.Empty);
    }

    public string ContextLabel => _editor.Title;

    public event EventHandler? PropertiesChanged;

    public IReadOnlyList<PropertyGroup> GetProperties()
    {
        var text  = _editor.GetText();
        var lines = text.Split('\n').Length;
        var bytes = System.Text.Encoding.UTF8.GetByteCount(text);

        return
        [
            new PropertyGroup
            {
                Name = "Document",
                Entries =
                [
                    new PropertyEntry { Name = "Lines",      Value = lines,
                                        Description = "Number of lines in the document." },
                    new PropertyEntry { Name = "Characters", Value = text.Length,
                                        Description = "Total character count." },
                    new PropertyEntry { Name = "Size (UTF-8)", Value = FormatSize(bytes),
                                        Description = "Encoded size in UTF-8 bytes." },
                    new PropertyEntry { Name = "Errors",    Value = _editor.ValidationErrorCount,
                                        Description = "Number of JSON validation errors." },
                    new PropertyEntry { Name = "Warnings",  Value = _editor.ValidationWarningCount,
                                        Description = "Number of JSON validation warnings." },
                ]
            }
        ];
    }

    private static string FormatSize(int bytes) =>
        bytes switch
        {
            < 1024           => $"{bytes} B",
            < 1024 * 1024    => $"{bytes / 1024.0:F1} KB",
            _                => $"{bytes / (1024.0 * 1024):F2} MB"
        };
}
