// ==========================================================
// Project: WpfHexEditor.App
// File: Options/TabsOptionsPage.cs
// Description:
//     Options page for document tab bar and tab hover-preview.
//     Category: Environment › Tabs
//     Sections: Tab Bar (placement, color mode, multi-row),
//               Tab Preview (enable, size, delays).
//
// Architecture Notes:
//     Replaces the former TabPreviewOptionsPage (absorbed here).
//     Tab Bar settings mutate DocumentTabBarSettings directly (INPC
//     propagates live to DockControl — no extra notification call needed).
//     Tab Preview settings write to AppSettings.TabPreview then call
//     TabPreviewAppSettings.NotifyChanged() so MainWindow can propagate
//     the changes to DockHost.TabPreviewSettings.
//     Constructor accepts DocumentTabBarSettings (nullable) injected by
//     MainWindow at registration time; falls back gracefully when null.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Options;
using WpfHexEditor.Docking.Core;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Environment › Tabs.
/// Unifies document tab bar layout settings and tab hover-preview configuration.
/// </summary>
public sealed class TabsOptionsPage : UserControl, IOptionsPage
{
    // ── IOptionsPage ─────────────────────────────────────────────────────────

    public event EventHandler? Changed;

    // ── Injected state ───────────────────────────────────────────────────────

    private readonly DocumentTabBarSettings? _tabBarSettings;

    // ── UI fields — Tab Bar ──────────────────────────────────────────────────

    private readonly ComboBox  _placementCombo;
    private readonly ComboBox  _colorModeCombo;
    private readonly CheckBox  _multiRowCheck;
    private readonly CheckBox  _multiRowWheelCheck;

    // ── UI fields — Tab Preview ───────────────────────────────────────────────

    private readonly CheckBox  _enableCheck;
    private readonly CheckBox  _showFileNameCheck;
    private readonly Slider    _widthSlider;
    private readonly TextBlock _widthLabel;
    private readonly Slider    _heightSlider;
    private readonly TextBlock _heightLabel;
    private readonly Slider    _openDelaySlider;
    private readonly TextBlock _openDelayLabel;
    private readonly Slider    _closeDelaySlider;
    private readonly TextBlock _closeDelayLabel;

    private bool _loading;

    // ── Constructor ──────────────────────────────────────────────────────────

    /// <param name="tabBarSettings">
    /// Live <see cref="DocumentTabBarSettings"/> from <c>DockHost.TabBarSettings</c>.
    /// May be null when the docking layout is not yet loaded; the section is shown
    /// but changes are silently no-ops until a non-null instance is provided.
    /// </param>
    public TabsOptionsPage(DocumentTabBarSettings? tabBarSettings = null)
    {
        _tabBarSettings = tabBarSettings;

        // ── Tab Bar — placement ───────────────────────────────────────────────
        _placementCombo = new ComboBox { Margin = new Thickness(0, 4, 0, 4) };
        _placementCombo.Items.Add("Top");
        _placementCombo.Items.Add("Left");
        _placementCombo.Items.Add("Right");
        _placementCombo.SelectionChanged += OnChanged;

        // ── Tab Bar — color mode ──────────────────────────────────────────────
        _colorModeCombo = new ComboBox { Margin = new Thickness(0, 4, 0, 4) };
        _colorModeCombo.Items.Add("None");
        _colorModeCombo.Items.Add("By Project");
        _colorModeCombo.Items.Add("By File Extension");
        _colorModeCombo.Items.Add("By Regex Rule");
        _colorModeCombo.SelectionChanged += OnChanged;

        // ── Tab Bar — multi-row ───────────────────────────────────────────────
        _multiRowCheck = new CheckBox
        {
            Content = "Wrap tabs to additional rows (multi-row)",
            Margin  = new Thickness(0, 4, 0, 4),
        };
        _multiRowCheck.Checked   += OnChanged;
        _multiRowCheck.Unchecked += OnChanged;

        _multiRowWheelCheck = new CheckBox
        {
            Content = "Scroll tab rows with mouse wheel",
            Margin  = new Thickness(16, 4, 0, 4),
        };
        _multiRowWheelCheck.Checked   += OnChanged;
        _multiRowWheelCheck.Unchecked += OnChanged;

        // ── Tab Preview ───────────────────────────────────────────────────────
        _enableCheck = new CheckBox { Content = "Enable tab hover preview", Margin = new Thickness(0, 4, 0, 4) };
        _enableCheck.Checked   += OnChanged;
        _enableCheck.Unchecked += OnChanged;

        _showFileNameCheck = new CheckBox { Content = "Show filename in footer", Margin = new Thickness(0, 4, 0, 4) };
        _showFileNameCheck.Checked   += OnChanged;
        _showFileNameCheck.Unchecked += OnChanged;

        (_widthSlider,      _widthLabel)      = MakeSlider(100, 400, 1);
        (_heightSlider,     _heightLabel)     = MakeSlider(80,  300, 1);
        (_openDelaySlider,  _openDelayLabel)  = MakeSlider(100, 1000, 50);
        (_closeDelaySlider, _closeDelayLabel) = MakeSlider(50,  500,  10);

        // ── Root layout ───────────────────────────────────────────────────────
        var root = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin      = new Thickness(12, 8, 12, 8),
        };

