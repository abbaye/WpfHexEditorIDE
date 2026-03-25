// ==========================================================
// Project: WpfHexEditor.Core.WorkspaceTemplates
// File: ProjectScaffolder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Creates the physical project directory structure from a
//     selected IProjectTemplate. Generates .whproj + .whsln files
//     and writes all template content files, substituting tokens.
//
// Architecture Notes:
//     Pattern: Builder — constructs the on-disk project skeleton.
//     Token substitution: {{ProjectName}}, {{RootNamespace}},
//     {{Year}}, {{Date}}, {{Language}}.
//     JSON is written directly (DTOs are internal to ProjectSystem).
// ==========================================================

using System.Text.Json;

namespace WpfHexEditor.Core.WorkspaceTemplates;

/// <summary>
/// Scaffolds a new project from a <see cref="IProjectTemplate"/>.
/// </summary>
public sealed class ProjectScaffolder
{
    // .whproj / .whsln serialization version — must match MigrationPipeline.CurrentVersion
    private const int FormatVersion = 2;

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a new project at <paramref name="parentDirectory"/>/<paramref name="projectName"/>
    /// from the given template.
    /// </summary>
    /// <param name="template">Template to scaffold.</param>
    /// <param name="projectName">Project name (also used as root namespace).</param>
    /// <param name="parentDirectory">Parent directory; project root = parentDirectory\projectName.</param>
    /// <param name="language">Override language (null = template default).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Scaffold result containing paths and warnings.</returns>
    public async Task<ScaffoldResult> ScaffoldAsync(
        IProjectTemplate template,
        string           projectName,
        string           parentDirectory,
        string?          language = null,
        CancellationToken ct = default)
    {
        var projectDir = System.IO.Path.Combine(parentDirectory, projectName);
        System.IO.Directory.CreateDirectory(projectDir);

        var warnings       = new List<string>();
        var generatedFiles = new List<string>();
        var lang           = language ?? template.DefaultLanguage;

        // Build token substitution map.
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProjectName"]    = projectName,
            ["RootNamespace"]  = ToNamespace(projectName),
            ["Language"]       = lang,
            ["Year"]           = DateTime.Now.Year.ToString(),
            ["Date"]           = DateTime.Now.ToString("yyyy-MM-dd"),
        };

        // Write template files.
        foreach (var file in template.Files)
        {
            var absPath  = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectDir, file.RelativePath));
            var content  = SubstituteTokens(file.Content, tokens);
            var dir      = System.IO.Path.GetDirectoryName(absPath)!;

            System.IO.Directory.CreateDirectory(dir);
            await System.IO.File.WriteAllTextAsync(absPath, content, ct);
            generatedFiles.Add(absPath);
        }

        // Generate .whproj.
        var projFileName = $"{projectName}.whproj";
        var projFilePath = System.IO.Path.Combine(projectDir, projFileName);
        var projJson     = BuildProjectJson(projectName, template.Id);
        await System.IO.File.WriteAllTextAsync(projFilePath, projJson, ct);
        generatedFiles.Add(projFilePath);

        // Generate .whsln at the parent directory level.
        var slnPath = System.IO.Path.Combine(parentDirectory, $"{projectName}.whsln");
        if (!System.IO.File.Exists(slnPath))
        {
            // Relative path from solution dir to the project file.
            var relProjPath = System.IO.Path.Combine(projectName, projFileName);
            var slnJson     = BuildSolutionJson(projectName, relProjPath);
            await System.IO.File.WriteAllTextAsync(slnPath, slnJson, ct);
            generatedFiles.Add(slnPath);
        }

        return new ScaffoldResult(projectDir, generatedFiles, warnings);
    }

    // -----------------------------------------------------------------------
    // JSON builders (mirrors internal ProjectSystem DTOs — format version 2)
    // -----------------------------------------------------------------------

    private static string BuildProjectJson(string name, string projectType)
    {
        var dto = new
        {
            version     = FormatVersion,
            name,
            description = (string?)null,
            items       = Array.Empty<object>(),
            virtualFolders = Array.Empty<object>(),
            projectType,
        };
        return JsonSerializer.Serialize(dto, _json);
    }

    private static string BuildSolutionJson(string name, string relativeProjectPath)
    {
        var dto = new
        {
            version  = FormatVersion,
            name,
            created  = DateTimeOffset.UtcNow,
            modified = DateTimeOffset.UtcNow,
            projects = new[]
            {
                new { name, path = relativeProjectPath.Replace('\\', '/') },
            },
        };
        return JsonSerializer.Serialize(dto, _json);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string SubstituteTokens(string content, Dictionary<string, string> tokens)
    {
        foreach (var (key, value) in tokens)
            content = content.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        return content;
    }

    private static string ToNamespace(string name)
        => new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray())
           .TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
}

/// <summary>Result of a scaffold operation.</summary>
public sealed record ScaffoldResult(
    string              ProjectPath,
    IReadOnlyList<string> GeneratedFiles,
    IReadOnlyList<string> Warnings);
