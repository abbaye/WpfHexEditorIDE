// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Controls;

/// <summary>
/// Thin strip along one edge showing tabs for auto-hidden tool windows.
/// Vertical for Left/Right sides (rotated text), horizontal for Top/Bottom.
/// Handles mouse-over on individual tabs to trigger the auto-hide popup.
/// </summary>
public class AutoHideTabStrip : Control
{
    static AutoHideTabStrip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoHideTabStrip),
            new FrameworkPropertyMetadata(typeof(AutoHideTabStrip)));
    }

    public static readonly DependencyProperty SideProperty =
        DependencyProperty.Register(nameof(Side), typeof(DockSide), typeof(AutoHideTabStrip),
            new PropertyMetadata(DockSide.Left));

    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(nameof(Model), typeof(LayoutAnchorSide), typeof(AutoHideTabStrip),
            new PropertyMetadata(null));

    /// <summary>Which side of the dock manager this strip is on.</summary>
    public DockSide Side
    {
        get => (DockSide)GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>The anchor side model.</summary>
    public LayoutAnchorSide? Model
    {
        get => (LayoutAnchorSide?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // Find which anchorable tab is under the mouse
        var result = VisualTreeHelper.HitTest(this, e.GetPosition(this));
        if (result?.VisualHit is FrameworkElement fe)
        {
            var anchorable = FindAnchorableFromVisual(fe);
            if (anchorable != null)
            {
                // Find ancestor DockManager and show popup
                var manager = FindAncestor<DockManager>(this);
                manager?.ShowAutoHidePopup(anchorable, Side);
            }
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        // Start the hide timer on the popup (it will cancel if mouse enters popup)
        var manager = FindAncestor<DockManager>(this);
        if (manager != null)
        {
            // Use the auto-hide popup's built-in hide timer
            var popup = GetAutoHidePopup(manager);
            popup?.StartHideTimer();
        }
    }

    private static LayoutAnchorable? FindAnchorableFromVisual(FrameworkElement element)
    {
        // Walk up the visual tree looking for a DataContext that's a LayoutAnchorable
        DependencyObject? current = element;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is LayoutAnchorable anch)
                return anch;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static AutoHidePopup? GetAutoHidePopup(DockManager manager)
    {
        // Access via template
        return manager.Template?.FindName("PART_AutoHidePopup", manager) as AutoHidePopup;
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T target) return target;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }
}
