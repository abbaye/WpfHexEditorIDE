// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: Services/DotnetTestRunner.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Runs `dotnet test` for a given project file, captures TRX output,
//     and returns parsed TestResult instances.
//
// Architecture Notes:
//     Each Run call is isolated: a fresh temp directory is created per project
//     and cleaned up after parsing. Output lines are streamed via IProgress<string>
//     so the panel can display live output.
// ==========================================================

using System.Diagnostics;
using System.IO;
using WpfHexEditor.Plugins.UnitTesting.Models;

namespace WpfHexEditor.Plugins.UnitTesting.Services;

/// <summary>
/// Executes <c>dotnet test</c> on a single project and parses the TRX output.
/// </summary>
public sealed class DotnetTestRunner
{
    /// <summary>
    /// Runs tests for <paramref name="projectFilePath"/> and returns all results.
    /// </summary>
    /// <param name="projectFilePath">Absolute path to the .csproj file.</param>
    /// <param name="testFilter">Optional dotnet test --filter expression (e.g. "FullyQualifiedName~MyClass").</param>
    /// <param name="progress">Receives stdout/stderr lines during the run.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<TestResult>> RunAsync(
        string            projectFilePath,
        string?           testFilter = null,
        IProgress<string>? progress = null,
        CancellationToken  ct       = default)
    {
        var resultsDir = Path.Combine(Path.GetTempPath(),
            $"WpfHexEditor.Tests.{Path.GetFileNameWithoutExtension(projectFilePath)}.{Guid.NewGuid():N}");

        Directory.CreateDirectory(resultsDir);
        try
        {
            var trxFile    = Path.Combine(resultsDir, "results.trx");
            var filterPart = string.IsNullOrWhiteSpace(testFilter) ? string.Empty : $" --filter \"{testFilter}\"";
            var args       = $"test \"{projectFilePath}\"{filterPart} --logger \"trx;LogFileName=results.trx\" --results-directory \"{resultsDir}\" --no-build";

            var psi = new ProcessStartInfo("dotnet", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.Start();

            // Drain stdout/stderr concurrently.
            var stdoutTask = DrainStreamAsync(proc.StandardOutput, progress, ct);
            var stderrTask = DrainStreamAsync(proc.StandardError,  progress, ct);

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            return TrxParser.Parse(trxFile);
        }
        finally
        {
            TryDeleteDir(resultsDir);
        }
    }

    private static async Task DrainStreamAsync(
        System.IO.TextReader  reader,
        IProgress<string>?    progress,
        CancellationToken     ct)
    {
        string? line;
        while (!ct.IsCancellationRequested &&
               (line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            if (!string.IsNullOrWhiteSpace(line))
                progress?.Report(line);
        }
    }

    private static void TryDeleteDir(string path)
    {
        try { Directory.Delete(path, recursive: true); }
        catch { /* ignore cleanup failures */ }
    }
}
