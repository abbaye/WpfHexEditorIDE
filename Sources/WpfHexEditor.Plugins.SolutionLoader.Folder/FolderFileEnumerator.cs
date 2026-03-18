// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.Folder
// File: FolderFileEnumerator.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Walks a folder tree recursively and produces a flat list of
//     IProjectItem + a virtual folder hierarchy mirroring the disk.
//     Applies exclude patterns from the .whfolder marker, hidden-file
//     filtering, gitignore rules, and extension-based ProjectItemType mapping.
//
// Architecture Notes:
//     Pattern: Pipeline — enumerate → filter → classify → build
//     Runs synchronously on a thread-pool thread (called via Task.Run).
//     gitignore filters are loaded per-directory and cached for the walk.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.SolutionLoader.Folder.FolderModels;

namespace WpfHexEditor.Plugins.SolutionLoader.Folder;

/// <summary>
/// Enumerates a root directory and maps the result to <see cref="IProjectItem"/> instances
/// and a <see cref="IVirtualFolder"/> hierarchy.
/// </summary>
internal static class FolderFileEnumerator
{
    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Walks <paramref name="rootDir"/> recursively and returns all accepted files
    /// as <see cref="IProjectItem"/> objects plus a corresponding virtual folder tree.
    /// </summary>
    public static (IReadOnlyList<IProjectItem> Items, IReadOnlyList<IVirtualFolder> RootFolders)
        Enumerate(string rootDir, FolderMarker marker)
    {
        var excludes = new HashSet<string>(marker.ExcludePatterns, StringComparer.OrdinalIgnoreCase);
        var items    = new List<IProjectItem>();
        var folders  = new Dictionary<string, FolderVirtualFolder>(StringComparer.OrdinalIgnoreCase);

        WalkDirectory(rootDir, rootDir, excludes, marker, items, folders);

        var rootFolders = BuildRootFolders(folders, rootDir);
        AttachItemsToFolders(items, folders);

        return (items, rootFolders);
    }

    // -----------------------------------------------------------------------
    // Recursive walk
    // -----------------------------------------------------------------------

    private static void WalkDirectory(
        string dir, string rootDir,
        HashSet<string> excludes,
        FolderMarker marker,
        List<IProjectItem> items,
        Dictionary<string, FolderVirtualFolder> folders)
    {
        // Load gitignore filter for this directory level.
        var gitFilter = marker.UseGitIgnore
            ? GitIgnoreFilter.LoadForDirectory(dir)
            : null;

        // --- Files ---------------------------------------------------------
        foreach (var file in Directory.EnumerateFiles(dir))
        {
            if (ShouldSkipFile(file, rootDir, excludes, marker, gitFilter)) continue;

            var relative = Path.GetRelativePath(rootDir, file);
            var relDir   = Path.GetDirectoryName(relative);
            string? folderId = null;

            if (!string.IsNullOrEmpty(relDir) && relDir != ".")
                folderId = EnsureFolder(relDir, rootDir, folders);

            items.Add(new FolderItem
            {
                Name            = Path.GetFileName(file),
                AbsolutePath    = file,
                RelativePath    = relative,
                ItemType        = MapItemType(file),
                VirtualFolderId = folderId,
            });
        }

        // --- Sub-directories -----------------------------------------------
        foreach (var subDir in Directory.EnumerateDirectories(dir))
        {
            var dirName = Path.GetFileName(subDir);

            // Skip by segment name.
            if (excludes.Contains(dirName)) continue;

            // Skip hidden directories.
            if (!marker.IncludeHidden)
            {
                var attrs = File.GetAttributes(subDir);
                if ((attrs & FileAttributes.Hidden) != 0) continue;
            }

            // Apply gitignore for directories.
            if (gitFilter != null)
            {
                var relPath = Path.GetRelativePath(rootDir, subDir);
                if (gitFilter.IsIgnored(relPath, isDirectory: true)) continue;
            }

            WalkDirectory(subDir, rootDir, excludes, marker, items, folders);
        }
    }

