
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.ProjectSystem.Services;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Adapts the SolutionManager to the ISolutionExplorerService SDK contract.
/// </summary>
public sealed class SolutionExplorerServiceImpl : ISolutionExplorerService
{
    private readonly ISolutionManager _solutionManager;

    public SolutionExplorerServiceImpl(ISolutionManager solutionManager)
    {
        _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
    }

    public bool HasActiveSolution => _solutionManager.CurrentSolution is not null;

    public string? ActiveSolutionPath => _solutionManager.CurrentSolution?.FilePath;

    public string? ActiveSolutionName => _solutionManager.CurrentSolution?.Name;

    /// <summary>
    /// Hook assigned by MainWindow to open a file as a standalone document tab.
    /// Called by OpenFileAsync so Terminal commands can open files without a direct App reference.
    /// </summary>
    public Func<string, Task>? OpenFileHandler { get; set; }

    public Task OpenFileAsync(string filePath, CancellationToken ct = default)
    {
        if (OpenFileHandler is not null)
            return OpenFileHandler(filePath);

        // No handler wired yet — log to output is not available here; silently ignore.
        return Task.CompletedTask;
    }

    /// <summary>Hook assigned by MainWindow to close an open file tab.</summary>
    public Func<string?, Task>? CloseFileHandler { get; set; }

    /// <summary>Hook assigned by MainWindow to save a file.</summary>
    public Func<string?, Task>? SaveFileHandler { get; set; }

    public Task CloseFileAsync(string? fileName = null, CancellationToken ct = default)
        => CloseFileHandler is not null ? CloseFileHandler(fileName) : Task.CompletedTask;

    public Task SaveFileAsync(string? fileName = null, CancellationToken ct = default)
        => SaveFileHandler is not null ? SaveFileHandler(fileName) : Task.CompletedTask;

    // -- Folder / project / solution management — stubs until UI is wired -----

    public Task OpenFolderAsync(string path, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task OpenProjectAsync(string name, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task CloseProjectAsync(string name, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task OpenSolutionAsync(string path, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task CloseSolutionAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public Task ReloadSolutionAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public IReadOnlyList<string> GetFilesInDirectory(string path)
    {
        try { return System.IO.Directory.GetFiles(path); }
        catch { return []; }
    }

    public event EventHandler? SolutionChanged
    {
        add => _solutionManager.SolutionChanged += value;
        remove => _solutionManager.SolutionChanged -= value;
    }

    public IReadOnlyList<string> GetOpenFilePaths()
    {
        if (_solutionManager.CurrentSolution is null) return [];
        return _solutionManager.CurrentSolution
            .GetAllItems()
            .Where(i => !string.IsNullOrEmpty(i.FilePath))
            .Select(i => i.FilePath!)
            .ToList();
    }

    public IReadOnlyList<string> GetSolutionFilePaths()
    {
        if (_solutionManager.CurrentSolution is null) return [];
        return _solutionManager.CurrentSolution
            .GetAllProjects()
            .SelectMany(p => p.GetAllItems())
            .Where(i => !string.IsNullOrEmpty(i.FilePath))
            .Select(i => i.FilePath!)
            .ToList();
    }
}
