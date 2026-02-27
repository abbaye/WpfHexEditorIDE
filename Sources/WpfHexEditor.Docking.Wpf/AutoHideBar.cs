//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// A bar on the edge of the dock area that shows buttons for auto-hidden panels.
/// </summary>
public class AutoHideBar : StackPanel
{
    public Dock Position { get; }

    /// <summary>
    /// Raised when a panel button is clicked to show/toggle its popup.
    /// </summary>
    public event Action<DockItem>? ItemClicked;

    public AutoHideBar(Dock position)
    {
        Position = position;
        Orientation = position is Dock.Top or Dock.Bottom
            ? Orientation.Horizontal
            : Orientation.Vertical;
        Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
    }

    /// <summary>
    /// Updates the bar with the given auto-hide items.
    /// </summary>
    public void UpdateItems(IEnumerable<DockItem> items)
    {
        Children.Clear();

        foreach (var item in items)
        {
            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = item.Title,
                    LayoutTransform = Position is Dock.Left or Dock.Right
                        ? new RotateTransform(Position == Dock.Left ? -90 : 90)
                        : Transform.Identity
                },
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(1),
                Background = Brushes.Transparent,
                Foreground = Brushes.LightGray,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = item
            };

            button.Click += (_, _) => ItemClicked?.Invoke(item);
            Children.Add(button);
        }

        Visibility = Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

/// <summary>
/// A popup that slides in from the edge to show an auto-hidden panel's content.
/// </summary>
public class AutoHidePopup : Popup
{
    private readonly ContentControl _contentHost;

    public DockItem? CurrentItem { get; private set; }

    public AutoHidePopup()
    {
        AllowsTransparency = true;
        PopupAnimation = PopupAnimation.Slide;
        StaysOpen = false;
        Width = 300;
        Height = 400;

        _contentHost = new ContentControl
        {
            Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
            Margin = new Thickness(1),
        };

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
            BorderThickness = new Thickness(1),
            Child = _contentHost
        };

        Child = border;
    }

    /// <summary>
    /// Shows the popup with content for the given item, placed according to the dock side.
    /// </summary>
    public void ShowForItem(DockItem item, UIElement placementTarget, Func<DockItem, object>? contentFactory = null, DockSide side = DockSide.Bottom)
    {
        CurrentItem = item;
        PlacementTarget = placementTarget;

        // Place the popup on the correct side relative to the bar button
        Placement = side switch
        {
            DockSide.Left => PlacementMode.Right,
            DockSide.Right => PlacementMode.Left,
            DockSide.Top => PlacementMode.Bottom,
            DockSide.Bottom => PlacementMode.Top,
            _ => PlacementMode.Right
        };

        _contentHost.Content = contentFactory?.Invoke(item) ?? new TextBlock
        {
            Text = $"Auto-hide: {item.Title}",
            Foreground = Brushes.White,
            Margin = new Thickness(8)
        };

        IsOpen = true;
    }
}
