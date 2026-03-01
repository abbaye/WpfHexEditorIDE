//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.ProjectSystem.Models;
using WpfHexEditor.ProjectSystem.Serialization;

namespace WpfHexEditor.ProjectSystem.Services;

/// <summary>
/// Singleton implementation of <see cref="ISolutionManager"/>.
/// All public members must be called from the UI thread.
/// </summary>
public sealed class SolutionManager : ISolutionManager
{
    // ── Singleton ────────────────────────────────────────────────────────
    private static SolutionManager? _instance;
    public  static SolutionManager  Instance => _instance ??= new SolutionManager();

    private SolutionManager()
    {
        _mru.Load();
    }

    // ── State ────────────────────────────────────────────────────────────
    private readonly MruService _mru = new();
    private Solution? _current;

    public ISolution? CurrentSolution => _current;
    public IReadOnlyList<string> RecentSolutions => _mru.RecentSolutions;
    public IReadOnlyList<string> RecentFiles     => _mru.RecentFiles;

    // ── Solution lifecycle ───────────────────────────────────────────────

    public async Task<ISolution> CreateSolutionAsync(string directory, string name, CancellationToken ct = default)
    {
        var solutionDir  = Path.Combine(directory, name);
        var solutionFile = Path.Combine(solutionDir, name + ".whsln");

        Directory.CreateDirectory(solutionDir);

        var solution = new Solution { Name = name, FilePath = solutionFile };
        await SolutionSerializer.WriteAsync(solution, ct);

        _current = solution;
        _mru.PushSolution(solutionFile);
        _mru.Save();

        RaiseSolutionChanged(SolutionChangeKind.Opened);
        return solution;
    }

    public async Task<ISolution> OpenSolutionAsync(string filePath, CancellationToken ct = default)
    {
        if (_current != null)
            await CloseSolutionAsync(ct);

        var solution = await SolutionSerializer.ReadAsync(filePath, ct);
        _current = solution;
        _mru.PushSolution(filePath);
        _mru.Save();

        RaiseSolutionChanged(SolutionChangeKind.Opened);
        return solution;
    }

    public async Task SaveSolutionAsync(ISolution solution, CancellationToken ct = default)
    {
        if (solution is not Solution sol) return;
        await SolutionSerializer.WriteAsync(sol, ct);
        foreach (var proj in sol.ProjectsMutable)
            await ProjectSerializer.WriteAsync(proj, ct);
        sol.IsModified = false;
        RaiseSolutionChanged(SolutionChangeKind.Modified);
    }

    public async Task CloseSolutionAsync(CancellationToken ct = default)
    {
        if (_current is null) return;
        _current = null;
        RaiseSolutionChanged(SolutionChangeKind.Closed);
        await Task.CompletedTask;
    }

    // ── Project management ───────────────────────────────────────────────

    public async Task<IProject> CreateProjectAsync(ISolution solution, string directory, string name, CancellationToken ct = default)
    {
        if (solution is not Solution sol)
            throw new ArgumentException("Invalid solution type.");

        var projDir  = Path.Combine(directory, name);
        var projFile = Path.Combine(projDir, name + ".whproj");

        var project = new Project { Name = name, ProjectFilePath = projFile };
        sol.ProjectsMutable.Add(project);
        sol.IsModified = true;

        await ProjectSerializer.WriteAsync(project, ct);
        await SolutionSerializer.WriteAsync(sol, ct);

        RaiseProjectChanged(project, ProjectChangeKind.Added);
        return project;
    }

    public async Task<IProject> AddExistingProjectAsync(ISolution solution, string projectFilePath, CancellationToken ct = default)
    {
        if (solution is not Solution sol)
            throw new ArgumentException("Invalid solution type.");

        var project = await ProjectSerializer.ReadAsync(projectFilePath, ct);
        sol.ProjectsMutable.Add(project);
        sol.IsModified = true;
        await SolutionSerializer.WriteAsync(sol, ct);
        RaiseProjectChanged(project, ProjectChangeKind.Added);
        return project;
    }

    public async Task SaveProjectAsync(IProject project, CancellationToken ct = default)
    {
        if (project is not Project proj) return;
        await ProjectSerializer.WriteAsync(proj, ct);
        proj.IsModified = false;
        RaiseProjectChanged(proj, ProjectChangeKind.Modified);
    }

    public async Task RemoveProjectAsync(ISolution solution, IProject project, CancellationToken ct = default)
    {
        if (solution is not Solution sol || project is not Project proj) return;
        sol.ProjectsMutable.Remove(proj);
        sol.IsModified = true;
        await SolutionSerializer.WriteAsync(sol, ct);
        RaiseProjectChanged(proj, ProjectChangeKind.Removed);
    }

    // ── Item management ──────────────────────────────────────────────────

