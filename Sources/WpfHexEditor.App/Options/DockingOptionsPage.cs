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
using WpfHexEditor.Docking.Core;

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

    // Active Panel Highlight
    private readonly ComboBox  _highlightModeCombo;
    private readonly Action<ActivePanelHighlightMode>? _livePreview;

    private bool _loading;

    public DockingOptionsPage(Action<ActivePanelHighlightMode>? livePreview = null)
    {
        _livePreview = livePreview;
        Padding = new Thickness(16);

        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/WpfHexEditor.App;component/Themes/DialogStyles.xaml")
        });

        _highlightModeCombo = new ComboBox { MinWidth = 160 };
        _highlightModeCombo.Items.Add("None");
        _highlightModeCombo.Items.Add("Top Bar");
        _highlightModeCombo.Items.Add("Full Border");
        _highlightModeCombo.Items.Add("Glow");
        _highlightModeCombo.SelectionChanged += OnHighlightModeChanged;

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

        (_minPaneSizeSlider,        _minPaneSizeLabel)        = MakeSlider(20, 200, 5);
        (_floatDefaultWidthSlider,  _floatDefaultWidthLabel)  = MakeSlider(200, 800, 10);
        (_floatDefaultHeightSlider, _floatDefaultHeightLabel) = MakeSlider(150, 600, 10);

        _profileDirBox = new TextBox { Margin = new Thickness(0, 4, 0, 4) };
        _profileDirBox.TextChanged += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };

        var root = new StackPanel { Orientation = Orientation.Vertical };

        // Active Panel Highlight — TOP
        root.Children.Add(SectionHeader("ACTIVE PANEL HIGHLIGHT"));
        root.Children.Add(MakeLabeledRow("Highlight mode:", _highlightModeCombo));
        root.Children.Add(Hint("None · Top Bar (2px top) · Full Border (2px outline) · Glow (border + shadow)"));

        // Auto-Hide
        root.Children.Add(SectionHeader("AUTO-HIDE PANEL TIMING"));
        root.Children.Add(MakeSliderRow("Open delay (ms):",      _openDelaySlider,  _openDelayLabel));
        root.Children.Add(MakeSliderRow("Close delay (ms):",     _closeDelaySlider, _closeDelayLabel));
        root.Children.Add(MakeSliderRow("Slide animation (ms):", _slideAnimSlider,  _slideAnimLabel));

        // Animations
        root.Children.Add(SectionHeader("DOCKING ANIMATIONS"));
        root.Children.Add(_animationsEnabledCheck);
        root.Children.Add(MakeSliderRow("Overlay fade-in (ms):",         _overlayFadeInSlider,  _overlayFadeInLabel));
        root.Children.Add(MakeSliderRow("Overlay fade-out (ms):",        _overlayFadeOutSlider, _overlayFadeOutLabel));
        root.Children.Add(MakeSliderRow("Floating window fade-in (ms):", _floatingFadeInSlider, _floatingFadeInLabel));
        root.Children.Add(Hint("Set to 0 for instant transitions. Uncheck to disable all animations."));

        // Panel Sizing
        root.Children.Add(SectionHeader("PANEL SIZING"));
        root.Children.Add(MakeSliderRow("Min pane size (px):",        _minPaneSizeSlider,        _minPaneSizeLabel));
        root.Children.Add(MakeSliderRow("Float default width (px):",  _floatDefaultWidthSlider,  _floatDefaultWidthLabel));
        root.Children.Add(MakeSliderRow("Float default height (px):", _floatDefaultHeightSlider, _floatDefaultHeightLabel));

        // Layout Profiles
        root.Children.Add(SectionHeader("LAYOUT PROFILES"));
        root.Children.Add(MakeLabeledRow("Profile directory:", _profileDirBox));
        root.Children.Add(Hint("Leave empty to use the default AppData location."));

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
            _highlightModeCombo.SelectedIndex = (int)settings.UI.ActivePanelHighlight;

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
        settings.UI.ActivePanelHighlight = (ActivePanelHighlightMode)Math.Max(0, _highlightModeCombo.SelectedIndex);

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

    private void OnHighlightModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var mode = (ActivePanelHighlightMode)Math.Max(0, _highlightModeCombo.SelectedIndex);
        _livePreview?.Invoke(mode);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TextBlock SectionHeader(string title) => OptionsPageHelper.SectionHeader(title);
    private static TextBlock Hint(string text)           => OptionsPageHelper.Hint(text);

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
