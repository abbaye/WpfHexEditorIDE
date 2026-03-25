// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: Services/DotnetTestDiscoverer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-24
// Updated: 2026-03-24 (ADR-UT-10 — two-pass: --no-build first, fallback without)
// Description:
//     Discovers tests via `dotnet test --list-tests` without running them.
//     Fast path: --no-build (already-built project, instant).
//     Fallback:  no --no-build flag (lets dotnet build first when output is stale).
// ==========================================================

using System.Diagnostics;
using System.IO;
using WpfHexEditor.Plugins.UnitTesting.Models;

namespace WpfHexEditor.Plugins.UnitTesting.Services;

/// <summary>
/// Discovers test cases via <c>dotnet test --list-tests</c> without executing them.
/// Uses a two-pass strategy: fast <c>--no-build</c> first, then falls back to a full
/// build pass when the project output is missing or stale.
/// </summary>
public sealed class DotnetTestDiscoverer
{
    public async Task<IReadOnlyList<DiscoveredTest>> DiscoverAsync(
        string            projectFilePath,
        CancellationToken ct = default)
    {
        // Fast path — already-built projects respond in < 1 s.
        var result = await RunListTestsAsync(projectFilePath, noBuild: true, ct)
                          .ConfigureAwait(false);

        // Fallback — allow dotnet to build first if nothing was found.
        if (result.Count == 0)
            result = await RunListTestsAsync(projectFilePath, noBuild: false, ct)
                          .ConfigureAwait(false);

        return result;
    }

    private static async Task<IReadOnlyList<DiscoveredTest>> RunListTestsAsync(
        string            projectFilePath,
        bool              noBuild,
        CancellationToken ct)
    {
        var noBuildArg   = noBuild ? " --no-build" : string.Empty;
        var verbosityArg = noBuild ? string.Empty  : " --verbosity quiet";
        var args         = $"test \"{projectFilePath}\" --list-tests{noBuildArg}{verbosityArg}";
        var psi        = new ProcessStartInfo("dotnet", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        // Drain stderr concurrently to avoid deadlock.
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        var names  = new List<string>();
        var inList = false;
        string? line;
        while ((line = await proc.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            if (!inList)
            {
                // English header — works on all locales where dotnet ships English.
                if (line.TrimStart().StartsWith("The following Tests are available",
                        StringComparison.OrdinalIgnoreCase))
                {
                    inList = true;
                    continue;
                }

                // Locale fallback: any 4-space-indented dotted line with no path separators
                // or colons IS a fully-qualified test name.  Works on all locales (French,
                // German, etc.) and on both passes — pass 2 uses --verbosity quiet to suppress
                // build noise so path/error lines never appear here.
                var trimmedFb = line.TrimStart();
                if (line.StartsWith("    ", StringComparison.Ordinal)
                    && trimmedFb.Contains('.')
                    && !trimmedFb.Contains(':')
                    && !trimmedFb.Contains('\\')
                    && !trimmedFb.Contains('/'))
                {
                    inList = true;
                    // fall through — first test name is on this line
                }
            }

            if (inList && line.StartsWith("    ", StringComparison.Ordinal))
                names.Add(line.Trim());
        }

        await stderrTask.ConfigureAwait(false);

        try
        {
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }

        var projectName = Path.GetFileNameWithoutExtension(projectFilePath);
        return names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => SplitFullName(n, projectName))
            .ToList();
    }

    /// <summary>
    /// Splits "Namespace.ClassName.MethodName(params)" into (ClassName, TestName).
    /// </summary>
    private static DiscoveredTest SplitFullName(string fullName, string projectName)
    {
        var parenIdx = fullName.IndexOf('(');
        var searchIn = parenIdx >= 0 ? fullName[..parenIdx] : fullName;
        var dotIdx   = searchIn.LastIndexOf('.');
        if (dotIdx < 0)
            return new(projectName, string.Empty, fullName);
        return new(projectName, fullName[..dotIdx], fullName[(dotIdx + 1)..]);
    }
}
