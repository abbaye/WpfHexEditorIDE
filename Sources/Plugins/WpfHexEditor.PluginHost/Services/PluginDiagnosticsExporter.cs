// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginDiagnosticsExporter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Exports plugin diagnostics history to CSV, JSON, or a
//     plain-text crash report. Used by the Plugin Monitor's
//     Export toolbar button.
//
// Architecture Notes:
//     Stateless service — no dependencies injected.
//     No external NuGet libraries: StreamWriter + JsonSerializer.
//     All paths resolved by the caller (ViewModel + SaveFileDialog).
// ==========================================================

using System.IO;
using System.Text;
using System.Text.Json;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Exports plugin diagnostics history to CSV, JSON, or an
/// individual plain-text crash report for a faulted plugin.
/// </summary>
public sealed class PluginDiagnosticsExporter
{
    // -- CSV -----------------------------------------------------------------------

    /// <summary>
    /// Writes all plugins' diagnostics history to a CSV file.
    /// Columns: PluginId, PluginName, TimestampUtc (ISO-8601), CpuPercent, MemoryMb, ExecTimeMs.
    /// </summary>
    public async Task ExportCsvAsync(
        IEnumerable<PluginEntry> plugins,
        string filePath,
        CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
        await writer.WriteLineAsync("PluginId,PluginName,TimestampUtc,CpuPercent,MemoryMb,ExecTimeMs")
                    .ConfigureAwait(false);

        foreach (var entry in plugins)
        {
            var id   = EscapeCsv(entry.Manifest.Id);
            var name = EscapeCsv(entry.Manifest.Name);

            foreach (var snap in entry.Diagnostics.GetHistory())
            {
                ct.ThrowIfCancellationRequested();
                var memMb  = snap.MemoryBytes / (1024 * 1024);
                var execMs = snap.LastExecutionTime.TotalMilliseconds;
                var ts     = snap.Timestamp.ToString("o");
                await writer.WriteLineAsync(
                    $"{id},{name},{ts},{snap.CpuPercent:F2},{memMb},{execMs:F2}")
                    .ConfigureAwait(false);
            }
        }
    }

    // -- JSON ---------------------------------------------------------------------

    /// <summary>
    /// Writes all plugins' diagnostics history to a structured JSON file.
    /// </summary>
    public async Task ExportJsonAsync(
        IEnumerable<PluginEntry> plugins,
        string filePath,
        CancellationToken ct = default)
    {
        var data = plugins.Select(entry => new
        {
            pluginId   = entry.Manifest.Id,
            pluginName = entry.Manifest.Name,
            state      = entry.State.ToString(),
            initMs     = entry.InitDuration.TotalMilliseconds,
            history    = entry.Diagnostics.GetHistory().Select(s => new
            {
                ts     = s.Timestamp,
                cpu    = s.CpuPercent,
                memMb  = s.MemoryBytes / (1024 * 1024),
                execMs = s.LastExecutionTime.TotalMilliseconds
            }).ToArray()
        }).ToArray();

        ct.ThrowIfCancellationRequested();

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(data, options);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    // -- Crash report -------------------------------------------------------------

    /// <summary>
    /// Writes a plain-text crash report for a faulted plugin.
    /// Includes manifest metadata, fault exception, and full diagnostics history.
    /// </summary>
    public async Task ExportCrashReportAsync(
        PluginEntry entry,
        string filePath,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== WpfHexEditor Plugin Crash Report ===");
        sb.AppendLine($"Generated  : {DateTime.UtcNow:o}");
        sb.AppendLine();

        sb.AppendLine("--- Plugin Info ---");
        sb.AppendLine($"Id         : {entry.Manifest.Id}");
        sb.AppendLine($"Name       : {entry.Manifest.Name}");
        sb.AppendLine($"Version    : {entry.Manifest.Version}");
        sb.AppendLine($"Author     : {entry.Manifest.Author}");
        sb.AppendLine($"Publisher  : {entry.Manifest.Publisher}");
        sb.AppendLine($"State      : {entry.State}");
        sb.AppendLine($"Loaded at  : {entry.LoadedAt?.ToLocalTime():o}");
        sb.AppendLine($"Init time  : {entry.InitDuration.TotalMilliseconds:F0} ms");
        sb.AppendLine();

        sb.AppendLine("--- Exception ---");
        sb.AppendLine(entry.FaultException?.ToString() ?? "(no exception recorded)");
        sb.AppendLine();

        sb.AppendLine("--- Diagnostics History ---");
        sb.AppendLine("Timestamp (UTC)              | CPU%    | Mem MB | Exec ms");
        sb.AppendLine("-------------------------------------------------------------");

        foreach (var snap in entry.Diagnostics.GetHistory())
        {
            ct.ThrowIfCancellationRequested();
            var memMb  = snap.MemoryBytes / (1024 * 1024);
            var execMs = snap.LastExecutionTime.TotalMilliseconds;
            sb.AppendLine(
                $"{snap.Timestamp:o} | {snap.CpuPercent,7:F2} | {memMb,6} | {execMs,10:F2}");
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8, ct)
                  .ConfigureAwait(false);
    }

    // -- Helpers ------------------------------------------------------------------

    private static string EscapeCsv(string s)
    {
        if (!s.Contains(',') && !s.Contains('"') && !s.Contains('\n')) return s;
        return $"\"{s.Replace("\"", "\"\"")}\"";
    }
}
