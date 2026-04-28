// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentHexPane.xaml.cs
// Description:
//     Embeds a WpfHexEditor.HexEditor instance to show the raw bytes
//     of the document. BinaryMap entries are rendered as colored
//     CustomBackgroundBlocks. Hex selection changes are forwarded to
//     BinaryMapSyncService to highlight the matching text block.
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Properties;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Hex view pane that reflects the raw binary of the loaded document
/// and syncs selection with the text pane via the binary map.
/// </summary>
public partial class DocumentHexPane : UserControl
{
    // Palette of semi-transparent block colors (cycled by block kind)
    private static readonly SolidColorBrush[] BlockPalette =
    [
        new SolidColorBrush(Color.FromArgb(50, 86,  156, 214)),  // paragraph  — VS-blue
        new SolidColorBrush(Color.FromArgb(50, 78,  201, 176)),  // run        — teal
        new SolidColorBrush(Color.FromArgb(50, 206, 145,  120)), // image      — salmon
        new SolidColorBrush(Color.FromArgb(50, 220, 220, 170)),  // table      — yellow
        new SolidColorBrush(Color.FromArgb(50, 156, 220, 254)),  // heading    — light blue
    ];

    static DocumentHexPane()
    {
        foreach (var b in BlockPalette) b.Freeze();
    }

    private DocumentModel? _model;
    private const string   BinaryMapTag = "DE_BinaryMap";

    /// <summary>Raised when the user changes hex selection (long = start offset).</summary>
    public event EventHandler<long>? HexOffsetSelected;

    public DocumentHexPane()
    {
        InitializeComponent();
        PART_HexEditor.SelectionChanged += OnHexSelectionChanged;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void BindModel(DocumentModel model)
    {
        if (_model is not null)
            _model.BinaryMap.MapRebuilt -= OnMapRebuilt;

        _model = model;
        _model.BinaryMap.MapRebuilt += OnMapRebuilt;

        LoadRawBytes(model.FilePath);
        RefreshBinaryMapOverlays();
    }

    /// <summary>Scrolls and highlights the hex range for <paramref name="block"/>.</summary>
    public void ScrollToBlock(DocumentBlock block)
    {
        if (_model is null) return;
        var entry = _model.BinaryMap.EntryOf(block);
        if (entry is null) return;

        PART_HexEditor.SetPosition(entry.Value.Offset);
        PART_HexEditor.SelectionStart  = entry.Value.Offset;
        PART_HexEditor.SelectionStop   = entry.Value.Offset + entry.Value.Length - 1;

        UpdateOffsetLabel(entry.Value.Offset);
    }

    // ── Raw byte loading ─────────────────────────────────────────────────────

    /// <summary>
    /// Loads raw bytes from <paramref name="filePath"/> into the hex view.
    /// Can be called even when full document model loading failed, providing a
    /// raw-bytes fallback so the user can inspect the file content.
    /// </summary>
    public void LoadFile(string filePath) => LoadRawBytes(filePath);

    private void LoadRawBytes(string filePath)
    {
        if (!File.Exists(filePath)) return;

        try
        {
            var stream = File.OpenRead(filePath);
            PART_HexEditor.OpenStream(stream, readOnly: true);
        }
        catch (Exception ex)
        {
            PART_OffsetLabel.Text = $"Cannot load: {ex.Message}";
        }
    }

    // ── BinaryMap overlays ────────────────────────────────────────────────────

    private void RefreshBinaryMapOverlays()
    {
        if (_model is null) return;

        PART_HexEditor.ClearCustomBackgroundBlockByTag(BinaryMapTag);

        int palette = 0;
        foreach (var entry in _model.BinaryMap.GetAll())
        {
            if (entry.Length <= 0) continue;

            var brush = BlockPalette[GetPaletteIndex(entry.Block.Kind, palette)];
            palette++;

            var cbBlock = new CustomBackgroundBlock(
                entry.Offset,
                entry.Length,
                brush,
                BinaryMapTag);

            PART_HexEditor.AddCustomBackgroundBlock(cbBlock);
        }
    }

    private static int GetPaletteIndex(string kind, int fallback) => kind switch
    {
        "paragraph" => 0,
        "run"       => 1,
        "image"     => 2,
        "table"     => 3,
        "heading"   => 4,
        _           => fallback % BlockPalette.Length
    };

    // ── Mutation overlays (called by DocumentHexHighlightManager) ────────────

    private static readonly SolidColorBrush EditedOverlayBrush =
        Freeze(new SolidColorBrush(Color.FromArgb(0x50, 0xE8, 0xA0, 0x20)));

    private static readonly SolidColorBrush DeletedOverlayBrush =
        Freeze(new SolidColorBrush(Color.FromArgb(0x50, 0xE0, 0x20, 0x20)));

    private static T Freeze<T>(T b) where T : Freezable { b.Freeze(); return b; }

    private const string EditedTag  = "DE_MutEdit";
    private const string DeletedTag = "DE_MutDel";

    /// <summary>Adds an edit or delete mutation overlay on the given raw byte range.</summary>
    public void AddMutationOverlay(long offset, long length, bool isDelete)
    {
        var brush = isDelete ? DeletedOverlayBrush : EditedOverlayBrush;
        var tag   = isDelete ? DeletedTag           : EditedTag;
        var block = new CustomBackgroundBlock(offset, length, brush, tag);
        PART_HexEditor.AddCustomBackgroundBlock(block);
    }

    /// <summary>Removes all mutation overlays (edit + delete).</summary>
    public void ClearMutationOverlays()
    {
        PART_HexEditor.ClearCustomBackgroundBlockByTag(EditedTag);
        PART_HexEditor.ClearCustomBackgroundBlockByTag(DeletedTag);
    }

    // ── Hex selection ─────────────────────────────────────────────────────────

    private void OnHexSelectionChanged(object? sender, EventArgs e)
    {
        long offset = PART_HexEditor.SelectionStart;
        UpdateOffsetLabel(offset);
        HexOffsetSelected?.Invoke(this, offset);
    }

    private void UpdateOffsetLabel(long offset)
    {
        PART_OffsetLabel.Text = string.Format(DocumentEditorResources.DocHexPane_OffsetPattern, offset);
    }

    // ── Map rebuild ──────────────────────────────────────────────────────────

    private void OnMapRebuilt(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(RefreshBinaryMapOverlays);
    }
}
