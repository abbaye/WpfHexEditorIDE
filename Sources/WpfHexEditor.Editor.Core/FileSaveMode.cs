// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Controls how the active document editor handles Ctrl+S in Solution mode.
/// </summary>
public enum FileSaveMode
{
    /// <summary>
    /// Classic behaviour: Ctrl+S writes immediately to the physical file.
    /// </summary>
    Direct,

    /// <summary>
    /// Tracked behaviour: Ctrl+S serialises in-memory edits to a companion
    /// .whchg file without modifying the physical file.
    /// The physical file is only updated by an explicit "Write to Disk" action.
    /// </summary>
    Tracked,
}
