// ==========================================================
// Project: WpfHexEditor.Core.SpellCheck
// File: SpellCheckerSettings.cs
// Description:
//     Persistent settings for the spell checker feature.
//     Serialized to %APPDATA%\WpfHexEditor\spellcheck-settings.json.
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Core.SpellCheck;

public sealed class SpellCheckerSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "spellcheck-settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented               = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>True until the user explicitly installs or dismisses the first dictionary prompt.</summary>
    [JsonPropertyName("isFirstRun")]
    public bool IsFirstRun { get; set; } = true;

    [JsonPropertyName("activeLanguage")]
    public string ActiveLanguage { get; set; } = "en-US";

    [JsonPropertyName("dictionariesPath")]
    public string DictionariesPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "Dictionaries");

    [JsonPropertyName("mirrorUrl")]
    public string MirrorUrl { get; set; } =
        "https://raw.githubusercontent.com/LibreOffice/dictionaries/master/";

    [JsonPropertyName("suppressedLanguagePrompts")]
    public HashSet<string> SuppressedLanguagePrompts { get; set; } = [];

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
        catch { }
        return new SpellCheckerSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { }
    }

    public bool IsLanguagePromptSuppressed(string languageCode) =>
        SuppressedLanguagePrompts.Contains(languageCode);

    public void SuppressLanguagePrompt(string languageCode)
    {
        SuppressedLanguagePrompts.Add(languageCode);
        Save();
    }
}
