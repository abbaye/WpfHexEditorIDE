//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.ProjectSystem.Models;
using WpfHexEditor.ProjectSystem.Serialization;
using WpfHexEditor.ProjectSystem.Serialization.Migration;

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

        var solution = new Solution
        {
            Name                = name,
            FilePath            = solutionFile,
            SourceFormatVersion = MigrationPipeline.CurrentVersion,
        };
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

        var (solution, migratedDockLayout) = await SolutionSerializer.ReadAsync(filePath, ct);
        _current = solution;
        _mru.PushSolution(filePath);
        _mru.Save();

        RaiseSolutionChanged(SolutionChangeKind.Opened);

        // Notify host if the format is older than current version.
        if (solution.FormatUpgradeRequired)
        {
            var affectedFiles = new List<string> { filePath };
            affectedFiles.AddRange(solution.Projects.Select(p => p.ProjectFilePath));

            FormatUpgradeRequired?.Invoke(this, new FormatUpgradeRequiredEventArgs
            {
                Solution      = solution,
                FromVersion   = solution.SourceFormatVersion,
                ToVersion     = MigrationPipeline.CurrentVersion,
                AffectedFiles = affectedFiles,
            });
        }

        return solution;
    }

    public async Task SaveSolutionAsync(ISolution solution, CancellationToken ct = default)
    {
        if (solution is not Solution sol) return;
        if (sol.IsReadOnlyFormat)
            throw new InvalidOperationException(
                $"Solution '{sol.Name}' is open in read-only format mode (v{sol.SourceFormatVersion}). " +
                "Call UpgradeFormatAsync first.");

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

    // ── Format upgrade ───────────────────────────────────────────────────

    public async Task UpgradeFormatAsync(ISolution solution, CancellationToken ct = default)
    {
        if (solution is not Solution sol) return;
        if (!sol.FormatUpgradeRequired) return;

        int fromVersion = sol.SourceFormatVersion;

        // Create versioned backups of every file BEFORE writing.
        MigrationPipeline.CreateBackup(sol.FilePath, fromVersion);
        foreach (var proj in sol.ProjectsMutable)
            MigrationPipeline.CreateBackup(proj.ProjectFilePath, fromVersion);

        // Write all files with the current version.
        await SolutionSerializer.WriteAsync(sol, ct);
        foreach (var proj in sol.ProjectsMutable)
            await ProjectSerializer.WriteAsync(proj, ct);

        // Mark the solution as upgraded.
        sol.SourceFormatVersion = MigrationPipeline.CurrentVersion;
        sol.IsReadOnlyFormat    = false;
        sol.IsModified          = false;

        RaiseSolutionChanged(SolutionChangeKind.Modified);
    }

    public void SetReadOnlyFormat(ISolution solution, bool readOnly)
    {
        if (solution is Solution sol)
            sol.IsReadOnlyFormat = readOnly;
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

    public async Task RenameProjectAsync(IProject project, string newName, CancellationToken ct = default)
    {
        if (project is not Project proj || _current is not Solution sol) return;

        var oldPath = proj.ProjectFilePath;
        var dir     = Path.GetDirectoryName(oldPath) ?? "";
        var ext     = Path.GetExtension(oldPath);
        var newPath = Path.Combine(dir, newName + ext);

        if (File.Exists(oldPath) &&
            !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            File.Move(oldPath, newPath);

        proj.Name            = newName;
        proj.ProjectFilePath = newPath;
        sol.IsModified       = true;

        RaiseProjectChanged(proj, ProjectChangeKind.Modified);
        await SaveSolutionAsync(sol, ct);
    }

    public Task RenameItemAsync(IProject project, IProjectItem item, string newName, CancellationToken ct = default)
    {
        if (project is not Project proj || item is not ProjectItem pi) return Task.CompletedTask;

        var oldName    = pi.Name;
        var oldAbsPath = pi.AbsolutePath;

        // Rename the physical file on disk
        var dir        = Path.GetDirectoryName(oldAbsPath)!;
        var newAbsPath = Path.Combine(dir, newName);
        if (File.Exists(oldAbsPath) &&
            !string.Equals(oldAbsPath, newAbsPath, StringComparison.OrdinalIgnoreCase))
            File.Move(oldAbsPath, newAbsPath);

        // Update model paths
        var relDir      = Path.GetDirectoryName(pi.RelativePath)?.Replace('\\', '/') ?? "";
        pi.RelativePath = relDir.Length > 0 ? $"{relDir}/{newName}" : newName;
        pi.AbsolutePath = newAbsPath;
        pi.Name         = newName;
        proj.IsModified = true;

        RaiseItemRenamed(pi, proj, oldName, oldAbsPath);
        return SaveProjectAsync(project, ct);
    }

    public Task MoveItemToFolderAsync(IProject project, IProjectItem item, string? targetFolderId, CancellationToken ct = default)
    {
        if (project is not Project proj || item is not ProjectItem pi) return Task.CompletedTask;

        // Remove item from all folders it currently belongs to
        foreach (var folder in proj.RootFoldersMutable)
            RemoveFromFolder(folder, pi.Id);

        // Add to target folder (null = project root = no folder assignment)
        if (targetFolderId is not null)
        {
            foreach (var folder in proj.RootFoldersMutable)
            {
                if (AddToFolder(folder, pi.Id, targetFolderId))
                    break;
            }
        }

        proj.IsModified = true;
        return SaveProjectAsync(project, ct);
    }

    // ── Folder CRUD ───────────────────────────────────────────────────────

    public async Task<IVirtualFolder> CreateFolderAsync(IProject project, string name,
        string? parentFolderId = null, bool createPhysical = false, CancellationToken ct = default)
    {
        if (project is not Project proj) return new VirtualFolder();

        string? physRelPath = null;
        if (createPhysical)
        {
            var parentRelPath = parentFolderId is not null
                ? FindPhysicalRelPath(proj.RootFoldersMutable, parentFolderId)
                : null;
            physRelPath = parentRelPath is not null ? $"{parentRelPath}/{name}" : name;
            var absPath = Path.Combine(Path.GetDirectoryName(proj.ProjectFilePath)!, physRelPath);
            Directory.CreateDirectory(absPath);
        }

        var folder = new VirtualFolder { Name = name, PhysicalRelativePath = physRelPath };
        if (parentFolderId is null)
            proj.RootFoldersMutable.Add(folder);
        else
            AddFolderToParent(proj.RootFoldersMutable, parentFolderId, folder);

        proj.IsModified = true;
        await SaveProjectAsync(project, ct);
        return folder;
    }

    public async Task RenameFolderAsync(IProject project, IVirtualFolder folder,
        string newName, CancellationToken ct = default)
    {
        if (project is not Project proj) return;
        if (FindFolder(proj.RootFoldersMutable, folder.Id) is not VirtualFolder vf) return;

        if (vf.PhysicalRelativePath is not null)
        {
            var projDir    = Path.GetDirectoryName(proj.ProjectFilePath)!;
            var oldAbsPath = Path.Combine(projDir, vf.PhysicalRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var lastSlash  = vf.PhysicalRelativePath.LastIndexOf('/');
            var newPhysRel = lastSlash >= 0
                ? $"{vf.PhysicalRelativePath[..lastSlash]}/{newName}"
                : newName;
            var newAbsPath = Path.Combine(projDir, newPhysRel.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(oldAbsPath))
                Directory.Move(oldAbsPath, newAbsPath);
            vf.PhysicalRelativePath = newPhysRel;
        }

        vf.Name = newName;
        proj.IsModified = true;
        await SaveProjectAsync(project, ct);
    }

    public async Task DeleteFolderAsync(IProject project, IVirtualFolder folder,
        CancellationToken ct = default)
    {
        if (project is not Project proj) return;
        if (FindFolder(proj.RootFoldersMutable, folder.Id) is not VirtualFolder vf) return;

        // Detach all items from the folder tree (they remain in project root — unclassified)
        foreach (var itemId in CollectItemIds(vf).ToList())
        {
            foreach (var root in proj.RootFoldersMutable)
                RemoveFromFolder(root, itemId);
        }

        RemoveFolderFromTree(proj.RootFoldersMutable, folder.Id);
        proj.IsModified = true;
        await SaveProjectAsync(project, ct);
    }

    public async Task<IVirtualFolder> AddFolderFromDiskAsync(IProject project, string physicalPath,
        string? parentVirtualFolderId = null, CancellationToken ct = default)
    {
        if (project is not Project proj) return new VirtualFolder();

        var folder = AddFolderFromDiskInternal(proj, physicalPath, parentVirtualFolderId);
        proj.IsModified = true;
        await SaveProjectAsync(project, ct);
        return folder;
    }

    // ── Modification tracking ─────────────────────────────────────────────

    public async Task PersistItemModificationsAsync(IProject project, IProjectItem item,
        byte[]? modifications, CancellationToken ct = default)
    {
        if (item is not ProjectItem pi) return;
        pi.UnsavedModifications = modifications;
        pi.IsModified           = modifications is { Length: > 0 };
        await SaveProjectAsync(project, ct);
    }

    public byte[]? GetItemModifications(IProject project, IProjectItem item)
        => item is ProjectItem pi ? pi.UnsavedModifications : null;

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

    private static bool AddFolderToParent(ObservableCollection<VirtualFolder> list, string parentId, VirtualFolder newFolder)
    {
        foreach (var folder in list)
        {
            if (folder.Id == parentId)
            {
                folder.ChildrenMutable.Add(newFolder);
                return true;
            }
            if (AddFolderToParent(folder.ChildrenMutable, parentId, newFolder))
                return true;
        }
        return false;
    }

    private static VirtualFolder? FindFolder(IEnumerable<VirtualFolder> roots, string folderId)
    {
        foreach (var folder in roots)
        {
            if (folder.Id == folderId) return folder;
            var found = FindFolder(folder.ChildrenMutable, folderId);
            if (found is not null) return found;
        }
        return null;
    }

    private static string? FindPhysicalRelPath(IEnumerable<VirtualFolder> roots, string folderId)
        => FindFolder(roots, folderId)?.PhysicalRelativePath;

    private static bool RemoveFolderFromTree(ObservableCollection<VirtualFolder> list, string folderId)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Id == folderId) { list.RemoveAt(i); return true; }
            if (RemoveFolderFromTree(list[i].ChildrenMutable, folderId)) return true;
        }
        return false;
    }

    private static IEnumerable<string> CollectItemIds(VirtualFolder folder)
    {
        foreach (var id in folder.ItemIdsMutable) yield return id;
        foreach (var child in folder.ChildrenMutable)
            foreach (var id in CollectItemIds(child))
                yield return id;
    }

    private static VirtualFolder AddFolderFromDiskInternal(Project proj, string physicalPath, string? parentVirtualFolderId)
    {
        var projectDir  = Path.GetDirectoryName(proj.ProjectFilePath)!;
        var folderName  = Path.GetFileName(physicalPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var physRelPath = Path.GetRelativePath(projectDir, physicalPath).Replace(Path.DirectorySeparatorChar, '/');

        var folder = new VirtualFolder { Name = folderName, PhysicalRelativePath = physRelPath };
        if (parentVirtualFolderId is null)
            proj.RootFoldersMutable.Add(folder);
        else
            AddFolderToParent(proj.RootFoldersMutable, parentVirtualFolderId, folder);

        foreach (var file in Directory.GetFiles(physicalPath))
        {
            var relPath  = Path.GetRelativePath(projectDir, file).Replace(Path.DirectorySeparatorChar, '/');
            var item     = new ProjectItem
            {
                Name         = Path.GetFileName(file),
                RelativePath = relPath,
                AbsolutePath = file,
                ItemType     = ResolveItemType(Path.GetExtension(file)),
            };
            proj.ItemsMutable.Add(item);
            folder.ItemIdsMutable.Add(item.Id);
        }

        foreach (var subDir in Directory.GetDirectories(physicalPath))
            AddFolderFromDiskInternal(proj, subDir, folder.Id);

        return folder;
    }

    internal static ProjectItemType ResolveItemType(string ext) => ext.ToLowerInvariant() switch
    {
        ".whfmt"                                             => ProjectItemType.FormatDefinition,
        ".tbl" or ".tblx"                                    => ProjectItemType.Tbl,
        ".ips" or ".bps"                                     => ProjectItemType.Patch,
        ".json"                                              => ProjectItemType.Json,
        ".txt" or ".md" or ".csv"                            => ProjectItemType.Text,
        ".lua" or ".py" or ".rb" or ".js" or ".ts"
            or ".sh" or ".bat" or ".ps1" or ".asm"
            or ".s" or ".c" or ".cpp" or ".h"
            or ".whlang"                                     => ProjectItemType.Script,
        ".png" or ".bmp" or ".jpg" or ".jpeg" or ".gif"
            or ".ico" or ".tiff" or ".tif" or ".webp"
            or ".dds" or ".tga"                              => ProjectItemType.Image,
        ".chr" or ".til" or ".gfx"                          => ProjectItemType.Tile,
        ".wav" or ".mp3" or ".ogg" or ".flac"
            or ".xm" or ".mod" or ".it" or ".s3m" or ".aiff" => ProjectItemType.Audio,
        ".scr" or ".msg" or ".evt" or ".script" or ".dec"   => ProjectItemType.Script,
        _                                                    => ProjectItemType.Binary,
    };

    private static string DefaultExtension(ProjectItemType type) => type switch
    {
        ProjectItemType.FormatDefinition => ".whfmt",
        ProjectItemType.Tbl              => ".tbl",
        ProjectItemType.Patch            => ".ips",
        ProjectItemType.Json             => ".json",
        ProjectItemType.Text             => ".txt",
        ProjectItemType.Script           => ".lua",
        _                                => ".bin",
    };

    // ── Events ────────────────────────────────────────────────────────────

    public event EventHandler<SolutionChangedEventArgs>?      SolutionChanged;
    public event EventHandler<ProjectChangedEventArgs>?       ProjectChanged;
    public event EventHandler<ProjectItemEventArgs>?          ItemAdded;
    public event EventHandler<ProjectItemEventArgs>?          ItemRemoved;
    public event EventHandler<ProjectItemRenamedEventArgs>?   ItemRenamed;
    public event EventHandler<FormatUpgradeRequiredEventArgs>? FormatUpgradeRequired;

    private void RaiseSolutionChanged(SolutionChangeKind kind)
        => SolutionChanged?.Invoke(this, new SolutionChangedEventArgs { Solution = _current, Kind = kind });

    private void RaiseProjectChanged(Project proj, ProjectChangeKind kind)
        => ProjectChanged?.Invoke(this, new ProjectChangedEventArgs { Project = proj, Kind = kind });

    private void RaiseItemAdded(ProjectItem item, Project proj)
        => ItemAdded?.Invoke(this, new ProjectItemEventArgs { Item = item, Project = proj });

    private void RaiseItemRemoved(ProjectItem item, Project proj)
        => ItemRemoved?.Invoke(this, new ProjectItemEventArgs { Item = item, Project = proj });

    private void RaiseItemRenamed(ProjectItem item, Project proj, string oldName, string oldAbsPath)
        => ItemRenamed?.Invoke(this, new ProjectItemRenamedEventArgs
           { Project = proj, Item = item, OldName = oldName, OldAbsolutePath = oldAbsPath });
}
