// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentHorizontalRuler.cs
// Description:
//     Word-style horizontal ruler with metric/imperial graduations,
//     interactive margin handles, and per-paragraph indent markers
//     (first-line, hanging, left, right). Drag commits flow back to
//     the canvas renderer (margins) or document mutator (indents).
//     Pure FrameworkElement — no XAML, no extra control template overhead.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Editing;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

internal sealed class DocumentHorizontalRuler : FrameworkElement
{
    // ── Layout constants ─────────────────────────────────────────────────────
    private const double RulerHeight       = 22.0;
    private const double TickHeightMajor   =  6.0;
    private const double TickHeightMinor   =  3.0;
    private const double LabelFontSize     =  9.0;
    private const double MarkerSize        =  7.0;   // half-side of indent triangles
    private const double MinDragDelta      =  0.5;

    // ── Dependencies ─────────────────────────────────────────────────────────
    private DocumentCanvasRenderer? _renderer;
    private DocumentMutator?        _mutator;

    // ── Interaction state ────────────────────────────────────────────────────
    private DragKind _dragKind = DragKind.None;
    private double  _dragStartX;
    private double  _dragStartValue;
    private DocumentBlock? _dragBlock;

    private enum DragKind { None, MarginLeft, MarginRight, IndentFirstLine, IndentLeft, IndentRight }

    // ── Brushes / pens (theme-resolved on first render) ──────────────────────
    private Brush? _bg, _marginZone, _fg, _markerFill, _markerStroke;
    private Pen?   _tickPen, _markerPen;
    private Typeface _typeface = new("Segoe UI");

    public DocumentHorizontalRuler()
    {
        Height       = RulerHeight;
        SnapsToDevicePixels = true;
        UseLayoutRounding   = true;
        Cursor              = Cursors.Arrow;
        ToolTipService.SetInitialShowDelay(this, 400);
        ToolTipService.SetBetweenShowDelay(this, 200);
        ToolTipService.SetShowDuration(this, 8000);
        ToolTipOpening += OnToolTipOpening;
        // The ruler lives outside the renderer's ScaleTransform, so its own
        // Width changes when the editor is resized or zoomed (zoom changes
        // the renderer's effective size). Force a redraw on every layout
        // pass so graduations / markers always track the page geometry.
        SizeChanged += (_, _) => InvalidateVisual();
    }

    private void OnToolTipOpening(object sender, System.Windows.Controls.ToolTipEventArgs e)
    {
        var hit = HitTest(Mouse.GetPosition(this));
        string? key = hit switch
        {
            DragKind.MarginLeft      => "Ruler_MarginLeft",
            DragKind.MarginRight     => "Ruler_MarginRight",
            DragKind.IndentLeft      => "Ruler_LeftIndent",
            DragKind.IndentRight     => "Ruler_RightIndent",
            DragKind.IndentFirstLine => "Ruler_FirstLineIndent",
            _                        => null
        };
        if (key is null) { e.Handled = true; ToolTip = null; return; }
        ToolTip = TryFindResource(key) as string ?? key;
    }

    /// <summary>Wires the ruler to its renderer + mutator. Subscribes to geometry events.</summary>
    public void Attach(DocumentCanvasRenderer renderer, DocumentMutator? mutator)
    {
        if (_renderer is not null)
        {
            _renderer.PageGeometryChanged -= OnRendererStateChanged;
            _renderer.CaretBlockChanged   -= OnRendererStateChanged;
            _renderer.CaretMoved          -= OnRendererStateChanged;
        }
        _renderer = renderer;
        _mutator  = mutator;
        if (_renderer is not null)
        {
            _renderer.PageGeometryChanged += OnRendererStateChanged;
            _renderer.CaretBlockChanged   += OnRendererStateChanged;
            _renderer.CaretMoved          += OnRendererStateChanged;
        }
        InvalidateVisual();
    }

    private void OnRendererStateChanged(object? sender, EventArgs e) => InvalidateVisual();

    // ── Coordinate helpers ───────────────────────────────────────────────────

