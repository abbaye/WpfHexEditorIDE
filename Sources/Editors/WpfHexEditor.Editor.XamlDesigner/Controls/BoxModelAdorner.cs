// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: BoxModelAdorner.cs
// Description:
//     Chrome DevTools-style box model overlay. Shows three concentric
//     colored zones (Margin / Element / Padding) with dimension labels
//     on each edge, similar to the "Computed" pane in browser DevTools.
//     Displayed when the user hovers with Alt+Shift held.
//
// Architecture Notes:
//     Purely decorative adorner placed on the AdornedElement's own
//     AdornerLayer. Non-hit-testable. Updated via Refresh().
//     Draws outside the element bounds to show the margin zone.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Box-model overlay adorner showing Margin / Element / Padding zones.
/// </summary>
public sealed class BoxModelAdorner : Adorner
{
    private Thickness _margin;
    private Thickness _padding;

    public BoxModelAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Clears the overlay (removes from adorner layer).</summary>
    public void Clear()
    {
        if (AdornedElement is UIElement el)
            AdornerLayer.GetAdornerLayer(el)?.Remove(this);
    }

    /// <summary>Updates margin and padding values and repaints.</summary>
    public void Refresh(Thickness margin, Thickness padding)
    {
        _margin  = margin;
        _padding = padding;
        InvalidateVisual();
    }

    // ── Adorner overrides ─────────────────────────────────────────────────────

    /// <summary>Expands hit-test area to cover the outer margin zone.</summary>
    protected override HitTestResult HitTestCore(PointHitTestParameters p)
        => base.HitTestCore(p); // non-hit-testable; base returns null by default

    protected override void OnRender(DrawingContext dc)
    {
        var w = AdornedElement.RenderSize.Width;
        var h = AdornedElement.RenderSize.Height;

        // Theme brushes.
        var marginBrush  = Application.Current?.TryFindResource("XD_BoxModelMarginBrush")  as Brush
                           ?? new SolidColorBrush(Color.FromArgb(60, 255, 165, 0));
        var paddingBrush = Application.Current?.TryFindResource("XD_BoxModelPaddingBrush") as Brush
                           ?? new SolidColorBrush(Color.FromArgb(60, 100, 180, 100));
        var borderZone   = Application.Current?.TryFindResource("XD_BoxModelBorderZoneBrush") as Brush
                           ?? new SolidColorBrush(Color.FromArgb(50, 80, 140, 255));

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // ── Margin zone (drawn outside element bounds) ────────────────────────
        // Top margin band
        if (_margin.Top > 0.5)
            dc.DrawRectangle(marginBrush, null,
                new Rect(-_margin.Left, -_margin.Top, w + _margin.Left + _margin.Right, _margin.Top));
        // Bottom margin band
        if (_margin.Bottom > 0.5)
            dc.DrawRectangle(marginBrush, null,
                new Rect(-_margin.Left, h, w + _margin.Left + _margin.Right, _margin.Bottom));
        // Left margin band
        if (_margin.Left > 0.5)
            dc.DrawRectangle(marginBrush, null,
                new Rect(-_margin.Left, 0, _margin.Left, h));
        // Right margin band
        if (_margin.Right > 0.5)
            dc.DrawRectangle(marginBrush, null,
                new Rect(w, 0, _margin.Right, h));

        // ── Element area background ───────────────────────────────────────────
        dc.DrawRectangle(borderZone, null, new Rect(0, 0, w, h));

        // ── Padding zone ──────────────────────────────────────────────────────
        if (_padding.Top > 0.5)
            dc.DrawRectangle(paddingBrush, null, new Rect(0, 0, w, _padding.Top));
        if (_padding.Bottom > 0.5)
            dc.DrawRectangle(paddingBrush, null, new Rect(0, h - _padding.Bottom, w, _padding.Bottom));
        if (_padding.Left > 0.5)
            dc.DrawRectangle(paddingBrush, null, new Rect(0, _padding.Top, _padding.Left, h - _padding.Top - _padding.Bottom));
        if (_padding.Right > 0.5)
            dc.DrawRectangle(paddingBrush, null, new Rect(w - _padding.Right, _padding.Top, _padding.Right, h - _padding.Top - _padding.Bottom));

        // ── Dimension labels ─────────────────────────────────────────────────
        var labelBrush = Brushes.White;
        DrawEdgeLabel(dc, $"{Math.Round(_margin.Top)}", new Point(w / 2, -_margin.Top / 2), dpi, labelBrush);
        DrawEdgeLabel(dc, $"{Math.Round(_margin.Bottom)}", new Point(w / 2, h + _margin.Bottom / 2), dpi, labelBrush);
        DrawEdgeLabel(dc, $"{Math.Round(_margin.Left)}", new Point(-_margin.Left / 2, h / 2), dpi, labelBrush);
        DrawEdgeLabel(dc, $"{Math.Round(_margin.Right)}", new Point(w + _margin.Right / 2, h / 2), dpi, labelBrush);

        if (_padding.Top > 0.5)
            DrawEdgeLabel(dc, $"{Math.Round(_padding.Top)}", new Point(w / 2, _padding.Top / 2), dpi, labelBrush);
        if (_padding.Bottom > 0.5)
            DrawEdgeLabel(dc, $"{Math.Round(_padding.Bottom)}", new Point(w / 2, h - _padding.Bottom / 2), dpi, labelBrush);
        if (_padding.Left > 0.5)
            DrawEdgeLabel(dc, $"{Math.Round(_padding.Left)}", new Point(_padding.Left / 2, h / 2), dpi, labelBrush);
        if (_padding.Right > 0.5)
            DrawEdgeLabel(dc, $"{Math.Round(_padding.Right)}", new Point(w - _padding.Right / 2, h / 2), dpi, labelBrush);
    }

    private static void DrawEdgeLabel(DrawingContext dc, string text, Point center, double dpi, Brush fg)
    {
        if (text == "0") return;

        var ft = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            8.0, fg, dpi);

        dc.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
    }
}
