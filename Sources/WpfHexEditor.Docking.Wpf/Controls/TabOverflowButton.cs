// ==========================================================
// Project: WpfHexEditor.Docking.Wpf
// File: TabOverflowButton.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Dropdown button rendered at the right of the tab strip. In standard mode,
//     shows only overflowed (hidden) tabs. In ShowAllDocuments mode, always shows
//     all open documents with a check-mark on the active one — VS2026 document
//     switcher style.
//
// Architecture Notes:
//     Inherits Button. Observes TabOverflowPanel.HasOverflow and OverflowItems via
//     DependencyProperty bindings. Popup is built dynamically on click to avoid
//     maintaining a stale list.
//
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf.Controls;

/// <summary>
/// Dropdown button rendered at the right of the tab strip.
/// <para>
/// When <see cref="ShowAllDocuments"/> is <see langword="false"/> (default):
/// visible only when tabs overflow; shows only the hidden (overflowed) tabs.
/// </para>
/// <para>
/// When <see cref="ShowAllDocuments"/> is <see langword="true"/> (document tab bar):
/// always visible while the tab control contains items; shows <em>all</em> open
/// documents with a check-mark on the currently active one — VS2026 document switcher style.
/// </para>
/// </summary>
public class TabOverflowButton : Button
{
    // --- OverflowPanel DP ----------------------------------------------------

    public static readonly DependencyProperty OverflowPanelProperty =
        DependencyProperty.Register(nameof(OverflowPanel), typeof(TabOverflowPanel), typeof(TabOverflowButton),
            new PropertyMetadata(null, OnOverflowPanelChanged));

    public TabOverflowPanel? OverflowPanel
    {
        get => (TabOverflowPanel?)GetValue(OverflowPanelProperty);
        set => SetValue(OverflowPanelProperty, value);
    }

    // --- ShowAllDocuments DP -------------------------------------------------

    public static readonly DependencyProperty ShowAllDocumentsProperty =
        DependencyProperty.Register(
            nameof(ShowAllDocuments),
            typeof(bool),
            typeof(TabOverflowButton),
            new PropertyMetadata(false, OnShowAllDocumentsChanged));

    /// <summary>
    /// When <see langword="true"/>, the button acts as a VS2026-style "all documents" dropdown:
    /// always visible, lists every tab with a check on the active one.
    /// When <see langword="false"/> (default), the button is only visible on overflow and
    /// shows only the hidden tabs.
    /// </summary>
    public bool ShowAllDocuments
    {
        get => (bool)GetValue(ShowAllDocumentsProperty);
        set => SetValue(ShowAllDocumentsProperty, value);
    }

    // --- Constructor ---------------------------------------------------------

    public TabOverflowButton()
    {
        Content = "\u22EF"; // ⋯
        FontSize = 10;
        Padding = new Thickness(4, 2, 4, 2);
        Cursor = System.Windows.Input.Cursors.Hand;
        VerticalAlignment = VerticalAlignment.Center;
        ToolTip = "Show all documents";
        Visibility = Visibility.Collapsed;
        SetResourceReference(StyleProperty, "DockTitleButtonStyle");
    }

    // --- DP callbacks --------------------------------------------------------

    private static void OnOverflowPanelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TabOverflowButton button)
            button.UpdateVisibilityBinding();
    }

    private static void OnShowAllDocumentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TabOverflowButton button)
            button.UpdateVisibilityBinding();
    }

    private void UpdateVisibilityBinding()
    {
        if (ShowAllDocuments)
        {
            // Always visible (ShowOverflowMenu will guard against empty list).
            BindingOperations.ClearBinding(this, VisibilityProperty);
            Visibility = Visibility.Visible;
        }
        else if (OverflowPanel is not null)
        {
            var binding = new Binding(nameof(TabOverflowPanel.HasOverflow))
            {
                Source = OverflowPanel,
                Converter = new BooleanToVisibilityConverter()
            };
            SetBinding(VisibilityProperty, binding);
        }
    }

    // --- Click ---------------------------------------------------------------

    protected override void OnClick()
    {
        base.OnClick();
        ShowOverflowMenu();
    }

    private void ShowOverflowMenu()
    {
        if (OverflowPanel is null) return;

        var tabControl = ItemsControl.GetItemsOwner(OverflowPanel) as TabControl;
        if (tabControl is null) return;

        var menu = new ContextMenu();

        IEnumerable<object> source = ShowAllDocuments
            ? tabControl.Items.Cast<object>()
            : OverflowPanel.OverflowItems.Cast<object>();

        var dockTabControl = tabControl as DockTabControl;

        foreach (var item in source)
        {
            if (item is not TabItem tabItem) continue;

            var isActive = ShowAllDocuments && tabControl.SelectedItem == tabItem;
            var capturedTab = tabItem;

            // --- Close (×) button — hidden until row hover ---
            // Note: DockTitleButtonStyle hover triggers are blocked in ContextMenu popup HwndSource,
            // so we drive background/border explicitly via MouseEnter/MouseLeave.
            var closeButton = new Button
            {
                Content           = "\u00D7",
                FontSize          = 12,
                Width             = 18,
                Height            = 18,
                Padding           = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility        = Visibility.Hidden,
                Cursor            = System.Windows.Input.Cursors.Arrow,
                ToolTip           = "Close",
                BorderThickness   = new Thickness(1),
                Background        = Brushes.Transparent
            };
            closeButton.SetResourceReference(ForegroundProperty,   "DockMenuForegroundBrush");
            closeButton.SetResourceReference(BorderBrushProperty,  "DockBorderBrush");

            // Explicit hover: show filled square (accent bg + border) since style triggers don't fire in popups.
            closeButton.MouseEnter += (_, _) => closeButton.SetResourceReference(BackgroundProperty, "DockAccentBrush");
            closeButton.MouseLeave += (_, _) => closeButton.Background = Brushes.Transparent;

            closeButton.Click += (s, e) =>
            {
                e.Handled   = true;   // prevent MenuItem activation
                menu.IsOpen = false;
                if (dockTabControl is not null && capturedTab.Tag is DockItem dockItem)
                    dockTabControl.RequestCloseTab(dockItem);
            };

            // --- Header panel ---
            var title = new TextBlock
            {
                Text              = tabItem.Header is DockTabHeader dth ? ExtractTitle(dth) : tabItem.Header?.ToString() ?? "Tab",
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming      = TextTrimming.CharacterEllipsis,
                Margin            = new Thickness(0, 0, 6, 0)
            };

            DockPanel.SetDock(closeButton, Dock.Right);
            var headerPanel = new DockPanel { LastChildFill = true, MinWidth = 200 };
            headerPanel.Children.Add(closeButton);
            headerPanel.Children.Add(title);

            var menuItem = new MenuItem
            {
                Header      = headerPanel,
                IsCheckable = ShowAllDocuments,
                IsChecked   = isActive
            };

            menuItem.MouseEnter += (_, _) => closeButton.Visibility = Visibility.Visible;
            menuItem.MouseLeave += (_, _) => closeButton.Visibility = Visibility.Hidden;

            menuItem.Click += (_, _) =>
            {
                tabControl.SelectedItem = capturedTab;
                OverflowPanel.InvalidateMeasure();
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
