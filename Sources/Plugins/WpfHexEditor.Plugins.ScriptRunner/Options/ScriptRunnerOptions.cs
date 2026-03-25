// ==========================================================
// Project: WpfHexEditor.Plugins.ScriptRunner
// File: Options/ScriptRunnerOptions.cs
// Description:
//     Singleton settings for the Script Runner plugin.
//     Persisted to %AppData%\WpfHexaEditor\Plugins\ScriptRunner.json.
//
// Architecture Notes:
//     Pure POCO singleton; JSON serialized on Save().
//     ScriptRunnerViewModel reads MaxHistoryEntries from Instance when
//     trimming the history collection in AddToHistory().
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Plugins.ScriptRunner.Options;

/// <summary>
/// User-configurable settings for the Script Runner plugin.
/// </summary>
public sealed class ScriptRunnerOptions
{
    // ── Singleton ────────────────────────────────────────────────────────────

    private static ScriptRunnerOptions? _instance;

    public static ScriptRunnerOptions Instance => _instance ??= Load();

    // ── Settings ─────────────────────────────────────────────────────────────

    /// <summary>Maximum number of script entries kept in the history dropdown.</summary>
    [JsonPropertyName("maxHistoryEntries")]
    public int MaxHistoryEntries { get; set; } = 20;

    /// <summary>
    /// When true, the output panel is cleared automatically at the start
    /// of each new script execution.
    /// </summary>
    [JsonPropertyName("autoClearOnNewSession")]
    public bool AutoClearOnNewSession { get; set; } = false;

    /// <summary>Default scripting language selected in the editor.</summary>
    [JsonPropertyName("defaultLanguage")]
    public string DefaultLanguage { get; set; } = "CSharp";

    // ── Persistence ───────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented               = true,
        PropertyNameCaseInsensitive = true,
    };

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexaEditor", "Plugins", "ScriptRunner.json");

    private static ScriptRunnerOptions Load()
    {
        try
        {
            var path = SettingsPath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<ScriptRunnerOptions>(json, _jsonOptions)
                       ?? new ScriptRunnerOptions();
            }
        }
        catch { /* return defaults on any I/O or parse error */ }

        return new ScriptRunnerOptions();
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
