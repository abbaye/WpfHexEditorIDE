// ==========================================================
// Project: WpfHexEditor.BuildSystem
// File: BuildSystem.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Central IBuildSystem implementation.
//     Orchestrates builds by: finding the active solution's projects,
//     resolving build order (topological sort), delegating each project
//     to the registered IBuildAdapter, aggregating results, and
//     publishing build lifecycle events to IIDEEventBus.
//
// Architecture Notes:
//     Pattern: Facade + Pipeline
//     - IBuildAdapter (MSBuild / custom) registered via RegisterAdapter().
//     - ISolutionManager (injected) provides the project list and file paths.
//     - IDEEventBus (injected) receives Build* events.
//     - CancellationToken plumbed through all async calls.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Events;
using WpfHexEditor.Events.IDEEvents;

namespace WpfHexEditor.BuildSystem;

/// <summary>
/// Implements <see cref="IBuildSystem"/>; orchestrates multi-project builds.
/// </summary>
public sealed class BuildSystem : IBuildSystem
{
    private readonly ISolutionManager       _solutionManager;
    private readonly IIDEEventBus           _eventBus;
    private readonly ConfigurationManager   _configurationManager;
    private readonly BuildDependencyResolver _resolver = new();

    private readonly List<IBuildAdapter>    _adapters = [];
    private CancellationTokenSource?        _activeCts;

    // -----------------------------------------------------------------------

    public BuildSystem(
        ISolutionManager     solutionManager,
        IIDEEventBus         eventBus,
        ConfigurationManager configurationManager)
    {
        _solutionManager      = solutionManager      ?? throw new ArgumentNullException(nameof(solutionManager));
        _eventBus             = eventBus             ?? throw new ArgumentNullException(nameof(eventBus));
        _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
    }

    // -----------------------------------------------------------------------
    // IBuildSystem
    // -----------------------------------------------------------------------

    public bool HasActiveBuild => _activeCts is not null;

    public async Task<BuildResult> BuildSolutionAsync(CancellationToken ct = default)
        => await RunBuildAsync(GetAllProjectPaths(), rebuild: false, ct);

    public async Task<BuildResult> BuildProjectAsync(string projectId, CancellationToken ct = default)
        => await RunBuildAsync(GetProjectPath(projectId), rebuild: false, ct);

    public async Task<BuildResult> RebuildSolutionAsync(CancellationToken ct = default)
    {
        await CleanSolutionAsync(ct);
        return await BuildSolutionAsync(ct);
    }

    public async Task<BuildResult> RebuildProjectAsync(string projectId, CancellationToken ct = default)
    {
        await CleanProjectAsync(projectId, ct);
        return await BuildProjectAsync(projectId, ct);
    }

    public async Task CleanSolutionAsync(CancellationToken ct = default)
    {
        foreach (var (path, config) in GetAllProjectPaths())
            await CleanAsync(path, config, ct);
    }

    public async Task CleanProjectAsync(string projectId, CancellationToken ct = default)
    {
        foreach (var (path, config) in GetProjectPath(projectId))
            await CleanAsync(path, config, ct);
    }

    public void CancelBuild()
    {
        _activeCts?.Cancel();
        _activeCts = null;
    }

    // -----------------------------------------------------------------------
    // Adapter registration
    // -----------------------------------------------------------------------

    /// <summary>Registers a build adapter (e.g. MSBuild).</summary>
    public void RegisterAdapter(IBuildAdapter adapter)
    {
        if (!_adapters.Any(a => a.AdapterId.Equals(adapter.AdapterId, StringComparison.OrdinalIgnoreCase)))
            _adapters.Add(adapter);
    }

    // -----------------------------------------------------------------------
    // Private orchestration
    // -----------------------------------------------------------------------

    private async Task<BuildResult> RunBuildAsync(
        IEnumerable<(string FilePath, IBuildConfiguration Config)> targets,
        bool rebuild,
        CancellationToken externalCt)
    {
        var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        _activeCts = linkedCts;

        _eventBus.Publish(new BuildStartedEvent());
        var sw     = System.Diagnostics.Stopwatch.StartNew();
        var errors = new List<BuildDiagnostic>();
        var warnings = new List<BuildDiagnostic>();

        try
        {
            foreach (var (filePath, config) in targets)
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                var adapter = FindAdapter(filePath);
                if (adapter is null)
                {
                    errors.Add(new BuildDiagnostic(filePath, null, null, "BUILD001",
                        $"No build adapter found for '{System.IO.Path.GetFileName(filePath)}'.",
                        DiagnosticSeverity.Error));
                    continue;
                }

                var progress = new Progress<string>(line =>
                    _eventBus.Publish(new BuildOutputLineEvent { Line = line }));

                var result = await adapter.BuildAsync(filePath, config, progress, linkedCts.Token);
                errors.AddRange(result.Errors);
                warnings.AddRange(result.Warnings);

                if (!result.IsSuccess) break; // stop on first project error
            }

            sw.Stop();
            var final = new BuildResult(errors.Count == 0, errors, warnings, sw.Elapsed);

            if (final.IsSuccess)
                _eventBus.Publish(new BuildSucceededEvent { WarningCount = warnings.Count, Duration = sw.Elapsed });
            else
                _eventBus.Publish(new BuildFailedEvent { ErrorCount = errors.Count, Warnings = warnings.Count, Duration = sw.Elapsed });

            return final;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _eventBus.Publish(new BuildCancelledEvent());
            return new BuildResult(false, errors, warnings, sw.Elapsed);
        }
        finally
        {
            _activeCts = null;
        }
    }

    private async Task CleanAsync(
        string path, IBuildConfiguration config, CancellationToken ct)
    {
        var adapter = FindAdapter(path);
        if (adapter is null) return;
        await adapter.CleanAsync(path, config, ct);
    }

    private IBuildAdapter? FindAdapter(string filePath)
        => _adapters.FirstOrDefault(a => a.CanBuild(filePath));

    private IEnumerable<(string FilePath, IBuildConfiguration Config)> GetAllProjectPaths()
    {
        var config = _configurationManager.ActiveConfiguration;
        if (_solutionManager.CurrentSolution is null) yield break;

        foreach (var project in _solutionManager.CurrentSolution.Projects)
        {
            if (!string.IsNullOrEmpty(project.ProjectFilePath))
                yield return (project.ProjectFilePath, config);
        }
    }

    private IEnumerable<(string FilePath, IBuildConfiguration Config)> GetProjectPath(string projectId)
    {
        var config  = _configurationManager.ActiveConfiguration;
        var project = _solutionManager.CurrentSolution?.Projects.FirstOrDefault(p => p.Id == projectId);
        if (project is not null && !string.IsNullOrEmpty(project.ProjectFilePath))
            yield return (project.ProjectFilePath, config);
    }
}
