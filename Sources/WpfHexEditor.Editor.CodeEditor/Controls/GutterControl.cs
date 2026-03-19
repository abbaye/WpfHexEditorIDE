// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: GutterControl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Thin strip rendered to the left of the CodeEditor that shows
//     folding toggle buttons for each FoldingRegion opener line.
//     All regions (brace and directive) use a unified VS Code-style
//     filled triangle (▶ expanded / ▼ collapsed).
//     Positioned and sized by the host CodeEditor after each layout pass.
//
// Architecture Notes:
//     Observer Pattern — subscribes to FoldingEngine.RegionsChanged to
//     trigger InvalidateVisual() without polling.
//     Rendered with DrawingContext (same pattern as CodeEditor) for performance.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Folding;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Renders fold toggle markers in the code editor gutter.
/// All regions (brace and directive) → ▶ / ▼ filled triangle (VS Code style).
/// The host <see cref="CodeEditor"/> must supply updated layout parameters
/// via <see cref="Update"/> before each render pass.
/// </summary>
internal sealed class GutterControl : FrameworkElement
{
    #region Fields

    private FoldingEngine?             _engine;
    private double                     _lineHeight;
    private int                        _firstVisibleLine;
    private int                        _lastVisibleLine;
    private double                     _topMargin;
    private double                     _scrollFraction; // sub-pixel smooth-scroll offset from CodeEditor
    private IReadOnlyDictionary<int, double> _lineYLookup = new Dictionary<int, double>(); // per-line Y from CodeEditor (includes CodeLens offsets)

    // Hit-test rectangles built during OnRender (line → rect).
    private readonly List<(Rect rect, int line)> _hitRects = new();

    // Visual constants.
    private static readonly Brush    _buttonBrush    = Brushes.DimGray;
    private static readonly Typeface _markerTypeface = new("Consolas");
    private const double             MarkerSize      = 11.0;
    private const double             MarkerFontSize  = 9.0;

    // Pre-built frozen triangle geometries used for all fold regions (▶ and ▼).
    private static readonly Geometry _triRight = MakeTriangle(pointRight: true);  // expanded state
    private static readonly Geometry _triDown  = MakeTriangle(pointRight: false); // collapsed state

    #endregion

    #region Constructor

    public GutterControl()
    {
        Width      = MarkerSize + 4;
        Cursor     = Cursors.Hand;
        MouseDown += OnMouseDown;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Connects this gutter to a <see cref="FoldingEngine"/> and triggers
    /// a visual update whenever the engine's region list changes.
    /// Pass <c>null</c> to disconnect.
    /// </summary>
    public void SetEngine(FoldingEngine? engine)
    {
        if (_engine != null)
            _engine.RegionsChanged -= OnRegionsChanged;

        _engine = engine;

        if (_engine != null)
            _engine.RegionsChanged += OnRegionsChanged;

        InvalidateVisual();
    }

    /// <summary>
    /// Updates the layout parameters the gutter needs to align markers with
    /// the code editor lines.  Call before <see cref="UIElement.InvalidateVisual"/>.
    /// </summary>
    public void Update(double lineHeight, int firstVisible, int lastVisible,
                       double topMargin, double scrollFraction,
                       IReadOnlyDictionary<int, double> lineYLookup)
    {
        // Always update all params and invalidate — the early-return optimisation that compared
        // the 5 scalar fields caused stale hit-rects: when the user toggled a fold without
        // scrolling, the scalars were unchanged so InvalidateVisual() was skipped, leaving the
        // gutter with wrong Y positions for sibling region toggles.
        // The gutter is a small element; the extra InvalidateVisual() calls are negligible.
        _lineHeight       = lineHeight;
        _firstVisibleLine = firstVisible;
        _lastVisibleLine  = lastVisible;
        _topMargin        = topMargin;
        _scrollFraction   = scrollFraction;
        _lineYLookup      = lineYLookup;
        InvalidateVisual();
    }

    #endregion

    #region Rendering

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        _hitRects.Clear();

        if (_engine == null || _lineHeight <= 0)
            return;

        foreach (var region in _engine.Regions)
        {
            int startLine = region.StartLine;
            if (startLine < _firstVisibleLine || startLine > _lastVisibleLine)
                continue;

            // Use the exact per-line Y from CodeEditor when available (accounts for CodeLens
            // hint rows that add LensLineHeight pixels above the code text).  Fall back to
            // uniform layout when the lookup has no entry for this line.
            double y;
            if (!_lineYLookup.TryGetValue(startLine, out y))
            {
                int visIdx = 0;
                for (int i = _firstVisibleLine; i < startLine; i++)
                    if (_engine == null || !_engine.IsLineHidden(i)) visIdx++;
                y = _topMargin + _scrollFraction + visIdx * _lineHeight;
            }

            double markerY = y + (_lineHeight - MarkerSize) / 2.0;
            double markerX = (ActualWidth - MarkerSize) / 2.0;

            // Hide marker when the opener line has scrolled outside the visible area.
            if (markerY + MarkerSize <= _topMargin || markerY >= ActualHeight)
                continue;

            var rect = new Rect(markerX, markerY, MarkerSize, MarkerSize);

            DrawDirectiveToggle(dc, rect, region.IsCollapsed);

            // Register hit rect for click detection.
            _hitRects.Add((rect, startLine));
        }
    }

    // Renders a filled triangle (▶ expanded / ▼ collapsed) for all fold regions.
    private static void DrawDirectiveToggle(DrawingContext dc, Rect rect, bool isCollapsed)
    {
        // Select the pre-built geometry: ▶ when expanded (collapsible), ▼ when collapsed.
        var tri = isCollapsed ? _triDown : _triRight;

        // Translate the [0,0]-based geometry to the marker position using PushTransform.
        dc.PushTransform(new TranslateTransform(rect.X, rect.Y));
        dc.DrawGeometry(_buttonBrush, null, tri);
        dc.Pop();
    }

    #endregion

    #region Mouse interaction

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_engine == null || e.ChangedButton != MouseButton.Left)
            return;

        var pos = e.GetPosition(this);
        foreach (var (rect, line) in _hitRects)
        {
            if (rect.Contains(pos))
            {
                _engine.ToggleRegion(line);
                e.Handled = true;
                return;
            }
        }
    }

    #endregion

    #region Event handlers

    private void OnRegionsChanged(object? sender, EventArgs e)
        => InvalidateVisual();

    #endregion

    #region Helpers

    /// <summary>
    /// Builds a frozen filled triangle fitting within a [0,0]→[MarkerSize,MarkerSize] box.
    /// <paramref name="pointRight"/> = <c>true</c> → ▶ (region expanded, click to collapse).
    /// <paramref name="pointRight"/> = <c>false</c> → ▼ (region collapsed, click to expand).
    /// </summary>
    private static Geometry MakeTriangle(bool pointRight)
    {
        const double m = MarkerSize;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            if (pointRight) // ▶
            {
                ctx.BeginFigure(new Point(2,     1),     isFilled: true, isClosed: true);
                ctx.LineTo(     new Point(m - 1, m / 2), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(     new Point(2,     m - 1), isStroked: false, isSmoothJoin: false);
            }
            else // ▼
            {
                ctx.BeginFigure(new Point(1,     2),     isFilled: true, isClosed: true);
                ctx.LineTo(     new Point(m - 1, 2),     isStroked: false, isSmoothJoin: false);
                ctx.LineTo(     new Point(m / 2, m - 1), isStroked: false, isSmoothJoin: false);
            }
        }
        g.Freeze();
        return g;
    }

    #endregion
}
