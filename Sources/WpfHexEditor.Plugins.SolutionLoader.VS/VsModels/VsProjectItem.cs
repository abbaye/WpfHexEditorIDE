// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: VsModels/VsProjectItem.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Lightweight IProjectItem implementation for VS project items
//     (Compile, Content, None, EmbeddedResource, etc.)
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.SolutionLoader.VS.VsModels;

internal sealed class VsProjectItem : IProjectItem
{
    public string          Id              { get; init; } = Guid.NewGuid().ToString();
    public string          Name            { get; init; } = string.Empty;
    public string          AbsolutePath    { get; init; } = string.Empty;
    public string          RelativePath    { get; init; } = string.Empty;
    public ProjectItemType ItemType        { get; init; } = ProjectItemType.Binary;
    public string?         VirtualFolderId { get; init; }

    public bool                        IsModified   => false;
    public EditorConfigDto?            EditorConfig { get; set; }
    public IReadOnlyList<IItemLink>    LinkedItems  { get; init; } = [];
    public IReadOnlyList<BookmarkDto>? Bookmarks    { get; set; }
}
