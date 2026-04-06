// ==========================================================
// Project: WpfHexEditor.App
// File: Options/LayoutOptionsPage.cs
// Description:
//     Options page for configuring Layout Customization defaults.
//     Provides 4 sections: Default Visibility, Default Positions,
//     Zen Mode Config, Presentation Mode Config.
//
// Architecture Notes:
//     Code-behind-only UserControl (no XAML) implementing IOptionsPage.
//     No constructor arguments — registered as a factory via OptionsPageRegistry.
//     Load/Flush read and write directly to AppSettings.Layout.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Environment › Layout.
/// Lets the user configure layout visibility, positions, and mode preferences.
/// </summary>
public sealed class LayoutOptionsPage : UserControl, IOptionsPage
{
    // ── IOptionsPage ────────────────────────────────────────────────────────

    public event EventHandler? Changed;

    // ── UI fields — Visibility ─────────────────────────────────────────────

    private readonly CheckBox _showMenuBar;
    private readonly CheckBox _showToolbar;
    private readonly CheckBox _showStatusBar;

    // ── UI fields — Positions ──────────────────────────────────────────────

    private readonly ComboBox _toolbarPosition;
    private readonly ComboBox _panelDockSide;
    private readonly ComboBox _tabBarPosition;

    // ── UI fields — Zen Mode ───────────────────────────────────────────────

    private readonly CheckBox _zenHideMenuBar;
    private readonly CheckBox _zenHideToolbar;
    private readonly CheckBox _zenHideStatusBar;
    private readonly CheckBox _zenHidePanels;

    // ── UI fields — Presentation ───────────────────────────────────────────

    private readonly Slider    _fontScaleSlider;
    private readonly TextBlock _fontScaleLabel;

    // ── Construction ────────────────────────────────────────────────────────

