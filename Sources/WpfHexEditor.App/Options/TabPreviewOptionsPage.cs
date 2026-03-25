// ==========================================================
// Project: WpfHexEditor.App
// File: Options/TabPreviewOptionsPage.cs
// Description:
//     Options page for configuring the docking tab hover-preview thumbnail.
//     Sections: Preview (enable), Appearance (filename footer), Size, Delays.
//
// Architecture Notes:
//     Code-behind-only UserControl (no XAML) implementing IOptionsPage.
//     Flush() writes to AppSettings.TabPreview then calls TabPreviewAppSettings.NotifyChanged()
//     so MainWindow can propagate live changes to DockHost.TabPreviewSettings.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Environment › Tab Preview.
/// Configures the hover-preview thumbnail shown above docking tabs.
/// </summary>
public sealed class TabPreviewOptionsPage : UserControl, IOptionsPage
{
    // ── IOptionsPage ─────────────────────────────────────────────────────────

    public event EventHandler? Changed;

    // ── UI fields ────────────────────────────────────────────────────────────

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

    // ── Constructor ──────────────────────────────────────────────────────────

    public TabPreviewOptionsPage()
    {
        // ── Enable ───────────────────────────────────────────────────────────
        _enableCheck = new CheckBox { Content = "Enable tab hover preview", Margin = new Thickness(0, 0, 0, 4) };
        _enableCheck.Checked   += OnChanged;
        _enableCheck.Unchecked += OnChanged;

        var enableGroup = MakeGroup("Preview", _enableCheck);

        // ── Appearance ───────────────────────────────────────────────────────
        _showFileNameCheck = new CheckBox { Content = "Show filename in footer", Margin = new Thickness(0, 0, 0, 4) };
        _showFileNameCheck.Checked   += OnChanged;
        _showFileNameCheck.Unchecked += OnChanged;

        var appearanceGroup = MakeGroup("Appearance", _showFileNameCheck);

        // ── Size ─────────────────────────────────────────────────────────────
        (_widthSlider,  _widthLabel)  = MakeSlider("Width:",  100, 400, 1);
        (_heightSlider, _heightLabel) = MakeSlider("Height:", 80,  300, 1);

        var sizePanel = new StackPanel();
        sizePanel.Children.Add(MakeSliderRow("Width (px):",  _widthSlider,  _widthLabel));
        sizePanel.Children.Add(MakeSliderRow("Height (px):", _heightSlider, _heightLabel));

        var sizeGroup = MakeGroup("Size", sizePanel);

        // ── Delays ───────────────────────────────────────────────────────────
        (_openDelaySlider,  _openDelayLabel)  = MakeSlider("Open delay:",  100, 1000, 50);
        (_closeDelaySlider, _closeDelayLabel) = MakeSlider("Close delay:", 50,  500,  10);

        var delayPanel = new StackPanel();
        delayPanel.Children.Add(MakeSliderRow("Open delay (ms):",  _openDelaySlider,  _openDelayLabel));
        delayPanel.Children.Add(MakeSliderRow("Close delay (ms):", _closeDelaySlider, _closeDelayLabel));

        var delayGroup = MakeGroup("Delays", delayPanel);

        // ── Root layout ──────────────────────────────────────────────────────
        var root = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin      = new Thickness(12, 8, 12, 8)
        };
        root.Children.Add(enableGroup);
        root.Children.Add(appearanceGroup);
        root.Children.Add(sizeGroup);
        root.Children.Add(delayGroup);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = root
        };
    }

    // ── IOptionsPage ─────────────────────────────────────────────────────────

    public void Load(AppSettings settings)
    {
        var s = settings.TabPreview;
        _enableCheck.IsChecked       = s.Enabled;
        _showFileNameCheck.IsChecked = s.ShowFileName;
        _widthSlider.Value           = s.PreviewWidth;
        _heightSlider.Value          = s.PreviewHeight;
        _openDelaySlider.Value       = s.OpenDelayMs;
        _closeDelaySlider.Value      = s.CloseDelayMs;
    }

    public void Flush(AppSettings settings)
    {
        var s = settings.TabPreview;
        s.Enabled       = _enableCheck.IsChecked       == true;
        s.ShowFileName  = _showFileNameCheck.IsChecked  == true;
        s.PreviewWidth  = (int)_widthSlider.Value;
        s.PreviewHeight = (int)_heightSlider.Value;
        s.OpenDelayMs   = (int)_openDelaySlider.Value;
        s.CloseDelayMs  = (int)_closeDelaySlider.Value;

        // Notify MainWindow to push new values to DockHost immediately.
        TabPreviewAppSettings.NotifyChanged();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void OnChanged(object sender, RoutedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private static GroupBox MakeGroup(string header, UIElement content)
        => new()
        {
            Header  = header,
            Margin  = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(8, 4, 8, 8),
            Content = content
        };

    private (Slider slider, TextBlock label) MakeSlider(string name, double min, double max, double tickFreq)
    {
        var label = new TextBlock
        {
            Width = 44,
            TextAlignment = System.Windows.TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        };

        var slider = new Slider
        {
            Minimum      = min,
            Maximum      = max,
            TickFrequency = tickFreq,
            IsSnapToTickEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        slider.ValueChanged += (_, e) =>
        {
            label.Text = ((int)e.NewValue).ToString();
            OnChanged(slider, new RoutedEventArgs());
        };

        return (slider, label);
    }

    private static StackPanel MakeSliderRow(string labelText, Slider slider, TextBlock valueLabel)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(0, 4, 0, 4)
        };

        row.Children.Add(new TextBlock
        {
            Text              = labelText,
            Width             = 120,
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(slider);
        row.Children.Add(valueLabel);

        return row;
    }
}
