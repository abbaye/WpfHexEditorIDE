// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: PreferencesExportService.cs
// Description:
//     Export/import of IDE preferences as a self-contained JSON bundle.
//     The bundle contains the current AppSettings plus an optional
//     dictionary of per-plugin payloads. Useful for backup, sharing a
//     team configuration, and seeding a fresh install.
// ==========================================================

using System.IO;
using System.Text.Json;

namespace WpfHexEditor.Core.Options;

/// <summary>One-shot preferences bundle written to disk.</summary>
public sealed class PreferencesBundle
{
    public int                          SchemaVersion { get; set; } = 1;
    public DateTime                     ExportedAt    { get; set; }
    public AppSettings?                 AppSettings   { get; set; }
    public Dictionary<string, object>   Plugins       { get; set; } = new();
}

/// <summary>Reads / writes <see cref="PreferencesBundle"/> JSON files.</summary>
public sealed class PreferencesExportService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public void Export(string path, IReadOnlyDictionary<string, object>? pluginPayloads = null)
    {
        var bundle = new PreferencesBundle
        {
            ExportedAt  = DateTime.UtcNow,
            AppSettings = AppSettingsService.Instance.Current,
            Plugins     = pluginPayloads is null
                              ? new Dictionary<string, object>()
                              : new Dictionary<string, object>(pluginPayloads, StringComparer.OrdinalIgnoreCase),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(bundle, Options));
    }

    /// <summary>
    /// Reads a bundle, replaces the live <see cref="AppSettingsService"/>
    /// content, and persists. Returns the imported bundle so callers can
    /// hand the per-plugin payloads to plugin <c>LoadOptions</c> hooks.
    /// </summary>
    public PreferencesBundle? Import(string path)
    {
        try
        {
            var json   = File.ReadAllText(path);
            var bundle = JsonSerializer.Deserialize<PreferencesBundle>(json, Options);
            if (bundle?.AppSettings is not null)
            {
                AppSettingsService.Instance.Replace(bundle.AppSettings);
                AppSettingsService.Instance.Save();
            }
            return bundle;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Preferences] import failed: {path} — {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
