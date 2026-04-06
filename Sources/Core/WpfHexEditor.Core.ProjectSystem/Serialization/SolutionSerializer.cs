//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.ProjectSystem.Dto;
using WpfHexEditor.Core.ProjectSystem.Models;
using WpfHexEditor.Core.ProjectSystem.Serialization.Migration;
using System.Collections.ObjectModel;

namespace WpfHexEditor.Core.ProjectSystem.Serialization;

internal static class SolutionSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() },
    };

    // -- Read -------------------------------------------------------------

    /// <summary>
    /// Reads a .whsln file, runs in-memory migration if needed, and returns the
    /// runtime <see cref="Solution"/> model together with an optional migrated
    /// dock-layout JSON string (for writing to the .whsln.user sidecar later).
    /// Files on disk are never modified here.
    /// </summary>
    public static async Task<(Solution Solution, string? MigratedDockLayout)> ReadAsync(
        string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var dto = await JsonSerializer.DeserializeAsync<SolutionDto>(stream, _options, ct)
                  ?? throw new InvalidDataException($"Cannot deserialise solution: {filePath}");

        // Reject files from future versions the app cannot understand.
        if (MigrationPipeline.IsNewerThanSupported(dto.Version))
            throw new InvalidDataException(
                $"Solution '{filePath}' uses format version {dto.Version}, " +
                $"but this application supports up to version {MigrationPipeline.CurrentVersion}. " +
                "Please update the application.");

        int sourceVersion = dto.Version;

        // Migrate older versions in memory (does NOT write back to disk yet).
        MigrationPipeline.UpgradeSolution(dto);
        string? migratedDockLayout = dto.MigratedDockLayout;

        var solution = new Solution
        {
            Name                = dto.Name,
            FilePath            = filePath,
            SourceFormatVersion = sourceVersion,
        };

        var solutionDir = Path.GetDirectoryName(filePath)!;

        foreach (var projRef in dto.Projects)
        {
            var projPath = Path.GetFullPath(
                Path.Combine(solutionDir, projRef.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(projPath))
            {
                var project = await ProjectSerializer.ReadAsync(projPath, ct);
                solution.ProjectsMutable.Add(project);
            }
        }

        // Rebuild Solution Folder tree from DTO.
        if (dto.SolutionFolders is { Count: > 0 })
            foreach (var folderDto in dto.SolutionFolders)
                solution.RootFoldersMutable.Add(DtoToSolutionFolder(folderDto));

        // Assign each project to its folder using the SolutionFolderId on the project ref.
        foreach (var projRef in dto.Projects.Where(p => p.SolutionFolderId is not null))
        {
            var folder = FindSolutionFolder(solution.RootFoldersMutable, projRef.SolutionFolderId!);
            folder?.ProjectIdsMutable.Add(projRef.Name);
        }

        if (dto.StartupProject is not null)
        {
            var startup = solution.ProjectsMutable.FirstOrDefault(p => p.Name == dto.StartupProject);
            solution.SetStartupProject(startup);
        }

        return (solution, migratedDockLayout);
    }

    // -- Write -------------------------------------------------------------

    public static async Task WriteAsync(Solution solution, CancellationToken ct = default)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath)!;

        var dto = new SolutionDto
        {
            Name           = solution.Name,
            Modified       = DateTimeOffset.UtcNow,
            StartupProject = solution.StartupProject?.Name,
            // DockLayout intentionally omitted — belongs in .whsln.user (v2+)
        };

        foreach (var proj in solution.ProjectsMutable)
        {
            var relPath  = Path.GetRelativePath(solutionDir, proj.ProjectFilePath)
                               .Replace(Path.DirectorySeparatorChar, '/');
            var folderId = FindProjectSolutionFolderId(solution.RootFoldersMutable, proj.Name);
            dto.Projects.Add(new SolutionProjectRefDto
            {
                Name             = proj.Name,
                Path             = relPath,
                SolutionFolderId = folderId,
            });
        }

        // Serialize Solution Folder tree (omitted when empty for backwards compatibility).
        if (solution.RootFoldersMutable.Count > 0)
            dto.SolutionFolders = solution.RootFoldersMutable
                .Select(SolutionFolderToDto)
                .ToList();

        Directory.CreateDirectory(solutionDir);
        await using var stream = File.Create(solution.FilePath);
        await JsonSerializer.SerializeAsync(stream, dto, _options, ct);
    }

    // -- Solution Folder mapping helpers -----------------------------------

    private static SolutionFolder DtoToSolutionFolder(SolutionFolderDto dto)
    {
        var folder = new SolutionFolder { Id = dto.Id, Name = dto.Name };
        if (dto.ProjectIds is not null)
            foreach (var id in dto.ProjectIds)
                folder.ProjectIdsMutable.Add(id);
        if (dto.Children is not null)
            foreach (var child in dto.Children)
                folder.ChildrenMutable.Add(DtoToSolutionFolder(child));
        if (dto.FileItems is not null)
            foreach (var file in dto.FileItems)
                folder.FileItemsMutable.Add(file);
        return folder;
    }

    private static SolutionFolderDto SolutionFolderToDto(SolutionFolder folder) => new()
    {
        Id         = folder.Id,
        Name       = folder.Name,
        ProjectIds = folder.ProjectIdsMutable.Count > 0 ? [.. folder.ProjectIdsMutable] : null,
        Children   = folder.ChildrenMutable.Count > 0
            ? folder.ChildrenMutable.Select(SolutionFolderToDto).ToList()
            : null,
        FileItems  = folder.FileItemsMutable.Count > 0 ? [.. folder.FileItemsMutable] : null,
    };

    internal static SolutionFolder? FindSolutionFolder(
        IEnumerable<SolutionFolder> roots, string id)
    {
        foreach (var f in roots)
        {
            if (f.Id == id) return f;
            var found = FindSolutionFolder(f.ChildrenMutable, id);
            if (found is not null) return found;
        }
        return null;
    }

    private static string? FindProjectSolutionFolderId(
        IEnumerable<SolutionFolder> roots, string projectName)
    {
        foreach (var f in roots)
        {
            if (f.ProjectIdsMutable.Contains(projectName)) return f.Id;
            var found = FindProjectSolutionFolderId(f.ChildrenMutable, projectName);
            if (found is not null) return found;
        }
        return null;
    }
}
