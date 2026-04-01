//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// Project     : WpfHexEditor.Editor.Core
// File        : Helpers/PanModeController.cs
// Description : Shared middle-click auto-scroll (pan mode) controller.
//               Encapsulates all state, cursor management, timer, and visual
//               indicator rendering. Editors wire it via a pixel-delta callback.
// Architecture: Composition helper — no base class dependency. Each editor
//               creates one instance in its constructor and delegates the 5
//               relevant events: MouseDown, MouseMove, KeyDown, LostFocus, Render.

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WpfHexEditor.Editor.Core.Helpers;

/// <summary>
/// Implements middle-click auto-scroll (pan mode) for any <see cref="FrameworkElement"/>
/// that exposes a pixel-delta scroll callback.
/// </summary>
/// <remarks>
/// Usage — in the host constructor:
/// <code>
///   _panMode = new PanModeController(this, (dx, dy) => ScrollBy(dx, dy));
/// </code>
/// Then delegate the five events:
/// <code>
///   OnMouseDown  → if (_panMode.HandleMouseDown(e)) return;
///   OnMouseMove  → if (_panMode.HandleMouseMove(e)) return;
///   OnKeyDown    → if (_panMode.HandleKeyDown(e))   return;
///   OnLostFocus  → _panMode.HandleLostFocus();
///   OnRender     → _panMode.Render(dc);          // call at end of OnRender
/// </code>
/// </remarks>
public sealed class PanModeController
{
    // ── Configuration ────────────────────────────────────────────────────────
    private const double DeadZoneRadius = 8.0;   // px — no scroll within this circle
    private const double SpeedFactor    = 0.12;  // px-scrolled / px-distance / tick (16 ms)

    // ── Dependencies ─────────────────────────────────────────────────────────
    private readonly FrameworkElement      _host;
    private readonly Action<double,double> _scrollBy;  // (dx, dy) in pixels
    private readonly DispatcherTimer       _timer;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool  _isActive;
    private Point _origin;      // middle-click position in _host coords
    private Point _lastMouse;   // updated on every MouseMove

    // ── Visual resources (created once, reused) ───────────────────────────────
    private static readonly SolidColorBrush IndicatorFill =
        new(Color.FromArgb(180, 220, 220, 220));
    private static readonly Pen IndicatorRingPen  = new(Brushes.Gray, 1.5);
    private static readonly Pen IndicatorCrossPen = new(Brushes.DarkSlateGray, 1.5);

    static PanModeController()
    {
        IndicatorFill.Freeze();
        IndicatorRingPen.Freeze();
        IndicatorCrossPen.Freeze();
    }

    // ── Public ────────────────────────────────────────────────────────────────

    public bool IsActive => _isActive;

    /// <param name="host">The FrameworkElement that owns mouse capture and cursor.</param>
    /// <param name="scrollBy">Callback invoked each timer tick: (dx, dy) in pixels.</param>
    public PanModeController(FrameworkElement host, Action<double, double> scrollBy)
    {
        _host      = host      ?? throw new ArgumentNullException(nameof(host));
        _scrollBy  = scrollBy  ?? throw new ArgumentNullException(nameof(scrollBy));
        _timer     = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
    }

    // ── Event Handlers (called by host) ──────────────────────────────────────

    /// <summary>Handles MouseDown. Returns <c>true</c> if the event was consumed.</summary>
    public bool HandleMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            if (_isActive) Exit();
            else Enter(e.GetPosition(_host));
            e.Handled = true;
            return true;
        }

        // Any other button press exits pan mode (event is NOT consumed)
        if (_isActive) Exit();
        return false;
    }

    /// <summary>Handles MouseMove. Returns <c>true</c> if in pan mode (host should return early).</summary>
    public bool HandleMouseMove(MouseEventArgs e)
    {
        _lastMouse = e.GetPosition(_host);
        if (!_isActive) return false;
        _host.Cursor = ComputeCursor(_lastMouse - _origin);
        return true;
    }

    /// <summary>Handles KeyDown. Returns <c>true</c> if Escape was consumed to exit pan mode.</summary>
    public bool HandleKeyDown(KeyEventArgs e)
    {
        if (_isActive && e.Key == Key.Escape)
        {
            Exit();
            e.Handled = true;
            return true;
        }
        return false;
    }

    /// <summary>Call from host's LostFocus / OnLostFocus handler.</summary>
    public void HandleLostFocus() { if (_isActive) Exit(); }

    /// <summary>
    /// Draws the pan-origin indicator. Must be called at the <em>end</em> of
    /// the host's <c>OnRender</c> override so it appears on top of all content.
    /// </summary>
    public void Render(DrawingContext dc)
    {
        if (!_isActive) return;

        // Outer circle
        dc.DrawEllipse(IndicatorFill, IndicatorRingPen, _origin, 10, 10);

        // Inner cross
        dc.DrawLine(IndicatorCrossPen,
            new Point(_origin.X - 5, _origin.Y),
            new Point(_origin.X + 5, _origin.Y));
        dc.DrawLine(IndicatorCrossPen,
            new Point(_origin.X, _origin.Y - 5),
            new Point(_origin.X, _origin.Y + 5));
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Enter(Point origin)
    {
        _origin    = origin;
        _lastMouse = origin;
        _isActive  = true;
        _host.CaptureMouse();
        _host.Cursor = Cursors.ScrollAll;
        _timer.Start();
        _host.InvalidateVisual();
    }

    private void Exit()
    {
        _isActive = false;
        _timer.Stop();
        _host.ReleaseMouseCapture();
        _host.Cursor = null;
        _host.InvalidateVisual();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_isActive) { _timer.Stop(); return; }

        var delta = _lastMouse - _origin;
        var dist  = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        if (dist < DeadZoneRadius) return;

        double speed = (dist - DeadZoneRadius) * SpeedFactor;
        _scrollBy(delta.X / dist * speed, delta.Y / dist * speed);
    }

    private static Cursor ComputeCursor(Vector d)
    {
        var dist = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        if (dist < DeadZoneRadius) return Cursors.ScrollAll;

        double a = Math.Atan2(d.Y, d.X) * 180.0 / Math.PI;  // –180 … 180
        return a switch
        {
            >= -22.5  and < 22.5   => Cursors.ScrollE,
            >= 22.5   and < 67.5   => Cursors.ScrollSE,
            >= 67.5   and < 112.5  => Cursors.ScrollS,
            >= 112.5  and < 157.5  => Cursors.ScrollSW,
            >= 157.5  or  < -157.5 => Cursors.ScrollW,
            >= -157.5 and < -112.5 => Cursors.ScrollNW,
            >= -112.5 and < -67.5  => Cursors.ScrollN,
            _                       => Cursors.ScrollNE,
        };
    }
}
