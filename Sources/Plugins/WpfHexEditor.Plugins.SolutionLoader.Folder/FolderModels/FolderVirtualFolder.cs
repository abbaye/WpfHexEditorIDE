// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.Folder
// File: FolderModels/FolderVirtualFolder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     IVirtualFolder adapter that mirrors a physical subdirectory.
//     Mutable during tree construction (FolderFileEnumerator.EnsureFolder);
//     read-only once returned to the IDE through ISolution.
//
// Architecture Notes:
//     Pattern: Builder (mutable during construction, sealed once handed off)
//     Mirrors VsVirtualFolder from the VS loader plugin.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.SolutionLoader.Folder.FolderModels;

internal sealed class FolderVirtualFolder : IVirtualFolder
{
    private readonly List<string>           _itemIds  = [];
    private readonly List<IVirtualFolder>   _children = [];

    public string                       Id                   { get; } = Guid.NewGuid().ToString();
    public string                       Name                 { get; init; } = string.Empty;
    public string?                      PhysicalRelativePath { get; init; }
    public IReadOnlyList<string>        ItemIds              => _itemIds;
    public IReadOnlyList<IVirtualFolder> Children            => _children;

    /// <summary>Called by <see cref="FolderFileEnumerator"/> while building the tree.</summary>
    internal void AddItemId(string itemId)  => _itemIds.Add(itemId);

    /// <summary>Called by <see cref="FolderFileEnumerator"/> while building the tree.</summary>
    internal void AddChild(FolderVirtualFolder child) => _children.Add(child);
}
