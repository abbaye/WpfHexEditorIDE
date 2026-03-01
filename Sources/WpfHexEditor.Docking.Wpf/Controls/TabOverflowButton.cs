//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfHexEditor.Docking.Wpf.Controls;

/// <summary>
/// Dropdown button that lists overflowed tab items from a companion <see cref="TabOverflowPanel"/>.
/// When clicked, shows a context menu listing the hidden tabs; selecting one activates it.
/// </summary>
public class TabOverflowButton : Button
{
    public static readonly DependencyProperty OverflowPanelProperty =
        DependencyProperty.Register(nameof(OverflowPanel), typeof(TabOverflowPanel), typeof(TabOverflowButton),
            new PropertyMetadata(null, OnOverflowPanelChanged));

    public TabOverflowPanel? OverflowPanel
    {
        get => (TabOverflowPanel?)GetValue(OverflowPanelProperty);
        set => SetValue(OverflowPanelProperty, value);
    }

    public TabOverflowButton()
    {
        Content = "\u25BC"; // ▼
        FontSize = 8;
        Padding = new Thickness(4, 2, 4, 2);
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;
        Cursor = System.Windows.Input.Cursors.Hand;
        VerticalAlignment = VerticalAlignment.Center;
        ToolTip = "Show hidden tabs";
        Visibility = Visibility.Collapsed;
    }

    private static void OnOverflowPanelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TabOverflowButton button && e.NewValue is TabOverflowPanel panel)
        {
            var binding = new Binding(nameof(TabOverflowPanel.HasOverflow))
            {
                Source = panel,
                Converter = new BooleanToVisibilityConverter()
            };
            button.SetBinding(VisibilityProperty, binding);
        }
    }

    protected override void OnClick()
    {
        base.OnClick();
        ShowOverflowMenu();
    }

    private void ShowOverflowMenu()
    {
        if (OverflowPanel is null) return;

        var menu = new ContextMenu();
        foreach (var item in OverflowPanel.OverflowItems)
        {
            if (item is not TabItem tabItem) continue;

            var menuItem = new MenuItem
            {
                Header = tabItem.Header is DockTabHeader dth
                    ? ExtractTitle(dth)
                    : tabItem.Header?.ToString() ?? "Tab"
            };

            var capturedTab = tabItem;
            menuItem.Click += (_, _) =>
            {
                if (ItemsControl.GetItemsOwner(OverflowPanel) is TabControl tabControl)
                {
                    tabControl.SelectedItem = capturedTab;
                    OverflowPanel.InvalidateMeasure();
                }
            };

            menu.Items.Add(menuItem);
        }

        if (menu.Items.Count > 0)
        {
            menu.PlacementTarget = this;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private static string ExtractTitle(DockTabHeader header)
    {
        foreach (var child in header.Children)
        {
            if (child is TextBlock tb)
                return tb.Text;
        }
        return "Tab";
    }
}
