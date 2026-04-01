// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using WpfHexEditor.Core;
using WpfHexEditor.Core.Models;

namespace WpfHexEditor.Core.Options;

/// <summary>
/// Default HexEditor presentation and behaviour settings.
/// Applied to every new HexEditor tab via ApplyHexEditorDefaults().
/// Serialised as a nested object: <c>"hexEditorDefaults": { … }</c>.
/// </summary>
public sealed class HexEditorDefaultSettings
{
    // -- Display ---------------------------------------------------------

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

    // -- Editing ----------------------------------------------------------

    /// <summary>Default edit mode when a new file is opened.</summary>
    public EditMode DefaultEditMode { get; set; } = EditMode.Overwrite;

    /// <summary>Allow zooming with Ctrl+MouseWheel.</summary>
    public bool AllowZoom { get; set; } = true;

    /// <summary>Mouse-wheel scroll speed.</summary>
    public MouseWheelSpeed MouseWheelSpeed { get; set; } = MouseWheelSpeed.System;

    /// <summary>Allow files to be opened by dragging them onto the editor.</summary>
    public bool AllowFileDrop { get; set; } = true;

    // -- Data interpretation ----------------------------------------------

    /// <summary>Default byte size for multi-byte data display (8 / 16 / 32 bit).</summary>
    public ByteSizeType ByteSize { get; set; } = ByteSizeType.Bit8;

    /// <summary>Default byte order for multi-byte data display (Lo-Hi / Hi-Lo).</summary>
    public ByteOrderType ByteOrder { get; set; } = ByteOrderType.LoHi;

    /// <summary>Default format used when copying data to the clipboard.</summary>
    public CopyPasteMode DefaultCopyToClipboardMode { get; set; } = CopyPasteMode.Auto;

    /// <summary>Visual style of the spacer between byte groups (Empty / Line / Dash).</summary>
    public ByteSpacerVisual ByteSpacerVisualStyle { get; set; } = ByteSpacerVisual.Line;

    // -- Scroll markers ---------------------------------------------------

    /// <summary>Show bookmark markers on the scroll bar.</summary>
    public bool ShowBookmarkMarkers { get; set; } = true;

    /// <summary>Show modified-byte markers on the scroll bar.</summary>
    public bool ShowModifiedMarkers { get; set; } = true;

    /// <summary>Show inserted-byte markers on the scroll bar.</summary>
    public bool ShowInsertedMarkers { get; set; } = true;

    /// <summary>Show deleted-byte markers on the scroll bar.</summary>
    public bool ShowDeletedMarkers { get; set; } = true;

    /// <summary>Show search-result markers on the scroll bar.</summary>
    public bool ShowSearchResultMarkers { get; set; } = true;

    // -- Status bar visibility ---------------------------------------------

    /// <summary>Show render frame time ("Refresh: N ms") in the IDE right-side status bar.</summary>
    public bool ShowRefreshRateInStatusBar { get; set; } = true;

    /// <summary>Show the status message row inside the HexEditor status bar.</summary>
    public bool ShowStatusMessage { get; set; } = true;

    /// <summary>Show file size in the HexEditor status bar.</summary>
    public bool ShowFileSizeInStatusBar { get; set; } = true;

    /// <summary>Show selection info in the HexEditor status bar.</summary>
    public bool ShowSelectionInStatusBar { get; set; } = true;

    /// <summary>Show current position in the HexEditor status bar.</summary>
    public bool ShowPositionInStatusBar { get; set; } = true;

    /// <summary>Show edit mode in the HexEditor status bar.</summary>
    public bool ShowEditModeInStatusBar { get; set; } = true;

    /// <summary>Show bytes-per-line count in the HexEditor status bar.</summary>
    public bool ShowBytesPerLineInStatusBar { get; set; } = true;

    // -- Advanced behaviour ------------------------------------------------

    /// <summary>Highlight all bytes with the same value as the byte under the cursor.</summary>
    public bool AllowAutoHighLightSelectionByte { get; set; } = false;

    /// <summary>Auto-select all identical bytes on double-click.</summary>
    public bool AllowAutoSelectSameByteAtDoubleClick { get; set; } = false;

    /// <summary>Enable the right-click context menu in the editor.</summary>
    public bool AllowContextMenu { get; set; } = true;

    /// <summary>Allow the user to delete bytes (Delete key / context menu).</summary>
    public bool AllowDeleteByte { get; set; } = true;

    /// <summary>Allow the user to extend the file by appending bytes.</summary>
    public bool AllowExtend { get; set; } = true;

    /// <summary>Show a confirmation dialog when a file is opened via drag-and-drop.</summary>
    public bool FileDroppingConfirmation { get; set; } = true;

    /// <summary>How many bytes to pre-load when the editor is first displayed.</summary>
    public PreloadByteInEditor PreloadByteInEditorMode { get; set; } = PreloadByteInEditor.None;

    // -- Tooltip ----------------------------------------------------------

    /// <summary>Controls where byte tooltips are shown (None / OnCustomBackgroundBlocks / Everywhere).</summary>
    public ByteToolTipDisplayMode ByteToolTipDisplayMode { get; set; } = ByteToolTipDisplayMode.OnCustomBackgroundBlocks;

    // -- Column / Row Highlight -------------------------------------------

    /// <summary>Show the active-column stripe in the hex byte panel.</summary>
    public bool ShowColumnHighlight { get; set; } = true;

    /// <summary>Show the active-column stripe in the ASCII panel.</summary>
    public bool ShowAsciiColumnHighlight { get; set; } = true;

    /// <summary>Show the active-row (line) highlight behind the cursor row.</summary>
    public bool ShowRowHighlight { get; set; } = true;

    // -- Breadcrumb Bar ---------------------------------------------------

    /// <summary>Show the breadcrumb bar above the hex viewport.</summary>
    public bool ShowBreadcrumbBar { get; set; } = true;

    /// <summary>Offset display format: 0=Hex, 1=Decimal, 2=Both. Default: Both.</summary>
    public int BreadcrumbOffsetFormat { get; set; } = 2;

    /// <summary>Show detected format name and confidence in breadcrumb.</summary>
    public bool BreadcrumbShowFormatInfo { get; set; } = true;

    /// <summary>Show active parsed field path in breadcrumb.</summary>
    public bool BreadcrumbShowFieldPath { get; set; } = true;

    /// <summary>Show selection length in breadcrumb when multiple bytes selected.</summary>
    public bool BreadcrumbShowSelectionLength { get; set; } = true;

    /// <summary>Font size for breadcrumb text. Default: 11.5.</summary>
    public double BreadcrumbFontSize { get; set; } = 11.5;
}