    // -----------------------------------------------------------------------
    // Skip predicates
    // -----------------------------------------------------------------------

    /// <summary>
    /// Skipped file extensions — project files belong to the project node, not content.
    /// </summary>
    private static readonly HashSet<string> ProjectFileExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".csproj", ".vbproj", ".fsproj", ".esproj", ".njsproj", ".pyproj", ".sqlproj",
            ".whfolder",
        };

    private static bool ShouldSkipFile(
        string file, string rootDir,
        HashSet<string> excludes,
        FolderMarker marker,
        GitIgnoreFilter? gitFilter)
    {
        var name = Path.GetFileName(file);
        var ext  = Path.GetExtension(file);

        // Skip by exact file name (e.g. "Thumbs.db").
        if (excludes.Contains(name)) return true;

        // Skip project / marker extensions.
        if (ProjectFileExtensions.Contains(ext)) return true;

        // Skip hidden files.
        if (!marker.IncludeHidden)
        {
            var attrs = File.GetAttributes(file);
            if ((attrs & FileAttributes.Hidden) != 0) return true;
        }

        // Check any ancestor segment against the exclude set (covers "bin\Debug\file.dll").
        var relative = Path.GetRelativePath(rootDir, file);
        var parts    = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (excludes.Contains(parts[i])) return true;
        }

        // Apply gitignore.
        if (gitFilter != null && gitFilter.IsIgnored(relative)) return true;

        return false;
    }

    // -----------------------------------------------------------------------
    // Virtual folder management
    // -----------------------------------------------------------------------

    private static string EnsureFolder(
        string relDirPath, string rootDir,
        Dictionary<string, FolderVirtualFolder> folders)
    {
        if (folders.TryGetValue(relDirPath, out var existing))
            return existing.Id;

        var folder = new FolderVirtualFolder
        {
            Name                 = Path.GetFileName(relDirPath),
            PhysicalRelativePath = relDirPath,
        };
        folders[relDirPath] = folder;

        // Ensure parent exists.
        var parent = Path.GetDirectoryName(relDirPath);
        if (!string.IsNullOrEmpty(parent) && parent != ".")
        {
            EnsureFolder(parent, rootDir, folders);
            folders[parent].AddChild(folder);
        }

        return folder.Id;
    }

    private static IReadOnlyList<IVirtualFolder> BuildRootFolders(
        Dictionary<string, FolderVirtualFolder> folders, string rootDir)
    {
        var roots = new List<IVirtualFolder>();

        foreach (var (path, folder) in folders)
        {
            var parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent) || parent == "." || !folders.ContainsKey(parent))
                roots.Add(folder);
        }

        return roots;
    }

    private static void AttachItemsToFolders(
        IEnumerable<IProjectItem> items,
        Dictionary<string, FolderVirtualFolder> folders)
    {
        foreach (var item in items.OfType<FolderItem>())
        {
            if (item.VirtualFolderId is null) continue;
            var folder = folders.Values.FirstOrDefault(f => f.Id == item.VirtualFolderId);
            folder?.AddItemId(item.Id);
        }
    }

    // -----------------------------------------------------------------------
    // Item type mapping (mirrors VSProjectParser.MapItemType)
    // -----------------------------------------------------------------------

    private static ProjectItemType MapItemType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".json"                              => ProjectItemType.Json,
            ".tbl"                               => ProjectItemType.Tbl,
            ".txt" or ".log" or ".md" or ".rst"  => ProjectItemType.Text,
            ".png" or ".bmp" or ".jpg" or ".jpeg"
                or ".gif" or ".ico" or ".webp"   => ProjectItemType.Image,
            ".wav" or ".mp3" or ".ogg" or ".flac" => ProjectItemType.Audio,
            ".bin" or ".rom" or ".img" or ".iso"
                or ".dat" or ".raw"              => ProjectItemType.Binary,
            ".patch" or ".diff"                  => ProjectItemType.Patch,
            _                                    => ProjectItemType.Text,
        };
    }
}
