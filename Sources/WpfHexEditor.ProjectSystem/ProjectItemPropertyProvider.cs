//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem;

/// <summary>
/// <see cref="IPropertyProvider"/> for an <see cref="IProjectItem"/> selected
/// in the Solution Explorer. Shown in the Properties panel (F4).
/// Properties do not change while this instance is alive, so
/// <see cref="PropertiesChanged"/> is never raised.
/// </summary>
public sealed class ProjectItemPropertyProvider : IPropertyProvider
{
    private readonly IProjectItem _item;

    public ProjectItemPropertyProvider(IProjectItem item) => _item = item;

    public string ContextLabel => $"{_item.Name} — {_item.ItemType}";

    public event EventHandler? PropertiesChanged { add { } remove { } }

    public IReadOnlyList<PropertyGroup> GetProperties()
    {
        var groups = new List<PropertyGroup>();

        // ── File group ─────────────────────────────────────────────────────
        FileInfo? info = null;
        try { if (File.Exists(_item.AbsolutePath)) info = new FileInfo(_item.AbsolutePath); }
        catch { /* ignore I/O errors */ }

        var fileEntries = new List<PropertyEntry>
        {
            new PropertyEntry { Name = "Name",     Value = _item.Name,         Description = "Display name inside the project." },
            new PropertyEntry { Name = "Type",     Value = _item.ItemType.ToString(), Description = "Logical item type." },
            new PropertyEntry { Name = "Path",     Value = _item.AbsolutePath, Type = PropertyEntryType.FilePath,
                                Description = "Absolute path on disk." },
            new PropertyEntry { Name = "Relative", Value = _item.RelativePath, Description = "Path relative to the .whproj directory." },
        };

        if (info is not null)
        {
            fileEntries.Add(new PropertyEntry { Name = "Size",     Value = FormatSize(info.Length),
                                                Description = "File size on disk." });
            fileEntries.Add(new PropertyEntry { Name = "Modified", Value = info.LastWriteTime.ToString("g"),
                                                Description = "Last modification date and time." });
        }

        groups.Add(new PropertyGroup { Name = "File", Entries = fileEntries });

        // ── Editor config group (if saved) ─────────────────────────────────
        var cfg = _item.EditorConfig;
        if (cfg is not null)
        {
            var cfgEntries = new List<PropertyEntry>();
            if (cfg.BytesPerLine > 0)
                cfgEntries.Add(new PropertyEntry { Name = "Bytes/line", Value = cfg.BytesPerLine,
                                                   Description = "Saved bytes-per-line setting." });
            if (!string.IsNullOrEmpty(cfg.EditMode))
                cfgEntries.Add(new PropertyEntry { Name = "Edit mode",  Value = cfg.EditMode,
                                                   Description = "Saved editor mode (Insert/Overwrite)." });
            if (!string.IsNullOrEmpty(cfg.FormatId))
                cfgEntries.Add(new PropertyEntry { Name = "Format",     Value = cfg.FormatId,
                                                   Description = "Detected or assigned format definition." });
            if (!string.IsNullOrEmpty(cfg.Encoding))
                cfgEntries.Add(new PropertyEntry { Name = "Encoding",   Value = cfg.Encoding,
                                                   Description = "Text encoding used for display." });

            if (cfgEntries.Count > 0)
                groups.Add(new PropertyGroup { Name = "Editor config", Entries = cfgEntries });
        }

        return groups;
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1024        => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _             => $"{bytes / (1024.0 * 1024):F2} MB"
        };
}
