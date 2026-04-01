//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
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
    /// <summary>
    /// Stable GUID-based identifier, unique within the project.
    /// </summary>
    string Id { get; }

    string Name { get; }

    /// <summary>
    /// Path relative to the .whproj file directory.
    /// </summary>
    string RelativePath { get; }

    /// <summary>
    /// Fully-resolved absolute path on the current machine.
    /// </summary>
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

    /// <summary>
    /// Typed links to other project items (e.g. TBL tables, patch files, format definitions).
    /// Replaces the legacy single-target <c>targetItemId</c> field.
    /// </summary>
    IReadOnlyList<IItemLink> LinkedItems { get; }

    /// <summary>
    /// Saved bookmarks for this item. <see langword="null"/> or empty when no bookmarks exist.
    /// Written by the host when the document tab is closed; restored on re-open via
    /// <see cref="IEditorPersistable.ApplyBookmarks"/>.
    /// </summary>
    IReadOnlyList<BookmarkDto>? Bookmarks { get; set; }
}
