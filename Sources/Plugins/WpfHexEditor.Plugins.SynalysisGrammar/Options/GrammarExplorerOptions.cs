// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: Options/GrammarExplorerOptions.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Persistent options model for the Grammar Explorer plugin.
//     Saved as JSON to %AppData%\WpfHexaEditor\Plugins\GrammarExplorer.json.
//     Accessed via the static Instance singleton.
//
// Architecture Notes:
//     Pattern: Singleton with lazy load (same pattern as AssemblyExplorerOptions).
//     Thread safety: Load/Save called from the UI thread only.
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Plugins.SynalysisGrammar.Options;

/// <summary>
/// Persistent options for the Grammar Explorer plugin.
/// Load from disk via <see cref="Load"/>; persist via <see cref="Save"/>.
/// </summary>
public sealed class GrammarExplorerOptions
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    private static GrammarExplorerOptions? _instance;

    public static GrammarExplorerOptions Instance
        => _instance ??= Load();

    // ── Settings: Behaviour ───────────────────────────────────────────────────

    /// <summary>Automatically apply a matching grammar when a file is opened.</summary>
    public bool AutoApplyOnFileOpen { get; set; } = false;

    /// <summary>
    /// Maximum file size (in KB) to read when performing an auto-apply scan.
    /// Limits memory usage on very large files.
    /// </summary>
    public int MaxSampleSizeKb { get; set; } = 64;

    // ── Settings: Filter ──────────────────────────────────────────────────────

    /// <summary>Show grammars bundled with WpfHexEditor.Definitions.</summary>
    public bool ShowEmbeddedGrammars { get; set; } = true;

    /// <summary>Show grammars loaded from disk by the user.</summary>
    public bool ShowFileGrammars { get; set; } = true;

    /// <summary>Show grammars contributed by other plugins via IGrammarProvider.</summary>
    public bool ShowPluginGrammars { get; set; } = true;

    // ── Settings: Overlay ─────────────────────────────────────────────────────

    /// <summary>
    /// When true, removing a disk grammar from the list also clears its overlay
    /// from the active HexEditor.
    /// </summary>
    public bool ClearOverlayOnRemoval { get; set; } = true;

    // ── Persistence ───────────────────────────────────────────────────────────

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexaEditor", "Plugins", "GrammarExplorer.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Loads options from disk; returns defaults if the file does not exist
    /// or cannot be parsed.
    /// </summary>
    public static GrammarExplorerOptions Load()
    {
        if (!File.Exists(FilePath)) return new GrammarExplorerOptions();

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<GrammarExplorerOptions>(json, JsonOpts)
                ?? new GrammarExplorerOptions();
        }
        catch
        {
            return new GrammarExplorerOptions();
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
    /// Forces a reload from disk and updates the singleton instance.
    /// Called by LoadOptions() in the plugin entry point.
    /// </summary>
    public static void Invalidate()
        => _instance = Load();
}
