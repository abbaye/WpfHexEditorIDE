//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : Options/ViewMenuOptionsPage.cs
// Description  : Options page for configuring the dynamic View menu organization
//                system — mode, display, favorites/pinning, sorting, and advanced settings.
// Architecture : Code-behind-only UserControl implementing IOptionsPage.
//                No constructor arguments — registered as factory via OptionsPageRegistry.
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Environment › View Menu.
/// Configures the dynamic View menu organization system.
/// </summary>
public sealed class ViewMenuOptionsPage : UserControl, IOptionsPage
{
    // ── IOptionsPage ────────────────────────────────────────────────────────

    public event EventHandler? Changed;

    // ── UI fields ───────────────────────────────────────────────────────────

    // Organization Mode
    private readonly RadioButton _modeFlat;
    private readonly RadioButton _modeCategorized;
    private readonly RadioButton _modeByDockSide;

    // Display
    private readonly CheckBox _showIcons;
    private readonly CheckBox _showGestureText;

    // Favorites
    private readonly CheckBox _pinFavoritesToTop;

    // Sorting
    private readonly ComboBox _sortOrder;

    // Advanced
    private readonly CheckBox _collapseEmpty;

    // ── Construction ────────────────────────────────────────────────────────

    public ViewMenuOptionsPage()
    {
        Padding = new Thickness(16);

        // Merge DialogStyles locally so implicit styles survive theme changes.
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/WpfHexEditor.App;component/Themes/DialogStyles.xaml")
        });

        var root = new StackPanel { Orientation = Orientation.Vertical };

        // ── Section 1: Organization Mode ─────────────────────────────────

        root.Children.Add(SectionHeader("Organization Mode"));

        _modeFlat        = Radio("_Flat (Classic)",  "All items in a single flat list with group separators.");
        _modeCategorized = Radio("_Categorized",     "Items grouped into submenus by functional category.");
        _modeByDockSide  = Radio("By _Dock Side",    "Items grouped by where panels dock (Left, Right, Bottom, etc.).");

        _modeFlat.GroupName = _modeCategorized.GroupName = _modeByDockSide.GroupName = "OrgMode";

        root.Children.Add(_modeFlat);
        root.Children.Add(_modeCategorized);
        root.Children.Add(_modeByDockSide);

        // ── Section 2: Display ───────────────────────────────────────────

        root.Children.Add(SectionHeader("Display"));

        _showIcons       = Check("Show _Icons",               "Display icon glyphs next to menu items.");
        _showGestureText = Check("Show _Keyboard Shortcuts",  "Display keyboard shortcut text on menu items.");

        root.Children.Add(_showIcons);
        root.Children.Add(_showGestureText);

        // ── Section 3: Favorites ─────────────────────────────────────────

        root.Children.Add(SectionHeader("Favorites"));

        _pinFavoritesToTop = Check("_Pin favorites to top of View menu",
            "Pinned items appear at the root level before category submenus. " +
            "Right-click any View menu item to pin or unpin it.");

        root.Children.Add(_pinFavoritesToTop);

        var hint = new TextBlock
        {
            Text              = "Tip: Right-click any item in the View menu to pin or unpin it.",
            FontStyle         = FontStyles.Italic,
            Opacity           = 0.7,
            Margin            = new Thickness(24, 2, 0, 8),
            TextWrapping      = TextWrapping.Wrap,
        };
        root.Children.Add(hint);

        // ── Section 4: Sorting ───────────────────────────────────────────

        root.Children.Add(SectionHeader("Sort Within Categories"));

        var sortPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 4, 0, 8) };
        sortPanel.Children.Add(new TextBlock
        {
            Text              = "Sort order:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0),
        });

        _sortOrder = new ComboBox { Width = 160, VerticalAlignment = VerticalAlignment.Center };
        _sortOrder.Items.Add(AppResources.App_ViewMenu_SortAlphabetical);
        _sortOrder.Items.Add(AppResources.App_ViewMenu_SortByFrequency);
        _sortOrder.Items.Add(AppResources.App_ViewMenu_SortCustom);
        _sortOrder.SelectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        sortPanel.Children.Add(_sortOrder);

        root.Children.Add(sortPanel);

        // ── Section 5: Advanced ──────────────────────────────────────────

        root.Children.Add(SectionHeader("Advanced"));

        _collapseEmpty = Check("_Collapse empty categories",
            "Hide category submenus that contain zero visible items.");

        root.Children.Add(_collapseEmpty);

        // Wire change events
        foreach (var rb in new[] { _modeFlat, _modeCategorized, _modeByDockSide })
            rb.Checked += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        foreach (var cb in new[] { _showIcons, _showGestureText, _pinFavoritesToTop, _collapseEmpty })
            cb.Checked   += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        foreach (var cb in new[] { _showIcons, _showGestureText, _pinFavoritesToTop, _collapseEmpty })
            cb.Unchecked += (_, _) => Changed?.Invoke(this, EventArgs.Empty);

        var scroll = new ScrollViewer
        {
            Content                    = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Content = scroll;
    }

    // ── IOptionsPage implementation ─────────────────────────────────────────

    public void Load(AppSettings settings)
    {
        var s = settings.ViewMenu;

        _modeFlat.IsChecked        = s.Mode == ViewMenuOrganizationMode.Flat;
        _modeCategorized.IsChecked = s.Mode == ViewMenuOrganizationMode.Categorized;
        _modeByDockSide.IsChecked  = s.Mode == ViewMenuOrganizationMode.ByDockSide;

        _showIcons.IsChecked       = s.ShowIcons;
        _showGestureText.IsChecked = s.ShowGestureText;
        _pinFavoritesToTop.IsChecked = s.PinFavoritesToTop;
        _collapseEmpty.IsChecked   = s.CollapseEmptyCategories;

        _sortOrder.SelectedIndex = s.SortOrder switch
        {
            ViewMenuSortOrder.Alphabetical => 0,
            ViewMenuSortOrder.ByFrequency  => 1,
            ViewMenuSortOrder.Custom       => 2,
            _                              => 0,
        };
    }

    public void Flush(AppSettings settings)
    {
        var s = settings.ViewMenu;

        s.Mode = _modeFlat.IsChecked == true        ? ViewMenuOrganizationMode.Flat
               : _modeByDockSide.IsChecked == true   ? ViewMenuOrganizationMode.ByDockSide
               :                                       ViewMenuOrganizationMode.Categorized;

        s.ShowIcons              = _showIcons.IsChecked == true;
        s.ShowGestureText        = _showGestureText.IsChecked == true;
        s.PinFavoritesToTop      = _pinFavoritesToTop.IsChecked == true;
        s.CollapseEmptyCategories = _collapseEmpty.IsChecked == true;

        s.SortOrder = _sortOrder.SelectedIndex switch
        {
            0 => ViewMenuSortOrder.Alphabetical,
            1 => ViewMenuSortOrder.ByFrequency,
            2 => ViewMenuSortOrder.Custom,
            _ => ViewMenuSortOrder.Alphabetical,
        };
    }

    // ── UI helpers ──────────────────────────────────────────────────────────

    private static TextBlock SectionHeader(string text) => new()
    {
        Text       = text,
        FontWeight = FontWeights.SemiBold,
        FontSize   = 14,
        Margin     = new Thickness(0, 12, 0, 6),
    };

    private static CheckBox Check(string header, string tooltip) => new()
    {
        Content = header,
        ToolTip = tooltip,
        Margin  = new Thickness(4, 4, 0, 4),
    };

    private static RadioButton Radio(string header, string tooltip) => new()
    {
        Content = header,
        ToolTip = tooltip,
        Margin  = new Thickness(4, 4, 0, 4),
    };
}
