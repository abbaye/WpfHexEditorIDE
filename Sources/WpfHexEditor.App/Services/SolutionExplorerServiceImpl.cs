
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
