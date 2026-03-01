//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Represents a single file entry inside a WpfHexEditor project (.whproj).
/// A project item links a physical file on disk to its in-project metadata
/// (editor configuration, bookmarks, unsaved modifications …).
/// </summary>
public interface IProjectItem
{
    /// <summary>Stable GUID-based identifier, unique within the project.</summary>
    string Id { get; }

    string Name { get; }

    /// <summary>Path relative to the .whproj file directory.</summary>
    string RelativePath { get; }

    /// <summary>Fully-resolved absolute path on the current machine.</summary>
    string AbsolutePath { get; }

    ProjectItemType ItemType { get; }

    /// <summary><see langword="true"/> when in-memory modifications exist that are not yet
    /// written to <see cref="AbsolutePath"/>.</summary>
    bool IsModified { get; }

    /// <summary>
    /// Last-saved editor state for this item. Written by the host when the
    /// document tab is closed; applied when the file is re-opened.
    /// </summary>
    EditorConfigDto? EditorConfig { get; set; }
}
