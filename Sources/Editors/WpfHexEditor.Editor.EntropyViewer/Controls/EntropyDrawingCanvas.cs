//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.EntropyViewer
// File: Controls/EntropyDrawingCanvas.cs
// Description:
//     High-performance DrawingContext-based renderers for the entropy and
//     byte-frequency charts.  Zero UIElement allocations per frame.
//
//     EntropyBarCanvas  — vertical bars coloured green→red by Shannon entropy
//                         (0–8 bits/byte).  Supports region highlight and
//                         configurable high-entropy threshold line.
//                         Fires OffsetRequested on left-click.
//
//     ByteFreqCanvas    — 256-bar histogram of byte values (0x00–0xFF).
//
// Architecture:
//     FrameworkElement OnRender override.  IsHitTestVisible kept true on
//     EntropyBarCanvas for click/hover; false on ByteFreqCanvas.
//     256-entry frozen Brush palette is shared across all instances.
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.EntropyViewer.Controls;

// ---------------------------------------------------------------------------
// EntropyBarCanvas
// ---------------------------------------------------------------------------

/// <summary>
/// DrawingContext renderer for per-block Shannon entropy.
/// Green (low entropy) → red (high entropy/compressed).
/// </summary>
public sealed class EntropyBarCanvas : FrameworkElement
{
    // ── Shared frozen palette (0 = pure green, 255 = pure red) ────────────────
    private static readonly Brush[] _palette = BuildPalette();

    private static Brush[] BuildPalette()
    {
        var p = new Brush[256];
        for (int i = 0; i < 256; i++)
        {
            double t  = i / 255.0;
            byte   r  = (byte)(t * 220);
            byte   g  = (byte)((1 - t) * 180);
            var    br = new SolidColorBrush(Color.FromArgb(230, r, g, 60));
            br.Freeze();
            p[i] = br;
        }
        return p;
    }

