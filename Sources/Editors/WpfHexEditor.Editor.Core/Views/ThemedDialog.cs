// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: ThemedDialog.cs
// Created: 2026-03-06
// Description:
//     Common themed base class for all application dialogs.
//     Provides a VS2022-style custom title bar, per-monitor
//     multi-screen maximize fix, and an optional resize grip.
//
// Architecture Notes:
//     Window subclass — the dialog's XAML content is captured
//     in OnInitialized and wrapped inside a themed chrome grid.
//     All chrome is built in code-behind (no XAML) to avoid
//     assembly coupling. DynamicResource keys resolve from the
//     active Docking.Wpf theme loaded by the host application.
//
//     Subclasses only need to:
//       1. Inherit ThemedDialog instead of Window
//       2. Set ResizeMode if not NoResize (default)
//       3. Write their dialog content as usual
// ==========================================================

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace WpfHexEditor.Editor.Core.Views;

/// <summary>
/// Base class for all application dialogs.
/// Replaces the OS title bar with a themed custom chrome that
/// follows the active Docking.Wpf theme (DockMenuBackgroundBrush etc.).
/// </summary>
/// <remarks>
/// Provides:
///   - Themed title bar (28 px) with close button and optional maximize/restore
///   - Per-monitor correct maximize via WM_GETMINMAXINFO + MonitorFromWindow
///   - Visual resize grip (3-dot diagonal) in the bottom-right corner when resizable
///   - Outer 1 px themed border, hidden automatically when maximized
/// </remarks>
public class ThemedDialog : Window
{
    // -- ShowIcon dependency property --------------------------------------
    public static readonly DependencyProperty ShowIconProperty =
        DependencyProperty.Register(nameof(ShowIcon), typeof(bool),
            typeof(ThemedDialog), new PropertyMetadata(false));

    /// <summary>
    /// When true, shows the application icon on the left of the title bar.
    /// Right-click opens the system menu; double-click closes the dialog.
    /// </summary>
    public bool ShowIcon
    {
        get => (bool)GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    // -- Wrapper elements kept as fields so StateChanged can mutate them --
    private Border? _outerBorder;
    private Button? _maxButton;

    // -- Win32 P/Invoke — multi-monitor maximize --------------------------
    [StructLayout(LayoutKind.Sequential)] private struct WINPOINT  { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct WINRECT   { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MINMAXINFO
    {
        public WINPOINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO
    {
        public int     cbSize;
        public WINRECT rcMonitor;
        public WINRECT rcWork;
        public uint    dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // --------------------------------------------------------------------

    public ThemedDialog()
    {
        WindowStyle           = WindowStyle.None;
        ResizeMode            = ResizeMode.NoResize;  // overridden per-dialog if needed
        ShowInTaskbar         = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // CaptionHeight=0: our title bar acts as caption via DragMove().
        // ResizeBorderThickness is adjusted in WrapWithChrome() once
        // the subclass constructor has had a chance to set ResizeMode.
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight         = 28,
            ResizeBorderThickness = new Thickness(4),
            GlassFrameThickness   = new Thickness(0),
            UseAeroCaptionButtons = false,
        });

        StateChanged += OnWindowStateChanged;
    }

    // -- Content wrapping -------------------------------------------------

    protected override void OnInitialized(EventArgs e)
    {
        WrapWithChrome();
        base.OnInitialized(e);
    }

    private void WrapWithChrome()
    {
        // Capture dialog content before overwriting Content.
        var dialogContent = Content as UIElement;
        Content = null;

        bool isResizable = ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

        // Adjust WindowChrome resize area: invisible for fixed-size dialogs.
        if (WindowChrome.GetWindowChrome(this) is { } chrome)
            chrome.ResizeBorderThickness = isResizable ? new Thickness(4) : new Thickness(0);

        // -- Title bar -------------------------------------------------
        var titleText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 0, 0),
            FontSize          = 12,
        };
        titleText.SetBinding(TextBlock.TextProperty, new Binding(nameof(Title)) { Source = this });
        titleText.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");

        var closeBtn = BuildTitleButton("\uE106", "TitleBarCloseButtonStyle");
        closeBtn.Click += (_, _) => Close();

        var titleDock = new DockPanel();
        DockPanel.SetDock(closeBtn, Dock.Right);
        titleDock.Children.Add(closeBtn);

        if (isResizable)
        {
            _maxButton = BuildTitleButton("\uE739", "TitleBarButtonStyle");
            _maxButton.Click += OnMaximizeRestoreClick;
            DockPanel.SetDock(_maxButton, Dock.Right);
            titleDock.Children.Add(_maxButton);
        }

        // -- Optional app icon (left of title) -------------------------
        if (ShowIcon)
        {
            var iconImage = new Image
            {
                Width             = 16,
                Height            = 16,
                Margin            = new Thickness(6, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor            = Cursors.Arrow,
            };
            iconImage.SetBinding(Image.SourceProperty,
                new Binding(nameof(Icon)) { Source = this });

            WindowChrome.SetIsHitTestVisibleInChrome(iconImage, true);

            // Double left-click → close (Windows standard)
            iconImage.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount == 2) { Close(); e.Handled = true; }
            };

            // Right-click → Win32 system menu
            iconImage.MouseRightButtonUp += (_, e) =>
            {
                var pos = iconImage.PointToScreen(new Point(0, 28));
                SystemCommands.ShowSystemMenu(this, pos);
                e.Handled = true;
            };

            DockPanel.SetDock(iconImage, Dock.Left);
            titleDock.Children.Insert(0, iconImage);
        }

        titleDock.Children.Add(titleText);

        var titleBar = new Border { Height = 28 };
        titleBar.SetResourceReference(BackgroundProperty, "DockMenuBackgroundBrush");
        titleBar.Child = titleDock;

        // CaptionHeight = 28 → OS handles drag and double-click maximize natively.
        // No MouseLeftButtonDown handler needed on the title bar.

        // -- Wrapper grid ----------------------------------------------
        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Grid.SetRow(titleBar, 0);
        rootGrid.Children.Add(titleBar);

        if (dialogContent is not null)
        {
            Grid.SetRow(dialogContent, 1);
            rootGrid.Children.Add(dialogContent);
        }

        if (isResizable)
        {
            var grip = BuildResizeGrip();
            Grid.SetRow(grip, 1);
            rootGrid.Children.Add(grip);
        }

        // -- Outer 1 px themed border ----------------------------------
        _outerBorder = new Border { BorderThickness = new Thickness(1) };
        _outerBorder.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");
        _outerBorder.Child = rootGrid;

        Content = _outerBorder;
    }

