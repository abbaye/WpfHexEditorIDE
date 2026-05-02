// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: Pages/WhfmtExplorerOptionsPage.cs
// Description:
//     Options page for the Whfmt Format Explorer/Manager.
//     Code-only UserControl implementing IOptionsPage.
//     Registered under "Format Editor (.whfmt)" / "Format Explorer".
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Options.Properties;

namespace WpfHexEditor.Core.Options.Pages;

/// <summary>
/// IDE options page — Format Editor (.whfmt) › Format Explorer.
/// </summary>
public sealed class WhfmtExplorerOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    // ── Controls ────────────────────────────────────────────────────────────

    private readonly CheckBox  _showBuiltIns;
    private readonly CheckBox  _showUserFmts;
    private readonly CheckBox  _showQualityScores;
    private readonly CheckBox  _enableHotReload;
    private readonly CheckBox  _showLoadFailures;
    private readonly ComboBox  _defaultViewMode;
    private readonly Slider    _qualityThreshold;
    private readonly TextBlock _qualityThresholdLabel;

    private bool _loading;

    // ── Construction ────────────────────────────────────────────────────────

    public WhfmtExplorerOptionsPage()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(20),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        // Header
        stack.Children.Add(new TextBlock
        {
            Text       = "Format Explorer — Settings",
            FontSize   = 16,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 4),
        });
        stack.Children.Add(new TextBlock
        {
            Text         = "Configure the Format Browser tool window and Format Catalog document tab.",
            Opacity      = 0.7,
            FontSize     = 12,
            Margin       = new Thickness(0, 0, 0, 16),
            TextWrapping = TextWrapping.Wrap,
        });

        // -- Catalog Visibility ------------------------------------------
        AddSectionHeader(stack, "Catalog Visibility");

        _showBuiltIns = AddCheckBox(stack,
            "Show built-in formats",
            "Display the 600+ embedded format definitions in the Format Browser.");

        _showUserFmts = AddCheckBox(stack,
            "Show user formats",
            "Display format definitions loaded from the user AppData directory.");

        _showLoadFailures = AddCheckBox(stack,
            "Show formats that failed to load",
            "Display formats with an error badge instead of hiding them silently.");

        // -- Display ---------------------------------------------------------
        AddSectionHeader(stack, "Display");

        _showQualityScores = AddCheckBox(stack,
            "Show quality score badges",
            "Display a 0-100 quality indicator beside each format in the browser.");

        stack.Children.Add(new TextBlock
        {
            Text       = "Default view mode",
            FontWeight = FontWeights.Medium,
            Margin     = new Thickness(0, 10, 0, 4),
        });
        _defaultViewMode = new ComboBox
        {
            Width               = 160,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin              = new Thickness(0, 0, 0, 4),
        };
        _defaultViewMode.Items.Add(OptionsResources.Opt_ViewMode_Tree);
        _defaultViewMode.Items.Add(OptionsResources.Opt_ViewMode_Flat);
        stack.Children.Add(_defaultViewMode);
        stack.Children.Add(new TextBlock
        {
            Text         = "Controls whether the Format Browser opens in grouped tree or flat list mode.",
            Opacity      = 0.6,
            FontSize     = 11,
            Margin       = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap,
        });

        stack.Children.Add(new TextBlock
        {
            Text       = "Hide formats with quality score below",
            FontWeight = FontWeights.Medium,
            Margin     = new Thickness(0, 4, 0, 4),
        });
        var sliderRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _qualityThreshold = new Slider
        {
            Minimum       = 0,
            Maximum       = 100,
            TickFrequency = 10,
            SmallChange   = 5,
            LargeChange   = 10,
        };
        Grid.SetColumn(_qualityThreshold, 0);

        _qualityThresholdLabel = new TextBlock
        {
            Width             = 36,
            Margin            = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Text              = "0",
        };
        Grid.SetColumn(_qualityThresholdLabel, 1);

        sliderRow.Children.Add(_qualityThreshold);
        sliderRow.Children.Add(_qualityThresholdLabel);
        stack.Children.Add(sliderRow);
        stack.Children.Add(new TextBlock
        {
            Text         = "Set to 0 to show all formats regardless of quality score.",
            Opacity      = 0.6,
            FontSize     = 11,
            Margin       = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap,
        });

        // -- Hot Reload ------------------------------------------------------
        AddSectionHeader(stack, "Hot Reload");

        _enableHotReload = AddCheckBox(stack,
            "Watch user format folder for changes",
            "Automatically detect new or modified .whfmt files in the user AppData directory " +
            "(within ~500 ms) without requiring a manual refresh.");

        scroll.Content = stack;
        Content        = scroll;

        // -- Wire events -------------------------------------------------------
        _showBuiltIns.Checked      += OnChanged; _showBuiltIns.Unchecked      += OnChanged;
        _showUserFmts.Checked      += OnChanged; _showUserFmts.Unchecked      += OnChanged;
        _showLoadFailures.Checked  += OnChanged; _showLoadFailures.Unchecked  += OnChanged;
        _showQualityScores.Checked += OnChanged; _showQualityScores.Unchecked += OnChanged;
        _enableHotReload.Checked   += OnChanged; _enableHotReload.Unchecked   += OnChanged;
        _defaultViewMode.SelectionChanged += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };
        _qualityThreshold.ValueChanged    += (_, e) =>
        {
            _qualityThresholdLabel.Text = $"{(int)e.NewValue}";
            if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    // ── IOptionsPage ────────────────────────────────────────────────────────

    public void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            var s = settings.WhfmtExplorer;
            _showBuiltIns.IsChecked      = s.ShowBuiltInFormats;
            _showUserFmts.IsChecked      = s.ShowUserFormats;
            _showLoadFailures.IsChecked  = s.ShowLoadFailures;
            _showQualityScores.IsChecked = s.ShowQualityScores;
            _enableHotReload.IsChecked   = s.EnableHotReload;
            _defaultViewMode.SelectedItem = s.DefaultViewMode == "Flat" ? "Flat" : "Tree";
            if (_defaultViewMode.SelectedIndex < 0) _defaultViewMode.SelectedIndex = 0;
            _qualityThreshold.Value       = Math.Clamp(s.QualityScoreThreshold, 0, 100);
            _qualityThresholdLabel.Text   = $"{s.QualityScoreThreshold}";
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings settings)
    {
        var s = settings.WhfmtExplorer;
        s.ShowBuiltInFormats    = _showBuiltIns.IsChecked    == true;
        s.ShowUserFormats       = _showUserFmts.IsChecked    == true;
        s.ShowLoadFailures      = _showLoadFailures.IsChecked == true;
        s.ShowQualityScores     = _showQualityScores.IsChecked == true;
        s.EnableHotReload       = _enableHotReload.IsChecked  == true;
        s.DefaultViewMode       = _defaultViewMode.SelectedItem?.ToString() ?? "Tree";
        s.QualityScoreThreshold = (int)_qualityThreshold.Value;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void AddSectionHeader(StackPanel panel, string title)
    {
        panel.Children.Add(new TextBlock
        {
            Text       = title,
            FontWeight = FontWeights.SemiBold,
            FontSize   = 13,
            Margin     = new Thickness(0, 16, 0, 6),
        });
        panel.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 8) });
    }

    private static CheckBox AddCheckBox(StackPanel panel, string label, string tooltip)
    {
        var cb = new CheckBox
        {
            Content = label,
            ToolTip = tooltip,
            Margin  = new Thickness(0, 4, 0, 4),
        };
        panel.Children.Add(cb);
        return cb;
    }
}
