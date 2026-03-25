// ==========================================================
// Project: WpfHexEditor.Plugins.Build.MSBuild
// File: MSBuildAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     IBuildAdapter implementation that compiles .csproj / .vbproj / .fsproj
//     projects by spawning "dotnet build" as an external process.
//     This avoids in-process MSBuild assembly loading issues entirely —
//     the same approach used by VS Code and dotnet CLI tooling.
//
// Architecture Notes:
//     Pattern: Adapter — bridges IBuildAdapter to the dotnet CLI.
//     Diagnostics are parsed from the compiler's structured output
//     (MSBuild format: "file(line,col): severity code: message [project]").
//     Real-time output is forwarded line-by-line via IProgress<string>.
// ==========================================================

using System.Diagnostics;
using System.Text.RegularExpressions;
using WpfHexEditor.Core.BuildSystem;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.Build.MSBuild;

/// <summary>
/// <see cref="IBuildAdapter"/> that invokes <c>dotnet build</c> / <c>dotnet clean</c>
/// to compile Visual Studio project files.
/// </summary>
public sealed class MSBuildAdapter : IBuildAdapter
{
    // MSBuild structured diagnostic pattern:
    // path(line,col): severity code: message [project]
    private static readonly Regex _diagnosticPattern = new(
        @"^(?<file>[^(]+)\((?<line>\d+),(?<col>\d+)\):\s*(?<sev>error|warning)\s+(?<code>\S+):\s*(?<msg>.+?)(?:\s+\[(?<proj>[^\]]+)\])?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // -----------------------------------------------------------------------
    // IBuildAdapter
    // -----------------------------------------------------------------------

    public string AdapterId => "msbuild";

    /// <inheritdoc />
    public bool CanBuild(string projectFilePath)
    {
        var ext = Path.GetExtension(projectFilePath).ToLowerInvariant();
        return ext is ".csproj" or ".vbproj" or ".fsproj" or ".sln";
    }

    /// <inheritdoc />
    public async Task<Editor.Core.BuildResult> BuildAsync(
        string              projectFilePath,
        IBuildConfiguration configuration,
        IProgress<string>?  outputProgress,
        CancellationToken   ct = default)
        => await RunDotnetAsync(projectFilePath, configuration, outputProgress, clean: false, ct);

    /// <inheritdoc />
    public async Task CleanAsync(
        string              projectFilePath,
        IBuildConfiguration configuration,
        IProgress<string>?  outputProgress,
        CancellationToken   ct = default)
        => await RunDotnetAsync(projectFilePath, configuration, outputProgress, clean: true, ct);

    // -----------------------------------------------------------------------
    // dotnet CLI invocation
    // -----------------------------------------------------------------------

    private static async Task<Editor.Core.BuildResult> RunDotnetAsync(
        string              projectFilePath,
        IBuildConfiguration configuration,
        IProgress<string>?  progress,
        bool                clean,
        CancellationToken   ct)
    {
        var isSolution = Path.GetExtension(projectFilePath).Equals(".sln", StringComparison.OrdinalIgnoreCase);
        var platform   = isSolution
            ? NormalizePlatformForSolution(configuration.Platform)
            : NormalizePlatform(configuration.Platform);

        var errors   = new List<BuildDiagnostic>();
        var warnings = new List<BuildDiagnostic>();
        var sw       = Stopwatch.StartNew();

        // Use ArgumentList (net5+) for correct quoting — avoids the trailing-backslash
        // bug where a path ending in '\' (e.g. "bin\Debug\net8.0\") mis-escapes the
        // closing quote when embedded in a manually-built Arguments string.
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory        = Path.GetDirectoryName(projectFilePath)!,
            RedirectStandardOutput  = true,
            RedirectStandardError   = true,
            UseShellExecute         = false,
            CreateNoWindow          = true,
            // Use UTF-8 explicitly: dotnet always emits UTF-8 to its stdout pipe,
            // but Process defaults to the host's ANSI code page on Windows.
            // Without this, accented characters in messages are garbled (e.g. French locale).
            // ASCII-only structural tokens (file path, "warning", code) are unaffected
            // either way, but explicit UTF-8 is cleaner and future-proof.
            StandardOutputEncoding  = System.Text.Encoding.UTF8,
        };

        psi.ArgumentList.Add(clean ? "clean" : "build");
        psi.ArgumentList.Add(projectFilePath);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(configuration.Name);

        if (!clean)
        {
            // Minimal verbosity: keeps individual file(line,col): warning/error lines
            // but suppresses the localized "N Warning(s) / N Error(s)" summary block
            // that dotnet emits in the host locale (French on French Windows, etc.).
            // The IDE's BuildOutputAdapter produces its own English summary from
            // BuildSucceededEvent / BuildFailedEvent, so we don't need the dotnet one.
            psi.ArgumentList.Add("-v:m");

            psi.ArgumentList.Add($"-p:Platform={platform}");

            if (!string.IsNullOrWhiteSpace(configuration.OutputPath))
                psi.ArgumentList.Add($"-p:OutputPath={configuration.OutputPath}");

            if (configuration.Optimize)
                psi.ArgumentList.Add("-p:Optimize=true");

            if (!string.IsNullOrWhiteSpace(configuration.DefineConstants))
            {
                // Semicolons in DefineConstants (e.g. "DEBUG;TRACE") are interpreted as
                // argument separators by the dotnet CLI. Percent-encode them so MSBuild
                // receives the full value as a single property.
                var encoded = configuration.DefineConstants.Replace(";", "%3B");
                psi.ArgumentList.Add($"-p:DefineConstants={encoded}");
            }
        }

        // Collect every stdout line so we can parse diagnostics *after* the process
        // has fully exited. Parsing inside OutputDataReceived has a race: callbacks
        // run on thread-pool threads and some may fire after WaitForExitAsync returns.
        // BeginOutputReadLine serialises callbacks for a single stream, so no lock needed.
        var collectedLines = new List<string>();

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            progress?.Report(e.Data);  // real-time stream to Output panel
            collectedLines.Add(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            progress?.Report(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        // WaitForExitAsync alone does NOT guarantee all OutputDataReceived callbacks
        // have fired. The no-arg WaitForExit() overload explicitly flushes the async
        // I/O pump so every line in collectedLines is present before we parse.
        process.WaitForExit();

        // Parse all collected lines now that stdout is fully consumed.
        foreach (var line in collectedLines)
            ParseDiagnostic(line, errors, warnings);

        sw.Stop();
        var success = process.ExitCode == 0;
        return new Editor.Core.BuildResult(success, errors, warnings, sw.Elapsed);
    }

    // -----------------------------------------------------------------------
    // Diagnostic parser
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parses a single MSBuild output line and appends to
    /// <paramref name="errors"/> or <paramref name="warnings"/> if it matches
    /// the structured diagnostic format.
    /// </summary>
    private static void ParseDiagnostic(
        string                  line,
        List<BuildDiagnostic>   errors,
        List<BuildDiagnostic>   warnings)
    {
        var m = _diagnosticPattern.Match(line.Trim());
        if (!m.Success) return;

        var severity = m.Groups["sev"].Value.Equals("error", StringComparison.OrdinalIgnoreCase)
            ? DiagnosticSeverity.Error
            : DiagnosticSeverity.Warning;

        _ = int.TryParse(m.Groups["line"].Value, out var lineNum);
        _ = int.TryParse(m.Groups["col"].Value,  out var colNum);

        var diag = new BuildDiagnostic(
            FilePath:    m.Groups["file"].Value.Trim(),
            Line:        lineNum > 0 ? lineNum : null,
            Column:      colNum  > 0 ? colNum  : null,
            Code:        m.Groups["code"].Value,
            Message:     m.Groups["msg"].Value.Trim(),
            Severity:    severity,
            ProjectName: m.Groups["proj"].Success ? m.Groups["proj"].Value : null);

        if (severity == DiagnosticSeverity.Error)
            errors.Add(diag);
        else
            warnings.Add(diag);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// For <c>.csproj</c>/<c>.vbproj</c>/<c>.fsproj</c>: MSBuild expects "AnyCPU" (no space).
    /// </summary>
    private static string NormalizePlatform(string platform)
        => platform.Replace(" ", string.Empty);

    /// <summary>
    /// For <c>.sln</c> files: MSBuild solution metaprojects require "Any CPU" (with space).
    /// The reverse of <see cref="NormalizePlatform"/>.
    /// </summary>
    private static string NormalizePlatformForSolution(string platform)
        => platform.Equals("AnyCPU", StringComparison.OrdinalIgnoreCase) ? "Any CPU" : platform;
}
