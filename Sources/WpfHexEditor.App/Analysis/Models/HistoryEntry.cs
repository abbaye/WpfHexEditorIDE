// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/HistoryEntry.cs
// Description: Lightweight per-run snapshot persisted to .ide/code-analysis/history.
//              Stores only the aggregates needed for the trending sparkline —
//              the full CodeAnalysisReport stays in latest.json.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public sealed class HistoryEntry
{
    public DateTime Timestamp  { get; set; }
    public int      Score      { get; set; }
    public string   Grade      { get; set; } = string.Empty;
    public int      TotalFiles { get; set; }
    public int      Errors     { get; set; }
    public int      Warnings   { get; set; }
    public int      Infos      { get; set; }
}
