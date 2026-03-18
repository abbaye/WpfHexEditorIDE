// ==========================================================
// Project: WpfHexEditor.BuildSystem
// File: StartupProjectRunner.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Handles the "Run startup project" workflow:
//       1. Build the solution (incremental).
//       2. Resolve the startup project's output executable via
//          `dotnet msbuild -getProperty:TargetPath`.
//       3. Launch the executable as a detached process.
//     Output is streamed to the IDE Output panel via IIDEEventBus.
//
// Architecture Notes:
//     Pattern: Command + Facade
//     - Delegates build to IBuildSystem (no duplicate build logic).
//     - Process launch is fire-and-forget; the launched app is
//       independent of the IDE process (matches VS "Start Without Debugging").
// ==========================================================

using System.Diagnostics;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Events;
using WpfHexEditor.Events.IDEEvents;

namespace WpfHexEditor.BuildSystem;

/// <summary>
/// Builds the active solution and launches the startup project's executable.
/// Equivalent to VS "Start Without Debugging" (Ctrl+F5).
/// </summary>
public sealed class StartupProjectRunner
{
    private readonly ISolutionManager    _solutionManager;
    private readonly IBuildSystem        _buildSystem;
    private readonly IIDEEventBus        _eventBus;
    private readonly ConfigurationManager _configManager;

    // -----------------------------------------------------------------------

    public StartupProjectRunner(
        ISolutionManager     solutionManager,
        IBuildSystem         buildSystem,
        IIDEEventBus         eventBus,
        ConfigurationManager configManager)
    {
        _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
        _buildSystem      = buildSystem      ?? throw new ArgumentNullException(nameof(buildSystem));
        _eventBus         = eventBus         ?? throw new ArgumentNullException(nameof(eventBus));
        _configManager    = configManager    ?? throw new ArgumentNullException(nameof(configManager));
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds the solution and launches the startup project.
    /// Returns <c>false</c> when no startup project is set or the build fails.
    /// </summary>
    public async Task<bool> RunAsync(CancellationToken ct = default)
    {
        var startup = _solutionManager.CurrentSolution?.StartupProject;
        if (startup?.ProjectFilePath is null)
        {
            Log("No startup project is set. Right-click a project and choose 'Set as Startup Project'.");
            return false;
        }

        // Build the solution first (incremental).
        var buildResult = await _buildSystem.BuildSolutionAsync(ct);
        if (!buildResult.IsSuccess)
        {
            Log("-- Run aborted: build failed. --");
            return false;
        }

        // Resolve the output executable path.
        var exePath = await ResolveTargetPathAsync(
            startup.ProjectFilePath,
            _configManager.ActiveConfiguration.Name,
            ct);

        if (exePath is null)
        {
            Log($"Cannot resolve output executable for '{startup.Name}'.");
            Log("Ensure the project is a .NET executable and has been built successfully.");
            return false;
        }

        // Launch as a detached process (fire-and-forget — like VS Ctrl+F5).
        Log($"-- Starting: {Path.GetFileName(exePath)} --");

        try
        {
            var psi = new ProcessStartInfo(exePath)
            {
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
                UseShellExecute  = true,   // UseShellExecute=true → detached, no console capture
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log($"Failed to launch '{exePath}': {ex.Message}");
            return false;
        }

        return true;
    }

    // -----------------------------------------------------------------------
    // Exe path resolution
    // -----------------------------------------------------------------------

    /// <summary>
    /// Queries <c>dotnet msbuild -getProperty:TargetPath</c> to obtain the
    /// absolute path of the compiled output without triggering a rebuild.
    /// Available since .NET SDK 7.0.
    /// Parses stdout line by line to handle SDK versions that emit extra header
    /// lines before the actual property value.
    /// </summary>
    private async Task<string?> ResolveTargetPathAsync(
        string            csprojPath,
        string            configName,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory       = Path.GetDirectoryName(csprojPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        psi.ArgumentList.Add("msbuild");
        psi.ArgumentList.Add(csprojPath);
        psi.ArgumentList.Add("-nologo");
        psi.ArgumentList.Add("-getProperty:TargetPath");
        psi.ArgumentList.Add($"-p:Configuration={configName}");

        using var proc = Process.Start(psi)!;

        // Read stdout and stderr concurrently to avoid deadlocks on large output.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        // Some SDK versions emit header/warning lines before the property value.
        // Take the last non-empty line that looks like an absolute path.
        var path = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(Path.IsPathRooted);

        if (!string.IsNullOrWhiteSpace(stderr))
            Log($"[TargetPath] {stderr.Trim()}");

        if (path is null || !File.Exists(path))
        {
            Log($"[TargetPath] stdout='{stdout.Trim()}' exitCode={proc.ExitCode}");
            return null;
        }

        return path;
    }

    // -----------------------------------------------------------------------

    private void Log(string line)
        => _eventBus.Publish(new BuildOutputLineEvent { Line = line });
}
