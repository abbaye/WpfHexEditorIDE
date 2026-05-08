// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/AnalysisSnapshotService.cs
// Description: Persists analysis snapshots as a rolling JSON history file.
//              Keeps the last N snapshots (default 100) for trending charts +
//              sparklines. Provides LoadLatest() for quick last-score lookup
//              (used to compute the trending delta).
//              Location: <solution-dir>/.ide/analysis-history.json
// ==========================================================

using System.IO;
using System.Text.Json;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Services;

internal sealed class AnalysisSnapshotService
{
    private const int MaxEntries = 100;

    private string _solutionDir = string.Empty;

    internal void SetSolutionDirectory(string dir) => _solutionDir = dir;

    internal SnapshotEntry? LoadLatest()
        => LoadAll().LastOrDefault();

    internal IReadOnlyList<SnapshotEntry> LoadAll()
    {
        var path = GetPath();
        if (!File.Exists(path)) return [];

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<SnapshotEntry>>(json, AnalysisJson.Default) ?? [];
        }
        catch { return []; }
    }

    internal void Save(CodeAnalysisReport report)
    {
        var path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var entry = new SnapshotEntry
        {
            Timestamp    = report.Timestamp,
            Score        = report.Score.Score,
            Grade        = report.Score.Grade,
            TotalFiles   = report.TotalFiles,
            TotalLines   = report.TotalLines,
            Issues       = report.Diagnostics.Count,
            Duplications = report.Duplications.Count,
            DeadSymbols  = report.DeadSymbols.Count,
            VolumeScore     = report.Score.VolumeScore,
            ComplexityScore = report.Score.ComplexityScore,
            CouplingScore   = report.Score.CouplingScore,
            DuplicationScore= report.Score.DuplicationScore,
            DeadCodeScore   = report.Score.DeadCodeScore,
            ConventionScore = report.Score.ConventionScore,
        };

        var history = LoadAll().ToList();
        history.Add(entry);
        if (history.Count > MaxEntries)
            history = history.Skip(history.Count - MaxEntries).ToList();

        File.WriteAllText(path, JsonSerializer.Serialize(history, AnalysisJson.Default));
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
        return Path.Combine(baseDir, ".ide", "analysis-history.json");
    }

    internal sealed class SnapshotEntry
    {
        public DateTime Timestamp        { get; set; }
        public int      Score            { get; set; }
        public string   Grade            { get; set; } = "?";
        public int      TotalFiles       { get; set; }
        public int      TotalLines       { get; set; }
        public int      Issues           { get; set; }
        public int      Duplications     { get; set; }
        public int      DeadSymbols      { get; set; }
        public int      VolumeScore      { get; set; }
        public int      ComplexityScore  { get; set; }
        public int      CouplingScore    { get; set; }
        public int      DuplicationScore { get; set; }
        public int      DeadCodeScore    { get; set; }
        public int      ConventionScore  { get; set; }
    }
}
