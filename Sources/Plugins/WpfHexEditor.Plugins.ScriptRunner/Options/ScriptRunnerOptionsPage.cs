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
//     Slider rows use DockPanel + LastChildFill so the slider track
//     fills all available width without explicit Width calculations.
//     Foreground is bound via SetResourceReference so text inherits
//     the active theme's DockMenuForegroundBrush.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Plugins.ScriptRunner.Properties;

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
        _languageCombo.Items.Add(ScriptRunnerResources.ScriptRunner_LangCSharp);
        _languageCombo.Items.Add(ScriptRunnerResources.ScriptRunner_LangFSharp);
        _languageCombo.Items.Add(ScriptRunnerResources.ScriptRunner_LangVBNet);

        var resetButton = new Button
        {
            Content             = "Reset to Defaults",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding             = new Thickness(10, 4, 10, 4),
            Margin              = new Thickness(0, 12, 0, 0),
        };
        resetButton.SetResourceReference(BackgroundProperty,  "Panel_ToolbarBrush");
        resetButton.SetResourceReference(ForegroundProperty,  "DockMenuForegroundBrush");
        resetButton.SetResourceReference(BorderBrushProperty, "DockBorderBrush");
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
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
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
            MinWidth            = 120,
        };
        slider.ValueChanged += (_, e) => label.Text = ((int)e.NewValue).ToString();

        return (slider, label);
    }

    /// <summary>
    /// DockPanel row: [120px label | LastChildFill slider | 44px value].
    /// </summary>
    private static DockPanel MakeSliderRow(string labelText, Slider slider, TextBlock valueLabel)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        var label = new TextBlock
        {
            Text              = labelText,
            Width             = 120,
            VerticalAlignment = VerticalAlignment.Center,
        };

        DockPanel.SetDock(label,      Dock.Left);
        DockPanel.SetDock(valueLabel, Dock.Right);

        dock.Children.Add(label);
        dock.Children.Add(valueLabel);
        dock.Children.Add(slider);

        return dock;
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
