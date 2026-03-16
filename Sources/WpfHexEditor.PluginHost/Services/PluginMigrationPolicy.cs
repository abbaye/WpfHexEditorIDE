// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginMigrationPolicy.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Configuration model for the dynamic plugin migration feature.
//     Defines thresholds (memory, CPU, crashes) and the migration mode
//     (Disabled / SuggestOnly / AutoMigrate) that controls host behaviour
//     when a running InProcess plugin exceeds a threshold.
//
// Architecture Notes:
//     Value object — no WPF/UI dependencies.
//     JSON-persisted to %APPDATA%\WpfHexEditor\plugin-migration-policy.json.
//     Loaded by WpfPluginHost at startup; live-updated via UpdateMigrationPolicy().
// ==========================================================

using System.IO;
using System.Text.Json;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Controls what happens when the migration monitor detects a problematic InProcess plugin.
/// </summary>
public enum PluginMigrationMode
{
    /// <summary>Migration monitoring is fully disabled. No thresholds are evaluated.</summary>
    Disabled,

    /// <summary>
    /// Raises <c>WpfPluginHost.MigrationSuggested</c> so the UI can display a banner.
    /// The user decides whether to migrate.
    /// </summary>
    SuggestOnly,

    /// <summary>
    /// Automatically calls <c>SetIsolationOverrideAsync(id, Sandbox)</c> when a threshold
    /// is exceeded. The override is persisted; the user can revert via the ComboBox.
    /// </summary>
    AutoMigrate
}

/// <summary>
/// Configurable thresholds and mode for the dynamic plugin migration feature.
/// </summary>
public sealed class PluginMigrationPolicy
{
    private static readonly string PersistencePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "WpfHexEditor", "plugin-migration-policy.json");

    // -- Memory thresholds --------------------------------------------------------

    /// <summary>
    /// Memory (MB) above which a <see cref="PluginMigrationMode.SuggestOnly"/> suggestion
    /// is raised. Default: 300 MB.
    /// </summary>
    public int MemorySuggestThresholdMb { get; set; } = 300;

    /// <summary>
    /// Memory (MB) above which an <see cref="PluginMigrationMode.AutoMigrate"/> migration
    /// is triggered (or a stronger suggestion when in SuggestOnly mode). Default: 600 MB.
    /// </summary>
    public int MemoryAutoMigrateThresholdMb { get; set; } = 600;

    // -- CPU thresholds -----------------------------------------------------------

    /// <summary>
    /// CPU% above which the sustained-window timer starts. Default: 50%.
    /// </summary>
    public double CpuSuggestThresholdPercent { get; set; } = 50.0;

    /// <summary>
    /// CPU% above which an AutoMigrate trigger fires (sustained window still required).
    /// Default: 80%.
    /// </summary>
    public double CpuAutoMigrateThresholdPercent { get; set; } = 80.0;

    /// <summary>
    /// Number of consecutive seconds a plugin must stay above the CPU threshold before
    /// a migration trigger fires. Default: 30 seconds.
    /// </summary>
    public int CpuSustainedWindowSeconds { get; set; } = 30;

    // -- Crash threshold ----------------------------------------------------------

    /// <summary>
    /// Number of crashes (faults) in the current session before migration is triggered.
    /// Default: 3 crashes.
    /// </summary>
    public int CrashCountThreshold { get; set; } = 3;

    // -- Mode ---------------------------------------------------------------------

    /// <summary>Controls the overall migration behaviour. Default: SuggestOnly.</summary>
    public PluginMigrationMode Mode { get; set; } = PluginMigrationMode.SuggestOnly;

    // -- Factory & helpers --------------------------------------------------------

    /// <summary>Returns a new instance with all default values.</summary>
    public static PluginMigrationPolicy CreateDefault() => new();

    /// <summary>Shallow clone — safe because all properties are value types.</summary>
    public PluginMigrationPolicy Clone() => (PluginMigrationPolicy)MemberwiseClone();

    /// <summary>
    /// Returns true when the two thresholds are logically consistent (suggest &lt; auto-migrate).
    /// </summary>
    public bool IsValid()
        => MemorySuggestThresholdMb > 0
        && MemoryAutoMigrateThresholdMb >= MemorySuggestThresholdMb
        && CpuSuggestThresholdPercent is >= 0 and <= 100
        && CpuAutoMigrateThresholdPercent >= CpuSuggestThresholdPercent
        && CpuSustainedWindowSeconds > 0
        && CrashCountThreshold > 0;

    // -- Persistence --------------------------------------------------------------

    /// <summary>
    /// Serializes this policy to <paramref name="path"/> (or the default path when null).
    /// Silently swallows I/O errors — migration is a best-effort feature.
    /// </summary>
    public void Save(string? path = null)
    {
        try
        {
            var target = path ?? PersistencePath;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort */ }
    }

    /// <summary>
    /// Loads the policy from <paramref name="path"/> (or the default path when null).
    /// Returns <see cref="CreateDefault"/> on any error or when the file does not exist.
    /// </summary>
    public static PluginMigrationPolicy Load(string? path = null)
    {
        try
        {
            var target = path ?? PersistencePath;
            if (!File.Exists(target)) return CreateDefault();

            var json = File.ReadAllText(target);
            var loaded = JsonSerializer.Deserialize<PluginMigrationPolicy>(json);
            return (loaded is not null && loaded.IsValid()) ? loaded : CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }
}
