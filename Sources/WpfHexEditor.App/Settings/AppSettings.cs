// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Settings;

/// <summary>
/// User-configurable application settings (persisted to JSON).
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Whether <c>Ctrl+S</c> writes directly to the physical file (Direct)
    /// or serialises edits to a companion .whchg file (Tracked).
    /// Default: Direct (existing behaviour).
    /// </summary>
    public FileSaveMode DefaultFileSaveMode { get; set; } = FileSaveMode.Direct;

    /// <summary>
    /// When true, a background timer periodically re-serialises all dirty
    /// project items in Tracked mode to keep .whchg files up-to-date.
    /// </summary>
    public bool AutoSerializeEnabled { get; set; } = false;

    /// <summary>Interval between auto-serialize passes, in seconds.</summary>
    public int AutoSerializeIntervalSeconds { get; set; } = 30;
}
