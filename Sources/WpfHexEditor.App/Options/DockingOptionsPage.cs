// ==========================================================
// Project: WpfHexEditor.App
// File: Options/DockingOptionsPage.cs
// Description:
//     Options page for docking panel behavior settings.
//     Category: Environment > Docking
//     Sections: Auto-Hide Timing, Layout Profiles.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Environment > Docking.
/// Configures auto-hide flyout timing and layout profile storage.
/// </summary>
public sealed class DockingOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    private readonly Slider    _openDelaySlider;
    private readonly TextBlock _openDelayLabel;
    private readonly Slider    _closeDelaySlider;
    private readonly TextBlock _closeDelayLabel;
    private readonly Slider    _slideAnimSlider;
    private readonly TextBlock _slideAnimLabel;

    // Animation controls
    private readonly CheckBox  _animationsEnabledCheck;
    private readonly Slider    _overlayFadeInSlider;
    private readonly TextBlock _overlayFadeInLabel;
    private readonly Slider    _overlayFadeOutSlider;
    private readonly TextBlock _overlayFadeOutLabel;
    private readonly Slider    _floatingFadeInSlider;
    private readonly TextBlock _floatingFadeInLabel;

    // Panel sizing controls
    private readonly Slider    _minPaneSizeSlider;
    private readonly TextBlock _minPaneSizeLabel;
    private readonly Slider    _floatDefaultWidthSlider;
    private readonly TextBlock _floatDefaultWidthLabel;
    private readonly Slider    _floatDefaultHeightSlider;
    private readonly TextBlock _floatDefaultHeightLabel;

    private readonly TextBox   _profileDirBox;

    private bool _loading;

    public DockingOptionsPage()
    {
        (_openDelaySlider,  _openDelayLabel)  = MakeSlider(100, 1000, 50);
        (_closeDelaySlider, _closeDelayLabel) = MakeSlider(50,  800,  50);
        (_slideAnimSlider,  _slideAnimLabel)  = MakeSlider(50,  500,  10);

        _animationsEnabledCheck = new CheckBox
        {
            Content = "Enable docking animations",
            Margin  = new Thickness(0, 4, 0, 4),
        };
        _animationsEnabledCheck.Checked   += OnChanged;
        _animationsEnabledCheck.Unchecked += OnChanged;

        (_overlayFadeInSlider,  _overlayFadeInLabel)  = MakeSlider(0, 500, 10);
        (_overlayFadeOutSlider, _overlayFadeOutLabel) = MakeSlider(0, 500, 10);
        (_floatingFadeInSlider, _floatingFadeInLabel) = MakeSlider(0, 500, 10);

        (_minPaneSizeSlider,      _minPaneSizeLabel)      = MakeSlider(20, 200, 5);
        (_floatDefaultWidthSlider,  _floatDefaultWidthLabel)  = MakeSlider(200, 800, 10);
        (_floatDefaultHeightSlider, _floatDefaultHeightLabel) = MakeSlider(150, 600, 10);

        _profileDirBox = new TextBox
        {
            Margin = new Thickness(0, 4, 0, 4),
        };
        _profileDirBox.TextChanged += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };

        var root = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin      = new Thickness(12, 8, 12, 8),
        };

        // Auto-Hide section
        root.Children.Add(MakeSectionHeader("Auto-Hide Panel Timing"));
        root.Children.Add(MakeSliderRow("Open delay (ms):",      _openDelaySlider,  _openDelayLabel));
        root.Children.Add(MakeSliderRow("Close delay (ms):",     _closeDelaySlider, _closeDelayLabel));
        root.Children.Add(MakeSliderRow("Slide animation (ms):", _slideAnimSlider,  _slideAnimLabel));

        // Animations section
        root.Children.Add(MakeSectionHeader("Docking Animations"));
        root.Children.Add(_animationsEnabledCheck);
        root.Children.Add(MakeSliderRow("Overlay fade-in (ms):",       _overlayFadeInSlider,  _overlayFadeInLabel));
        root.Children.Add(MakeSliderRow("Overlay fade-out (ms):",      _overlayFadeOutSlider, _overlayFadeOutLabel));
        root.Children.Add(MakeSliderRow("Floating window fade-in (ms):", _floatingFadeInSlider, _floatingFadeInLabel));
        root.Children.Add(new TextBlock
        {
            Text   = "Set to 0 for instant transitions. Uncheck to disable all animations.",
            Margin = new Thickness(0, 2, 0, 8),
            FontStyle = FontStyles.Italic,
            Opacity = 0.6,
        });

        // Panel Sizing section
        root.Children.Add(MakeSectionHeader("Panel Sizing"));
        root.Children.Add(MakeSliderRow("Min pane size (px):",             _minPaneSizeSlider,        _minPaneSizeLabel));
        root.Children.Add(MakeSliderRow("Float default width (px):",       _floatDefaultWidthSlider,  _floatDefaultWidthLabel));
        root.Children.Add(MakeSliderRow("Float default height (px):",      _floatDefaultHeightSlider, _floatDefaultHeightLabel));

        // Layout Profiles section
        root.Children.Add(MakeSectionHeader("Layout Profiles"));
        root.Children.Add(MakeLabeledRow("Profile directory:", _profileDirBox));
        root.Children.Add(new TextBlock
        {
            Text   = "Leave empty to use the default AppData location.",
            Margin = new Thickness(0, 2, 0, 8),
            FontStyle = FontStyles.Italic,
            Opacity = 0.6,
        });

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = root,
        };
    }

    public void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            _openDelaySlider.Value  = settings.AutoHideOpenDelayMs;
            _closeDelaySlider.Value = settings.AutoHideCloseDelayMs;
            _slideAnimSlider.Value  = settings.AutoHideSlideAnimationMs;

            _animationsEnabledCheck.IsChecked = settings.DockAnimationsEnabled;
            _overlayFadeInSlider.Value  = settings.DockOverlayFadeInMs;
            _overlayFadeOutSlider.Value = settings.DockOverlayFadeOutMs;
            _floatingFadeInSlider.Value = settings.FloatingWindowFadeInMs;

            _minPaneSizeSlider.Value        = settings.MinPaneSize;
            _floatDefaultWidthSlider.Value  = settings.FloatingWindowDefaultWidth;
            _floatDefaultHeightSlider.Value = settings.FloatingWindowDefaultHeight;

            _profileDirBox.Text = settings.LayoutProfileDirectory ?? string.Empty;
        }
        finally
        {
            _loading = false;
        }
    }

    public void Flush(AppSettings settings)
    {
        settings.AutoHideOpenDelayMs      = (int)_openDelaySlider.Value;
        settings.AutoHideCloseDelayMs     = (int)_closeDelaySlider.Value;
        settings.AutoHideSlideAnimationMs = (int)_slideAnimSlider.Value;

        settings.DockAnimationsEnabled  = _animationsEnabledCheck.IsChecked == true;
        settings.DockOverlayFadeInMs    = (int)_overlayFadeInSlider.Value;
        settings.DockOverlayFadeOutMs   = (int)_overlayFadeOutSlider.Value;
        settings.FloatingWindowFadeInMs = (int)_floatingFadeInSlider.Value;

        settings.MinPaneSize               = (int)_minPaneSizeSlider.Value;
        settings.FloatingWindowDefaultWidth  = (int)_floatDefaultWidthSlider.Value;
        settings.FloatingWindowDefaultHeight = (int)_floatDefaultHeightSlider.Value;

        settings.LayoutProfileDirectory = string.IsNullOrWhiteSpace(_profileDirBox.Text) ? null : _profileDirBox.Text.Trim();

        AutoHideAppSettings.NotifyChanged();
    }

    // ── Event helpers ─────────────────────────────────────────────────────────

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TextBlock MakeSectionHeader(string title) => new()
    {
        Text       = title,
        FontWeight = FontWeights.SemiBold,
        Margin     = new Thickness(0, 8, 0, 4),
    };

    private static Grid MakeLabeledRow(string labelText, Control control)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text              = labelText,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        Grid.SetColumn(control, 1);
        grid.Children.Add(label);
        grid.Children.Add(control);
        return grid;
    }

    private (Slider slider, TextBlock label) MakeSlider(double min, double max, double tickFreq)
    {
        var label = new TextBlock
        {
            Width             = 44,
            TextAlignment     = System.Windows.TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(6, 0, 0, 0),
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

        slider.ValueChanged += (_, e) =>
        {
            label.Text = ((int)e.NewValue).ToString();
            if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
        };

        return (slider, label);
    }

    private static DockPanel MakeSliderRow(string labelText, Slider slider, TextBlock valueLabel)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        var label = new TextBlock
        {
            Text              = labelText,
            Width             = 160,
            VerticalAlignment = VerticalAlignment.Center,
        };

        DockPanel.SetDock(label, Dock.Left);
        DockPanel.SetDock(valueLabel, Dock.Right);

        dock.Children.Add(label);
        dock.Children.Add(valueLabel);
        dock.Children.Add(slider);

        return dock;
    }
}
