//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.ProjectSystem.Dto;
using WpfHexEditor.ProjectSystem.Models;
using WpfHexEditor.ProjectSystem.Serialization.Migration;

namespace WpfHexEditor.ProjectSystem.Serialization;

internal static class ProjectSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() },
    };

    // -- Read -------------------------------------------------------------

    public static async Task<Project> ReadAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var dto = await JsonSerializer.DeserializeAsync<ProjectDto>(stream, _options, ct)
                  ?? throw new InvalidDataException($"Cannot deserialise project: {filePath}");

        // Reject files from future versions the app cannot understand.
        if (MigrationPipeline.IsNewerThanSupported(dto.Version))
            throw new InvalidDataException(
                $"Project '{filePath}' uses format version {dto.Version}, " +
                $"but this application supports up to version {MigrationPipeline.CurrentVersion}. " +
                "Please update the application.");

        // Migrate older versions in memory (does NOT write back to disk yet).
        MigrationPipeline.UpgradeProject(dto);

        var projectDir = Path.GetDirectoryName(filePath)!;
        var project = new Project
        {
            Name             = dto.Name,
            ProjectFilePath  = filePath,
            DefaultTblItemId = dto.DefaultTblItemId,
            ProjectType      = dto.ProjectType,
        };

        foreach (var itemDto in dto.Items)
        {
            var absPath = Path.GetFullPath(
                Path.Combine(projectDir, itemDto.Path.Replace('/', Path.DirectorySeparatorChar)));

            var item = new ProjectItem
            {
                Id           = itemDto.Id,
                Name         = itemDto.Name,
                RelativePath = itemDto.Path,
                AbsolutePath = absPath,
                ItemType     = itemDto.Type,
                EditorConfig = itemDto.EditorConfig,
                Bookmarks    = itemDto.Bookmarks?.Count > 0 ? itemDto.Bookmarks.AsReadOnly() : null,
            };

            if (itemDto.UnsavedModifications is not null)
                item.UnsavedModifications = Convert.FromBase64String(itemDto.UnsavedModifications);

            // Restore linked items (v2+).
            if (itemDto.LinkedItems is { Count: > 0 })
            {
                foreach (var linkDto in itemDto.LinkedItems)
                    item.LinkedItemsMutable.Add(new ItemLink { ItemId = linkDto.ItemId, Role = linkDto.Role });
            }

            project.ItemsMutable.Add(item);
        }

        // Rebuild virtual folder tree.
        foreach (var folderDto in dto.VirtualFolders)
            project.RootFoldersMutable.Add(MapFolder(folderDto));

        return project;
    }

    // -- Write -------------------------------------------------------------

    public static async Task WriteAsync(Project project, CancellationToken ct = default)
    {
        var projectDir = Path.GetDirectoryName(project.ProjectFilePath)!;

        var dto = new ProjectDto { Name = project.Name, DefaultTblItemId = project.DefaultTblItemId, ProjectType = project.ProjectType };

        foreach (var item in project.ItemsMutable)
        {
            var relPath = Path.GetRelativePath(projectDir, item.AbsolutePath)
                              .Replace(Path.DirectorySeparatorChar, '/');

            var itemDto = new ProjectItemDto
            {
                Id           = item.Id,
                Type         = item.ItemType,
                Name         = item.Name,
                Path         = relPath,
                EditorConfig = item.EditorConfig,
                UnsavedModifications = item.UnsavedModifications is not null
                    ? Convert.ToBase64String(item.UnsavedModifications)
                    : null,
                // TargetItemId is intentionally null on write (v2 uses linkedItems).
                Bookmarks = item.Bookmarks?.Count > 0
                    ? item.Bookmarks.ToList()
                    : null,
                LinkedItems = item.LinkedItemsMutable.Count > 0
                    ? item.LinkedItemsMutable.Select(l => new ItemLinkDto { ItemId = l.ItemId, Role = l.Role }).ToList()
                    : null,
            };
            dto.Items.Add(itemDto);
        }

        foreach (var folder in project.RootFoldersMutable)
            dto.VirtualFolders.Add(MapFolderToDto(folder));

        Directory.CreateDirectory(projectDir);
        await using var stream = File.Create(project.ProjectFilePath);
        await JsonSerializer.SerializeAsync(stream, dto, _options, ct);
    }

    // -- Helpers -----------------------------------------------------------

    private static VirtualFolder MapFolder(VirtualFolderDto dto)
    {
        var folder = new VirtualFolder
        {
            Id                   = dto.Id,
            Name                 = dto.Name,
            PhysicalRelativePath = dto.PhysicalRelativePath,
        };
        foreach (var id in dto.ItemIds)
            folder.ItemIdsMutable.Add(id);
        foreach (var child in dto.Children)
            folder.ChildrenMutable.Add(MapFolder(child));
        return folder;
    }

    private static VirtualFolderDto MapFolderToDto(VirtualFolder folder)
    {
        var dto = new VirtualFolderDto
        {
            Id                   = folder.Id,
            Name                 = folder.Name,
            PhysicalRelativePath = folder.PhysicalRelativePath,
        };
        dto.ItemIds.AddRange(folder.ItemIdsMutable);
        foreach (var child in folder.ChildrenMutable)
            dto.Children.Add(MapFolderToDto(child));
        return dto;
    }
}
