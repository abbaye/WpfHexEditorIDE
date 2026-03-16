// ==========================================================
// Project: WpfHexEditor.Plugins.Build.MSBuild
// File: NuGetRestoreStep.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Pre-build step that runs "dotnet restore" when the NuGet
//     assets file (project.assets.json) is absent or outdated.
//     Runs dotnet.exe as an external process to avoid loading the
//     NuGet SDK assemblies in-process.
// ==========================================================

using System.Diagnostics;

namespace WpfHexEditor.Plugins.Build.MSBuild;

/// <summary>
/// Runs <c>dotnet restore</c> on a project when the assets file is missing.
/// </summary>
internal static class NuGetRestoreStep
{
    /// <summary>
    /// Restores NuGet packages for <paramref name="projectFilePath"/> if
    /// <c>project.assets.json</c> is absent in the obj directory.
    /// </summary>
    public static async Task RestoreIfNeededAsync(
        string             projectFilePath,
        IProgress<string>? outputProgress,
        CancellationToken  ct)
    {
        var projectDir  = System.IO.Path.GetDirectoryName(projectFilePath)!;
        var assetsFile  = System.IO.Path.Combine(projectDir, "obj", "project.assets.json");

        if (System.IO.File.Exists(assetsFile)) return;

        outputProgress?.Report($"  Restoring NuGet packages for {System.IO.Path.GetFileName(projectFilePath)}...");

        var psi = new ProcessStartInfo("dotnet")
        {
            Arguments              = $"restore \"{projectFilePath}\"",
            WorkingDirectory       = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                outputProgress?.Report("  " + e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                outputProgress?.Report("  " + e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
    }
}
