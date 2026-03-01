//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.TblEditor.Controls;

namespace WpfHexEditor.Editor.TblEditor;

/// <summary>
/// <see cref="IPropertyProvider"/> that surfaces the selected TBL entry and
/// file statistics in the Properties panel (F4).
/// </summary>
internal sealed class TblEditorPropertyProvider : IPropertyProvider
{
    private readonly Controls.TblEditor _editor;

    public TblEditorPropertyProvider(Controls.TblEditor editor)
    {
        _editor = editor;
        _editor.SelectionChanged += (_, _) =>
            PropertiesChanged?.Invoke(this, EventArgs.Empty);
    }

    public string ContextLabel
    {
        get
        {
            var entry = _editor.SelectedEntry;
            var title = _editor.Title;
            return entry is null ? title : $"{title} — {entry.Entry}={entry.Value}";
        }
    }

    public event EventHandler? PropertiesChanged;

    public IReadOnlyList<PropertyGroup> GetProperties()
    {
        var groups = new List<PropertyGroup>();

        // ── Entry group (only when a row is selected) ──────────────────────
        var entry = _editor.SelectedEntry;
        if (entry is not null)
        {
            groups.Add(new PropertyGroup
            {
                Name = "Entry",
                Entries =
                [
                    new PropertyEntry
                    {
                        Name        = "Code (hex)",
                        Value       = entry.Entry,
                        Type        = PropertyEntryType.Text,
                        Description = "Hexadecimal code of the selected TBL entry."
                    },
                    new PropertyEntry
                    {
                        Name        = "Character",
                        Value       = entry.Value,
                        Type        = PropertyEntryType.Text,
                        Description = "Character or text mapped to this code."
                    },
                    new PropertyEntry
                    {
                        Name        = "Type",
                        Value       = entry.Type.ToString(),
                        Type        = PropertyEntryType.Text,
                        Description = "Entry category: ASCII, DTE, MTE, EndLine, EndBlock, …"
                    },
                ]
            });
        }

        // ── File statistics group ──────────────────────────────────────────
        var src          = _editor.Source;
        var fileEntries  = new List<PropertyEntry>
        {
            new PropertyEntry
            {
                Name        = "File",
                Value       = src?.FileName is { Length: > 0 } fn ? fn : "(unsaved)",
                Type        = PropertyEntryType.FilePath,
                Description = "Path of the open TBL file."
            },
            new PropertyEntry { Name = "Total entries", Value = _editor.EntryCount,
                                Description = "Number of entries in the table." },
        };

        if (src is not null)
        {
            fileEntries.Add(new PropertyEntry { Name = "ASCII",     Value = src.TotalAscii,    Description = "ASCII-type entries." });
            fileEntries.Add(new PropertyEntry { Name = "DTE",       Value = src.TotalDte,      Description = "Dual-Title Encoding entries." });
            fileEntries.Add(new PropertyEntry { Name = "MTE",       Value = src.TotalMte,      Description = "Multi-Title Encoding entries." });
            fileEntries.Add(new PropertyEntry { Name = "End Line",  Value = src.TotalEndLine,  Description = "End-of-line entries." });
            fileEntries.Add(new PropertyEntry { Name = "End Block", Value = src.TotalEndBlock, Description = "End-of-block entries." });
        }

        groups.Add(new PropertyGroup { Name = "File", Entries = fileEntries });
        return groups;
    }
}
