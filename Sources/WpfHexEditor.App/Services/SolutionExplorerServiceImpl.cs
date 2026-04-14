
//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Editor.Core;
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
        _solutionManager.SolutionChanged += OnManagerSolutionChanged;
    }

    private void OnManagerSolutionChanged(object? sender, SolutionChangedEventArgs e)
        => SolutionChanged?.Invoke(sender, EventArgs.Empty);

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

    // -- Folder / project / solution management --------------------------------

    public Task OpenFolderAsync(string path, CancellationToken ct = default)
        => _solutionManager.OpenSolutionAsync(path, ct);

    public Task OpenSolutionAsync(string path, CancellationToken ct = default)
        => _solutionManager.OpenSolutionAsync(path, ct);

    public Task CloseSolutionAsync(CancellationToken ct = default)
        => _solutionManager.CloseSolutionAsync(ct);

    public async Task ReloadSolutionAsync(CancellationToken ct = default)
    {
        var path = ActiveSolutionPath;
        if (path is null) return;
        await _solutionManager.CloseSolutionAsync(ct).ConfigureAwait(false);
        await _solutionManager.OpenSolutionAsync(path, ct).ConfigureAwait(false);
    }

    public Task OpenProjectAsync(string projectFilePath, CancellationToken ct = default)
    {
        var solution = _solutionManager.CurrentSolution;
        if (solution is null) return Task.CompletedTask;
        return _solutionManager.AddExistingProjectAsync(solution, projectFilePath, ct);
    }

    public Task CloseProjectAsync(string name, CancellationToken ct = default)
    {
        var solution = _solutionManager.CurrentSolution;
        if (solution is null) return Task.CompletedTask;
        var project = solution.Projects.FirstOrDefault(p =>
            p.Name == name ||
            string.Equals(p.ProjectFilePath, name, StringComparison.OrdinalIgnoreCase));
        if (project is null) return Task.CompletedTask;
        return _solutionManager.RemoveProjectAsync(solution, project, ct);
    }

    public IReadOnlyList<string> GetFilesInDirectory(string path)
    {
        try { return System.IO.Directory.GetFiles(path); }
        catch { return []; }
    }

    public event EventHandler? SolutionChanged;

    public IReadOnlyList<string> GetOpenFilePaths()
    {
        if (_solutionManager.CurrentSolution is null) return [];
        return _solutionManager.CurrentSolution
            .Projects
            .SelectMany(p => p.Items)
            .Where(i => !string.IsNullOrEmpty(i.AbsolutePath))
            .Select(i => i.AbsolutePath)
            .ToList();
    }

    public IReadOnlyList<string> GetSolutionFilePaths()
    {
        if (_solutionManager.CurrentSolution is null) return [];
        return _solutionManager.CurrentSolution
            .Projects
            .SelectMany(p => p.Items)
            .Where(i => !string.IsNullOrEmpty(i.AbsolutePath))
            .Select(i => i.AbsolutePath)
            .ToList();
    }

    public IReadOnlyList<SolutionProjectInfo> GetSolutionProjects()
    {
        if (_solutionManager.CurrentSolution is null) return [];

        var result = new List<SolutionProjectInfo>();
        foreach (IProject project in _solutionManager.CurrentSolution.Projects)
        {
            var files = project.Items
                .Where(i => !string.IsNullOrEmpty(i.AbsolutePath)
                         && IsSourceFile(i.AbsolutePath))
                .Select(i => i.AbsolutePath)
                .ToList() as IReadOnlyList<string>;

            result.Add(new SolutionProjectInfo(
                Name:        project.Name,
                ProjectPath: project.ProjectFilePath,
                SourceFiles: files));
        }
        return result;
    }

    private static bool IsSourceFile(string path)
        => path.EndsWith(".cs",  StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".vb",  StringComparison.OrdinalIgnoreCase);
}
