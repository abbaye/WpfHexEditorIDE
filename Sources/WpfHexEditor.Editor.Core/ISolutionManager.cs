//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Central service that owns the lifecycle of the active solution.
/// Implemented as a singleton in WpfHexEditor.ProjectSystem; the host
/// (MainWindow) references it via this interface so it remains decoupled
/// from the implementation assembly.
/// </summary>
public interface ISolutionManager
{
    // ── State ────────────────────────────────────────────────────────────
    ISolution? CurrentSolution { get; }

    /// <summary>Paths of the 10 most-recently used solutions (MRU list).</summary>
    IReadOnlyList<string> RecentSolutions { get; }

    /// <summary>Paths of the 10 most-recently used standalone files.</summary>
    IReadOnlyList<string> RecentFiles { get; }

    // ── Solution lifecycle ───────────────────────────────────────────────
    Task<ISolution> CreateSolutionAsync(string directory, string name, CancellationToken ct = default);
    Task<ISolution> OpenSolutionAsync(string filePath, CancellationToken ct = default);
    Task SaveSolutionAsync(ISolution solution, CancellationToken ct = default);
    Task CloseSolutionAsync(CancellationToken ct = default);

    // ── Project management ───────────────────────────────────────────────
    Task<IProject> CreateProjectAsync(ISolution solution, string directory, string name, CancellationToken ct = default);
    Task<IProject> AddExistingProjectAsync(ISolution solution, string projectFilePath, CancellationToken ct = default);
    Task SaveProjectAsync(IProject project, CancellationToken ct = default);
    Task RemoveProjectAsync(ISolution solution, IProject project, CancellationToken ct = default);

    // ── Item management ──────────────────────────────────────────────────
    Task<IProjectItem> AddItemAsync(IProject project, string absolutePath, string? virtualFolderId = null, CancellationToken ct = default);
    Task<IProjectItem> CreateItemAsync(IProject project, string name, ProjectItemType type, string? virtualFolderId = null, byte[]? initialContent = null, CancellationToken ct = default);
    Task RemoveItemAsync(IProject project, IProjectItem item, bool deleteFromDisk = false, CancellationToken ct = default);
    Task RenameItemAsync(IProject project, IProjectItem item, string newName, CancellationToken ct = default);
    /// <summary>
    /// Moves <paramref name="item"/> to <paramref name="targetFolderId"/> within the same project.
    /// Pass <see langword="null"/> for <paramref name="targetFolderId"/> to move to the project root (no folder).
    /// </summary>
    Task MoveItemToFolderAsync(IProject project, IProjectItem item, string? targetFolderId, CancellationToken ct = default);

    // ── Folder CRUD ───────────────────────────────────────────────────────
    /// <summary>
    /// Creates a virtual folder in the project, optionally also creating the corresponding
    /// physical directory on disk.  Persists the project immediately.
    /// </summary>
    Task<IVirtualFolder> CreateFolderAsync(IProject project, string name,
        string? parentFolderId = null, bool createPhysical = false,
        CancellationToken ct = default);

    /// <summary>
    /// Renames a virtual folder.  If the folder has a physical counterpart,
    /// the directory is renamed on disk and <see cref="IVirtualFolder.PhysicalRelativePath"/> is updated.
    /// </summary>
    Task RenameFolderAsync(IProject project, IVirtualFolder folder,
        string newName, CancellationToken ct = default);

    /// <summary>
    /// Removes a virtual folder from the project tree.  All items it contained (recursively)
    /// become unclassified (visible at project root). The physical directory is NOT deleted.
    /// </summary>
    Task DeleteFolderAsync(IProject project, IVirtualFolder folder,
        CancellationToken ct = default);

    /// <summary>
    /// Recursively imports a physical directory as a virtual folder hierarchy:
    /// creates matching virtual sub-folders and registers all files as project items.
    /// </summary>
    Task<IVirtualFolder> AddFolderFromDiskAsync(IProject project, string physicalPath,
        string? parentVirtualFolderId = null, CancellationToken ct = default);

    // ── Modification tracking ─────────────────────────────────────────────
    /// <summary>
    /// Stores <paramref name="modifications"/> (IPS patch bytes) for the given project item
    /// and persists the project file immediately.
    /// Pass <see langword="null"/> to clear pending modifications.
    /// </summary>
    Task PersistItemModificationsAsync(IProject project, IProjectItem item,
                                       byte[]? modifications, CancellationToken ct = default);

    /// <summary>
    /// Returns the raw IPS patch bytes previously stored for <paramref name="item"/>,
    /// or <see langword="null"/> when the item has no pending modifications.
    /// </summary>
    byte[]? GetItemModifications(IProject project, IProjectItem item);

    // ── TBL helpers ──────────────────────────────────────────────────────
    /// <summary>Designates <paramref name="tblItem"/> as the default TBL for <paramref name="project"/>.
    /// Pass <see langword="null"/> to clear the designation.</summary>
    void SetDefaultTbl(IProject project, IProjectItem? tblItem);

    // ── MRU helpers ──────────────────────────────────────────────────────
    void PushRecentFile(string absolutePath);

    // ── Events ───────────────────────────────────────────────────────────
    event EventHandler<SolutionChangedEventArgs>?      SolutionChanged;
    event EventHandler<ProjectChangedEventArgs>?       ProjectChanged;
    event EventHandler<ProjectItemEventArgs>?          ItemAdded;
    event EventHandler<ProjectItemEventArgs>?          ItemRemoved;
}
