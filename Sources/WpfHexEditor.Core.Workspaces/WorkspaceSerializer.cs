// ==========================================================
// Project: WpfHexEditor.Core.Workspaces
// File: WorkspaceSerializer.cs
// Description:
//     Reads and writes .whidews workspace files.
//     Format: ZIP archive containing manifest.json, layout.json,
//     solution.json, openfiles.json, and settings.json.
// Architecture: Static, stateless — pure I/O with no side-effects.
// ==========================================================

using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Core.Workspaces;

/// <summary>
/// Reads and writes <c>.whidews</c> workspace archive files.
/// </summary>
public static class WorkspaceSerializer
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNameCaseInsensitive    = true,
        PropertyNamingPolicy           = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition         = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented                  = true,
    };

    private const string ManifestEntry  = "manifest.json";
    private const string LayoutEntry    = "layout.json";
    private const string SolutionEntry  = "solution.json";
    private const string OpenFilesEntry = "openfiles.json";
    private const string SettingsEntry  = "settings.json";

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>Writes a <see cref="WorkspaceState"/> to a .whidews file.</summary>
    public static async Task WriteAsync(
        string         filePath,
        WorkspaceState state,
        CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = new FileStream(
            filePath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, useAsync: true);

        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        await WriteEntryAsync(zip, ManifestEntry,  state.Manifest,  ct);
        await WriteRawEntryAsync(zip, LayoutEntry, state.Layout,     ct);
        await WriteEntryAsync(zip, SolutionEntry,  state.Solution,  ct);
        await WriteEntryAsync(zip, OpenFilesEntry, state.Files,     ct);
        await WriteEntryAsync(zip, SettingsEntry,  state.Settings,  ct);
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>Reads a .whidews file and returns its <see cref="WorkspaceState"/>.</summary>
    public static async Task<WorkspaceState> ReadAsync(
        string filePath,
        CancellationToken ct = default)
    {
        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, useAsync: true);

        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var manifest  = await ReadEntryAsync<WorkspaceManifest>(zip, ManifestEntry, ct)
                        ?? new WorkspaceManifest("Unnamed");
        var layout    = await ReadRawEntryAsync(zip, LayoutEntry, ct) ?? string.Empty;
        var solution  = await ReadEntryAsync<WorkspaceSolutionState>(zip, SolutionEntry, ct)
                        ?? new WorkspaceSolutionState(null);
        var files     = await ReadEntryAsync<List<OpenFileEntry>>(zip, OpenFilesEntry, ct)
                        ?? [];
        var settings  = await ReadEntryAsync<WorkspaceSettingsOverride>(zip, SettingsEntry, ct)
                        ?? new WorkspaceSettingsOverride(null);

        return new WorkspaceState
        {
            Manifest = manifest,
            Layout   = layout,
            Solution = solution,
            Files    = files,
            Settings = settings,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task WriteEntryAsync<T>(
        ZipArchive zip, string name, T value, CancellationToken ct)
    {
        var entry  = zip.CreateEntry(name, CompressionLevel.SmallestSize);
        await using var ws = entry.Open();
        await JsonSerializer.SerializeAsync(ws, value, s_json, ct);
    }

    private static async Task WriteRawEntryAsync(
        ZipArchive zip, string name, string content, CancellationToken ct)
    {
        var entry  = zip.CreateEntry(name, CompressionLevel.SmallestSize);
        await using var ws = entry.Open();
        await using var sw = new StreamWriter(ws, System.Text.Encoding.UTF8);
        await sw.WriteAsync(content.AsMemory(), ct);
    }

    private static async Task<T?> ReadEntryAsync<T>(
        ZipArchive zip, string name, CancellationToken ct)
    {
        var entry = zip.GetEntry(name);
        if (entry is null) return default;

        await using var rs = entry.Open();
        return await JsonSerializer.DeserializeAsync<T>(rs, s_json, ct);
    }

    private static async Task<string?> ReadRawEntryAsync(
        ZipArchive zip, string name, CancellationToken ct)
    {
        var entry = zip.GetEntry(name);
        if (entry is null) return null;

        await using var rs = entry.Open();
        using var sr = new StreamReader(rs, System.Text.Encoding.UTF8);
        return await sr.ReadToEndAsync(ct);
    }
}
