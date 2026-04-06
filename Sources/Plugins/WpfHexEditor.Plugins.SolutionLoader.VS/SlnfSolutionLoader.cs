// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: SlnfSolutionLoader.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-05
// Description:
//     ISolutionLoader implementation for Visual Studio .slnf (solution filter)
//     files. Delegates to VsSolutionLoader after applying the project filter.
//
// Architecture Notes:
//     Pattern: Decorator — loads the referenced .sln via VsSolutionLoader,
//     then filters out projects not in the .slnf project list.
//     .slnf format: JSON { "solution": { "path": "...", "projects": ["..."] } }
// ==========================================================

using System.Text.Json;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.SolutionLoader.VS.VsModels;

namespace WpfHexEditor.Plugins.SolutionLoader.VS;

/// <summary>
/// Loads a Visual Studio <c>.slnf</c> (solution filter) file by delegating to
/// <see cref="VsSolutionLoader"/> and filtering the projects to the subset
/// specified in the filter file.
/// </summary>
public sealed class SlnfSolutionLoader : ISolutionLoader
{
    private readonly VsSolutionLoader _slnLoader = new();

    public string LoaderName => "Visual Studio (slnf)";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions { get; } = ["slnf"];

    /// <inheritdoc />
    public bool CanLoad(string filePath)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        return ext == "slnf";
    }

    /// <inheritdoc />
    public async Task<ISolution> LoadAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Solution filter file not found.", filePath);

        var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var doc  = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("solution", out var solutionElement))
            throw new InvalidDataException($"Invalid .slnf file — missing 'solution' property: {filePath}");

        // Resolve the referenced .sln path.
        var slnRelPath = solutionElement.GetProperty("path").GetString()
                         ?? throw new InvalidDataException($"Invalid .slnf file — missing 'solution.path': {filePath}");

        var slnfDir     = Path.GetDirectoryName(filePath)!;
        var slnAbsPath  = Path.GetFullPath(Path.Combine(slnfDir, slnRelPath));

        if (!File.Exists(slnAbsPath))
            throw new FileNotFoundException($".slnf references missing solution: {slnAbsPath}", slnAbsPath);

        // Load the full solution.
        var solution = await _slnLoader.LoadAsync(slnAbsPath, ct).ConfigureAwait(false);

        // Parse the project filter list.
        var allowedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (solutionElement.TryGetProperty("projects", out var projectsArray)
            && projectsArray.ValueKind == JsonValueKind.Array)
        {
            var slnDir = Path.GetDirectoryName(slnAbsPath)!;
            foreach (var item in projectsArray.EnumerateArray())
            {
                var relPath = item.GetString();
                if (relPath is not null)
                {
                    var absPath = Path.GetFullPath(
                        Path.Combine(slnDir, relPath.Replace('\\', '/')));
                    allowedProjects.Add(absPath);
                }
            }
        }

        // If no filter list, return the full solution.
        if (allowedProjects.Count == 0)
            return solution;

        // Filter projects.
        var filteredProjects = solution.Projects
            .Where(p => allowedProjects.Contains(p.ProjectFilePath))
            .ToList();

        // Rebuild with filtered project list.
        var filteredSolution = new VsSolution
        {
            Name                     = Path.GetFileNameWithoutExtension(filePath),
            FilePath                 = filePath,
            Projects                 = filteredProjects,
            RootFolders              = FilterSolutionFolders(solution.RootFolders, filteredProjects),
            DefaultConfigurationName = (solution as VsSolution)?.DefaultConfigurationName,
            DefaultPlatform          = (solution as VsSolution)?.DefaultPlatform,
        };

        // Preserve startup project if it passes the filter.
        if (solution.StartupProject is not null
            && filteredProjects.Any(p => p.Id == solution.StartupProject.Id))
        {
            filteredSolution.InitStartupProject(
                filteredProjects.First(p => p.Id == solution.StartupProject.Id));
        }

        return filteredSolution;
    }

    /// <summary>
    /// Removes solution folders that contain no filtered projects (recursively).
    /// </summary>
    private static List<ISolutionFolder> FilterSolutionFolders(
        IReadOnlyList<ISolutionFolder> folders,
        IReadOnlyList<IProject> filteredProjects)
    {
        var projectNames = filteredProjects.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<ISolutionFolder>();

        foreach (var folder in folders)
        {
            var filteredChildren = FilterSolutionFolders(folder.Children, filteredProjects);
            var filteredProjectIds = folder.ProjectIds
                .Where(id => projectNames.Contains(id))
                .ToList();

            // Keep the folder if it has any filtered content.
            if (filteredChildren.Count > 0 || filteredProjectIds.Count > 0 || folder.FileItems.Count > 0)
            {
                result.Add(new FilteredSolutionFolder(
                    folder.Id, folder.Name, filteredProjectIds, filteredChildren, folder.FileItems));
            }
        }

        return result;
    }

    private sealed class FilteredSolutionFolder(
        string id, string name,
        List<string> projectIds,
        List<ISolutionFolder> children,
        IReadOnlyList<string> fileItems) : ISolutionFolder
    {
        public string Id   => id;
        public string Name => name;
        public IReadOnlyList<string>          ProjectIds => projectIds;
        public IReadOnlyList<ISolutionFolder> Children   => children;
        public IReadOnlyList<string>          FileItems  => fileItems;
    }
}
