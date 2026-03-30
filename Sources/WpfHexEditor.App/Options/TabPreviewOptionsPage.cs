// ==========================================================
// Project: WpfHexEditor.App
// File: Options/TabPreviewOptionsPage.cs
// Description:
//     Options page for Tab Hover Preview settings.
//     Category: Environment > Tab Preview
//     Sections: General, Dimensions, Timing, Display.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Environment > Tab Preview.
/// Configures tab thumbnail hover-preview popup behavior.
/// </summary>
public sealed class TabPreviewOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    private readonly CheckBox  _enabledCheck;
    private readonly Slider    _widthSlider;
    private readonly TextBlock _widthLabel;
    private readonly Slider    _heightSlider;
    private readonly TextBlock _heightLabel;
    private readonly Slider    _openDelaySlider;
    private readonly TextBlock _openDelayLabel;
    private readonly Slider    _closeDelaySlider;
    private readonly TextBlock _closeDelayLabel;
    private readonly CheckBox  _showFileNameCheck;

    private bool _loading;

    public TabPreviewOptionsPage()
    {
        _enabledCheck = new CheckBox
        {
            Content = "Enable tab preview on hover",
            Margin  = new Thickness(0, 4, 0, 4),
        };
        _enabledCheck.Checked   += OnChanged;
        _enabledCheck.Unchecked += OnChanged;

        (_widthSlider,      _widthLabel)      = MakeSlider(100, 400, 10);
        (_heightSlider,     _heightLabel)     = MakeSlider(80,  300, 10);
        (_openDelaySlider,  _openDelayLabel)  = MakeSlider(100, 1000, 50);
        (_closeDelaySlider, _closeDelayLabel) = MakeSlider(50,  500,  50);

        _showFileNameCheck = new CheckBox
        {
            Content = "Show file name below preview",
            Margin  = new Thickness(0, 4, 0, 4),
        };
        _showFileNameCheck.Checked   += OnChanged;
        _showFileNameCheck.Unchecked += OnChanged;

        var root = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin      = new Thickness(12, 8, 12, 8),
        };

        root.Children.Add(MakeSectionHeader("General"));
        root.Children.Add(_enabledCheck);

        root.Children.Add(MakeSectionHeader("Dimensions"));
        root.Children.Add(MakeSliderRow("Preview width (px):",  _widthSlider,  _widthLabel));
        root.Children.Add(MakeSliderRow("Preview height (px):", _heightSlider, _heightLabel));

        root.Children.Add(MakeSectionHeader("Timing"));
        root.Children.Add(MakeSliderRow("Open delay (ms):",  _openDelaySlider,  _openDelayLabel));
        root.Children.Add(MakeSliderRow("Close delay (ms):", _closeDelaySlider, _closeDelayLabel));

        root.Children.Add(MakeSectionHeader("Display"));
        root.Children.Add(_showFileNameCheck);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = root,
        };
    }

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            _enabledCheck.IsChecked      = s.TabPreview.Enabled;
            _widthSlider.Value           = s.TabPreview.PreviewWidth;
            _heightSlider.Value          = s.TabPreview.PreviewHeight;
            _openDelaySlider.Value       = s.TabPreview.OpenDelayMs;
            _closeDelaySlider.Value      = s.TabPreview.CloseDelayMs;
            _showFileNameCheck.IsChecked = s.TabPreview.ShowFileName;
        }
        finally
        {
            _loading = false;
        }
    }

    public void Flush(AppSettings s)
    {
        s.TabPreview.Enabled       = _enabledCheck.IsChecked == true;
        s.TabPreview.PreviewWidth  = (int)_widthSlider.Value;
        s.TabPreview.PreviewHeight = (int)_heightSlider.Value;
        s.TabPreview.OpenDelayMs   = (int)_openDelaySlider.Value;
        s.TabPreview.CloseDelayMs  = (int)_closeDelaySlider.Value;
        s.TabPreview.ShowFileName  = _showFileNameCheck.IsChecked == true;
        TabPreviewAppSettings.NotifyChanged();
    }

    // ── Event helpers ─────────────────────────────────────────────────────────

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private (Slider slider, TextBlock label) MakeSlider(double min, double max, double tickFreq)
    {
        var label = new TextBlock { MinWidth = 40, Margin = new Thickness(6, 0, 0, 0) };
        var slider = new Slider
        {
            Minimum           = min,
            Maximum           = max,
            TickFrequency     = tickFreq,
            IsSnapToTickEnabled = true,
            Margin            = new Thickness(0, 4, 0, 4),
        };
        slider.ValueChanged += (_, args) =>
        {
            label.Text = ((int)args.NewValue).ToString();
            if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
        };
        return (slider, label);
    }

    private static TextBlock MakeSectionHeader(string title) => new()
    {
        Text       = title,
        FontWeight = FontWeights.SemiBold,
        Margin     = new Thickness(0, 8, 0, 4),
    };

    private static Grid MakeSliderRow(string labelText, Slider slider, TextBlock valueLabel)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var lbl = new TextBlock
        {
            Text              = labelText,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(lbl,        0);
        Grid.SetColumn(slider,     1);
        Grid.SetColumn(valueLabel, 2);

        grid.Children.Add(lbl);
        grid.Children.Add(slider);
        grid.Children.Add(valueLabel);
        return grid;
    }
}
