// ==========================================================
// Project: WpfHexEditor.App
// File: Services/SolutionBreakpointStore.cs
// Description:
//     Reads/writes per-solution breakpoint JSON files stored in
//     <solutionDir>/.whide/breakpoints.json.
//     Thread-safe. Falls back gracefully when the file is missing.
// Architecture:
//     Stateless helper called by BreakpointPersistenceManager.
//     No dependency on WPF or SDK — pure I/O.
// ==========================================================

using System.IO;
using System.Text.Json;
using WpfHexEditor.Core.Debugger.Models;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Per-solution breakpoint storage: <c>&lt;solutionDir&gt;/.whide/breakpoints.json</c>.
/// </summary>
internal sealed class SolutionBreakpointStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented   = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly object _lock = new();

    /// <summary>
    /// Load breakpoints from the solution-local store.
    /// Returns empty list when the file does not exist or is malformed.
    /// </summary>
    public IReadOnlyList<BreakpointLocation> Load(string solutionFilePath)
    {
        var jsonPath = GetJsonPath(solutionFilePath);
        if (!File.Exists(jsonPath)) return [];

        lock (_lock)
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var dtos = JsonSerializer.Deserialize<List<PersistedBp>>(json, JsonOptions);
                if (dtos is null) return [];

                return dtos.Select(d => new BreakpointLocation
                {
                    FilePath  = d.FilePath ?? string.Empty,
                    Line      = d.Line,
                    Condition = d.Condition ?? string.Empty,
                    IsEnabled = d.IsEnabled,
                }).ToList();
            }
            catch
            {
                return [];
            }
        }
    }

    /// <summary>
    /// Save breakpoints to the solution-local store.
    /// Creates the <c>.whide/</c> directory if missing.
    /// </summary>
    public void Save(string solutionFilePath, IEnumerable<BreakpointLocation> breakpoints)
    {
        var jsonPath = GetJsonPath(solutionFilePath);

        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(jsonPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var dtos = breakpoints.Select(b => new PersistedBp
                {
                    FilePath  = b.FilePath,
                    Line      = b.Line,
                    Condition = b.Condition,
                    IsEnabled = b.IsEnabled,
                }).ToList();

                var json = JsonSerializer.Serialize(dtos, JsonOptions);
                File.WriteAllText(jsonPath, json);
            }
            catch
            {
                // Silently fail — solution dir may be read-only.
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GetJsonPath(string solutionFilePath)
    {
        var solutionDir = Path.GetDirectoryName(solutionFilePath) ?? string.Empty;
        return Path.Combine(solutionDir, ".whide", "breakpoints.json");
    }

    /// <summary>Local serialization DTO (minimal, no SDK dependency).</summary>
    private sealed class PersistedBp
    {
        public string? FilePath  { get; set; }
        public int     Line      { get; set; }
        public string? Condition { get; set; }
        public bool    IsEnabled { get; set; }
    }
}
