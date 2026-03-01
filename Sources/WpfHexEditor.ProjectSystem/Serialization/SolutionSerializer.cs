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

internal static class SolutionSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() },
    };

    // ── Read ─────────────────────────────────────────────────────────────

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

        if (dto.StartupProject is not null)
        {
            var startup = solution.ProjectsMutable.FirstOrDefault(p => p.Name == dto.StartupProject);
            solution.SetStartupProject(startup);
        }

        return (solution, migratedDockLayout);
    }

    // ── Write ─────────────────────────────────────────────────────────────

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
            var relPath = Path.GetRelativePath(solutionDir, proj.ProjectFilePath)
                              .Replace(Path.DirectorySeparatorChar, '/');
            dto.Projects.Add(new SolutionProjectRefDto { Name = proj.Name, Path = relPath });
        }

        Directory.CreateDirectory(solutionDir);
        await using var stream = File.Create(solution.FilePath);
        await JsonSerializer.SerializeAsync(stream, dto, _options, ct);
    }
}
