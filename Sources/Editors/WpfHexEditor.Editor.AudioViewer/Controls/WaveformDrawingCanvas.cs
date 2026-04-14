//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.AudioViewer
// File: Controls/WaveformDrawingCanvas.cs
// Description:
//     High-performance DrawingContext-based waveform renderer.
//     Renders peak min/max columns per pixel — zero UIElement allocation.
//     Supports mono (single band) and stereo (L top / R bottom) layouts.
//     Click → fires OffsetRequested(fileOffset).
//     Hover → fires HoverChanged(column, normalizedAmplitude).
// Architecture:
//     FrameworkElement + OnRender(DrawingContext).
//     Pre-computed WaveformPeaks passed from background thread.
//     Frozen brushes + pens for all drawing operations.
//     Line height adapts to ActualHeight and ChannelCount.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.AudioViewer.Controls;

/// <summary>
/// Pre-computed waveform peaks at a given horizontal resolution.
/// </summary>
/// <param name="MinL">Normalized min amplitude, left channel [-1, 0].</param>
/// <param name="MaxL">Normalized max amplitude, left channel [0, 1].</param>
/// <param name="MinR">Normalized min amplitude, right channel [-1, 0]. Null for mono.</param>
/// <param name="MaxR">Normalized max amplitude, right channel [0, 1]. Null for mono.</param>
/// <param name="ChannelCount">1 = mono, 2 = stereo.</param>
/// <param name="DataOffset">File offset of the first sample byte (for click → HexEditor sync).</param>
/// <param name="DataLength">Total sample data length in bytes.</param>
public sealed record WaveformPeaks(
    double[] MinL,
    double[] MaxL,
    double[]? MinR,
    double[]? MaxR,
    int      ChannelCount,
    long     DataOffset,
    long     DataLength);

/// <summary>
/// Event args for <see cref="WaveformDrawingCanvas.HoverChanged"/>.
/// </summary>
public sealed record WaveformHoverEventArgs(int Column, double Amplitude, long FileOffset);

/// <summary>
/// DrawingContext-based waveform renderer. Fires <see cref="OffsetRequested"/> on click.
/// </summary>
public sealed class WaveformDrawingCanvas : FrameworkElement
{
    // ── Frozen brushes & pens ─────────────────────────────────────────────────

    private static readonly Brush  _brushWaveL;
    private static readonly Brush  _brushWaveR;
    private static readonly Brush  _brushCenter;
    private static readonly Brush  _brushHover;
    private static readonly Brush  _brushSeparator;
    private static readonly Brush  _brushPlayhead;
    private static readonly Pen    _penCenter;
    private static readonly Pen    _penSeparator;
    private static readonly Pen    _penPlayhead;