    // Threshold line pen — bright orange, 1 px dashed
    private static readonly Pen _threshPen;
    private static readonly Brush _regionHighBrush;
    private static readonly Brush _regionLowBrush;
    private static readonly Brush _hoverBrush;
    private static readonly Brush _labelBrush;
    private static readonly Pen   _labelBorderPen;
    private static readonly Typeface _labelTypeface = new(
        new FontFamily("Consolas, Segoe UI"),
        FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    static EntropyBarCanvas()
    {
        _threshPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 165, 0)), 1.0)
        {
            DashStyle = DashStyles.Dash
        };
        _threshPen.Freeze();

        _regionHighBrush = new SolidColorBrush(Color.FromArgb(30, 220, 60, 60));
        _regionHighBrush.Freeze();
        _regionLowBrush  = new SolidColorBrush(Color.FromArgb(15, 60, 180, 60));
        _regionLowBrush.Freeze();
        _hoverBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
        _hoverBrush.Freeze();

        _labelBrush = new SolidColorBrush(Color.FromArgb(220, 220, 220, 100));
        _labelBrush.Freeze();
        _labelBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 200, 200, 80)), 0.5);
        _labelBorderPen.Freeze();
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private double[]? _blockEntropy;
    private int       _windowSize   = 1024;
    private int       _hoverIndex   = -1;

    // Section labels: (offset, length, name)
    private IReadOnlyList<(long Offset, long Length, string Name)> _sectionLabels
        = Array.Empty<(long, long, string)>();

    // Zoom: 1.0 = default, valid range [0.5, 8.0]
    private double _zoom = 1.0;

    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty ShowRegionsProperty =
        DependencyProperty.Register(nameof(ShowRegions), typeof(bool), typeof(EntropyBarCanvas),
            new PropertyMetadata(true, OnVisualChanged));

    public bool ShowRegions
    {
        get => (bool)GetValue(ShowRegionsProperty);
        set => SetValue(ShowRegionsProperty, value);
    }

    public static readonly DependencyProperty HighEntropyThresholdProperty =
        DependencyProperty.Register(nameof(HighEntropyThreshold), typeof(double), typeof(EntropyBarCanvas),
            new PropertyMetadata(7.2, OnVisualChanged));

    public double HighEntropyThreshold
    {
        get => (double)GetValue(HighEntropyThresholdProperty);
        set => SetValue(HighEntropyThresholdProperty, value);
    }

    public static readonly DependencyProperty ShowThresholdLineProperty =
        DependencyProperty.Register(nameof(ShowThresholdLine), typeof(bool), typeof(EntropyBarCanvas),
            new PropertyMetadata(true, OnVisualChanged));

    public bool ShowThresholdLine
    {
        get => (bool)GetValue(ShowThresholdLineProperty);
        set => SetValue(ShowThresholdLineProperty, value);
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((EntropyBarCanvas)d).InvalidateVisual();

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the user clicks a block. Argument is the file offset.</summary>
    public event EventHandler<long>? OffsetRequested;

    /// <summary>Raised on mouse-move with (blockIndex, offset, entropy) info.</summary>
    public event EventHandler<EntropyHoverEventArgs>? HoverChanged;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Provide entropy data and trigger a redraw.</summary>
    public void SetData(double[]? blockEntropy, int windowSize)
    {
        _blockEntropy = blockEntropy;
        _windowSize   = windowSize > 0 ? windowSize : 1024;
        _hoverIndex   = -1;
        UpdateMinWidth();
        InvalidateVisual();
    }

    /// <summary>Overlay PE/ELF section names on the entropy chart.</summary>
    public void SetSectionLabels(IReadOnlyList<(long Offset, long Length, string Name)> labels)
    {
        _sectionLabels = labels ?? Array.Empty<(long, long, string)>();
        InvalidateVisual();
    }

    /// <summary>Current zoom factor [0.5, 8.0].</summary>
    public double Zoom => _zoom;

    /// <summary>Sets the zoom factor [0.5, 8.0] and updates MinWidth for the parent ScrollViewer.</summary>
    public void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, 0.5, 8.0);
        UpdateMinWidth();
        InvalidateVisual();
    }

    private void UpdateMinWidth()
    {
        if (_blockEntropy is null || _blockEntropy.Length == 0) { MinWidth = 0; return; }
        MinWidth = _blockEntropy.Length * _zoom;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        double w = Math.Max(ActualWidth, MinWidth);
        double h = ActualHeight;
        if (w <= 0 || h <= 0 || _blockEntropy is null || _blockEntropy.Length == 0) return;

        int    count = _blockEntropy.Length;
        double barW  = w / count;
        double threshold = HighEntropyThreshold;

        for (int i = 0; i < count; i++)
        {
            double ent   = _blockEntropy[i];
            double normT = Math.Clamp(ent / 8.0, 0, 1);
            double barH  = normT * (h - 4);

            if (ShowRegions)
            {
                var tint = ent >= threshold ? _regionHighBrush : _regionLowBrush;
                dc.DrawRectangle(tint, null, new Rect(i * barW, 0, barW, h));
            }

            int    palIdx = (int)(normT * 255);
            Brush  fill   = _palette[Math.Clamp(palIdx, 0, 255)];
            double bx     = i * barW;
            double by     = h - barH - 2;
            dc.DrawRectangle(fill, null, new Rect(bx, by, Math.Max(1, barW - 0.5), Math.Max(1, barH)));

            if (i == _hoverIndex)
                dc.DrawRectangle(_hoverBrush, null, new Rect(i * barW, 0, barW, h));
        }

        if (ShowThresholdLine && threshold >= 0 && threshold <= 8)
        {
            double lineY = h - (threshold / 8.0) * (h - 4) - 2;
            lineY = Math.Clamp(lineY, 0, h);
            dc.DrawLine(_threshPen, new Point(0, lineY), new Point(w, lineY));
        }

        // Section label overlays
        if (_sectionLabels.Count > 0 && count > 0)
        {
            long   totalBytes   = (long)count * _windowSize;
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            foreach (var (offset, length, name) in _sectionLabels)
            {
                if (totalBytes <= 0 || string.IsNullOrEmpty(name)) continue;
                double xStart = (offset / (double)totalBytes) * w;
                double xEnd   = ((offset + length) / (double)totalBytes) * w;
                double lw     = Math.Max(1, xEnd - xStart);

                dc.DrawRectangle(_labelBrush, _labelBorderPen, new Rect(xStart, 0, lw, 14));

                var ft = new FormattedText(name, System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, _labelTypeface, 9, Brushes.Black,
                    pixelsPerDip);
                ft.MaxTextWidth  = Math.Max(1, lw - 2);
                ft.Trimming      = TextTrimming.CharacterEllipsis;
                dc.DrawText(ft, new Point(xStart + 1, 2));
            }
        }
    }

    // ── Mouse ─────────────────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_blockEntropy is null || _blockEntropy.Length == 0) return;

        var pos  = e.GetPosition(this);
        int idx  = BlockIndexAt(pos.X);
        if (idx != _hoverIndex)
        {
            _hoverIndex = idx;
            InvalidateVisual();
        }

        if (idx >= 0 && idx < _blockEntropy.Length)
        {
            long offset = (long)idx * _windowSize;
            HoverChanged?.Invoke(this, new EntropyHoverEventArgs
            {
                BlockIndex = idx,
                Offset     = offset,
                Entropy    = _blockEntropy[idx],
            });
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex >= 0)
        {
            _hoverIndex = -1;
            InvalidateVisual();
        }
        HoverChanged?.Invoke(this, new EntropyHoverEventArgs { BlockIndex = -1 });
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_blockEntropy is null) return;

        int idx = BlockIndexAt(e.GetPosition(this).X);
        if (idx >= 0 && idx < _blockEntropy.Length)
        {
            long offset = (long)idx * _windowSize;
            OffsetRequested?.Invoke(this, offset);
            e.Handled = true;
        }
    }

    private int BlockIndexAt(double x)
    {
        if (_blockEntropy is null || _blockEntropy.Length == 0) return -1;
        double w    = Math.Max(ActualWidth, MinWidth);
        if (w <= 0) return -1;
        double barW = w / _blockEntropy.Length;
        int    idx  = (int)(x / barW);
        return Math.Clamp(idx, 0, _blockEntropy.Length - 1);
    }
}

