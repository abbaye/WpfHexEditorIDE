// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: GutterControl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Thin strip rendered to the left of the CodeEditor that shows
//     folding toggle buttons ([+] / [-]) for each FoldingRegion opener line.
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
/// Renders fold toggle markers ([+] / [-]) in the code editor gutter.
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

    // Hit-test rectangles built during OnRender (line → rect).
    private readonly List<(Rect rect, int line)> _hitRects = new();

    // Visual constants.
    private static readonly Brush   _buttonBrush      = Brushes.DimGray;
    private static readonly Pen     _buttonPen         = MakeFrozenPen(Colors.DimGray, 1);
    private static readonly Typeface _markerTypeface   = new("Consolas");
    private const double             MarkerSize        = 11.0;
    private const double             MarkerFontSize    = 9.0;

    #endregion

    #region Constructor

    public GutterControl()
    {
        Width       = MarkerSize + 4;
        Cursor      = Cursors.Hand;
        MouseDown  += OnMouseDown;
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
    public void Update(double lineHeight, int firstVisible, int lastVisible, double topMargin)
    {
        // Only invalidate when something actually changed (called on every CodeEditor OnRender).
        if (_lineHeight == lineHeight && _firstVisibleLine == firstVisible
            && _lastVisibleLine == lastVisible && _topMargin == topMargin)
            return;

        _lineHeight       = lineHeight;
        _firstVisibleLine = firstVisible;
        _lastVisibleLine  = lastVisible;
        _topMargin        = topMargin;
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

            double y = _topMargin + ((startLine - _firstVisibleLine) * _lineHeight);
            double markerY = y + (_lineHeight - MarkerSize) / 2.0;
            double markerX = (ActualWidth - MarkerSize) / 2.0;

            var rect = new Rect(markerX, markerY, MarkerSize, MarkerSize);

            // Draw marker box.
            dc.DrawRectangle(Brushes.Transparent, _buttonPen, rect);

            // Draw + or - symbol.
            string symbol = region.IsCollapsed ? "+" : "\u2212"; // minus sign
            var ft = new FormattedText(
                symbol,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _markerTypeface,
                MarkerFontSize,
                _buttonBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(ft, new Point(
                rect.X + (rect.Width  - ft.Width)  / 2,
                rect.Y + (rect.Height - ft.Height) / 2));

            // Register hit rect for click detection.
            _hitRects.Add((rect, startLine));
        }
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

    private static Pen MakeFrozenPen(Color color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }

    #endregion
}
