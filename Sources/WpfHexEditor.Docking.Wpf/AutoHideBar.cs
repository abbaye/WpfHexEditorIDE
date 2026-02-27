//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
/// An overlay panel that slides in from the edge to show an auto-hidden panel's content.
/// Replaces Popup-based approach to avoid transparency issues and provide VS-like sizing.
/// </summary>
public class AutoHideFlyout : Grid
{
    private readonly Border _panel;
    private readonly ContentControl _contentHost;
    private readonly Border _clickCatcher;
    private readonly TextBlock _titleBlock;

    private const double SideWidth = 300;
    private const double TopBottomHeight = 250;

    public DockItem? CurrentItem { get; private set; }
    public bool IsOpen => _panel.Visibility == Visibility.Visible;

    /// <summary>Raised when the user clicks the pin button to re-dock the panel.</summary>
    public event Action<DockItem>? RestoreRequested;

    /// <summary>Raised when the user clicks close.</summary>
    public event Action<DockItem>? CloseRequested;

    /// <summary>Raised when the user chooses "Float" from the context menu.</summary>
    public event Action<DockItem>? FloatRequested;

    public AutoHideFlyout()
    {
        // Full overlay - covers the entire DockControl area
        Visibility = Visibility.Collapsed;

        // Click-catcher: transparent background catches clicks to close the flyout
        _clickCatcher = new Border { Background = Brushes.Transparent };
        _clickCatcher.MouseLeftButtonDown += (_, _) => Close();
        Children.Add(_clickCatcher);

        // Title bar
        _titleBlock = new TextBlock
        {
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Margin = new Thickness(8, 6, 8, 6),
            VerticalAlignment = VerticalAlignment.Center
        };

        // --- Title bar buttons (right side): chevron, pin, close ---
        var menuBrush = new SolidColorBrush(Color.FromRgb(45, 45, 48));
        var menuFgBrush = new SolidColorBrush(Color.FromRgb(241, 241, 241));
        var menuHighlight = new SolidColorBrush(Color.FromRgb(62, 62, 64));
        var menuBorderBrush = new SolidColorBrush(Color.FromRgb(51, 51, 55));

        Button MakeTitleButton(string content, string tooltip) => new()
        {
            Content = content,
            FontSize = 12,
            Padding = new Thickness(4, 2, 4, 2),
            Background = Brushes.Transparent,
            Foreground = Brushes.LightGray,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = tooltip
        };

        // Close button
        var closeButton = MakeTitleButton("\u2715", "Close");
        closeButton.Click += (_, _) =>
        {
            if (CurrentItem is not null) CloseRequested?.Invoke(CurrentItem);
        };

        // Pin button
        var pinButton = MakeTitleButton("\uD83D\uDCCC", "Dock");
        pinButton.Click += (_, _) =>
        {
            if (CurrentItem is not null) RestoreRequested?.Invoke(CurrentItem);
        };

        // Chevron dropdown button with VS-style context menu
        var chevronButton = MakeTitleButton("\u25BC", "Options");
        chevronButton.FontSize = 9;
        chevronButton.Click += (sender, _) =>
        {
            if (CurrentItem is null || sender is not Button btn) return;
            var item = CurrentItem;

            var menu = new ContextMenu
            {
                Background = menuBrush,
                BorderBrush = menuBorderBrush,
                Foreground = menuFgBrush
            };

            var dockItem = new MenuItem { Header = "Dock", Foreground = menuFgBrush };
            dockItem.Click += (_, _) => RestoreRequested?.Invoke(item);
            menu.Items.Add(dockItem);

            var autoHideItem = new MenuItem
            {
                Header = "Auto Hide",
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                IsEnabled = false
            };
            menu.Items.Add(autoHideItem);

            var floatItem = new MenuItem { Header = "Float", Foreground = menuFgBrush };
            floatItem.Click += (_, _) => FloatRequested?.Invoke(item);
            menu.Items.Add(floatItem);

            menu.Items.Add(new Separator());

            var closeItem = new MenuItem { Header = "Close", Foreground = menuFgBrush };
            closeItem.Click += (_, _) => CloseRequested?.Invoke(item);
            menu.Items.Add(closeItem);

            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        };

        var titleContent = new DockPanel();
        DockPanel.SetDock(closeButton, Dock.Right);
        DockPanel.SetDock(pinButton, Dock.Right);
        DockPanel.SetDock(chevronButton, Dock.Right);
        titleContent.Children.Add(closeButton);
        titleContent.Children.Add(pinButton);
        titleContent.Children.Add(chevronButton);
        titleContent.Children.Add(_titleBlock);

        var titleBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
            Child = titleContent
        };

        // Content area
        _contentHost = new ContentControl();

        var innerStack = new DockPanel();
        DockPanel.SetDock(titleBar, Dock.Top);
        innerStack.Children.Add(titleBar);
        innerStack.Children.Add(_contentHost);

        // The slide-in panel with border and opaque background
        _panel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
            BorderThickness = new Thickness(1),
            Child = innerStack,
            Visibility = Visibility.Visible
        };

        Children.Add(_panel);
    }

    /// <summary>
    /// Shows the flyout with content for the given item on the specified side.
    /// </summary>
    public void ShowForItem(DockItem item, Func<DockItem, object>? contentFactory = null, DockSide side = DockSide.Bottom)
    {
        CurrentItem = item;
        _titleBlock.Text = item.Title;

        // Position the panel on the correct side, stretching to fill available space
        _panel.HorizontalAlignment = side switch
        {
            DockSide.Left => HorizontalAlignment.Left,
            DockSide.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Stretch
        };

        _panel.VerticalAlignment = side switch
        {
            DockSide.Top => VerticalAlignment.Top,
            DockSide.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Stretch
        };

        // Set size: fixed width for sides, fixed height for top/bottom
        if (side is DockSide.Left or DockSide.Right)
        {
            _panel.Width = SideWidth;
            _panel.Height = double.NaN; // stretch vertically
        }
        else
        {
            _panel.Height = TopBottomHeight;
            _panel.Width = double.NaN; // stretch horizontally
        }

        _contentHost.Content = contentFactory?.Invoke(item) ?? new TextBlock
        {
            Text = $"Auto-hide: {item.Title}",
            Foreground = Brushes.White,
            Margin = new Thickness(8)
        };

        Visibility = Visibility.Visible;
    }

    public void Close()
    {
        Visibility = Visibility.Collapsed;
        _contentHost.Content = null;
        CurrentItem = null;
    }
}