    public Task<IProjectItem> AddItemAsync(IProject project, string absolutePath, string? virtualFolderId = null, CancellationToken ct = default)
    {
        if (project is not Project proj)
            throw new ArgumentException("Invalid project type.");

        var projDir  = Path.GetDirectoryName(proj.ProjectFilePath)!;
        var relPath  = Path.GetRelativePath(projDir, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
        var itemType = ResolveItemType(Path.GetExtension(absolutePath));

        var item = new ProjectItem
        {
            Name         = Path.GetFileName(absolutePath),
            RelativePath = relPath,
            AbsolutePath = absolutePath,
            ItemType     = itemType,
        };

        AddToProject(proj, item, virtualFolderId);
        RaiseItemAdded(item, proj);
        return Task.FromResult<IProjectItem>(item);
    }

    public async Task<IProjectItem> CreateItemAsync(IProject project, string name, ProjectItemType type, string? virtualFolderId = null, byte[]? initialContent = null, CancellationToken ct = default)
    {
        if (project is not Project proj)
            throw new ArgumentException("Invalid project type.");

        var projDir  = Path.GetDirectoryName(proj.ProjectFilePath)!;
        var ext      = DefaultExtension(type);
        var fileName = name.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? name : name + ext;
        var absPath  = Path.Combine(projDir, fileName);

        Directory.CreateDirectory(projDir);
        await File.WriteAllBytesAsync(absPath, initialContent ?? [], ct);

        return await AddItemAsync(project, absPath, virtualFolderId, ct);
    }

    public Task RemoveItemAsync(IProject project, IProjectItem item, bool deleteFromDisk = false, CancellationToken ct = default)
    {
        if (project is not Project proj || item is not ProjectItem pi) return Task.CompletedTask;

        proj.ItemsMutable.Remove(pi);

        // Remove from virtual folders
        foreach (var folder in proj.RootFoldersMutable)
            RemoveFromFolder(folder, pi.Id);

        if (deleteFromDisk && File.Exists(pi.AbsolutePath))
            File.Delete(pi.AbsolutePath);

        proj.IsModified = true;
        RaiseItemRemoved(pi, proj);
        return Task.CompletedTask;
    }

    public Task RenameItemAsync(IProject project, IProjectItem item, string newName, CancellationToken ct = default)
    {
        if (project is not Project proj || item is not ProjectItem pi) return Task.CompletedTask;
        pi.Name = newName;
        proj.IsModified = true;
        return SaveProjectAsync(project, ct);
    }

    // ── TBL helpers ──────────────────────────────────────────────────────

    public void SetDefaultTbl(IProject project, IProjectItem? tblItem)
    {
        if (project is not Project proj) return;
        proj.DefaultTblItemId = tblItem?.Id;
        proj.IsModified = true;
        RaiseProjectChanged(proj, ProjectChangeKind.Modified);
    }

    // ── MRU helpers ──────────────────────────────────────────────────────

    public void PushRecentFile(string absolutePath)
    {
        _mru.PushFile(absolutePath);
        _mru.Save();
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static void AddToProject(Project proj, ProjectItem item, string? folderId)
    {
        proj.ItemsMutable.Add(item);
        proj.IsModified = true;

        if (folderId is null) return;

        foreach (var folder in proj.RootFoldersMutable)
        {
            if (AddToFolder(folder, item.Id, folderId))
                return;
        }
    }

    private static bool AddToFolder(VirtualFolder folder, string itemId, string targetFolderId)
    {
        if (folder.Id == targetFolderId)
        {
            folder.ItemIdsMutable.Add(itemId);
            return true;
        }
        foreach (var child in folder.ChildrenMutable)
            if (AddToFolder(child, itemId, targetFolderId))
                return true;
        return false;
    }

    private static void RemoveFromFolder(VirtualFolder folder, string itemId)
    {
        folder.ItemIdsMutable.Remove(itemId);
        foreach (var child in folder.ChildrenMutable)
            RemoveFromFolder(child, itemId);
    }

    private static ProjectItemType ResolveItemType(string ext) => ext.ToLowerInvariant() switch
    {
        ".whjson"                   => ProjectItemType.FormatDefinition,
        ".tbl" or ".tblx"           => ProjectItemType.Tbl,
        ".ips" or ".bps"            => ProjectItemType.Patch,
        ".json"                     => ProjectItemType.Json,
        ".txt" or ".md" or ".csv"   => ProjectItemType.Text,
        _                           => ProjectItemType.Binary,
    };

    private static string DefaultExtension(ProjectItemType type) => type switch
    {
        ProjectItemType.FormatDefinition => ".whjson",
        ProjectItemType.Tbl              => ".tbl",
        ProjectItemType.Patch            => ".ips",
        ProjectItemType.Json             => ".json",
        ProjectItemType.Text             => ".txt",
        _                                => ".bin",
    };

    // ── Events ────────────────────────────────────────────────────────────

    public event EventHandler<SolutionChangedEventArgs>? SolutionChanged;
    public event EventHandler<ProjectChangedEventArgs>?  ProjectChanged;
    public event EventHandler<ProjectItemEventArgs>?     ItemAdded;
    public event EventHandler<ProjectItemEventArgs>?     ItemRemoved;

    private void RaiseSolutionChanged(SolutionChangeKind kind)
        => SolutionChanged?.Invoke(this, new SolutionChangedEventArgs { Solution = _current, Kind = kind });

    private void RaiseProjectChanged(Project proj, ProjectChangeKind kind)
        => ProjectChanged?.Invoke(this, new ProjectChangedEventArgs { Project = proj, Kind = kind });

    private void RaiseItemAdded(ProjectItem item, Project proj)
        => ItemAdded?.Invoke(this, new ProjectItemEventArgs { Item = item, Project = proj });

    private void RaiseItemRemoved(ProjectItem item, Project proj)
        => ItemRemoved?.Invoke(this, new ProjectItemEventArgs { Item = item, Project = proj });
}
