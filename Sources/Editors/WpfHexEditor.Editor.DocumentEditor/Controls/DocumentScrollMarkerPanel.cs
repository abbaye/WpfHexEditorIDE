// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentScrollMarkerPanel.cs
// Description:
//     Lightweight overlay panel that renders colored tick marks over the
//     vertical scrollbar to indicate block-level positions of interest:
//     bookmarks (cyan), unsaved changes (gold), search hits (blue),
//     forensic alerts (red), and the caret position (yellow).
//     Fully click-through (IsHitTestVisible = false).
// Architecture:
//     FrameworkElement overlay; arranged by DocumentEditorHost to exactly
//     cover the ScrollViewer's vertical scrollbar. Proportional mapping:
//     blockIndex / totalBlocks * drawableHeight. Static frozen brushes.
// ==========================================================

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Renders proportional tick marks over the document editor's vertical
/// scrollbar to indicate bookmarks, unsaved changes, search results,
/// forensic alerts, and the current caret block position.
/// </summary>
internal sealed class DocumentScrollMarkerPanel : FrameworkElement
{
    // ── Layout constants ─────────────────────────────────────────────────────

    private const double TopMargin    = 17.0; // scrollbar arrow-button height
    private const double BottomMargin = 17.0;
    private const double TickWidth    = 4.0;
    private const double TickHeight   = 3.0;
    private const double TrackRestWidth = 12.0; // visible track width, right-aligned

    // ── Static frozen brushes ────────────────────────────────────────────────

    private static readonly Brush s_searchTick   = MakeFrozenBrush(Color.FromArgb(200,  86, 156, 214)); // blue
    private static readonly Brush s_changeTick   = MakeFrozenBrush(Color.FromArgb(200, 226, 192, 141)); // gold
    private static readonly Brush s_forensicTick = MakeFrozenBrush(Color.FromArgb(200, 244,  71,  71)); // red
    private static readonly Brush s_bookmarkTick = MakeFrozenBrush(Color.FromArgb(220,  78, 201, 176)); // cyan
    private static readonly Brush s_caretTick    = MakeFrozenBrush(Color.FromArgb(220, 255, 255, 100)); // yellow

    // ── State ────────────────────────────────────────────────────────────────

    private IEnumerable<int> _searchBlocks   = [];
    private IEnumerable<int> _changeBlocks   = [];
    private IEnumerable<int> _forensicBlocks = [];
    private IEnumerable<int> _bookmarkBlocks = [];
    private int              _caretBlock     = -1;
    private int              _totalBlocks    = 1;

    private (int block, double normalizedEntropy)[] _entropyBlocks = [];

    // ── Constructor ──────────────────────────────────────────────────────────

    public DocumentScrollMarkerPanel()
    {
        IsHitTestVisible = false; // click-through to scrollbar beneath
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void UpdateSearchMarkers(IEnumerable<int> blockIndices, int totalBlocks)
    {
        _searchBlocks = blockIndices;
        _totalBlocks  = Math.Max(1, totalBlocks);
        InvalidateVisual();
    }

    public void UpdateChangeMarkers(IEnumerable<int> blockIndices, int totalBlocks)
    {
        _changeBlocks = blockIndices;
        _totalBlocks  = Math.Max(1, totalBlocks);
        InvalidateVisual();
    }

    public void UpdateForensicMarkers(IEnumerable<int> blockIndices, int totalBlocks)
    {
        _forensicBlocks = blockIndices;
        _totalBlocks    = Math.Max(1, totalBlocks);
        InvalidateVisual();
    }

    public void UpdateBookmarkMarkers(IEnumerable<int> blockIndices, int totalBlocks)
    {
        _bookmarkBlocks = blockIndices;
        _totalBlocks    = Math.Max(1, totalBlocks);
        InvalidateVisual();
    }

    public void UpdateCaretMarker(int blockIndex, int totalBlocks)
    {
        _caretBlock  = blockIndex;
        _totalBlocks = Math.Max(1, totalBlocks);
        InvalidateVisual();
    }

    /// <summary>
    /// Updates entropy ticks using a green-to-red gradient based on normalized entropy.
    /// Call with an empty enumerable to clear entropy marks.
    /// </summary>
    /// <param name="blocks">Sequence of (blockIndex, entropy 0.0–8.0) pairs.</param>
    /// <param name="totalBlocks">Total number of blocks in the file.</param>
    public void UpdateEntropyMarkers(IEnumerable<(int block, double entropy)> blocks, int totalBlocks)
    {
        _entropyBlocks = blocks.Select(b => (b.block, b.entropy / 8.0)).ToArray();
        _totalBlocks   = Math.Max(1, totalBlocks);
        InvalidateVisual();
    }

    public void ClearAll()
    {
        _searchBlocks   = [];
        _changeBlocks   = [];
        _forensicBlocks = [];
        _bookmarkBlocks = [];
        _entropyBlocks  = [];
        _caretBlock     = -1;
        InvalidateVisual();
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (ActualHeight <= TopMargin + BottomMargin) return;

        double drawableH = ActualHeight - TopMargin - BottomMargin;
        double trackLeft = ActualWidth - TrackRestWidth;
        double tickX     = trackLeft + (TrackRestWidth - TickWidth) / 2.0;

        // Z-order: entropy → change → search → forensic → bookmark → caret (topmost)

        foreach (var (b, norm) in _entropyBlocks)
            dc.DrawRectangle(EntropyBrush(norm), null,
                new Rect(tickX, Y(b, drawableH), TickWidth, TickHeight));

        foreach (int b in _changeBlocks)
            dc.DrawRectangle(s_changeTick, null,
                new Rect(tickX, Y(b, drawableH), TickWidth, TickHeight));

        foreach (int b in _searchBlocks)
            dc.DrawRectangle(s_searchTick, null,
                new Rect(tickX, Y(b, drawableH), TickWidth, TickHeight));

        foreach (int b in _forensicBlocks)
            dc.DrawRectangle(s_forensicTick, null,
                new Rect(tickX, Y(b, drawableH), TickWidth, TickHeight));

        foreach (int b in _bookmarkBlocks)
            dc.DrawRectangle(s_bookmarkTick, null,
                new Rect(tickX, Y(b, drawableH), TickWidth, TickHeight));

        if (_caretBlock >= 0)
            dc.DrawRectangle(s_caretTick, null,
                new Rect(tickX - 1, Y(_caretBlock, drawableH), TickWidth + 2, 2));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private double Y(int blockIndex, double drawableH) =>
        TopMargin + (blockIndex / (double)_totalBlocks) * drawableH;

    private static Brush MakeFrozenBrush(Color color)
    {
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }

    // 64-step LUT: green (low entropy) → red (high entropy), alpha=160.
    private static readonly Brush[] s_entropyLut = BuildEntropyLut();

    private static Brush[] BuildEntropyLut()
    {
        const int Steps = 64;
        var lut = new Brush[Steps];
        for (int i = 0; i < Steps; i++)
        {
            double t = (double)i / (Steps - 1);
            byte r = (byte)(t * 244);
            byte g = (byte)((1.0 - t) * 200 + 56);
            lut[i] = MakeFrozenBrush(Color.FromArgb(160, r, g, 60));
        }
        return lut;
    }

    private static Brush EntropyBrush(double normalizedEntropy)
    {
        int idx = (int)(Math.Clamp(normalizedEntropy, 0.0, 1.0) * (s_entropyLut.Length - 1));
        return s_entropyLut[idx];
    }
}
