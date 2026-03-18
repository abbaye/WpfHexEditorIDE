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
    {
        var slnPath = GetVsSolutionFilePath();
        return slnPath is not null
            ? await RunVsSolutionBuildAsync(slnPath, ct)
            : await RunBuildAsync(GetAllProjectPaths(), rebuild: false, ct);
    }

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
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeCts = linkedCts;
        try
        {
            _eventBus.Publish(new BuildOutputLineEvent { Line = "========== Clean started ==========" });
            var progress = new Progress<string>(line => _eventBus.Publish(new BuildOutputLineEvent { Line = line }));

            var slnPath = GetVsSolutionFilePath();
            if (slnPath is not null)
            {
                var adapter = FindAdapter(slnPath);
                if (adapter is not null)
                    await adapter.CleanAsync(slnPath, _configurationManager.ActiveConfiguration, progress, linkedCts.Token);
            }
            else
            {
                foreach (var (path, config) in GetAllProjectPaths())
                    await CleanAsync(path, config, progress, linkedCts.Token);
            }

            _eventBus.Publish(new BuildOutputLineEvent { Line = "========== Clean finished ==========" });
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal user action — publish a banner and suppress the exception
            // so it doesn't escape through the async void click handler and crash the app.
            _eventBus.Publish(new BuildOutputLineEvent { Line = "========== Clean cancelled ==========" });
        }
        finally
        {
            _activeCts = null;
        }
    }

    public async Task CleanProjectAsync(string projectId, CancellationToken ct = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeCts = linkedCts;
        try
        {
            _eventBus.Publish(new BuildOutputLineEvent { Line = "========== Clean started ==========" });
            var progress = new Progress<string>(line => _eventBus.Publish(new BuildOutputLineEvent { Line = line }));

            foreach (var (path, config) in GetProjectPath(projectId))
                await CleanAsync(path, config, progress, linkedCts.Token);

            _eventBus.Publish(new BuildOutputLineEvent { Line = "========== Clean finished ==========" });
        }
        catch (OperationCanceledException)
        {
            _eventBus.Publish(new BuildOutputLineEvent { Line = "========== Clean cancelled ==========" });
        }
        finally
        {
            _activeCts = null;
        }
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

        var startedAt  = DateTime.Now;
        var targetList = targets.ToList(); // materialise once to get total count
        var sw         = System.Diagnostics.Stopwatch.StartNew();
        var errors     = new List<BuildDiagnostic>();
        var warnings   = new List<BuildDiagnostic>();
        var succeeded  = 0;
        var failed     = 0;

        _eventBus.Publish(new BuildStartedEvent { StartedAt = startedAt });

        try
        {
            foreach (var (filePath, config) in targetList)
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                var adapter = FindAdapter(filePath);
                if (adapter is null)
                {
                    errors.Add(new BuildDiagnostic(filePath, null, null, "BUILD001",
                        $"No build adapter found for '{System.IO.Path.GetFileName(filePath)}'.",
                        DiagnosticSeverity.Error));
                    failed++;
                    break; // no adapter — treat as hard failure
                }

                var progress = new Progress<string>(line =>
                    _eventBus.Publish(new BuildOutputLineEvent { Line = line }));

                var result = await adapter.BuildAsync(filePath, config, progress, linkedCts.Token);
                errors.AddRange(result.Errors);
                warnings.AddRange(result.Warnings);

                if (result.IsSuccess)
                    succeeded++;
                else
                    failed++;
            }

            sw.Stop();
            var skipped = targetList.Count - succeeded - failed;
            var isSuccess = failed == 0 && errors.Count == 0;
            var final = new BuildResult(isSuccess, errors, warnings, sw.Elapsed);

            if (isSuccess)
                _eventBus.Publish(new BuildSucceededEvent
                {
                    WarningCount   = warnings.Count,
                    Duration       = sw.Elapsed,
                    StartedAt      = startedAt,
                    SucceededCount = succeeded,
                    FailedCount    = failed,
                    SkippedCount   = skipped,
                });
            else
                _eventBus.Publish(new BuildFailedEvent
                {
                    ErrorCount     = errors.Count,
                    Warnings       = warnings.Count,
                    Duration       = sw.Elapsed,
                    StartedAt      = startedAt,
                    SucceededCount = succeeded,
                    FailedCount    = failed,
                    SkippedCount   = skipped,
                });

            return final;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _eventBus.Publish(new BuildCancelledEvent());
            return new BuildResult(false, errors, warnings, sw.Elapsed);
        }
        catch (Exception ex)
        {
            // Surface unexpected adapter / infrastructure failures so they appear in the
            // Output panel instead of being silently swallowed by the caller's fire-and-forget.
            sw.Stop();
            errors.Add(new BuildDiagnostic(
                FilePath: null, Line: null, Column: null,
                Code: "BUILD002",
                Message: $"Build engine error: {ex.GetType().Name}: {ex.Message}",
                Severity: DiagnosticSeverity.Error));
            _eventBus.Publish(new BuildOutputLineEvent { Line = $"  BUILD002: {ex}" });
            _eventBus.Publish(new BuildFailedEvent
            {
                ErrorCount     = 1,
                Warnings       = 0,
                Duration       = sw.Elapsed,
                StartedAt      = startedAt,
                SucceededCount = succeeded,
                FailedCount    = failed + 1,
                SkippedCount   = targetList.Count - succeeded - failed - 1,
            });
            return new BuildResult(false, errors, warnings, sw.Elapsed);
        }
        finally
        {
            _activeCts = null;
        }
    }

    /// <summary>
    /// Builds a VS <c>.sln</c> file as a single unit so MSBuild handles
    /// dependency ordering and project-level skipping (same as VS).
    /// Per-project counts are derived from the error diagnostics.
    /// </summary>
    private async Task<BuildResult> RunVsSolutionBuildAsync(string slnPath, CancellationToken ct)
    {
        var startedAt  = DateTime.Now;
        var config     = _configurationManager.ActiveConfiguration;
        var totalProjs = _solutionManager.CurrentSolution?.Projects.Count ?? 0;
        var sw         = System.Diagnostics.Stopwatch.StartNew();
        var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeCts = linkedCts;

        _eventBus.Publish(new BuildStartedEvent { StartedAt = startedAt });

        try
        {
            var adapter = FindAdapter(slnPath);
            if (adapter is null)
            {
                sw.Stop();
                var noAdapterErr = new BuildDiagnostic(slnPath, null, null, "BUILD001",
                    $"No build adapter found for '{System.IO.Path.GetFileName(slnPath)}'.",
                    DiagnosticSeverity.Error);
                _eventBus.Publish(new BuildFailedEvent
                {
                    ErrorCount = 1, Duration = sw.Elapsed, StartedAt = startedAt,
                    FailedCount = totalProjs, SkippedCount = 0,
                });
                return new BuildResult(false, [noAdapterErr], [], sw.Elapsed);
            }

            var progress = new Progress<string>(line =>
                _eventBus.Publish(new BuildOutputLineEvent { Line = line }));

            var result = await adapter.BuildAsync(slnPath, config, progress, linkedCts.Token);
            sw.Stop();

            // Derive per-project counts from the parsed diagnostics.
            // MSBuild embeds the project path in each diagnostic line — count distinct projects.
            var failedProjs = result.Errors
                .Where(e => e.ProjectName is not null)
                .Select(e => e.ProjectName!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            // If the build failed but no diagnostics matched the regex, assume 1 failed.
            if (!result.IsSuccess && failedProjs == 0)
                failedProjs = 1;

            var succeededProjs = result.IsSuccess ? totalProjs : Math.Max(0, totalProjs - failedProjs);
            var skippedProjs   = Math.Max(0, totalProjs - succeededProjs - failedProjs);

            var final = new BuildResult(result.IsSuccess, result.Errors, result.Warnings, sw.Elapsed);

            if (result.IsSuccess)
                _eventBus.Publish(new BuildSucceededEvent
                {
                    WarningCount   = result.Warnings.Count,
                    Duration       = sw.Elapsed,
                    StartedAt      = startedAt,
                    SucceededCount = succeededProjs,
                    FailedCount    = 0,
                    SkippedCount   = skippedProjs,
                });
            else
                _eventBus.Publish(new BuildFailedEvent
                {
                    ErrorCount     = result.Errors.Count,
                    Warnings       = result.Warnings.Count,
                    Duration       = sw.Elapsed,
                    StartedAt      = startedAt,
                    SucceededCount = succeededProjs,
                    FailedCount    = failedProjs,
                    SkippedCount   = skippedProjs,
                });

            return final;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _eventBus.Publish(new BuildCancelledEvent());
            return new BuildResult(false, [], [], sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var err = new BuildDiagnostic(null, null, null, "BUILD002",
                $"Build engine error: {ex.GetType().Name}: {ex.Message}",
                DiagnosticSeverity.Error);
            _eventBus.Publish(new BuildOutputLineEvent { Line = $"  BUILD002: {ex}" });
            _eventBus.Publish(new BuildFailedEvent
            {
                ErrorCount = 1, Duration = sw.Elapsed, StartedAt = startedAt,
                FailedCount = 1, SkippedCount = Math.Max(0, totalProjs - 1),
            });
            return new BuildResult(false, [err], [], sw.Elapsed);
        }
        finally
        {
            _activeCts = null;
        }
    }

    /// <summary>
    /// Returns the <c>.sln</c> file path if the current solution is a VS solution,
    /// otherwise <see langword="null"/> (falls back to per-project builds).
    /// </summary>
    private string? GetVsSolutionFilePath()
    {
        var path = _solutionManager.CurrentSolution?.FilePath;
        return path is not null
            && Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase)
            ? path
            : null;
    }

    private async Task CleanAsync(
        string             path,
        IBuildConfiguration config,
        IProgress<string>? progress,
        CancellationToken  ct)
    {
        var adapter = FindAdapter(path);
        if (adapter is null) return;
        await adapter.CleanAsync(path, config, progress, ct);
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