    /// <summary>Pixels per centimetre at 96 DPI = 96/2.54.</summary>
    private const double PxPerCm   = 96.0 / 2.54;
    private const double PxPerInch = 96.0;

    private static bool UseMetric => RegionInfo.CurrentRegion.IsMetric;

    /// <summary>
    /// True page-left X in ruler-local coordinates, computed by transforming the renderer's
    /// canvas origin through all intermediate transforms (zoom ScaleTransform, scroll offset,
    /// grid column offsets). This avoids double-multiplying zoom when the VRuler column
    /// adds an offset that is not part of the renderer's own coordinate system.
    /// </summary>
    private double PageLeftX
    {
        get
        {
            if (_renderer is null) return 0;
            try
            {
                // Transform the page-left point from renderer canvas space to ruler space.
                var pt = _renderer.TranslatePoint(new Point(_renderer.PageLeftOffset, 0), this);
                return pt.X;
            }
            catch { return _renderer.PageLeftOffset * _renderer.ZoomFactor; }
        }
    }

    private double PageWidthDip  => _renderer is null ? 0 : _renderer.PageWidth * _renderer.ZoomFactor;
    private double Zoom          => _renderer?.ZoomFactor ?? 1.0;

    private DocumentPageSettings PS => _renderer?.PageSettings ?? DocumentPageSettings.Default;

    // ── Render ───────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        EnsureBrushCache();
        double w = ActualWidth;
        if (w <= 0 || _renderer is null) return;

        // Background strip
        dc.DrawRectangle(_bg, null, new Rect(0, 0, w, ActualHeight));

        double pageL = PageLeftX;
        double pageW = PageWidthDip;
        if (pageW <= 0) return;

        double mLeft  = PS.MarginLeft  * Zoom;
        double mRight = PS.MarginRight * Zoom;
        double contentL = pageL + mLeft;
        double contentR = pageL + pageW - mRight;

        // Margin zones (darker)
        dc.DrawRectangle(_marginZone, null, new Rect(pageL,    0, mLeft,  ActualHeight));
        dc.DrawRectangle(_marginZone, null, new Rect(contentR, 0, pageW - (contentR - pageL), ActualHeight));

