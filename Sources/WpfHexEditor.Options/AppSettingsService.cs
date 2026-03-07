// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Options;

/// <summary>
/// Singleton that loads/saves <see cref="AppSettings"/> to
/// <c>%AppData%/WpfHexEditor/settings.json</c>.
/// </summary>
public sealed class AppSettingsService
{
    // -- Singleton ---------------------------------------------------------
    public static readonly AppSettingsService Instance = new();
    private AppSettingsService() { }

    // -- State -------------------------------------------------------------
    public AppSettings Current { get; private set; } = new();

    // -- Path --------------------------------------------------------------
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "settings.json");

    /// <summary>Absolute path to the settings JSON file.</summary>
    public string FilePath => SettingsPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented               = true,
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters                  = { new JsonStringEnumConverter() },
    };

    // -- Load / Save -------------------------------------------------------

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json     = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings != null) Current = settings;
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* best-effort */ }
    }
}
