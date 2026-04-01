// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: SolutionUserSerializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Reads and writes the .whsln.user sidecar file.
//     The sidecar stores per-user UI state (tree expand state, dock layout)
//     that must NOT be committed to version control alongside the .whsln file.
//
// Architecture Notes:
//     Pattern: static async helpers (mirrors SolutionSerializer style).
//     ReadAsync returns null when no sidecar exists — callers must handle gracefully.
//     WriteAsync creates the directory if needed.
//
// ==========================================================

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Core.ProjectSystem.Dto;

namespace WpfHexEditor.Core.ProjectSystem.Serialization;

/// <summary>
/// Reads and writes the <c>.whsln.user</c> sidecar file that stores per-user
/// UI state (dock layout, Solution Explorer tree expand/collapse) alongside a solution.
/// </summary>
public static class SolutionUserSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // -- Path helper ------------------------------------------------------

    /// <summary>
    /// Returns the <c>.whsln.user</c> path that corresponds to a given
    /// <c>.whsln</c> file path (same directory, same base name, <c>.user</c> appended).
    /// </summary>
    public static string GetUserFilePath(string solutionFilePath)
        => solutionFilePath + ".user";

    // -- Read -------------------------------------------------------------

    /// <summary>
    /// Reads the Solution Explorer expanded-node keys from the sidecar for
    /// <paramref name="solutionFilePath"/>. Returns <see langword="null"/> when the
    /// sidecar does not exist (first time opening the solution on this machine).
    /// </summary>
    public static async Task<IReadOnlyList<string>?> ReadExpandedKeysAsync(
        string solutionFilePath, CancellationToken ct = default)
    {
        var dto = await ReadDtoAsync(GetUserFilePath(solutionFilePath), ct);
        return dto.TreeState?.ExpandedKeys is { Count: > 0 } keys ? keys : null;
    }

    /// <summary>
    /// Reads the user-persisted startup project path from the sidecar for
    /// <paramref name="solutionFilePath"/>. Returns <see langword="null"/> when the
    /// sidecar does not exist or no preference has been stored yet.
    /// </summary>
    public static async Task<string?> ReadStartupProjectPathAsync(
        string solutionFilePath, CancellationToken ct = default)
    {
        var dto = await ReadDtoAsync(GetUserFilePath(solutionFilePath), ct);
        return string.IsNullOrWhiteSpace(dto.StartupProjectPath) ? null : dto.StartupProjectPath;
    }

    // -- Write ------------------------------------------------------------

    /// <summary>
    /// Writes the Solution Explorer <paramref name="expandedKeys"/> to the sidecar file
    /// alongside <paramref name="solutionFilePath"/>. Preserves all other sidecar fields.
    /// </summary>
    public static async Task WriteTreeStateAsync(
        string solutionFilePath, IReadOnlyList<string> expandedKeys, CancellationToken ct = default)
    {
        var userFilePath = GetUserFilePath(solutionFilePath);
        var dto          = await ReadDtoAsync(userFilePath, ct);
        dto.TreeState    = new SolutionTreeStateDto { ExpandedKeys = [.. expandedKeys] };
        await WriteDtoAsync(userFilePath, dto, ct);
    }

    /// <summary>
    /// Persists the user's startup project choice to the sidecar file alongside
    /// <paramref name="solutionFilePath"/>. Preserves all other sidecar fields.
    /// Pass <see langword="null"/> to clear the stored preference.
    /// </summary>
    public static async Task WriteStartupProjectPathAsync(
        string solutionFilePath, string? relativeProjectPath, CancellationToken ct = default)
    {
        var userFilePath         = GetUserFilePath(solutionFilePath);
        var dto                  = await ReadDtoAsync(userFilePath, ct);
        dto.StartupProjectPath   = relativeProjectPath;
        await WriteDtoAsync(userFilePath, dto, ct);
    }

    // -- Private helpers --------------------------------------------------

    /// <summary>
    /// Reads the sidecar DTO, returning an empty DTO when the file is absent or corrupt.
    /// </summary>
    private static async Task<SolutionUserDto> ReadDtoAsync(
        string userFilePath, CancellationToken ct)
    {
        if (!File.Exists(userFilePath)) return new SolutionUserDto();
        try
        {
            await using var stream = File.OpenRead(userFilePath);
            return await JsonSerializer.DeserializeAsync<SolutionUserDto>(stream, _options, ct)
                   ?? new SolutionUserDto();
        }
        catch
        {
            // Corrupt or unreadable sidecar — treat as missing.
            return new SolutionUserDto();
        }
    }

    /// <summary>Writes the DTO to disk, creating the directory if needed.</summary>
    private static async Task WriteDtoAsync(
        string userFilePath, SolutionUserDto dto, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(userFilePath)!);
        await using var stream = File.Create(userFilePath);
        await JsonSerializer.SerializeAsync(stream, dto, _options, ct);
    }
}
