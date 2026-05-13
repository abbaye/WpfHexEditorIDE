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

    /// <summary>Accept a word if ANY installed dictionary recognizes it.</summary>
    [JsonPropertyName("multiLanguageMode")]
    public bool MultiLanguageMode { get; set; } = true;

    /// <summary>Milliseconds to wait after document changes before re-analyzing.</summary>
    [JsonPropertyName("analysisDebounceMs")]
    public int AnalysisDebounceMs { get; set; } = 800;

    /// <summary>Maximum number of spelling suggestions returned per misspelled word.</summary>
    [JsonPropertyName("maxSuggestions")]
    public int MaxSuggestions { get; set; } = 5;

    /// <summary>Minimum confidence (0–100) required for automatic language detection.</summary>
    [JsonPropertyName("detectionConfidencePercent")]
    public int DetectionConfidencePercent { get; set; } = 4;

    /// <summary>Words permanently ignored by the user (persisted across sessions).</summary>
    [JsonPropertyName("ignoredWords")]
    public HashSet<string> IgnoredWords { get; set; } = [];

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
