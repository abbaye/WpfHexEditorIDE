//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
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

    /// <summary>
    /// Paths of the 10 most-recently used solutions (MRU list).
    /// </summary>
    IReadOnlyList<string> RecentSolutions { get; }

    /// <summary>
    /// Paths of the 10 most-recently used standalone files.
    /// </summary>
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
    /// <summary>
    /// Renames the solution: updates the model, renames the .whsln file on disk,
    /// and re-saves the solution file.
    /// </summary>
    Task RenameSolutionAsync(ISolution solution, string newName, CancellationToken ct = default);

    /// <summary>
    /// Renames the project: updates the model, renames the .whproj file on disk,
    /// and re-saves the solution to persist the new file path.
    /// </summary>
    Task RenameProjectAsync(IProject project, string newName, CancellationToken ct = default);

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

    /// <summary>
    /// Copies an item whose physical file lives outside the project directory into the
    /// project directory (or into <paramref name="targetSubDirectory"/> relative to it),
    /// then updates the item's <c>AbsolutePath</c> / <c>RelativePath</c> and saves the project.
    /// </summary>
    /// <param name="targetSubDirectory">
    /// Optional path relative to the project directory for the destination.
    /// Pass <see langword="null"/> to copy directly into the project root.
    /// </param>
    Task ImportExternalItemAsync(IProject project, IProjectItem item, string? targetSubDirectory = null, CancellationToken ct = default);

    // ── Solution Folder CRUD ─────────────────────────────────────────────
    /// <summary>
    /// Creates a VS-like Solution Folder in the solution, optionally nested under
    /// an existing folder. Persists the solution immediately.
    /// Solution Folders hold <see cref="IProject"/>s, not project items (files).
    /// </summary>
    Task<ISolutionFolder> CreateSolutionFolderAsync(ISolution solution, string name,
        string? parentFolderId = null, CancellationToken ct = default);

    /// <summary>
    /// Renames a Solution Folder. Persists the solution immediately.
    /// </summary>
    Task RenameSolutionFolderAsync(ISolution solution, ISolutionFolder folder,
        string newName, CancellationToken ct = default);

    /// <summary>
    /// Removes a Solution Folder from the solution tree.
    /// Projects it contained are moved to the solution root.
    /// Persists the solution immediately.
    /// </summary>
    Task DeleteSolutionFolderAsync(ISolution solution, ISolutionFolder folder,
        CancellationToken ct = default);

    /// <summary>
    /// Moves <paramref name="project"/> into <paramref name="targetFolderId"/>.
    /// Pass <see langword="null"/> for <paramref name="targetFolderId"/> to place it at the solution root.
    /// Persists the solution immediately.
    /// </summary>
    Task MoveProjectToSolutionFolderAsync(ISolution solution, IProject project,
        string? targetFolderId, CancellationToken ct = default);

    // ── Project-level Folder CRUD ─────────────────────────────────────────
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

    // ── Modification tracking (legacy IPS — kept for migration) ──────────
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

    // ── WHChg changeset operations ────────────────────────────────────────

    /// <summary>
    /// Applies the pending .whchg changeset for <paramref name="item"/> to its
    /// physical file on disk, then removes the .whchg companion file.
    /// The caller is responsible for reloading the editor after this returns.
    /// </summary>
    Task WriteItemToDiskAsync(IProject project, IProjectItem item,
                              CancellationToken ct = default);

    /// <summary>
    /// Deletes the .whchg companion file for <paramref name="item"/> without
    /// applying it, effectively discarding all pending tracked changes.
    /// </summary>
    Task DiscardChangesetAsync(IProject project, IProjectItem item,
                               CancellationToken ct = default);

    // ── TBL helpers ──────────────────────────────────────────────────────
    /// <summary>Designates <paramref name="tblItem"/> as the default TBL for <paramref name="project"/>.
    /// Pass <see langword="null"/> to clear the designation.</summary>
    void SetDefaultTbl(IProject project, IProjectItem? tblItem);

    // ── MRU helpers ──────────────────────────────────────────────────────
    void PushRecentFile(string absolutePath);

    // ── Format upgrade ───────────────────────────────────────────────────

    /// <summary>
    /// Upgrades all files belonging to <paramref name="solution"/> from their on-disk
    /// format to the current application format version.
    /// Creates a versioned backup of each file before writing (e.g. <c>.whsln.v1.bak</c>).
    /// After a successful upgrade the solution is no longer read-only.
    /// </summary>
    Task UpgradeFormatAsync(ISolution solution, CancellationToken ct = default);

    /// <summary>
    /// Switches the solution into read-only mode.
    /// All saves will be blocked; the user can still view and interact with the data.
    /// Call <see cref="UpgradeFormatAsync"/> to leave read-only mode.
    /// </summary>
    void SetReadOnlyFormat(ISolution solution, bool readOnly);

    // ── Events ───────────────────────────────────────────────────────────
    event EventHandler<SolutionChangedEventArgs>?      SolutionChanged;
    event EventHandler<ProjectChangedEventArgs>?       ProjectChanged;
    event EventHandler<ProjectItemEventArgs>?          ItemAdded;
    event EventHandler<ProjectItemEventArgs>?          ItemRemoved;
    /// <summary>
    /// Raised after a project item has been renamed (disk + model updated).
    /// </summary>
    event EventHandler<ProjectItemRenamedEventArgs>?   ItemRenamed;
    /// <summary>
    /// Raised when a solution is opened whose on-disk format is older than the current
    /// application format. The host should present upgrade UI to the user.
    /// </summary>
    event EventHandler<FormatUpgradeRequiredEventArgs>? FormatUpgradeRequired;
}
