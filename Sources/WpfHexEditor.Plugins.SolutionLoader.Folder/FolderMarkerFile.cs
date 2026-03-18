// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.Folder
// File: FolderMarkerFile.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Read/write helper for the .whfolder JSON marker file.
//     The marker anchors a folder session: stores root path,
//     exclude patterns, gitignore flag, and timestamps.
//     Uses System.Text.Json (inbox in net8.0, no NuGet required).
//
// Architecture Notes:
//     Pattern: Value Object + Static Factory
//     The marker is immutable after creation; callers get a fresh instance
//     on each ReadOrCreateAsync call. lastOpened is updated on every load.
// ==========================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Plugins.SolutionLoader.Folder;

/// <summary>
/// Represents the content of a <c>.whfolder</c> marker file.
/// </summary>
public sealed class FolderMarker
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    /// <summary>Relative path from the marker file to the root of the folder session. Always ".".</summary>
    [JsonPropertyName("rootPath")]
    public string RootPath { get; init; } = ".";

    /// <summary>Display name shown in the Solution Explorer title.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Directory/segment names to skip during enumeration.</summary>
    [JsonPropertyName("excludePatterns")]
    public IReadOnlyList<string> ExcludePatterns { get; init; } = DefaultExcludes;

    /// <summary>When false, files/directories whose Hidden attribute is set are skipped.</summary>
    [JsonPropertyName("includeHidden")]
    public bool IncludeHidden { get; init; } = false;

    /// <summary>When true, .gitignore files at each level are loaded and applied.</summary>
    [JsonPropertyName("useGitIgnore")]
    public bool UseGitIgnore { get; init; } = true;

    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("lastOpened")]
    public DateTimeOffset LastOpened { get; init; } = DateTimeOffset.UtcNow;

    // -----------------------------------------------------------------------
    // Defaults
    // -----------------------------------------------------------------------

    internal static readonly IReadOnlyList<string> DefaultExcludes =
    [
        "obj", "bin", ".git", ".vs", ".idea",
        "node_modules", "__pycache__", ".venv", "venv",
        ".gradle", "build", "target", "out", "dist",
        "Thumbs.db", ".DS_Store",
    ];
}

/// <summary>
/// Static helper to read, create, and update <c>.whfolder</c> marker files.
/// </summary>
public static class FolderMarkerFile
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads an existing <c>.whfolder</c> at <paramref name="markerPath"/>.
    /// Updates <c>lastOpened</c> and writes it back.
    /// </summary>
    public static async Task<FolderMarker> ReadOrCreateAsync(
        string markerPath, CancellationToken ct = default)
    {
        FolderMarker marker;

        if (File.Exists(markerPath))
        {
            await using var stream = File.OpenRead(markerPath);
            marker = await JsonSerializer.DeserializeAsync<FolderMarker>(stream, JsonOpts, ct)
                     ?? CreateDefault(markerPath);
        }
        else
        {
            marker = CreateDefault(markerPath);
        }

        // Refresh lastOpened timestamp.
        var updated = new FolderMarker
        {
            Version         = marker.Version,
            RootPath        = marker.RootPath,
            Name            = marker.Name,
            ExcludePatterns = marker.ExcludePatterns,
            IncludeHidden   = marker.IncludeHidden,
            UseGitIgnore    = marker.UseGitIgnore,
            Created         = marker.Created,
            LastOpened      = DateTimeOffset.UtcNow,
        };

        await WriteAsync(markerPath, updated, ct);
        return updated;
    }

    /// <summary>
    /// Creates the marker file if absent; leaves it unchanged if it already exists.
    /// Used by <c>MainWindow.OpenFolderAsSolutionAsync</c> before routing to the loader.
    /// </summary>
    public static async Task EnsureExistsAsync(
        string markerPath, string folderName, CancellationToken ct = default)
    {
        if (File.Exists(markerPath)) return;

        var marker = new FolderMarker
        {
            Name    = folderName,
            Created = DateTimeOffset.UtcNow,
        };

        await WriteAsync(markerPath, marker, ct);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static FolderMarker CreateDefault(string markerPath)
    {
        var folderName = Path.GetFileNameWithoutExtension(markerPath);
        return new FolderMarker { Name = folderName };
    }

    private static async Task WriteAsync(
        string markerPath, FolderMarker marker, CancellationToken ct)
    {
        await using var stream = File.Open(markerPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, marker, JsonOpts, ct);
    }
}
