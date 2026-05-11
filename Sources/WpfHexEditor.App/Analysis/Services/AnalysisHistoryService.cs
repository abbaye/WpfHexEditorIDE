// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/AnalysisHistoryService.cs
// Description: Persists one HistoryEntry per analysis run to
//              <solution>/.ide/code-analysis/history/{ISO8601}.json so the
//              Overview tab can render a trending sparkline. Pruning honors
//              CodeAnalysisOptions.SnapshotRetentionDays.
// Architecture Notes:
//     - Each run = one tiny json (~200 bytes), so we never grow a hot file
//       and can prune by mtime/filename without re-serializing.
//     - Stateless w.r.t. read order; LoadAll() sorts ascending by Timestamp.
//     - I/O off the UI thread by design (called from CodeAnalysisRunner result
//       publication in CodeAnalysisModule).
// ==========================================================

using System.Globalization;
using System.IO;
using System.Text.Json;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Services;

internal sealed class AnalysisHistoryService
{
    private string _solutionDir = string.Empty;

    internal void SetSolutionDirectory(string dir) => _solutionDir = dir;

    internal void Append(CodeAnalysisReport report)
    {
        if (report is null) return;
        try
        {
            var entry = ToEntry(report);
            var dir   = EnsureHistoryDir();
            var file  = Path.Combine(dir, FileNameFor(entry.Timestamp));
            File.WriteAllText(file, JsonSerializer.Serialize(entry, AnalysisJson.Default));
        }
        catch { /* never crash a run because of trend persistence */ }
    }

    internal IReadOnlyList<HistoryEntry> LoadAll()
    {
        try
        {
            var dir = GetHistoryDir();
            if (!Directory.Exists(dir)) return [];
            var list = new List<HistoryEntry>();
            foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var e = JsonSerializer.Deserialize<HistoryEntry>(json, AnalysisJson.Default);
                    if (e is not null) list.Add(e);
                }
                catch { /* skip corrupt files */ }
            }
            list.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            return list;
        }
        catch { return []; }
    }

    internal void Prune(int retentionDays)
    {
        if (retentionDays <= 0) return;
        try
        {
            var dir = GetHistoryDir();
            if (!Directory.Exists(dir)) return;
            var cutoff = DateTime.UtcNow - TimeSpan.FromDays(retentionDays);
            foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
            {
                try { if (File.GetLastWriteTimeUtc(path) < cutoff) File.Delete(path); }
                catch { /* file in use — skip */ }
            }
        }
        catch { }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HistoryEntry ToEntry(CodeAnalysisReport r)
    {
        int errors   = r.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        int warnings = r.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        int infos    = r.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info);
        return new HistoryEntry
        {
            Timestamp  = DateTime.UtcNow,
            Score      = r.Score.Score,
            Grade      = r.Score.Grade,
            TotalFiles = r.TotalFiles,
            Errors     = errors,
            Warnings   = warnings,
            Infos      = infos,
        };
    }

    private static string FileNameFor(DateTime ts)
        => ts.ToString("yyyy-MM-ddTHH-mm-ss", CultureInfo.InvariantCulture) + ".json";

    private string EnsureHistoryDir()
    {
        var dir = GetHistoryDir();
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string GetHistoryDir()
    {
        var baseDir = string.IsNullOrEmpty(_solutionDir)
            ? AppDomain.CurrentDomain.BaseDirectory
            : _solutionDir;
        return Path.Combine(baseDir, ".ide", "code-analysis", "history");
    }
}
