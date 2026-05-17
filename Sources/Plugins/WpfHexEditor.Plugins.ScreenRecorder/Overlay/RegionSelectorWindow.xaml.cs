// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Overlay/RegionSelectorWindow.xaml.cs
// Description: Rubber-band region selector — full-screen transparent drag canvas.
//              Returns screen-coordinate CaptureRegion on mouse-up; null on ESC.
// ==========================================================

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Plugins.ScreenRecorder.Models;

namespace WpfHexEditor.Plugins.ScreenRecorder.Overlay;

public partial class RegionSelectorWindow : Window
{
    public CaptureRegion? SelectedRegion { get; private set; }

    private Point _startPoint;
    private bool  _dragging;

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int n);
    private const int SM_XVIRTUALSCREEN  = 76;
    private const int SM_YVIRTUALSCREEN  = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    public RegionSelectorWindow()
    {
        InitializeComponent();

        // Use physical pixel dimensions so the overlay truly covers all monitors
        // regardless of DPI scaling. WPF device-independent units equal physical pixels
        // at 96 Dpi; we compensate via the visual's DPI transform when needed.
        // GetSystemMetrics returns physical pixels — divide by DPI factor for WPF coords.
        var source = PresentationSource.FromVisual(Application.Current.MainWindow);
        var dpiX   = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var dpiY   = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        Left   = GetSystemMetrics(SM_XVIRTUALSCREEN)  / dpiX;
        Top    = GetSystemMetrics(SM_YVIRTUALSCREEN)  / dpiY;
        Width  = GetSystemMetrics(SM_CXVIRTUALSCREEN) / dpiX;
        Height = GetSystemMetrics(SM_CYVIRTUALSCREEN) / dpiY;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        RootCanvas.Focus();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(RootCanvas);
        _dragging   = true;
        RootCanvas.CaptureMouse();

        Canvas.SetLeft(SelectionRect, _startPoint.X);
        Canvas.SetTop(SelectionRect, _startPoint.Y);
        SelectionRect.Width      = 0;
        SelectionRect.Height     = 0;
        SelectionRect.Visibility = Visibility.Visible;
        InstructionText.Visibility = Visibility.Collapsed;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        UpdateRect(e.GetPosition(RootCanvas));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        RootCanvas.ReleaseMouseCapture();
        UpdateRect(e.GetPosition(RootCanvas));

        SelectedRegion = BuildRegion();
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { SelectedRegion = null; Close(); }
    }

    private void UpdateRect(Point current)
    {
        var x = Math.Min(_startPoint.X, current.X);
        var y = Math.Min(_startPoint.Y, current.Y);
        var w = Math.Abs(current.X - _startPoint.X);
        var h = Math.Abs(current.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width  = w;
        SelectionRect.Height = h;
    }

    private CaptureRegion? BuildRegion()
    {
        var logW = SelectionRect.Width;
        var logH = SelectionRect.Height;
        if (logW < 10 || logH < 10) return null;

        var source = PresentationSource.FromVisual(this);
        var dpiX   = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var dpiY   = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        var screenPt = PointToScreen(new Point(Canvas.GetLeft(SelectionRect), Canvas.GetTop(SelectionRect)));
        return new CaptureRegion(
            (int)screenPt.X, (int)screenPt.Y,
            (int)(logW * dpiX), (int)(logH * dpiY));
    }
}
