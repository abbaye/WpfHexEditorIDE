// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: Options/XamlDesignerOptions.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Plugin options model. Persisted as JSON to
//     %AppData%\WpfHexaEditor\Plugins\XamlDesigner.json.
//     Accessed via the static Instance singleton.
//
// Architecture Notes:
//     Pattern: Singleton with lazy load (same as AssemblyExplorerOptions).
//     Thread safety: single-threaded UI; Load/Save called from UI thread only.
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Plugins.XamlDesigner.Options;

/// <summary>
/// Persistent options for the XAML Designer plugin.
/// Load from disk via <see cref="Load"/>; persist via <see cref="Save"/>.
/// </summary>
public sealed class XamlDesignerOptions
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    private static XamlDesignerOptions? _instance;

    public static XamlDesignerOptions Instance
        => _instance ??= Load();

    // ── Settings ──────────────────────────────────────────────────────────────

    /// <summary>Enable live preview: re-renders the design canvas after each edit.</summary>
    public bool AutoPreviewEnabled { get; set; } = true;

    /// <summary>Debounce delay (ms) before triggering live preview after a text change.</summary>
    public int AutoPreviewDelayMs { get; set; } = 500;

    /// <summary>Snap moved/resized elements to a grid when using the selection adorner.</summary>
    public bool SnapToGrid { get; set; } = true;

    /// <summary>Grid snap size in device-independent pixels.</summary>
    public int GridSnapSize { get; set; } = 8;

    /// <summary>
    /// Default view mode when a .xaml file is opened.
    /// "Split" | "CodeOnly" | "DesignOnly"
    /// </summary>
    public string DefaultViewMode { get; set; } = "Split";

    /// <summary>Show properties with default values in the Property Inspector.</summary>
    public bool ShowDefaultProperties { get; set; } = false;

    // ── Persistence ───────────────────────────────────────────────────────────

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexaEditor", "Plugins", "XamlDesigner.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Loads options from disk; returns defaults if the file does not exist
    /// or cannot be parsed.
    /// </summary>
    public static XamlDesignerOptions Load()
    {
        if (!File.Exists(FilePath)) return new XamlDesignerOptions();

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<XamlDesignerOptions>(json, JsonOpts)
                ?? new XamlDesignerOptions();
        }
        catch
        {
            return new XamlDesignerOptions();
        }
    }

    /// <summary>Persists current options to disk. Creates parent directories if needed.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch
        {
            // Silently ignore — disk errors must not crash the IDE.
        }
    }

    /// <summary>
    /// Forces a reload from disk and updates the singleton.
    /// Called by LoadOptions() in the plugin entry point.
    /// </summary>
    public static void Invalidate()
        => _instance = Load();
}
