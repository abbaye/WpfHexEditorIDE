// ==========================================================
// Project: WpfHexEditor.Shell
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

namespace WpfHexEditor.Shell.Controls;

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
            // Use Hidden (not Collapsed) so the button always reserves its column width.
            // Collapsed → column width = 0 → panel gets full width → all tabs appear to
            // fit → HasOverflow stays false → chicken-and-egg: button never appears until
            // a manual resize forces a second measure with the real constraint.
            // Hidden → column always reserves the button width → panel gets the correct
            // constrained width from the first pass → overflow is detected on load.
            var binding = new Binding(nameof(TabOverflowPanel.HasOverflow))
            {
                Source = OverflowPanel,
                Converter = new BoolToHiddenVisibilityConverter()
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

            // --- Close (×) box — hidden until row hover ---
            // Use Border+TextBlock instead of Button: the default WPF Button ControlTemplate applies
            // its own IsMouseOver trigger directly on the inner Border (TargetName), overriding
            // TemplateBinding and breaking theme-keyed brushes inside popup HwndSources.
            var closeGlyph = new TextBlock
            {
                Text                = "\u00D7",
                FontSize            = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            closeGlyph.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");

            var closeBox = new Border
            {
                Width             = 18,
                Height            = 18,
                CornerRadius      = new CornerRadius(2),
                Background        = Brushes.Transparent,
                BorderThickness   = new Thickness(1),
                BorderBrush       = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor            = System.Windows.Input.Cursors.Hand,
                Visibility        = Visibility.Hidden,
                ToolTip           = "Close",
                Child             = closeGlyph
            };

            // Theme-keyed hover: border + accent background (popup-safe, no style triggers needed).
            closeBox.MouseEnter += (_, _) =>
            {
                closeBox.SetResourceReference(Border.BackgroundProperty,   "DockAccentBrush");
                closeBox.SetResourceReference(Border.BorderBrushProperty,  "DockBorderBrush");
            };
            closeBox.MouseLeave += (_, _) =>
            {
                closeBox.Background   = Brushes.Transparent;
                closeBox.BorderBrush  = Brushes.Transparent;
            };

            closeBox.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled   = true;   // prevent MenuItem activation
                menu.IsOpen = false;
                if (dockTabControl is not null && capturedTab.Tag is DockItem dockItem)
                    dockTabControl.RequestCloseTab(dockItem);
            };

            // --- Header panel ---
            // Use DockItem.Title directly — ExtractTitle() would pick up the LSP-dot TextBlock
            // (added before _titleBlock in DockTabHeader.Children) and return "●" instead of the filename.
            var titleText = tabItem.Tag is DockItem diTitle
                ? (diTitle.IsDirty ? diTitle.Title + " \u2022" : diTitle.Title)
                : (tabItem.Header is DockTabHeader dth ? ExtractTitle(dth) : tabItem.Header?.ToString() ?? "Tab");
            var title = new TextBlock
            {
                Text              = titleText,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming      = TextTrimming.CharacterEllipsis,
                Margin            = new Thickness(0, 0, 8, 0),
                ToolTip           = titleText
            };

            // --- Editor name badge (italic, dimmed, right-aligned) ---
            TextBlock? editorBadge = null;
            if (tabItem.Tag is DockItem di
                && di.Metadata.TryGetValue("EditorDisplayName", out var editorDisplayName)
                && !string.IsNullOrEmpty(editorDisplayName))
            {
                editorBadge = new TextBlock
                {
                    Text                = editorDisplayName,
                    FontStyle           = FontStyles.Italic,
                    Opacity             = 0.55,
                    VerticalAlignment   = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin              = new Thickness(4, 0, 8, 0),
                    TextTrimming        = TextTrimming.CharacterEllipsis
                };
                editorBadge.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
            }

            // Grid with 3 columns: title (Star), badge (fixed), close (fixed)
            // Ensures the editor-badge column is always right-aligned at the same X position
            // regardless of title length — fixes the misaligned badge column in the overflow menu.
            var headerPanel = new Grid { MinWidth = 200 };
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(editorBadge is not null ? 130 : 0) });
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });

            Grid.SetColumn(title,    0);
            Grid.SetColumn(closeBox, 2);
            headerPanel.Children.Add(title);
            headerPanel.Children.Add(closeBox);
            if (editorBadge is not null)
            {
                Grid.SetColumn(editorBadge, 1);
                headerPanel.Children.Add(editorBadge);
            }

            var menuItem = new MenuItem
            {
                Header      = headerPanel,
                IsCheckable = ShowAllDocuments,
                IsChecked   = isActive
            };

            menuItem.MouseEnter += (_, _) => closeBox.Visibility = Visibility.Visible;
            menuItem.MouseLeave += (_, _) => closeBox.Visibility = Visibility.Hidden;

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

/// <summary>
/// Converts a <see cref="bool"/> to <see cref="Visibility"/>:
/// <see langword="true"/> → <see cref="Visibility.Visible"/>,
/// <see langword="false"/> → <see cref="Visibility.Hidden"/> (not Collapsed).
/// Using Hidden keeps the element's layout slot so its parent column always
/// reserves the button width, preventing the chicken-and-egg overflow detection
/// problem that occurs when Collapsed collapses the column on first measure.
/// </summary>
internal sealed class BoolToHiddenVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Hidden;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is Visibility.Visible;
}