    static WaveformDrawingCanvas()
    {
        static Brush FB(byte r, byte g, byte b, byte a = 255)
        {
            var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            br.Freeze();
            return br;
        }
        static Pen FP(Brush brush, double thickness)
        {
            var p = new Pen(brush, thickness);
            p.Freeze();
            return p;
        }

        _brushWaveL    = FB(80,  200, 120);           // green — left channel
        _brushWaveR    = FB(80,  160, 240);           // blue  — right channel
        _brushCenter   = FB(128, 128, 128, 80);       // gray  — center line
        _brushHover    = FB(255, 255, 255, 22);       // white tint — hover column
        _brushSeparator = FB(128, 128, 128, 40);      // gray  — L/R divider
        _brushPlayhead = FB(255, 255, 255, 200);      // bright white — playhead line
        _penCenter     = FP(_brushCenter, 1);
        _penSeparator  = FP(_brushSeparator, 1);
        _penPlayhead   = FP(_brushPlayhead, 1.5);
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private WaveformPeaks? _peaks;
    private int            _hoverCol     = -1;
    private double         _playheadFrac = -1;  // -1 = hidden

    /// <summary>
    /// Fraction (0.0–1.0) of the playback position across the waveform.
    /// Set to -1 to hide. Setting any value triggers a redraw.
    /// </summary>
    public double PlayheadFraction
    {
        get => _playheadFrac;
        set
        {
            if (Math.Abs(_playheadFrac - value) < 0.0001) return;
            _playheadFrac = value;
            InvalidateVisual();
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the user clicks a waveform column. Argument is the file byte offset.</summary>
    public event EventHandler<long>?                 OffsetRequested;

    /// <summary>Fired when hover column changes.</summary>
    public event EventHandler<WaveformHoverEventArgs>? HoverChanged;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Sets peak data and triggers a full redraw.</summary>
    public void SetPeaks(WaveformPeaks? peaks)
    {
        _peaks        = peaks;
        _hoverCol     = -1;
        _playheadFrac = -1;
        InvalidateVisual();
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
        => new(
            availableSize.Width  == double.PositiveInfinity ? 400 : availableSize.Width,
            availableSize.Height == double.PositiveInfinity ? 120 : availableSize.Height);

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 1 || h < 1) return;

        if (_peaks is null || _peaks.MinL.Length == 0)
        {
            DrawEmptyState(dc, w, h);
            return;
        }

        var  p          = _peaks;
        int  cols       = p.MinL.Length;
        bool stereo     = p.ChannelCount == 2 && p.MinR is not null;
        double bandH    = stereo ? h / 2.0 : h;
        double scaleX   = w / cols;

        // ── Channel L ─────────────────────────────────────────────────────────
        DrawBand(dc, p.MinL, p.MaxL, _brushWaveL, 0, bandH, w, cols, scaleX);

        // ── Center line — L ───────────────────────────────────────────────────
        double midL = bandH / 2.0;
        dc.DrawLine(_penCenter, new Point(0, midL), new Point(w, midL));

        // ── Channel R (stereo) ────────────────────────────────────────────────
        if (stereo)
        {
            // Divider
            dc.DrawLine(_penSeparator, new Point(0, bandH), new Point(w, bandH));

            DrawBand(dc, p.MinR!, p.MaxR!, _brushWaveR, bandH, bandH, w, cols, scaleX);

            double midR = bandH + bandH / 2.0;
            dc.DrawLine(_penCenter, new Point(0, midR), new Point(w, midR));
        }

        // ── Hover column ──────────────────────────────────────────────────────
        if (_hoverCol >= 0 && _hoverCol < cols)
        {
            double hx = _hoverCol * scaleX;
            dc.DrawRectangle(_brushHover, null, new Rect(hx, 0, Math.Max(scaleX, 1), h));
        }

        // ── Playhead line ──────────────────────────────────────────────────────
        if (_playheadFrac >= 0 && _playheadFrac <= 1)
        {
            double px = _playheadFrac * w;
            dc.DrawLine(_penPlayhead, new Point(px, 0), new Point(px, h));
        }
    }

    private static void DrawBand(
        DrawingContext dc,
        double[] min, double[] max,
        Brush brush,
        double bandTop, double bandH,
        double totalW, int cols, double scaleX)
    {
        double midY = bandTop + bandH / 2.0;

        for (int i = 0; i < cols; i++)
        {
            double lo = min[i]; // negative
            double hi = max[i]; // positive

            double yTop = midY - hi * (bandH / 2.0);
            double yBot = midY - lo * (bandH / 2.0);

            double rectH = Math.Max(1, yBot - yTop);
            double colW  = Math.Max(1, scaleX);

            dc.DrawRectangle(brush, null, new Rect(i * scaleX, yTop, colW, rectH));
        }
    }

    private static void DrawEmptyState(DrawingContext dc, double w, double h)
    {
        // Just a faint center line when no data
        var brush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
        brush.Freeze();
        var pen = new Pen(brush, 1);
        pen.Freeze();
        dc.DrawLine(pen, new Point(0, h / 2), new Point(w, h / 2));
    }

    // ── Mouse ─────────────────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int col = ColAt(e.GetPosition(this).X);
        if (col != _hoverCol)
        {
            _hoverCol = col;
            InvalidateVisual();

            if (_peaks is not null && col >= 0 && col < _peaks.MinL.Length)
            {
                double amp      = _peaks.MaxL[col];
                long   fileOff  = FileOffsetAt(col);
                HoverChanged?.Invoke(this, new WaveformHoverEventArgs(col, amp, fileOff));
            }
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverCol >= 0) { _hoverCol = -1; InvalidateVisual(); }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        int col = ColAt(e.GetPosition(this).X);
        if (col >= 0 && _peaks is not null)
        {
            OffsetRequested?.Invoke(this, FileOffsetAt(col));
            e.Handled = true;
        }
    }

    private int ColAt(double x)
    {
        if (_peaks is null || ActualWidth < 1) return -1;
        int cols = _peaks.MinL.Length;
        int col  = (int)(x / ActualWidth * cols);
        return col >= 0 && col < cols ? col : -1;
    }

    private long FileOffsetAt(int col)
    {
        if (_peaks is null || _peaks.MinL.Length == 0) return 0;
        double ratio = (double)col / _peaks.MinL.Length;
        return _peaks.DataOffset + (long)(ratio * _peaks.DataLength);
    }
}
