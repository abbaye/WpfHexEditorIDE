//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// A WpfHexEditor project (.whproj). A project groups related files
/// (binaries, format definitions, patches, TBL tables …) under a common
/// root directory and stores per-file metadata.
/// </summary>
public interface IProject
{
    string Id   { get; }
    string Name { get; }

    /// <summary>Absolute path to the .whproj file.</summary>
    string ProjectFilePath { get; }

    /// <summary>All items registered in this project, regardless of virtual folder.</summary>
    IReadOnlyList<IProjectItem> Items { get; }

    /// <summary>Top-level virtual folder nodes. Items not in any folder are accessed via <see cref="Items"/>.</summary>
    IReadOnlyList<IVirtualFolder> RootFolders { get; }

    bool IsModified { get; }

    /// <summary>Returns the item with the given id, or <see langword="null"/>.</summary>
    IProjectItem? FindItem(string id);

    /// <summary>Returns the item whose <see cref="IProjectItem.AbsolutePath"/> matches, or <see langword="null"/>.</summary>
    IProjectItem? FindItemByPath(string absolutePath);
}
