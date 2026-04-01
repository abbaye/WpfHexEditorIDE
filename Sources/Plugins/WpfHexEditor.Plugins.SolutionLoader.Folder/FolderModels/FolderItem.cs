// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.Folder
// File: FolderModels/FolderItem.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     IProjectItem adapter for a physical file discovered during folder enumeration.
//     Mirrors VsProjectItem structure — init-only, immutable after construction.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.SolutionLoader.Folder.FolderModels;

internal sealed class FolderItem : IProjectItem
{
    public string          Id              { get; init; } = Guid.NewGuid().ToString();
    public string          Name            { get; init; } = string.Empty;
    public string          AbsolutePath    { get; init; } = string.Empty;
    public string          RelativePath    { get; init; } = string.Empty;
    public ProjectItemType ItemType        { get; init; } = ProjectItemType.Binary;

    /// <summary>Id of the <see cref="FolderVirtualFolder"/> containing this item, or null if at root.</summary>
    public string?         VirtualFolderId { get; init; }

    public bool                        IsModified   => false;
    public EditorConfigDto?            EditorConfig { get; set; }
    public IReadOnlyList<IItemLink>    LinkedItems  { get; init; } = [];
    public IReadOnlyList<BookmarkDto>? Bookmarks    { get; set; }
}
