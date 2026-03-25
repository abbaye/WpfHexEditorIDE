// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Options/DiagnosticToolsOptions.cs
// Description:
//     Singleton settings for the Diagnostic Tools plugin.
//     Persisted to %AppData%\WpfHexaEditor\Plugins\DiagnosticTools.json.
//
// Architecture Notes:
//     Pure POCO singleton; JSON serialized on Save().
//     ProcessMonitor and DiagnosticToolsPanelViewModel read from Instance
//     at construction time (changes take effect on next diagnostic session).
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Plugins.DiagnosticTools.Options;

/// <summary>
/// User-configurable settings for the Diagnostic Tools plugin.
/// </summary>
public sealed class DiagnosticToolsOptions
{
    // ── Singleton ────────────────────────────────────────────────────────────

    private static DiagnosticToolsOptions? _instance;

    public static DiagnosticToolsOptions Instance => _instance ??= Load();

    // ── Settings ─────────────────────────────────────────────────────────────

    /// <summary>Process polling interval in milliseconds (CPU + memory sampling).</summary>
    [JsonPropertyName("pollIntervalMs")]
    public int PollIntervalMs { get; set; } = 500;

    /// <summary>Number of data points kept in the CPU / memory ring buffers.</summary>
    [JsonPropertyName("ringCapacity")]
    public int RingCapacity { get; set; } = 120;

    /// <summary>Maximum number of diagnostic events retained in the event log.</summary>
    [JsonPropertyName("eventMaxCount")]
    public int EventMaxCount { get; set; } = 500;

    /// <summary>Maximum number of metric samples kept for CSV export.</summary>
    [JsonPropertyName("metricMaxCount")]
    public int MetricMaxCount { get; set; } = 10_000;

    // ── Persistence ───────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented            = true,
        PropertyNameCaseInsensitive = true,
    };

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexaEditor", "Plugins", "DiagnosticTools.json");

    private static DiagnosticToolsOptions Load()
    {
        try
        {
            var path = SettingsPath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<DiagnosticToolsOptions>(json, _jsonOptions)
                       ?? new DiagnosticToolsOptions();
            }
        }
        catch { /* return defaults on any I/O or parse error */ }

        return new DiagnosticToolsOptions();
    }

    public void Save()
    {
        try
        {
            var path = SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, _jsonOptions));
        }
        catch { /* best-effort */ }
    }
}
