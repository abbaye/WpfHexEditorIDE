// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Options/DiagnosticToolsOptionsPage.cs
// Description:
//     Options page for the Diagnostic Tools plugin.
//     Displayed under Plugins › Diagnostic Tools in the IDE Options panel.
//     Sections: Sampling, Buffers, Reset.
//
// Architecture Notes:
//     Code-behind-only UserControl (no XAML).
//     Slider rows use a 3-column Grid (fixed label | star slider | fixed value)
//     so that the slider track stretches to fill available width.
//     Foreground is bound via SetResourceReference so all text inherits
//     the active theme's DockMenuForegroundBrush.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Plugins.DiagnosticTools.Options;

/// <summary>
/// IDE options page — Plugins › Diagnostic Tools.
/// </summary>
public sealed class DiagnosticToolsOptionsPage : UserControl
{
    // ── UI fields ────────────────────────────────────────────────────────────

    private readonly Slider    _pollIntervalSlider;
    private readonly TextBlock _pollIntervalLabel;
    private readonly Slider    _ringCapacitySlider;
    private readonly TextBlock _ringCapacityLabel;
    private readonly Slider    _eventMaxSlider;
    private readonly TextBlock _eventMaxLabel;
    private readonly Slider    _metricMaxSlider;
    private readonly TextBlock _metricMaxLabel;

    // ── Constructor ──────────────────────────────────────────────────────────

    public DiagnosticToolsOptionsPage()
    {
        (_pollIntervalSlider, _pollIntervalLabel) = MakeSlider(100,    5000,    100);
        (_ringCapacitySlider, _ringCapacityLabel) = MakeSlider(30,     500,      10);
        (_eventMaxSlider,     _eventMaxLabel)     = MakeSlider(100,    5000,    100);
        (_metricMaxSlider,    _metricMaxLabel)    = MakeSlider(1000,   100_000, 1000);

        var restartNote = new TextBlock
        {
            Text         = "Changes take effect at the start of the next diagnostic session.",
            FontStyle    = FontStyles.Italic,
            Opacity      = 0.6,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 8, 0, 8),
        };

        var resetButton = new Button
        {
            Content             = "Reset to Defaults",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding             = new Thickness(10, 4, 10, 4),
            Margin              = new Thickness(0, 8, 0, 0),
        };
        resetButton.Click += OnReset;

        var root = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin      = new Thickness(12, 8, 12, 8),
        };

        // Foreground inherits from DynamicResource so all child TextBlocks
        // automatically pick up the active theme colour.
        root.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");

        root.Children.Add(MakeSectionHeader("Sampling"));
        root.Children.Add(MakeSliderRow("Poll interval (ms):",    _pollIntervalSlider, _pollIntervalLabel));

        root.Children.Add(MakeSectionHeader("Buffers"));
        root.Children.Add(MakeSliderRow("Graph ring capacity:",   _ringCapacitySlider, _ringCapacityLabel));
        root.Children.Add(MakeSliderRow("Max event log entries:", _eventMaxSlider,     _eventMaxLabel));
        root.Children.Add(MakeSliderRow("Max metric samples:",    _metricMaxSlider,    _metricMaxLabel));

        root.Children.Add(restartNote);
        root.Children.Add(resetButton);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = root,
        };

        // Drive slider widths from the page's actual width so they always fill
        // the available space regardless of how the host container measures us.
        SizeChanged += (_, e) => { if (e.WidthChanged) ApplySliderWidths(e.NewSize.Width); };
        Loaded      += (_, _) => ApplySliderWidths(ActualWidth);
    }

    // Slider widths = pageWidth – StackPanel margins (12+12) – label col (160) – value col (60)
    private void ApplySliderWidths(double pageWidth)
    {
        double w = Math.Max(pageWidth - 244, 80);
        _pollIntervalSlider.Width = w;
        _ringCapacitySlider.Width = w;
        _eventMaxSlider.Width     = w;
        _metricMaxSlider.Width    = w;
    }

    // ── Load / Save / Reset ───────────────────────────────────────────────────

    public void Load()
    {
        var opts = DiagnosticToolsOptions.Instance;
        _pollIntervalSlider.Value = Math.Clamp(opts.PollIntervalMs, 100,    5000);
        _ringCapacitySlider.Value = Math.Clamp(opts.RingCapacity,   30,     500);
        _eventMaxSlider.Value     = Math.Clamp(opts.EventMaxCount,  100,    5000);
        _metricMaxSlider.Value    = Math.Clamp(opts.MetricMaxCount, 1000, 100_000);
    }

    public void Save()
    {
        var opts = DiagnosticToolsOptions.Instance;
        opts.PollIntervalMs = (int)_pollIntervalSlider.Value;
        opts.RingCapacity   = (int)_ringCapacitySlider.Value;
        opts.EventMaxCount  = (int)_eventMaxSlider.Value;
        opts.MetricMaxCount = (int)_metricMaxSlider.Value;
        opts.Save();
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        var defaults = new DiagnosticToolsOptions();
        _pollIntervalSlider.Value = defaults.PollIntervalMs;
        _ringCapacitySlider.Value = defaults.RingCapacity;
        _eventMaxSlider.Value     = defaults.EventMaxCount;
        _metricMaxSlider.Value    = defaults.MetricMaxCount;
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
            Width             = 60,
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
    /// 3-column Grid: [160px label] [* slider] [60px value].
    /// The star column makes the slider track fill all available width.
    /// </summary>
    private static Grid MakeSliderRow(string labelText, Slider slider, TextBlock valueLabel)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

        var label = new TextBlock
        {
            Text              = labelText,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Grid.SetColumn(label,      0);
        Grid.SetColumn(slider,     1);
        Grid.SetColumn(valueLabel, 2);

        grid.Children.Add(label);
        grid.Children.Add(slider);
        grid.Children.Add(valueLabel);

        return grid;
    }
}
