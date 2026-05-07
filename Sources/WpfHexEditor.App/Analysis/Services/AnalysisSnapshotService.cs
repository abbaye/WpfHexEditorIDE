// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/AnalysisSnapshotService.cs
// Description: Persists the last analysis report as a lightweight snapshot
//              JSON file for trending comparisons. Manages retention.
//              Location: <solution-dir>/.ide/analysis-snapshot.json
// ==========================================================

using System.Text.Json;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Services;

internal sealed class AnalysisSnapshotService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private string _solutionDir = string.Empty;

    internal void SetSolutionDirectory(string dir) => _solutionDir = dir;

    internal SnapshotEntry? LoadLatest()
    {
        var path = GetPath();
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SnapshotEntry>(json, JsonOpts);
        }
        catch { return null; }
    }

    internal void Save(CodeAnalysisReport report)
    {
        var path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var entry = new SnapshotEntry
        {
            Timestamp    = report.Timestamp,
            Score        = report.Score.Score,
            TotalFiles   = report.TotalFiles,
            TotalLines   = report.TotalLines,
        };

        File.WriteAllText(path, JsonSerializer.Serialize(entry, JsonOpts));
    }

    internal void Clear()
    {
        var path = GetPath();
        if (File.Exists(path)) File.Delete(path);
    }

    private string GetPath()
    {
        var baseDir = string.IsNullOrEmpty(_solutionDir)
            ? AppDomain.CurrentDomain.BaseDirectory
            : _solutionDir;
        return Path.Combine(baseDir, ".ide", "analysis-snapshot.json");
    }

    internal sealed class SnapshotEntry
    {
        public DateTime Timestamp  { get; set; }
        public int      Score      { get; set; }
        public int      TotalFiles { get; set; }
        public int      TotalLines { get; set; }
    }
}
