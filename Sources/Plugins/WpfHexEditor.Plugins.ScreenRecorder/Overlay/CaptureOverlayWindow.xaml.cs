// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Overlay/CaptureOverlayWindow.xaml.cs
// Description: Non-activating, transparent overlay drawn around the capture region.
//              Excluded from captures via HWND hide/show in FrameCaptureEngine.
// Architecture Notes:
//     WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW set via HwndSource after Show() to
//     prevent focus theft. WM_MOUSEACTIVATE returns MA_NOACTIVATE.
// ==========================================================

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WpfHexEditor.Plugins.ScreenRecorder.Models;
using WpfHexEditor.Plugins.ScreenRecorder.Options;
using WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

namespace WpfHexEditor.Plugins.ScreenRecorder.Overlay;

public partial class CaptureOverlayWindow : Window
{
    private const int GWL_EXSTYLE       = -20;
    private const int WS_EX_NOACTIVATE  = 0x08000000;
    private const int WS_EX_TOOLWINDOW  = 0x00000080;
    private const int WM_MOUSEACTIVATE  = 0x0021;
    private const int WM_HOTKEY         = 0x0312;
    private const int MA_NOACTIVATE     = 3;

    private const int HOTKEY_F9         = 1;
    private const int HOTKEY_SHIFT_F9   = 2;
    private const int MOD_SHIFT         = 0x0004;
    private const int VK_F9             = 0x78;

    /// <summary>Fired when F9 is pressed globally during a capture session.</summary>
    public event EventHandler? CaptureHotkeyPressed;
    /// <summary>Fired when Shift+F9 is pressed globally during a capture session.</summary>
    public event EventHandler? StopHotkeyPressed;

    public IntPtr OverlayHwnd { get; private set; }

    private Brush? _overlayBrush;
    public Brush OverlayBrush => _overlayBrush ??=
        ColorConverter.ConvertFromString(ScreenRecorderOptions.Instance.OverlayColor) is Color c
            ? new SolidColorBrush(c)
            : new SolidColorBrush(Colors.Red);

    public CaptureOverlayWindow() => InitializeComponent();

    public void ShowOverlay(CaptureRegion region, CaptureHudViewModel hud)
    {
        Hud.DataContext = hud;

        if (!IsVisible)
        {
            Show();
            ApplyNonActivatingStyle();
        }

        // ApplyRegion after Show() so PresentationSource.FromVisual(this) is non-null
        // and DPI scaling is read correctly from the window's own composition target.
        ApplyRegion(region);
    }

    public void UpdateRegion(CaptureRegion region) => ApplyRegion(region);

    public void HideOverlay() => Hide();

    // ── Win32 non-activating setup ────────────────────────────────────────────

    private void ApplyNonActivatingStyle()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        OverlayHwnd = hwnd;

        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

        // Register global hotkeys on the overlay HWND — received even when IDE is not foreground.
        RegisterHotKey(hwnd, HOTKEY_F9,       0,        VK_F9);
        RegisterHotKey(hwnd, HOTKEY_SHIFT_F9, MOD_SHIFT, VK_F9);

        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    public void UnregisterHotkeys()
    {
        if (OverlayHwnd == IntPtr.Zero) return;
        UnregisterHotKey(OverlayHwnd, HOTKEY_F9);
        UnregisterHotKey(OverlayHwnd, HOTKEY_SHIFT_F9);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE) { handled = true; return (IntPtr)MA_NOACTIVATE; }

        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_F9)       { CaptureHotkeyPressed?.Invoke(this, EventArgs.Empty); handled = true; }
            if (id == HOTKEY_SHIFT_F9) { StopHotkeyPressed?.Invoke(this, EventArgs.Empty);    handled = true; }
        }

        return IntPtr.Zero;
    }

    private void ApplyRegion(CaptureRegion r)
    {
        // CaptureRegion is in physical pixels; WPF Left/Top/Width/Height need logical units.
        var src  = PresentationSource.FromVisual(this) ??
                   PresentationSource.FromVisual(Application.Current.MainWindow);
        var dpiX = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var dpiY = src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        Left   = r.X      / dpiX;
        Top    = r.Y      / dpiY;
        Width  = r.Width  / dpiX;
        Height = r.Height / dpiY;
    }

    [DllImport("user32.dll")] private static extern int  GetWindowLong(IntPtr hwnd, int idx);
    [DllImport("user32.dll")] private static extern int  SetWindowLong(IntPtr hwnd, int idx, int val);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, int mods, int vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
