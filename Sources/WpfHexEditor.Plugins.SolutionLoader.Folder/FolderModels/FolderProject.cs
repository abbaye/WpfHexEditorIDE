// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.Folder
// File: FolderModels/FolderProject.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     IProject adapter representing one opened folder on disk.
//     ProjectFilePath points to the .whfolder marker file.
//     ProjectType = "Folder" so SolutionExplorerNodeVm can render
//     a folder icon instead of a project icon.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.SolutionLoader.Folder.FolderModels;

internal sealed class FolderProject : IProject
{
    public string                       Id              { get; } = Guid.NewGuid().ToString();
    public string                       Name            { get; init; } = string.Empty;
    public string                       ProjectFilePath { get; init; } = string.Empty;
    public IReadOnlyList<IProjectItem>  Items           { get; init; } = [];
    public IReadOnlyList<IVirtualFolder> RootFolders    { get; init; } = [];
    public bool                         IsModified      => false;
    public string?                      DefaultTblItemId => null;
    public string?                      ProjectType     { get; set; } = "Folder";

    public IProjectItem? FindItem(string id)
        => Items.FirstOrDefault(i => i.Id == id);

    public IProjectItem? FindItemByPath(string absolutePath)
        => Items.FirstOrDefault(i =>
            string.Equals(i.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase));
}
