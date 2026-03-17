// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: DotNetProjectTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Abstract base class for self-contained .NET project templates.
//     Handles .sln generation and directory creation; subclasses provide
//     the .csproj content and source files.
//
// Architecture Notes:
//     Pattern: Template Method
//     Implements ISelfContainedProjectTemplate — bypasses .whproj creation flow.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.ProjectSystem.Templates;

namespace WpfHexEditor.Plugins.SolutionLoader.VS.Templates;

/// <summary>
/// Base class for .NET project templates (Console, ClassLib, WPF, WebAPI).
/// Generates a standard VS 2022 .sln + .csproj structure on disk.
/// </summary>
internal abstract class DotNetProjectTemplate : ISelfContainedProjectTemplate
{
    // C# project type GUID — used in .sln format
    private const string CSharpProjectTypeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";

    public abstract string Id          { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public string          Category    => "Development";

    // ScaffoldAsync is not used for self-contained templates (CreateAsync is the entry point).
    public Task<ProjectScaffold> ScaffoldAsync(string projectDirectory, string projectName,
                                               CancellationToken ct = default)
        => Task.FromResult(new ProjectScaffold());

    /// <inheritdoc/>
    public async Task<string> CreateAsync(string parentDirectory, string projectName,
                                           CancellationToken ct = default)
    {
        var projectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
        var projectDir  = Path.Combine(parentDirectory, projectName);
        var slnPath     = Path.Combine(parentDirectory, $"{projectName}.sln");

        Directory.CreateDirectory(projectDir);

        await WriteCsprojAsync(projectDir, projectName, ct);
        await WriteSourceFilesAsync(projectDir, projectName, ct);
        await File.WriteAllTextAsync(slnPath, BuildSln(projectName, projectGuid), ct);

        return slnPath;
    }

    /// <inheritdoc/>
    public async Task<string> AddToSolutionAsync(string existingSlnPath, string parentDirectory,
                                                   string projectName, CancellationToken ct = default)
    {
        var projectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
        var projectDir  = Path.Combine(parentDirectory, projectName);

        Directory.CreateDirectory(projectDir);

        await WriteCsprojAsync(projectDir, projectName, ct);
        await WriteSourceFilesAsync(projectDir, projectName, ct);

        // Compute .csproj path relative to the .sln file (VS requirement).
        var slnDir      = Path.GetDirectoryName(existingSlnPath) ?? "";
        var csprojAbs   = Path.Combine(projectDir, $"{projectName}.csproj");
        var csprojRel   = Path.GetRelativePath(slnDir, csprojAbs).Replace('/', '\\');

        await PatchSlnAsync(existingSlnPath, projectName, projectGuid, csprojRel, ct);

        return existingSlnPath;
    }

    // Appends a project entry to an existing .sln file and registers build configurations.
    private static async Task PatchSlnAsync(string slnPath, string projectName,
                                             string projectGuid, string csprojRelPath,
                                             CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(slnPath, ct);

        // Insert Project(...)...EndProject block just before "Global".
        var projectBlock =
            $"Project(\"{CSharpProjectTypeGuid}\") = \"{projectName}\", \"{csprojRelPath}\", \"{projectGuid}\"\r\n" +
            $"EndProject\r\n";

        content = content.Replace("\r\nGlobal\r\n", $"\r\n{projectBlock}Global\r\n");
        if (!content.Contains(projectBlock))
        {
            // Fallback: sln uses LF line endings
            content = content.Replace("\nGlobal\n", $"\n{projectBlock}Global\n");
        }

        // Insert build configuration entries before the closing EndGlobalSection of ProjectConfigurationPlatforms.
        var configEntries =
            $"\t\t{projectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU\r\n" +
            $"\t\t{projectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU\r\n" +
            $"\t\t{projectGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU\r\n" +
            $"\t\t{projectGuid}.Release|Any CPU.Build.0 = Release|Any CPU\r\n";

        const string configSectionMarker = "GlobalSection(ProjectConfigurationPlatforms)";
        var markerIdx = content.IndexOf(configSectionMarker, StringComparison.Ordinal);
        if (markerIdx >= 0)
        {
            // Find the EndGlobalSection that closes this section.
            var endIdx = content.IndexOf("EndGlobalSection", markerIdx, StringComparison.Ordinal);
            if (endIdx >= 0)
                content = content.Insert(endIdx, configEntries);
        }

        await File.WriteAllTextAsync(slnPath, content, System.Text.Encoding.UTF8, ct);
    }

    /// <summary>Writes the .csproj file into <paramref name="projectDir"/>.</summary>
    protected abstract Task WriteCsprojAsync(string projectDir, string projectName,
                                              CancellationToken ct);

    /// <summary>Writes all source files into <paramref name="projectDir"/>.</summary>
    protected abstract Task WriteSourceFilesAsync(string projectDir, string projectName,
                                                   CancellationToken ct);

    // Generates a standard Visual Studio 2022 solution file.
    private string BuildSln(string projectName, string projectGuid) =>
        $"""

        Microsoft Visual Studio Solution File, Format Version 12.00
        # Visual Studio Version 17
        VisualStudioVersion = 17.0.31903.59
        MinimumVisualStudioVersion = 10.0.40219.1
        Project("{CSharpProjectTypeGuid}") = "{projectName}", "{projectName}\{projectName}.csproj", "{projectGuid}"
        EndProject
        Global
        	GlobalSection(SolutionConfigurationPlatforms) = preSolution
        		Debug|Any CPU = Debug|Any CPU
        		Release|Any CPU = Release|Any CPU
        	EndGlobalSection
        	GlobalSection(ProjectConfigurationPlatforms) = postSolution
        		{projectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        		{projectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU
        		{projectGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU
        		{projectGuid}.Release|Any CPU.Build.0 = Release|Any CPU
        	EndGlobalSection
        EndGlobal
        """;

    protected static Task WriteAsync(string path, string content, CancellationToken ct) =>
        File.WriteAllTextAsync(path, content, System.Text.Encoding.UTF8, ct);
}
