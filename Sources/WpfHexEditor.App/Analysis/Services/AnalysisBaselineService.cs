// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/AnalysisBaselineService.cs
// Description: Manages the analysis baseline — a snapshot of accepted
//              violations. After SaveBaseline(), only NEW violations show up.
//              Location: <solution-dir>/.ide/analysis-baseline.json
// Architecture Notes:
//     Identity for an issue = (RuleId, FilePath relative to solution, Line ±2).
// ==========================================================

using System.IO;
using System.Text.Json;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Services;

internal sealed class AnalysisBaselineService
{
    private string _solutionDir = string.Empty;

    internal void SetSolutionDirectory(string dir) => _solutionDir = dir;

    internal sealed class BaselineEntry
    {
        public string RuleId   { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int    Line     { get; set; }
    }

    internal IReadOnlyList<BaselineEntry> Load()
    {
        var path = GetPath();
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<BaselineEntry>>(File.ReadAllText(path), AnalysisJson.Default) ?? [];
        }
        catch { return []; }
    }

    internal void Save(IReadOnlyList<AnalysisDiagnostic> diagnostics)
    {
        var entries = diagnostics
            .Select(d => new BaselineEntry
            {
                RuleId   = d.Id,
                FilePath = ToRelative(d.FilePath),
                Line     = d.Line,
            })
            .ToList();

        var path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(entries, AnalysisJson.Default));
    }

    internal void Delete()
    {
        var path = GetPath();
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Returns true if a diagnostic matches an entry in the baseline (within ±2 lines).</summary>
    internal static bool IsBaselined(AnalysisDiagnostic d, IReadOnlyList<BaselineEntry> baseline, string solutionDir)
    {
        string rel = ToRelative(d.FilePath, solutionDir);
        return baseline.Any(b =>
            b.RuleId == d.Id
         && string.Equals(b.FilePath, rel, StringComparison.OrdinalIgnoreCase)
         && Math.Abs(b.Line - d.Line) <= 2);
    }

    private string ToRelative(string filePath)
        => ToRelative(filePath, _solutionDir);

    private static string ToRelative(string filePath, string solutionDir)
    {
        if (string.IsNullOrEmpty(solutionDir) || string.IsNullOrEmpty(filePath)) return filePath;
        try
        {
            return Path.GetRelativePath(solutionDir, filePath).Replace('\\', '/');
        }
        catch { return filePath; }
    }

    private string GetPath()
    {
        var baseDir = string.IsNullOrEmpty(_solutionDir)
            ? AppDomain.CurrentDomain.BaseDirectory
            : _solutionDir;
        return Path.Combine(baseDir, ".ide", "analysis-baseline.json");
    }
}
