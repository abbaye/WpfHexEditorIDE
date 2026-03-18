// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.Folder
// File: FolderSolutionLoader.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     ISolutionLoader implementation for the .whfolder marker format.
//     Reads / creates the marker, enumerates the folder tree on a thread-pool
//     thread, builds the ISolution graph, and starts the FileSystemWatcher.
//
// Architecture Notes:
//     Pattern: Adapter — converts physical folder → WpfHexEditor ISolution model
//     Holds exactly one FolderFileWatcher at a time; it is replaced on each
//     LoadAsync call and disposed on plugin shutdown.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.SolutionLoader.Folder.FolderModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.SolutionLoader.Folder;

/// <summary>
/// Loads a <c>.whfolder</c> session file and returns a <see cref="ISolution"/>
/// whose single project mirrors the physical directory tree.
/// </summary>
public sealed class FolderSolutionLoader : ISolutionLoader, IDisposable
{
    private readonly IIDEHostContext _context;
    private FolderFileWatcher?       _watcher;

    // -----------------------------------------------------------------------
    // ISolutionLoader
    // -----------------------------------------------------------------------

    public string LoaderName => "Folder Mode";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions { get; } = ["whfolder"];

    /// <inheritdoc />
    public bool CanLoad(string filePath)
        => Path.GetExtension(filePath).Equals(".whfolder", StringComparison.OrdinalIgnoreCase);

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    internal FolderSolutionLoader(IIDEHostContext context)
    {
        _context = context;
    }

    // -----------------------------------------------------------------------
    // LoadAsync
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<ISolution> LoadAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Folder marker file not found.", filePath);

        // Read (or create) the .whfolder marker.
        var marker  = await FolderMarkerFile.ReadOrCreateAsync(filePath, ct).ConfigureAwait(false);
        var rootDir = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(filePath)!, marker.RootPath));

        if (!Directory.Exists(rootDir))
            throw new DirectoryNotFoundException($"Folder not found: {rootDir}");

        // Enumerate the folder tree on a thread-pool thread (I/O bound, potentially large).
        var (items, folders) = await Task.Run(
            () => FolderFileEnumerator.Enumerate(rootDir, marker), ct).ConfigureAwait(false);

        // Replace the previous watcher (restarts on reload).
        _watcher?.Dispose();
        _watcher = new FolderFileWatcher(rootDir, filePath, _context);

        return BuildSolution(filePath, marker, items, folders);
    }

    // -----------------------------------------------------------------------
    // Model building
    // -----------------------------------------------------------------------

    private static ISolution BuildSolution(
        string markerPath,
        FolderMarker marker,
        IReadOnlyList<IProjectItem> items,
        IReadOnlyList<IVirtualFolder> folders)
    {
        var project = new FolderProject
        {
            Name            = marker.Name,
            ProjectFilePath = markerPath,
            Items           = items,
            RootFolders     = folders,
        };

        return new FolderSolution
        {
            Name     = marker.Name,
            FilePath = markerPath,
            Projects = [project],
        };
    }

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
