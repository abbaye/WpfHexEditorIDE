// ==========================================================
// Project: WpfHexEditor.Plugins.ScriptRunner
// File: Options/ScriptRunnerOptionsPage.cs
// Description:
//     Options page for the Script Runner plugin.
//     Displayed under Plugins › Script Runner in the IDE Options panel.
//     Sections: History, Behavior, Reset.
//
// Architecture Notes:
//     Code-behind-only UserControl (no XAML).
//     Slider rows use a 3-column Grid (fixed label | star slider | fixed value)
//     so that the slider track stretches to fill available width.
//     Foreground is bound via SetResourceReference so text inherits
//     the active theme's DockMenuForegroundBrush.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Plugins.ScriptRunner.Options;

/// <summary>
/// IDE options page — Plugins › Script Runner.
/// </summary>
public sealed class ScriptRunnerOptionsPage : UserControl
{
    // ── UI fields ────────────────────────────────────────────────────────────

    private readonly Slider    _maxHistorySlider;
    private readonly TextBlock _maxHistoryLabel;
    private readonly CheckBox  _autoClearCheck;
    private readonly ComboBox  _languageCombo;

    // ── Constructor ──────────────────────────────────────────────────────────

    public ScriptRunnerOptionsPage()
    {
        (_maxHistorySlider, _maxHistoryLabel) = MakeSlider(5, 100, 5);

        _autoClearCheck = new CheckBox
        {
            Content = "Clear output automatically before each run",
            Margin  = new Thickness(0, 4, 0, 4),
        };

        _languageCombo = new ComboBox { Margin = new Thickness(0, 4, 0, 4) };
        _languageCombo.Items.Add("CSharp");
        _languageCombo.Items.Add("FSharp");
        _languageCombo.Items.Add("VBNet");

        var resetButton = new Button
        {
            Content             = "Reset to Defaults",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding             = new Thickness(10, 4, 10, 4),
            Margin              = new Thickness(0, 12, 0, 0),
        };
        resetButton.Click += OnReset;

        var root = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin      = new Thickness(12, 8, 12, 8),
        };
        root.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");

        root.Children.Add(MakeSectionHeader("History"));
        root.Children.Add(MakeSliderRow("Max entries:", _maxHistorySlider, _maxHistoryLabel));

        root.Children.Add(MakeSectionHeader("Behavior"));
        root.Children.Add(_autoClearCheck);
        root.Children.Add(MakeLabeledRow("Default language:", _languageCombo));

        root.Children.Add(resetButton);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = root,
        };
    }

    // ── Load / Save / Reset ───────────────────────────────────────────────────

    public void Load()
    {
        var opts = ScriptRunnerOptions.Instance;
        _maxHistorySlider.Value     = Math.Clamp(opts.MaxHistoryEntries, 5, 100);
        _autoClearCheck.IsChecked   = opts.AutoClearOnNewSession;
        _languageCombo.SelectedItem = opts.DefaultLanguage;
        if (_languageCombo.SelectedItem is null) _languageCombo.SelectedIndex = 0;
    }

    public void Save()
    {
        var opts = ScriptRunnerOptions.Instance;
        opts.MaxHistoryEntries     = (int)_maxHistorySlider.Value;
        opts.AutoClearOnNewSession = _autoClearCheck.IsChecked == true;
        opts.DefaultLanguage       = _languageCombo.SelectedItem?.ToString() ?? "CSharp";
        opts.Save();
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        var defaults = new ScriptRunnerOptions();
        _maxHistorySlider.Value     = defaults.MaxHistoryEntries;
        _autoClearCheck.IsChecked   = defaults.AutoClearOnNewSession;
        _languageCombo.SelectedItem = defaults.DefaultLanguage;
        if (_languageCombo.SelectedItem is null) _languageCombo.SelectedIndex = 0;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TextBlock MakeSectionHeader(string title) => new()
    {
        Text       = title,
        FontSize   = 13,
        FontWeight = FontWeights.SemiBold,
        Margin     = new Thickness(0, 8, 0, 4),
    };

    private static (Slider slider, TextBlock label) MakeSlider(double min, double max, double tickFreq)
    {
        var label = new TextBlock
        {
            Width             = 44,
            TextAlignment     = TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 0, 0),
        };

        var slider = new Slider
        {
            Minimum             = min,
            Maximum             = max,
            TickFrequency       = tickFreq,
            IsSnapToTickEnabled = true,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        slider.ValueChanged += (_, e) => label.Text = ((int)e.NewValue).ToString();

        return (slider, label);
    }

    /// <summary>
    /// 3-column Grid: [120px label] [* slider] [44px value].
    /// Star column stretches slider track to fill available width.
    /// </summary>
    private static Grid MakeSliderRow(string labelText, Slider slider, TextBlock valueLabel)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });

        var label = new TextBlock { Text = labelText, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(label,      0);
        Grid.SetColumn(slider,     1);
        Grid.SetColumn(valueLabel, 2);
        grid.Children.Add(label);
        grid.Children.Add(slider);
        grid.Children.Add(valueLabel);
        return grid;
    }

    private static Grid MakeLabeledRow(string labelText, Control control)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock { Text = labelText, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(label,   0);
        Grid.SetColumn(control, 1);
        grid.Children.Add(label);
        grid.Children.Add(control);
        return grid;
    }
}