    public LayoutOptionsPage()
    {
        Padding = new Thickness(16);

        // Merge DialogStyles locally
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/WpfHexEditor.App;component/Themes/DialogStyles.xaml")
        });

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Content = scrollViewer;

        var root = new StackPanel { Margin = new Thickness(0) };
        scrollViewer.Content = root;

        // ═══════════════════════════════════════════════════════════════════
        //  Section 1: Default Visibility
        // ═══════════════════════════════════════════════════════════════════

        root.Children.Add(SectionHeader("DEFAULT VISIBILITY"));

        _showMenuBar  = Chk("Show Menu Bar on startup");   root.Children.Add(_showMenuBar);
        _showToolbar  = Chk("Show Toolbar on startup");     root.Children.Add(_showToolbar);
        _showStatusBar = Chk("Show Status Bar on startup"); root.Children.Add(_showStatusBar);

        // ═══════════════════════════════════════════════════════════════════
        //  Section 2: Default Positions
        // ═══════════════════════════════════════════════════════════════════

        root.Children.Add(SectionHeader("DEFAULT POSITIONS"));

        root.Children.Add(LabeledCombo("Toolbar Position:", out _toolbarPosition, "Top", "Bottom"));
        root.Children.Add(LabeledCombo("Panel Default Side:", out _panelDockSide, "Left", "Right", "Bottom"));
        root.Children.Add(LabeledCombo("Tab Bar Position:", out _tabBarPosition, "Top", "Bottom"));

        // ═══════════════════════════════════════════════════════════════════
        //  Section 3: Zen Mode
        // ═══════════════════════════════════════════════════════════════════

        root.Children.Add(SectionHeader("ZEN MODE"));

        _zenHideMenuBar   = Chk("Hide Menu Bar in Zen Mode");    root.Children.Add(_zenHideMenuBar);
        _zenHideToolbar   = Chk("Hide Toolbar in Zen Mode");     root.Children.Add(_zenHideToolbar);
        _zenHideStatusBar = Chk("Hide Status Bar in Zen Mode");  root.Children.Add(_zenHideStatusBar);
        _zenHidePanels    = Chk("Hide All Panels in Zen Mode");  root.Children.Add(_zenHidePanels);

        // ═══════════════════════════════════════════════════════════════════
        //  Section 4: Presentation Mode
        // ═══════════════════════════════════════════════════════════════════

        root.Children.Add(SectionHeader("PRESENTATION MODE"));

        var fontRow = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
        _fontScaleLabel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Width = 40
        };
        DockPanel.SetDock(_fontScaleLabel, Dock.Right);
        fontRow.Children.Add(_fontScaleLabel);

        var fontLabel = new TextBlock
        {
            Text = "Font Scale:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Width = 120
        };
        DockPanel.SetDock(fontLabel, Dock.Left);
        fontRow.Children.Add(fontLabel);

        _fontScaleSlider = new Slider
        {
            Minimum = 1.0,
            Maximum = 3.0,
            TickFrequency = 0.25,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center
        };
        _fontScaleSlider.ValueChanged += (_, _) =>
        {
            _fontScaleLabel.Text = $"{_fontScaleSlider.Value:F1}x";
            Changed?.Invoke(this, EventArgs.Empty);
        };
        fontRow.Children.Add(_fontScaleSlider);

        root.Children.Add(fontRow);

        // ═══════════════════════════════════════════════════════════════════
        //  Reset button
        // ═══════════════════════════════════════════════════════════════════

        var resetBtn = new Button
        {
            Content = "Reset to Defaults",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 16, 0, 0),
            Padding = new Thickness(12, 4, 12, 4)
        };
        resetBtn.Click += (_, _) =>
        {
            var defaults = new LayoutSettings();
            _showMenuBar.IsChecked   = defaults.ShowMenuBar;
            _showToolbar.IsChecked   = defaults.ShowToolbar;
            _showStatusBar.IsChecked = defaults.ShowStatusBar;
            _toolbarPosition.SelectedItem  = defaults.ToolbarPosition;
            _panelDockSide.SelectedItem    = defaults.DefaultPanelDockSide;
            _tabBarPosition.SelectedItem   = defaults.TabBarPosition;
            _zenHideMenuBar.IsChecked   = defaults.ZenHideMenuBar;
            _zenHideToolbar.IsChecked   = defaults.ZenHideToolbar;
            _zenHideStatusBar.IsChecked = defaults.ZenHideStatusBar;
            _zenHidePanels.IsChecked    = defaults.ZenHidePanels;
            _fontScaleSlider.Value = defaults.PresentationFontScale;
            Changed?.Invoke(this, EventArgs.Empty);
        };
        root.Children.Add(resetBtn);
    }

    // ── IOptionsPage ────────────────────────────────────────────────────────

    public void Load(AppSettings settings)
    {
        var s = settings.Layout;
        _showMenuBar.IsChecked   = s.ShowMenuBar;
        _showToolbar.IsChecked   = s.ShowToolbar;
        _showStatusBar.IsChecked = s.ShowStatusBar;

        _toolbarPosition.SelectedItem = s.ToolbarPosition;
        _panelDockSide.SelectedItem   = s.DefaultPanelDockSide;
        _tabBarPosition.SelectedItem  = s.TabBarPosition;

        _zenHideMenuBar.IsChecked   = s.ZenHideMenuBar;
        _zenHideToolbar.IsChecked   = s.ZenHideToolbar;
        _zenHideStatusBar.IsChecked = s.ZenHideStatusBar;
        _zenHidePanels.IsChecked    = s.ZenHidePanels;

        _fontScaleSlider.Value = s.PresentationFontScale;
        _fontScaleLabel.Text   = $"{s.PresentationFontScale:F1}x";
    }

    public void Flush(AppSettings settings)
    {
        var s = settings.Layout;
        s.ShowMenuBar   = _showMenuBar.IsChecked == true;
        s.ShowToolbar   = _showToolbar.IsChecked == true;
        s.ShowStatusBar = _showStatusBar.IsChecked == true;

        s.ToolbarPosition      = _toolbarPosition.SelectedItem as string ?? "Top";
        s.DefaultPanelDockSide = _panelDockSide.SelectedItem as string ?? "Right";
        s.TabBarPosition       = _tabBarPosition.SelectedItem as string ?? "Top";

        s.ZenHideMenuBar   = _zenHideMenuBar.IsChecked == true;
        s.ZenHideToolbar   = _zenHideToolbar.IsChecked == true;
        s.ZenHideStatusBar = _zenHideStatusBar.IsChecked == true;
        s.ZenHidePanels    = _zenHidePanels.IsChecked == true;

        s.PresentationFontScale = _fontScaleSlider.Value;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static TextBlock SectionHeader(string text) => OptionsPageHelper.SectionHeader(text);

    private CheckBox Chk(string label)
    {
        var cb = new CheckBox
        {
            Content = label,
            Margin = new Thickness(0, 4, 0, 4)
        };
        cb.Checked   += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        cb.Unchecked += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        return cb;
    }

    private DockPanel LabeledCombo(string label, out ComboBox combo, params string[] options)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        var tb = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 140,
            Margin = new Thickness(0, 0, 8, 0)
        };
        DockPanel.SetDock(tb, Dock.Left);
        dock.Children.Add(tb);

        combo = new ComboBox
        {
            Width = 140,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var opt in options)
            combo.Items.Add(opt);
        combo.SelectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);

        dock.Children.Add(combo);
        return dock;
    }
}
