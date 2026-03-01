//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.ProjectSystem.Dto;
using WpfHexEditor.ProjectSystem.Models;

namespace WpfHexEditor.ProjectSystem.Serialization;

internal static class SolutionSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters           = { new JsonStringEnumConverter() },
    };

    // ── Read ─────────────────────────────────────────────────────────────

    public static async Task<Solution> ReadAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var dto = await JsonSerializer.DeserializeAsync<SolutionDto>(stream, _options, ct)
                  ?? throw new InvalidDataException($"Cannot deserialise solution: {filePath}");

        var solution = new Solution
        {
            Name     = dto.Name,
            FilePath = filePath,
        };

        var solutionDir = Path.GetDirectoryName(filePath)!;

        foreach (var projRef in dto.Projects)
        {
            var projPath = Path.GetFullPath(Path.Combine(solutionDir, projRef.Path.Replace('/', Path.DirectorySeparatorChar)));
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

        return solution;
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
