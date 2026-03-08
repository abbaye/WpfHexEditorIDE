// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Options/AssemblyExplorerOptions.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Plugin options model. Persisted as JSON to
//     %AppData%\WpfHexaEditor\Plugins\AssemblyExplorer.json.
//     Accessed via the static Instance singleton.
//
// Architecture Notes:
//     Pattern: Singleton with lazy load (same as DataInspectorOptions).
//     Thread safety: single-threaded UI; Load/Save called from UI thread only.
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Options;

/// <summary>
/// Persistent options for the Assembly Explorer plugin.
/// Load from disk via <see cref="Load"/>; persist via <see cref="Save"/>.
/// </summary>
public sealed class AssemblyExplorerOptions
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    private static AssemblyExplorerOptions? _instance;

    public static AssemblyExplorerOptions Instance
        => _instance ??= Load();

    // ── Settings ──────────────────────────────────────────────────────────────

    /// <summary>Font size for the decompiler / detail pane TextBox (8–24).</summary>
    public int DecompilerFontSize { get; set; } = 12;

    /// <summary>True to inherit and apply the IDE global theme to this panel.</summary>
    public bool InheritIDETheme { get; set; } = true;

    /// <summary>
    /// Decompiler backend identifier.
    /// "None" = stub (Phase 1).  Future: "ILSpy", "dnSpy".
    /// </summary>
    public string DecompilerBackend { get; set; } = "None";

    /// <summary>Automatically synchronize HexEditor cursor when selecting a node.</summary>
    public bool AutoSyncWithHexEditor { get; set; } = true;

    /// <summary>Show the Metadata Tables group in the tree.</summary>
    public bool ShowMetadataTables { get; set; } = false;

    /// <summary>Show the Resources group in the tree.</summary>
    public bool ShowResources { get; set; } = true;

    /// <summary>
    /// Decompilation language.
    /// "CSharp" (default) | "IL" | "VBNet" (future) | "FSharp" (future).
    /// </summary>
    public string DecompileLanguage { get; set; } = "CSharp";

    /// <summary>Automatically analyze the file when it is opened in the HexEditor.</summary>
    public bool AutoAnalyzeOnFileOpen { get; set; } = true;

    // ── Phase 6 additions ─────────────────────────────────────────────────────

    /// <summary>Recently opened assembly paths (most-recent first, max 20 items).</summary>
    public List<string> RecentFiles { get; set; } = [];

    /// <summary>Keep loaded assemblies pinned when the active HexEditor file changes.</summary>
    public bool PinAssembliesAcrossFileChange { get; set; } = false;

    /// <summary>Show non-public members in the tree.</summary>
    public bool ShowNonPublicMembers { get; set; } = true;

    /// <summary>Show inherited members in the tree.</summary>
    public bool ShowInheritedMembers { get; set; } = false;

    /// <summary>
    /// Default tab to activate in the detail pane.
    /// "Code" | "IL" | "Info" | "Hex".
    /// </summary>
    public string DefaultDecompileTab { get; set; } = "Code";

    /// <summary>
    /// File path of the last assembly loaded in the explorer.
    /// Restored automatically on next IDE startup when the file still exists.
    /// Null when no assembly was loaded or the user explicitly cleared the panel.
    /// </summary>
    public string? LastSessionAssemblyPath { get; set; } = null;

    // ── Persistence ───────────────────────────────────────────────────────────

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexaEditor", "Plugins", "AssemblyExplorer.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented         = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Loads options from disk; returns defaults if the file does not exist
    /// or cannot be parsed.
    /// </summary>
    public static AssemblyExplorerOptions Load()
    {
        if (!File.Exists(FilePath)) return new AssemblyExplorerOptions();

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AssemblyExplorerOptions>(json, JsonOpts)
                ?? new AssemblyExplorerOptions();
        }
        catch
        {
            return new AssemblyExplorerOptions();
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
    /// Adds <paramref name="filePath"/> to the top of <see cref="RecentFiles"/>,
    /// removing any existing duplicate and trimming the list to 20 items.
    /// Automatically saves options to disk.
    /// </summary>
    public void AddRecentFile(string filePath)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, filePath);
        if (RecentFiles.Count > 20)
            RecentFiles.RemoveRange(20, RecentFiles.Count - 20);
        Save();
    }

    /// <summary>
    /// Forces a reload from disk and updates the singleton.
    /// Called by LoadOptions() in the plugin entry point.
    /// </summary>
    public static void Invalidate()
        => _instance = Load();
}
