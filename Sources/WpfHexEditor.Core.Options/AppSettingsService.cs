// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Core.Options;

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
            if (settings is null) return;

            bool migrated = MigrateIfNeeded(settings);
            Current = settings;
            if (migrated) Save();   // persist migrated values immediately
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    // -- Migration ---------------------------------------------------------

    /// <summary>
    /// Applies incremental migration blocks to bring <paramref name="s"/> up to
    /// <see cref="AppSettings.CurrentSettingsVersion"/>.
    /// Returns <c>true</c> if any migration was applied (caller should re-save).
    /// </summary>
    /// <remarks>
    /// RULES:
    ///  • Each block is a single if (s.SettingsVersion &lt; N) { … s.SettingsVersion = N; }
    ///  • Never modify previous blocks — add new blocks at the end only.
    ///  • Keep blocks idempotent — safe to run twice on the same settings object.
    /// </remarks>
    private static bool MigrateIfNeeded(AppSettings s)
    {
        bool changed = false;

        // v0 → v1 (2026-03-29)
        //   BreakpointLineHighlightEnabled added to DebuggerSettings (default true).
        //   Old settings.json files without this field deserialise it as false (bool default),
        //   so migration resets it to the intended default of true.
        if (s.SettingsVersion < 1)
        {
            s.Debugger.BreakpointLineHighlightEnabled = true;
            s.SettingsVersion = 1;
            changed = true;
        }

        // v1 → v2: (reserved for next breaking change)

        return changed;
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
