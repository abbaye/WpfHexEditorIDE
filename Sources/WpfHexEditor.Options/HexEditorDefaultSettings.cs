// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using WpfHexEditor.Core;
using WpfHexEditor.Core.Models;

namespace WpfHexEditor.Options;

/// <summary>
/// Default HexEditor presentation and behaviour settings.
/// Applied to every new HexEditor tab via ApplyHexEditorDefaults().
/// Serialised as a nested object: <c>"hexEditorDefaults": { … }</c>.
/// </summary>
public sealed class HexEditorDefaultSettings
{
    // ── Display ─────────────────────────────────────────────────────────

    /// <summary>Number of bytes displayed per line (8, 16, 32, 64 …).</summary>
    public int BytePerLine { get; set; } = 16;

    /// <summary>Show the offset column on the left.</summary>
    public bool ShowOffset { get; set; } = true;

    /// <summary>Show the ASCII column on the right.</summary>
    public bool ShowAscii { get; set; } = true;

    /// <summary>Format used to display byte values (Hex / Decimal / Binary).</summary>
    public DataVisualType DataStringVisual { get; set; } = DataVisualType.Hexadecimal;

    /// <summary>Format used to display the offset header.</summary>
    public DataVisualType OffSetStringVisual { get; set; } = DataVisualType.Hexadecimal;

    /// <summary>Number of bytes grouped visually between spacers.</summary>
    public ByteSpacerGroup ByteGrouping { get; set; } = ByteSpacerGroup.FourByte;

    /// <summary>Position of byte spacers relative to the data columns.</summary>
    public ByteSpacerPosition ByteSpacerPositioning { get; set; } = ByteSpacerPosition.Both;

    // ── Editing ──────────────────────────────────────────────────────────

    /// <summary>Default edit mode when a new file is opened.</summary>
    public EditMode DefaultEditMode { get; set; } = EditMode.Overwrite;

    /// <summary>Allow zooming with Ctrl+MouseWheel.</summary>
    public bool AllowZoom { get; set; } = true;

    /// <summary>Mouse-wheel scroll speed.</summary>
    public MouseWheelSpeed MouseWheelSpeed { get; set; } = MouseWheelSpeed.Normal;

    /// <summary>Allow files to be opened by dragging them onto the editor.</summary>
    public bool AllowFileDrop { get; set; } = true;
}
