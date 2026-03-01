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
