// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentVerticalRuler.cs
// Description:
//     Word-style vertical ruler showing the page header/footer/body
//     bands with draggable top + bottom margin handles.
//     The ruler does not scroll with the document — it is anchored to
//     the page card position derived from the renderer's geometry.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

internal sealed class DocumentVerticalRuler : FrameworkElement
{
    private const double TickHeightMajor = 6.0;
    private const double TickHeightMinor = 3.0;
    private const double LabelFontSize   = 9.0;
    private const double MinDragDelta    = 0.5;
    private const double PxPerCm   = 96.0 / 2.54;
    private const double PxPerInch = 96.0;

    private DocumentCanvasRenderer? _renderer;
    private DragKind _dragKind = DragKind.None;
    private double  _dragStartY;
    private double  _dragStartValue;

    private enum DragKind { None, MarginTop, MarginBottom }

    private Brush? _bg, _marginZone, _fg;
    private Pen?   _tickPen;
    private readonly Typeface _typeface = new("Segoe UI");

    public DocumentVerticalRuler()
    {
        Width               = 18;
        SnapsToDevicePixels = true;
        UseLayoutRounding   = true;
        ToolTipService.SetInitialShowDelay(this, 400);
        ToolTipOpening += (_, e) =>
        {
            var hit = HitTest(Mouse.GetPosition(this));
            string? key = hit switch
            {
                DragKind.MarginTop    => "Ruler_MarginTop",
                DragKind.MarginBottom => "Ruler_MarginBottom",
                _                     => null
            };
            if (key is null) { e.Handled = true; ToolTip = null; return; }
            ToolTip = TryFindResource(key) as string ?? key;
        };
    }

    public void Attach(DocumentCanvasRenderer renderer)
    {
        if (_renderer is not null)
            _renderer.PageGeometryChanged -= OnRendererStateChanged;
        _renderer = renderer;
        if (_renderer is not null)
            _renderer.PageGeometryChanged += OnRendererStateChanged;
        InvalidateVisual();
    }

    private void OnRendererStateChanged(object? sender, EventArgs e) => InvalidateVisual();

    private static bool UseMetric => RegionInfo.CurrentRegion.IsMetric;

    private double Zoom => _renderer?.ZoomFactor ?? 1.0;
    private DocumentPageSettings PS => _renderer?.PageSettings ?? DocumentPageSettings.Default;

    protected override void OnRender(DrawingContext dc)
    {
        EnsureBrushCache();
        if (_renderer is null) return;
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        dc.DrawRectangle(_bg, null, new Rect(0, 0, w, h));

        // Page content starts at PageCanvasPad in renderer canvas space.
        // Ruler Y = 0 corresponds to renderer viewport top = scrollOffset.
        // So page top in ruler space = PageCanvasPad - scrollOffset (may be negative when scrolled).
        double scrollY   = _renderer.VerticalOffset * Zoom;
        double pageTopY  = DocumentCanvasRenderer.PageCanvasPadding * Zoom - scrollY;
        double mTop      = PS.MarginTop    * Zoom;
        double mBottom   = PS.MarginBottom * Zoom;
        double pageH     = (PS.MarginTop + PS.ContentHeight + PS.MarginBottom) * Zoom;

        // Top margin zone (header band)
        double topMarginRulerY = pageTopY;
        if (topMarginRulerY < h && topMarginRulerY + mTop > 0)
            dc.DrawRectangle(_marginZone, null, new Rect(0, Math.Max(0, topMarginRulerY),
                w, Math.Min(mTop, h - Math.Max(0, topMarginRulerY))));

        // Bottom margin zone (footer band)
        double botMarginRulerY = pageTopY + pageH - mBottom;
        if (botMarginRulerY < h && botMarginRulerY + mBottom > 0)
            dc.DrawRectangle(_marginZone, null, new Rect(0, Math.Max(0, botMarginRulerY),
                w, Math.Min(mBottom, h - Math.Max(0, botMarginRulerY))));

        DrawGraduations(dc, w, h, pageTopY + mTop);
    }

