// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Overlay/RegionSelectorWindow.xaml.cs
// Description: Rubber-band region selector — full-screen transparent drag canvas.
//              Shows dim overlay outside selection, live dimensions, corner handles.
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
        CenterInstruction();
        UpdateDim(0, 0, 0, 0);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        CenterInstruction();
    }

    private void CenterInstruction()
    {
        InstructionBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(InstructionBorder, (RootCanvas.ActualWidth  - InstructionBorder.DesiredSize.Width)  / 2);
        Canvas.SetTop(InstructionBorder,  (RootCanvas.ActualHeight - InstructionBorder.DesiredSize.Height) / 2);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(RootCanvas);
        _dragging   = true;
        CaptureMouse();

        InstructionBorder.Visibility = Visibility.Collapsed;
        SelectionRect.Visibility     = Visibility.Visible;
        SizeLabel.Visibility         = Visibility.Visible;
        SetHandleVisibility(Visibility.Visible);

        UpdateRect(_startPoint);
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
        UpdateRect(e.GetPosition(RootCanvas));

        SelectedRegion = BuildRegion();
        ReleaseMouseCapture();
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
        Canvas.SetTop(SelectionRect,  y);
        SelectionRect.Width  = w;
        SelectionRect.Height = h;

        UpdateDim(x, y, w, h);
        UpdateHandles(x, y, w, h);
        UpdateSizeLabel(x, y, w, h);
    }

    private void UpdateDim(double x, double y, double w, double h)
    {
        var cw = RootCanvas.ActualWidth;
        var ch = RootCanvas.ActualHeight;

        // Top band
        DimTop.Width  = cw;
        DimTop.Height = Math.Max(0, y);
        Canvas.SetLeft(DimTop, 0);
        Canvas.SetTop(DimTop,  0);

        // Bottom band
        DimBottom.Width  = cw;
        DimBottom.Height = Math.Max(0, ch - y - h);
        Canvas.SetLeft(DimBottom, 0);
        Canvas.SetTop(DimBottom,  y + h);

        // Left band (between top and bottom bands)
        DimLeft.Width  = Math.Max(0, x);
        DimLeft.Height = h;
        Canvas.SetLeft(DimLeft, 0);
        Canvas.SetTop(DimLeft,  y);

        // Right band
        DimRight.Width  = Math.Max(0, cw - x - w);
        DimRight.Height = h;
        Canvas.SetLeft(DimRight, x + w);
        Canvas.SetTop(DimRight,  y);
    }

    private void UpdateHandles(double x, double y, double w, double h)
    {
        const double hs = 8;
        const double half = hs / 2;
        Canvas.SetLeft(HandleTL, x - half);       Canvas.SetTop(HandleTL, y - half);
        Canvas.SetLeft(HandleTR, x + w - half);   Canvas.SetTop(HandleTR, y - half);
        Canvas.SetLeft(HandleBL, x - half);       Canvas.SetTop(HandleBL, y + h - half);
        Canvas.SetLeft(HandleBR, x + w - half);   Canvas.SetTop(HandleBR, y + h - half);
    }

    private void UpdateSizeLabel(double x, double y, double w, double h)
    {
        var source = PresentationSource.FromVisual(this);
        var dpiX   = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var dpiY   = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        var px     = (int)(w * dpiX);
        var py     = (int)(h * dpiY);
        SizeText.Text = $"{px} × {py}";

        SizeLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var lw = SizeLabel.DesiredSize.Width;
        var lh = SizeLabel.DesiredSize.Height;

        // Place label below selection if space, otherwise above
        var labelY = y + h + 6;
        if (labelY + lh > RootCanvas.ActualHeight) labelY = y - lh - 6;
        Canvas.SetLeft(SizeLabel, Math.Clamp(x + (w - lw) / 2, 4, RootCanvas.ActualWidth - lw - 4));
        Canvas.SetTop(SizeLabel,  Math.Max(4, labelY));
    }

    private void SetHandleVisibility(Visibility v)
    {
        HandleTL.Visibility = v;
        HandleTR.Visibility = v;
        HandleBL.Visibility = v;
        HandleBR.Visibility = v;
    }

    private CaptureRegion? BuildRegion()
    {
        var logW = SelectionRect.Width;
        var logH = SelectionRect.Height;
        if (logW < 10 || logH < 10) return null;

        var source  = PresentationSource.FromVisual(this);
        var dpiX    = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var dpiY    = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        var screenPt = PointToScreen(new Point(Canvas.GetLeft(SelectionRect), Canvas.GetTop(SelectionRect)));
        return new CaptureRegion(
            (int)screenPt.X, (int)screenPt.Y,
            (int)(logW * dpiX), (int)(logH * dpiY));
    }
}
