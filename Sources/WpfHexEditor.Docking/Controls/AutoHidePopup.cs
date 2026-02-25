// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Controls;

/// <summary>
/// Slide-in panel that appears when hovering over an auto-hide tab.
/// Animates from the edge using TranslateTransform.
/// </summary>
public class AutoHidePopup : ContentControl
{
    private DispatcherTimer? _hideTimer;
    private TranslateTransform _translateTransform = new();
    private bool _isAnimatingOut;

    static AutoHidePopup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoHidePopup),
            new FrameworkPropertyMetadata(typeof(AutoHidePopup)));
    }

    public AutoHidePopup()
    {
        RenderTransform = _translateTransform;
        UseLayoutRounding = true;
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _hideTimer.Tick += (s, e) => HidePopup();
    }

    public static readonly DependencyProperty CurrentAnchorableProperty =
        DependencyProperty.Register(nameof(CurrentAnchorable), typeof(LayoutAnchorable), typeof(AutoHidePopup),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SideProperty =
        DependencyProperty.Register(nameof(Side), typeof(DockSide), typeof(AutoHidePopup),
            new PropertyMetadata(DockSide.Left));

    public static readonly DependencyProperty PopupSizeProperty =
        DependencyProperty.Register(nameof(PopupSize), typeof(double), typeof(AutoHidePopup),
            new PropertyMetadata(300.0));

    public static readonly DependencyProperty AutoHideDelayProperty =
        DependencyProperty.Register(nameof(AutoHideDelay), typeof(int), typeof(AutoHidePopup),
            new PropertyMetadata(500, OnAutoHideDelayChanged));

    public LayoutAnchorable? CurrentAnchorable
    {
        get => (LayoutAnchorable?)GetValue(CurrentAnchorableProperty);
        set => SetValue(CurrentAnchorableProperty, value);
    }

    public DockSide Side
    {
        get => (DockSide)GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    public double PopupSize
    {
        get => (double)GetValue(PopupSizeProperty);
        set => SetValue(PopupSizeProperty, value);
    }

    public int AutoHideDelay
    {
        get => (int)GetValue(AutoHideDelayProperty);
        set => SetValue(AutoHideDelayProperty, value);
    }

    private static void OnAutoHideDelayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoHidePopup popup && popup._hideTimer != null)
            popup._hideTimer.Interval = TimeSpan.FromMilliseconds((int)e.NewValue);
    }

    /// <summary>Show the popup for a specific anchorable from a specific side.</summary>
    public void ShowForAnchorable(LayoutAnchorable anchorable, DockSide side)
    {
        _hideTimer?.Stop();
        _isAnimatingOut = false;

        CurrentAnchorable = anchorable;
        Side = side;
        Content = anchorable.Content;

        // Set size based on side
        PopupSize = side is DockSide.Left or DockSide.Right
            ? anchorable.AutoHideWidth
            : anchorable.AutoHideHeight;

        // Configure layout based on side
        HorizontalAlignment = side switch
        {
            DockSide.Left => HorizontalAlignment.Left,
            DockSide.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Stretch
        };
        VerticalAlignment = side switch
        {
            DockSide.Top => VerticalAlignment.Top,
            DockSide.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Stretch
        };

        if (side is DockSide.Left or DockSide.Right)
        {
            Width = PopupSize;
            Height = double.NaN;
        }
        else
        {
            Width = double.NaN;
            Height = PopupSize;
        }

        // Clear any running animations and pre-position off-screen
        // BEFORE making visible to prevent the one-frame flash at final position
        _translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _translateTransform.BeginAnimation(TranslateTransform.YProperty, null);

        if (side is DockSide.Left or DockSide.Right)
        {
            _translateTransform.X = side == DockSide.Left ? -PopupSize : PopupSize;
            _translateTransform.Y = 0;
        }
        else
        {
            _translateTransform.X = 0;
            _translateTransform.Y = side == DockSide.Top ? -PopupSize : PopupSize;
        }

        Visibility = Visibility.Visible;
        SlideIn();
    }

    /// <summary>Hide the popup.</summary>
    public void HidePopup()
    {
        _hideTimer?.Stop();

        if (_isAnimatingOut || Visibility != Visibility.Visible)
            return;

        _isAnimatingOut = true;
        SlideOut(() =>
        {
            Visibility = Visibility.Collapsed;
            Content = null;
            CurrentAnchorable = null;
            _isAnimatingOut = false;
        });
    }

    /// <summary>Start the delayed hide timer.</summary>
    public void StartHideTimer()
    {
        if (_isAnimatingOut) return;
        _hideTimer?.Start();
    }

    /// <summary>Cancel the hide timer (mouse re-entered).</summary>
    public void CancelHideTimer()
    {
        _hideTimer?.Stop();
    }

    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        CancelHideTimer();
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        StartHideTimer();
    }

    private void SlideIn()
    {
        var property = Side is DockSide.Left or DockSide.Right
            ? TranslateTransform.XProperty
            : TranslateTransform.YProperty;

        // Animate from current position (already set to off-screen) to 0
        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };

        _translateTransform.BeginAnimation(property, animation);
    }

    private void SlideOut(Action? onCompleted = null)
    {
        var to = Side switch
        {
            DockSide.Left => -PopupSize,
            DockSide.Right => PopupSize,
            DockSide.Top => -PopupSize,
            DockSide.Bottom => PopupSize,
            _ => 0
        };

        var property = Side is DockSide.Left or DockSide.Right
            ? TranslateTransform.XProperty
            : TranslateTransform.YProperty;

        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseIn }
        };

        if (onCompleted != null)
            animation.Completed += (s, e) => onCompleted();

        _translateTransform.BeginAnimation(property, animation);
    }
}
