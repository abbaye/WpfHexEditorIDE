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

        foreach (var item in source)
        {
            if (item is not TabItem tabItem) continue;

            var isActive = ShowAllDocuments && tabControl.SelectedItem == tabItem;
            var menuItem = new MenuItem
            {
                Header = tabItem.Header is DockTabHeader dth
                    ? ExtractTitle(dth)
                    : tabItem.Header?.ToString() ?? "Tab",
                IsCheckable = ShowAllDocuments,
                IsChecked   = isActive
            };

            var capturedTab = tabItem;
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