        // Tab Bar section
        root.Children.Add(MakeSectionHeader("Tab Bar"));
        root.Children.Add(MakeLabeledRow("Tab placement:", _placementCombo));
        root.Children.Add(MakeLabeledRow("Tab color mode:", _colorModeCombo));
        root.Children.Add(_multiRowCheck);
        root.Children.Add(_multiRowWheelCheck);

        // Tab Preview section
        root.Children.Add(MakeSectionHeader("Tab Preview"));
        root.Children.Add(_enableCheck);
        root.Children.Add(_showFileNameCheck);
        root.Children.Add(MakeSectionHeader("Size"));
        root.Children.Add(MakeSliderRow("Width (px):",  _widthSlider,  _widthLabel));
        root.Children.Add(MakeSliderRow("Height (px):", _heightSlider, _heightLabel));
        root.Children.Add(MakeSectionHeader("Delays"));
        root.Children.Add(MakeSliderRow("Open delay (ms):",  _openDelaySlider,  _openDelayLabel));
        root.Children.Add(MakeSliderRow("Close delay (ms):", _closeDelaySlider, _closeDelayLabel));

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = root,
        };

        SizeChanged += (_, e) => { if (e.WidthChanged) ApplySliderWidths(e.NewSize.Width); };
        Loaded      += (_, _) => ApplySliderWidths(ActualWidth);
    }

    // Slider widths = pageWidth – StackPanel margins (12+12) – label col (140) – value col (44)
    private void ApplySliderWidths(double pageWidth)
    {
        double w = Math.Max(pageWidth - 208, 80);
        _widthSlider.Width      = w;
        _heightSlider.Width     = w;
        _openDelaySlider.Width  = w;
        _closeDelaySlider.Width = w;
    }

    // ── IOptionsPage ─────────────────────────────────────────────────────────

    public void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            // Tab Bar
            if (_tabBarSettings is not null)
            {
                _placementCombo.SelectedIndex  = (int)_tabBarSettings.TabPlacement;
                _colorModeCombo.SelectedIndex  = (int)_tabBarSettings.ColorMode;
                _multiRowCheck.IsChecked       = _tabBarSettings.MultiRowTabs;
                _multiRowWheelCheck.IsChecked  = _tabBarSettings.MultiRowWithMouseWheel;
            }
            else
            {
                _placementCombo.SelectedIndex = 0;
                _colorModeCombo.SelectedIndex = 0;
            }

            // Tab Preview
            var p = settings.TabPreview;
            _enableCheck.IsChecked        = p.Enabled;
            _showFileNameCheck.IsChecked  = p.ShowFileName;
            _widthSlider.Value            = p.PreviewWidth;
            _heightSlider.Value           = p.PreviewHeight;
            _openDelaySlider.Value        = p.OpenDelayMs;
            _closeDelaySlider.Value       = p.CloseDelayMs;
        }
        finally
        {
            _loading = false;
        }
    }

    public void Flush(AppSettings settings)
    {
        // Tab Bar — mutate shared INPC object directly (live update to DockControl)
        if (_tabBarSettings is not null)
        {
            _tabBarSettings.TabPlacement           = (DocumentTabPlacement)(_placementCombo.SelectedIndex >= 0 ? _placementCombo.SelectedIndex : 0);
            _tabBarSettings.ColorMode              = (DocumentTabColorMode)(_colorModeCombo.SelectedIndex  >= 0 ? _colorModeCombo.SelectedIndex  : 0);
            _tabBarSettings.MultiRowTabs           = _multiRowCheck.IsChecked      == true;
            _tabBarSettings.MultiRowWithMouseWheel = _multiRowWheelCheck.IsChecked == true;
        }

        // Tab Preview — write to AppSettings then notify MainWindow
        var p = settings.TabPreview;
        p.Enabled       = _enableCheck.IsChecked       == true;
        p.ShowFileName  = _showFileNameCheck.IsChecked  == true;
        p.PreviewWidth  = (int)_widthSlider.Value;
        p.PreviewHeight = (int)_heightSlider.Value;
        p.OpenDelayMs   = (int)_openDelaySlider.Value;
        p.CloseDelayMs  = (int)_closeDelaySlider.Value;

        TabPreviewAppSettings.NotifyChanged();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private static TextBlock MakeSectionHeader(string title) => new()
    {
        Text       = title,
        FontWeight = FontWeights.SemiBold,
        Margin     = new Thickness(0, 8, 0, 4),
    };

    private static Grid MakeLabeledRow(string labelText, Control control)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text              = labelText,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label,   0);
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

    private static Grid MakeSliderRow(string labelText, Slider slider, TextBlock valueLabel)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
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
}
