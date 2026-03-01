//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// A virtual (logical) folder inside a project. Virtual folders exist only
/// in the project file; they do not necessarily correspond to physical directories
/// on disk. They are used to organise project items in the Solution Explorer tree.
/// </summary>
public interface IVirtualFolder
{
    string Id   { get; }
    string Name { get; }

    /// <summary>Ids of <see cref="IProjectItem"/> instances directly under this folder.</summary>
    IReadOnlyList<string> ItemIds { get; }

    /// <summary>Nested sub-folders.</summary>
    IReadOnlyList<IVirtualFolder> Children { get; }

    /// <summary>
    /// Path relative to the .whproj directory of the physical directory backing this folder,
    /// or <see langword="null"/> when the folder is purely logical (no physical counterpart).
    /// </summary>
    string? PhysicalRelativePath { get; }
}
