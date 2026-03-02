// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Options;

/// <summary>
/// User-configurable application settings (persisted to JSON).
/// </summary>
public sealed class AppSettings
{
    // ── Environment > General ───────────────────────────────────────────

    /// <summary>
    /// Theme file name stem (e.g. "DarkTheme", "Generic").
    /// Applied via ApplyThemeFromSettings() in the host window.
    /// Default: "DarkTheme" (preserves existing behaviour).
    /// </summary>
    public string ActiveThemeName { get; set; } = "DarkTheme";

    // ── Environment > Enregistrement (formerly AppSettingsDialog) ───────

    /// <summary>
    /// Whether Ctrl+S writes directly to the physical file (Direct)
    /// or serialises edits to a companion .whchg file (Tracked).
    /// </summary>
    public FileSaveMode DefaultFileSaveMode { get; set; } = FileSaveMode.Direct;

    /// <summary>
    /// When true, a background timer periodically re-serialises all dirty
    /// project items in Tracked mode to keep .whchg files up-to-date.
    /// </summary>
    public bool AutoSerializeEnabled { get; set; } = false;

    /// <summary>Interval between auto-serialize passes, in seconds.</summary>
    public int AutoSerializeIntervalSeconds { get; set; } = 30;

    // ── Hex Editor defaults ─────────────────────────────────────────────

    /// <summary>
    /// Applied to every newly-opened HexEditor tab.
    /// Serialised as "hexEditorDefaults": { … } in settings.json.
    /// </summary>
    public HexEditorDefaultSettings HexEditorDefaults { get; set; } = new();
}