        DrawGraduations(dc, pageL, pageW);
        DrawIndentMarkers(dc, contentL, contentR);
    }

    private void DrawGraduations(DrawingContext dc, double pageL, double pageW)
    {
        double pxPerUnit  = (UseMetric ? PxPerCm : PxPerInch) * Zoom;
        double minorEvery = UseMetric ? 0.5 : 0.125;
        double majorEvery = 1.0;
        double y = ActualHeight - 1;

        // Iterate from page-left, drawing units 0,1,2…
        double maxRel = pageW;
        double rel    = 0;
        int    i      = 0;
        while (rel <= maxRel)
        {
            double x = pageL + rel;
            dc.DrawLine(_tickPen, new Point(x, y - TickHeightMajor), new Point(x, y));
            // Major label
            if (i > 0)
            {
                var ft = new FormattedText(i.ToString(CultureInfo.CurrentCulture),
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface,
                    LabelFontSize, _fg ?? Brushes.Gray, 1.0);
                dc.DrawText(ft, new Point(x - ft.Width / 2, 1));
            }

            // Minor ticks
            for (double m = minorEvery; m < majorEvery; m += minorEvery)
            {
                double xm = x + m * pxPerUnit;
                if (xm > pageL + pageW) break;
                dc.DrawLine(_tickPen, new Point(xm, y - TickHeightMinor), new Point(xm, y));
            }

            rel += majorEvery * pxPerUnit;
            i++;
        }
    }

    private void DrawIndentMarkers(DrawingContext dc, double contentL, double contentR)
    {
        var block = _renderer?.CurrentBlock;
        double h = ActualHeight;

        // Caret position marker — thin vertical line through the full ruler height.
        if (_renderer is not null)
        {
            double cx = _renderer.CaretContentX;
            if (cx >= 0)
            {
                double caretRulerX = contentL + cx * Zoom;
                var caretPen = new Pen(_markerFill ?? Brushes.LightGray, 1.0);
                caretPen.Freeze();
                dc.DrawLine(caretPen, new Point(caretRulerX, 0), new Point(caretRulerX, h));
            }
        }

        if (block is null) return;

        double zoom = Zoom;
        double indentLeft      = ReadAttr(block, "indent")          * zoom * PtToDip;
        double indentRight     = ReadAttr(block, "indentRight")     * zoom * PtToDip;
        double indentFirstLine = ReadAttr(block, "indentFirstLine") * zoom * PtToDip;

        double leftX      = contentL + indentLeft;
        double firstLineX = leftX + indentFirstLine;
        double rightX     = contentR - indentRight;

        DrawTriangle(dc, firstLineX, top: true,  h, _markerFill, _markerPen);  // ▽ first-line indent
        DrawTriangle(dc, leftX,      top: false, h, _markerFill, _markerPen);  // △ left indent
        var squareR = new Rect(leftX - 4, h - 4, 8, 3);
        dc.DrawRectangle(_markerFill, _markerPen, squareR);
        DrawTriangle(dc, rightX,     top: false, h, _markerFill, _markerPen);  // △ right indent
    }

    private static void DrawTriangle(DrawingContext dc, double x, bool top, double h, Brush? fill, Pen? pen)
    {
        if (fill is null) return;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            if (top)
            {
                g.BeginFigure(new Point(x - MarkerSize, 0), true, true);
                g.LineTo(new Point(x + MarkerSize, 0),                 true, false);
                g.LineTo(new Point(x,               MarkerSize + 1),   true, false);
            }
            else
            {
                g.BeginFigure(new Point(x - MarkerSize, h),              true, true);
                g.LineTo(new Point(x + MarkerSize,      h),              true, false);
                g.LineTo(new Point(x,                   h - MarkerSize), true, false);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(fill, pen, geo);
    }

    private void EnsureBrushCache()
    {
        if (_bg is not null) return;
        _bg           = TryFindResource("DE_RulerBackground")     as Brush ?? new SolidColorBrush(Color.FromRgb(60, 60, 60));
        _marginZone   = TryFindResource("DE_RulerMarginZone")     as Brush ?? new SolidColorBrush(Color.FromRgb(40, 40, 40));
        _fg           = TryFindResource("DE_RulerForeground")     as Brush ?? new SolidColorBrush(Color.FromRgb(200, 200, 200));
        _markerFill   = TryFindResource("DE_RulerMarkerFill")     as Brush ?? new SolidColorBrush(Color.FromRgb(220, 220, 220));
        _markerStroke = TryFindResource("DE_RulerMarkerStroke")   as Brush ?? new SolidColorBrush(Color.FromRgb(80, 80, 80));
        _tickPen      = new Pen(_fg, 0.8);
        _tickPen.Freeze();
        _markerPen    = new Pen(_markerStroke, 0.7);
        _markerPen.Freeze();
        if (_bg is Freezable f1 && !f1.IsFrozen) f1.Freeze();
        if (_marginZone is Freezable f2 && !f2.IsFrozen) f2.Freeze();
        if (_fg is Freezable f3 && !f3.IsFrozen) f3.Freeze();
        if (_markerFill is Freezable f4 && !f4.IsFrozen) f4.Freeze();
        if (_markerStroke is Freezable f5 && !f5.IsFrozen) f5.Freeze();
    }

    // ── Drag handling ────────────────────────────────────────────────────────

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_renderer is null) return;
        var pt = e.GetPosition(this);
        var hit = HitTest(pt);
        if (hit == DragKind.None) return;

        _dragKind  = hit;
        _dragStartX = pt.X;
        var block   = _renderer.CurrentBlock;
        _dragBlock  = block;
        _dragStartValue = hit switch
        {
            DragKind.MarginLeft      => PS.MarginLeft,
            DragKind.MarginRight     => PS.MarginRight,
            DragKind.IndentLeft      => block is null ? 0 : ReadAttr(block, "indent")          * PtToDip,
            DragKind.IndentRight     => block is null ? 0 : ReadAttr(block, "indentRight")     * PtToDip,
            DragKind.IndentFirstLine => block is null ? 0 : ReadAttr(block, "indentFirstLine") * PtToDip,
            _ => 0
        };
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragKind == DragKind.None || _renderer is null) return;
        double pt = e.GetPosition(this).X;
        double dxScreen = pt - _dragStartX;
        if (Math.Abs(dxScreen) < MinDragDelta) return;

        // Convert screen DIPs back to model pixels (page coordinate space).
        double dxModel = dxScreen / Zoom;
        double next    = _dragStartValue + (_dragKind == DragKind.MarginRight || _dragKind == DragKind.IndentRight
                                            ? -dxModel : dxModel);
        if (next < 0) next = 0;

        switch (_dragKind)
        {
            case DragKind.MarginLeft:  _renderer.PageSettings = PS.WithMargins(left:  next); break;
            case DragKind.MarginRight: _renderer.PageSettings = PS.WithMargins(right: next); break;
            case DragKind.IndentLeft:
            case DragKind.IndentRight:
            case DragKind.IndentFirstLine:
                if (_dragBlock is not null && _mutator is not null)
                {
                    string key = _dragKind switch
                    {
                        DragKind.IndentRight     => "indentRight",
                        DragKind.IndentFirstLine => "indentFirstLine",
                        _                        => "indent"
                    };
                    // Indent attribs are stored in points; UI works in DIPs.
                    _mutator.SetBlockAttribute(_dragBlock, key, next / PtToDip);
                }
                break;
        }
        InvalidateVisual();
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragKind == DragKind.None) return;
        _dragKind  = DragKind.None;
        _dragBlock = null;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private DragKind HitTest(Point pt)
    {
        if (_renderer is null) return DragKind.None;
        double pageL = PageLeftX, pageW = PageWidthDip;
        if (pageW <= 0) return DragKind.None;
        double mLeftEdge  = pageL + PS.MarginLeft  * Zoom;
        double mRightEdge = pageL + pageW - PS.MarginRight * Zoom;
        const double tol = 4.0;

        // Indent markers take priority near content edges
        var block = _renderer.CurrentBlock;
        if (block is not null)
        {
            double leftX      = mLeftEdge      + ReadAttr(block, "indent")      * Zoom * PtToDip;
            double rightX     = mRightEdge     - ReadAttr(block, "indentRight") * Zoom * PtToDip;
            double firstLineX = leftX          + ReadAttr(block, "indentFirstLine") * Zoom * PtToDip;
            // Top half of ruler = first-line; bottom half = left/right indent
            if (pt.Y < ActualHeight / 2)
            {
                if (Math.Abs(pt.X - firstLineX) <= MarkerSize + tol) return DragKind.IndentFirstLine;
            }
            else
            {
                if (Math.Abs(pt.X - leftX)  <= MarkerSize + tol) return DragKind.IndentLeft;
                if (Math.Abs(pt.X - rightX) <= MarkerSize + tol) return DragKind.IndentRight;
            }
        }

        if (Math.Abs(pt.X - mLeftEdge)  <= tol) return DragKind.MarginLeft;
        if (Math.Abs(pt.X - mRightEdge) <= tol) return DragKind.MarginRight;
        return DragKind.None;
    }

    protected override void OnQueryCursor(QueryCursorEventArgs e)
    {
        var hit = _dragKind != DragKind.None ? _dragKind : HitTest(Mouse.GetPosition(this));
        e.Cursor = hit switch
        {
            DragKind.None => Cursors.Arrow,
            _             => Cursors.SizeWE
        };
        e.Handled = true;
    }

    // ── Attribute helpers ────────────────────────────────────────────────────
    private const double PtToDip = 96.0 / 72.0;

    private static double ReadAttr(DocumentBlock block, string key)
    {
        if (!block.Attributes.TryGetValue(key, out var v) || v is null) return 0;
        return v switch
        {
            double d => d,
            int    i => i,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d2) => d2,
            _ => 0
        };
    }
}