// ---------------------------------------------------------------------------
// ByteFreqCanvas
// ---------------------------------------------------------------------------

/// <summary>
/// DrawingContext renderer for a 256-bar byte-value frequency histogram.
/// </summary>
public sealed class ByteFreqCanvas : FrameworkElement
{
    private static readonly Brush _barBrush;
    private static readonly Brush _zeroBrush;

    static ByteFreqCanvas()
    {
        _barBrush  = new SolidColorBrush(Color.FromArgb(200, 30, 144, 255));
        _barBrush.Freeze();
        _zeroBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
        _zeroBrush.Freeze();
    }

    private long[]? _byteFrequency;

    /// <summary>Sets frequency data and triggers a redraw.</summary>
    public void SetData(long[]? byteFrequency)
    {
        _byteFrequency = byteFrequency;
        InvalidateVisual();
    }

    public ByteFreqCanvas() => IsHitTestVisible = false;

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0 || _byteFrequency is null) return;

        double barW = w / 256.0;
        long   max  = 0;
        foreach (var f in _byteFrequency)
            if (f > max) max = f;
        if (max == 0) return;

        for (int i = 0; i < 256; i++)
        {
            long   freq = _byteFrequency[i];
            Brush  fill;
            double barH;

            if (freq == 0)
            {
                fill = _zeroBrush;
                barH = 2;
            }
            else
            {
                fill = _barBrush;
                barH = Math.Max(2, (freq / (double)max) * (h - 4));
            }

            dc.DrawRectangle(fill, null,
                new Rect(i * barW, h - barH - 2, Math.Max(1, barW), barH));
        }
    }
}

// ---------------------------------------------------------------------------
// Event args
// ---------------------------------------------------------------------------

/// <summary>Data for <see cref="EntropyBarCanvas.HoverChanged"/>.</summary>
public sealed class EntropyHoverEventArgs : EventArgs
{
    /// <summary>-1 when the mouse has left the canvas.</summary>
    public int    BlockIndex { get; init; } = -1;
    public long   Offset     { get; init; }
    public double Entropy    { get; init; }
}
