// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: SpellCheck/SpellCheckerSettings.cs
// Description:
//     Persistent settings for the spell checker feature.
//     Serialized to %APPDATA%\WpfHexEditor\spellcheck-settings.json.
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Editor.DocumentEditor.SpellCheck;

internal sealed class SpellCheckerSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "spellcheck-settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented              = true,
        DefaultIgnoreCondition     = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    // ── Persisted properties ──────────────────────────────────────────────

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("activeLanguage")]
    public string ActiveLanguage { get; set; } = "en-US";

    [JsonPropertyName("dictionariesPath")]
    public string DictionariesPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "Dictionaries");

    [JsonPropertyName("mirrorUrl")]
    public string MirrorUrl { get; set; } =
        "https://raw.githubusercontent.com/LibreOffice/dictionaries/master/";

    /// <summary>Language codes for which the "install dictionary?" prompt is permanently suppressed.</summary>
    [JsonPropertyName("suppressedLanguagePrompts")]
    public HashSet<string> SuppressedLanguagePrompts { get; set; } = [];

    // ── Load / Save ───────────────────────────────────────────────────────

    public static SpellCheckerSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<SpellCheckerSettings>(json, JsonOpts)
                       ?? new SpellCheckerSettings();
            }
        }
        catch { /* corrupt file — fall back to defaults */ }
        return new SpellCheckerSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* non-critical */ }
    }

    public bool IsLanguagePromptSuppressed(string languageCode) =>
        SuppressedLanguagePrompts.Contains(languageCode);

    public void SuppressLanguagePrompt(string languageCode)
    {
        SuppressedLanguagePrompts.Add(languageCode);
        Save();
    }
}