    private static Button BuildTitleButton(string icon, string styleKey)
    {
        var btn = new Button
        {
            Content             = icon,
            Width               = 46,
            Height              = 28,
            FontFamily          = new FontFamily("Segoe MDL2 Assets"),
            FontSize            = 10,
            VerticalAlignment   = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        btn.SetResourceReference(StyleProperty, styleKey);
        WindowChrome.SetIsHitTestVisibleInChrome(btn, true);
        return btn;
    }

    /// <summary>Builds the 3-dot diagonal resize-grip visual (bottom-right).</summary>
    private static UIElement BuildResizeGrip()
    {
        // Three 2×2 squares arranged diagonally — mirrors the VS2022 resize grip.
        var canvas = new Canvas
        {
            Width               = 12,
            Height              = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Bottom,
            IsHitTestVisible    = false,
            Opacity             = 0.5,
            Margin              = new Thickness(0, 0, 2, 2),
        };
        Panel.SetZIndex(canvas, 20);

        // Dot coordinates inside the 12×12 canvas (x, y from top-left).
        (int x, int y)[] dots = [(2, 8), (5, 5), (8, 2)];
        foreach (var (x, y) in dots)
        {
            var dot = new Rectangle { Width = 2, Height = 2 };
            dot.SetResourceReference(Shape.FillProperty, "DockMenuForegroundBrush");
            Canvas.SetLeft(dot, x);
            Canvas.SetTop(dot,  y);
            canvas.Children.Add(dot);
        }

        return canvas;
    }

    // -- Maximize / restore ------------------------------------------------

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        // Update maximize/restore icon.
        if (_maxButton is not null)
            _maxButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE739";

        // Hide outer border when maximized (standard OS-window behavior).
        if (_outerBorder is not null)
            _outerBorder.BorderThickness = WindowState == WindowState.Maximized
                ? new Thickness(0)
                : new Thickness(1);
    }

    // -- Multi-monitor maximize fix ----------------------------------------

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        (PresentationSource.FromVisual(this) as HwndSource)?.AddHook(HwndHook);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Intercept WM_GETMINMAXINFO to constrain maximize to the current monitor's
        // work area (excludes taskbars). MonitorFromWindow identifies the correct
        // monitor for multi-screen setups — unlike SystemParameters.WorkArea which
        // always returns the primary monitor's work area.
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg != WM_GETMINMAXINFO) return IntPtr.Zero;

        var hMon = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
        var mi   = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMon, ref mi)) return IntPtr.Zero;

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        // ptMaxPosition is relative to the monitor's top-left corner, NOT the virtual screen.
        // Using rcWork directly (screen coords) causes the window to offset by the monitor
        // origin and fly off-screen on secondary monitors.
        mmi.ptMaxPosition.x = Math.Abs(mi.rcWork.left - mi.rcMonitor.left);
        mmi.ptMaxPosition.y = Math.Abs(mi.rcWork.top  - mi.rcMonitor.top);
        mmi.ptMaxSize.x     = mi.rcWork.right  - mi.rcWork.left;
        mmi.ptMaxSize.y     = mi.rcWork.bottom - mi.rcWork.top;

        // WPF's own WM_GETMINMAXINFO hook is bypassed when handled=true, so we must
        // apply MinWidth/MinHeight manually by converting logical units → physical pixels.
        if (PresentationSource.FromVisual(this) is { CompositionTarget: { } ct })
        {
            var scaleX = ct.TransformToDevice.M11;
            var scaleY = ct.TransformToDevice.M22;
            if (!double.IsNaN(MinWidth)  && MinWidth  > 0)
                mmi.ptMinTrackSize.x = (int)Math.Ceiling(MinWidth  * scaleX);
            if (!double.IsNaN(MinHeight) && MinHeight > 0)
                mmi.ptMinTrackSize.y = (int)Math.Ceiling(MinHeight * scaleY);
        }

        Marshal.StructureToPtr(mmi, lParam, true);

        handled = true;
        return IntPtr.Zero;
    }
}