    // pageContentY0: ruler-local Y where page content (unit 0) begins.
    private void DrawGraduations(DrawingContext dc, double w, double h, double pageContentY0)
    {
        double pxPerUnit  = (UseMetric ? PxPerCm : PxPerInch) * Zoom;
        double minorEvery = UseMetric ? 0.5 : 0.125;

        // Start from the first unit that is visible (may be negative offset).
        int startUnit = (int)Math.Floor(-pageContentY0 / pxPerUnit);
        startUnit     = Math.Max(0, startUnit);

        for (int i = startUnit; ; i++)
        {
            double y = pageContentY0 + i * pxPerUnit;
            if (y > h) break;
            if (y >= 0)
            {
                dc.DrawLine(_tickPen, new Point(w - TickHeightMajor, y), new Point(w, y));
                if (i > 0)
                {
                    var ft = new FormattedText(i.ToString(CultureInfo.CurrentCulture),
                        CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface,
                        LabelFontSize, _fg ?? Brushes.Gray, 1.0);
                    dc.DrawText(ft, new Point((w - ft.Width) / 2, y - ft.Height / 2));
                }
            }

            for (double m = minorEvery; m < 1.0; m += minorEvery)
            {
                double ym = y + m * pxPerUnit;
                if (ym > h) break;
                if (ym >= 0)
                    dc.DrawLine(_tickPen, new Point(w - TickHeightMinor, ym), new Point(w, ym));
            }
        }
    }

    private void EnsureBrushCache()
    {
        if (_bg is not null) return;
        _bg         = TryFindResource("DE_RulerBackground") as Brush ?? new SolidColorBrush(Color.FromRgb(60, 60, 60));
        _marginZone = TryFindResource("DE_RulerMarginZone") as Brush ?? new SolidColorBrush(Color.FromRgb(40, 40, 40));
        _fg         = TryFindResource("DE_RulerForeground") as Brush ?? new SolidColorBrush(Color.FromRgb(200, 200, 200));
        _tickPen    = new Pen(_fg, 0.8);
        _tickPen.Freeze();
        if (_bg is Freezable f1 && !f1.IsFrozen) f1.Freeze();
        if (_marginZone is Freezable f2 && !f2.IsFrozen) f2.Freeze();
        if (_fg is Freezable f3 && !f3.IsFrozen) f3.Freeze();
    }

    // ── Drag ─────────────────────────────────────────────────────────────────

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_renderer is null) return;
        var pt = e.GetPosition(this);
        var hit = HitTest(pt);
        if (hit == DragKind.None) return;
        _dragKind = hit;
        _dragStartY = pt.Y;
        _dragStartValue = hit == DragKind.MarginTop ? PS.MarginTop : PS.MarginBottom;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragKind == DragKind.None || _renderer is null) return;
        double dy = e.GetPosition(this).Y - _dragStartY;
        if (Math.Abs(dy) < MinDragDelta) return;
        double dyModel = dy / Zoom;
        double next = _dragKind == DragKind.MarginTop
            ? Math.Max(0, _dragStartValue + dyModel)
            : Math.Max(0, _dragStartValue - dyModel);
        _renderer.PageSettings = _dragKind == DragKind.MarginTop
            ? PS.WithMargins(top: next)
            : PS.WithMargins(bottom: next);
        InvalidateVisual();
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragKind == DragKind.None) return;
        _dragKind = DragKind.None;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private DragKind HitTest(Point pt)
    {
        if (_renderer is null) return DragKind.None;
        double mTop    = PS.MarginTop    * Zoom;
        double mBottom = PS.MarginBottom * Zoom;
        const double tol = 4.0;
        if (Math.Abs(pt.Y - mTop)              <= tol) return DragKind.MarginTop;
        if (Math.Abs(pt.Y - (ActualHeight - mBottom)) <= tol) return DragKind.MarginBottom;
        return DragKind.None;
    }

    protected override void OnQueryCursor(QueryCursorEventArgs e)
    {
        var hit = _dragKind != DragKind.None ? _dragKind : HitTest(Mouse.GetPosition(this));
        e.Cursor = hit == DragKind.None ? Cursors.Arrow : Cursors.SizeNS;
        e.Handled = true;
    }
}
