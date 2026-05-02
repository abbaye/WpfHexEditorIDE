// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WhfmtFormatDetailPanel.xaml.cs
// Description: Code-behind for the format detail card panel.
//              Hosts a JSON CodeEditor preview below/beside the tab area.
//              PreviewOrientation (Vertical=tabs-above/preview-below,
//              Horizontal=tabs-left/preview-right) is set by the host layout.
// ==========================================================

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Shell.Panels.ViewModels;

namespace WpfHexEditor.Shell.Panels.Panels;

public enum DetailPreviewOrientation { Vertical, Horizontal }

public partial class WhfmtFormatDetailPanel : UserControl
{
    // ----------------------------------------------------------------
    // Dependency property — set by host when layout position changes
    // ----------------------------------------------------------------

    public static readonly DependencyProperty PreviewOrientationProperty =
        DependencyProperty.Register(
            nameof(PreviewOrientation),
            typeof(DetailPreviewOrientation),
            typeof(WhfmtFormatDetailPanel),
            new PropertyMetadata(DetailPreviewOrientation.Vertical, OnOrientationChanged));

    public DetailPreviewOrientation PreviewOrientation
    {
        get => (DetailPreviewOrientation)GetValue(PreviewOrientationProperty);
        set => SetValue(PreviewOrientationProperty, value);
    }

    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((WhfmtFormatDetailPanel)d).ApplyOrientation((DetailPreviewOrientation)e.NewValue);

    // ----------------------------------------------------------------

    private WhfmtFormatDetailVm? _vm;

    public WhfmtFormatDetailPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => ApplyOrientation(PreviewOrientation);
    }

    // ----------------------------------------------------------------
    // DataContext wiring
    // ----------------------------------------------------------------

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = e.NewValue as WhfmtFormatDetailVm;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            LoadJson(_vm.RawJson);
        }
        else
        {
            JsonCodeEditor.LoadText(string.Empty);
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WhfmtFormatDetailVm.RawJson))
            LoadJson(_vm?.RawJson);
    }

    private void LoadJson(string? json)
        => JsonCodeEditor.LoadText(json ?? string.Empty);

    // ----------------------------------------------------------------
    // Layout switching
    // ----------------------------------------------------------------

    private void ApplyOrientation(DetailPreviewOrientation orientation)
    {
        if (orientation == DetailPreviewOrientation.Horizontal)
        {
            // Tabs left | splitter | preview right
            TabRow.Height     = new GridLength(1, GridUnitType.Star);
            SplitterRow.Height = new GridLength(0);
            PreviewRow.Height  = new GridLength(0);

            TabCol.Width      = new GridLength(1, GridUnitType.Star);
            SplitterCol.Width = new GridLength(4);
            PreviewCol.Width  = new GridLength(200, GridUnitType.Pixel);

            Grid.SetRow(DetailTabControl, 0); Grid.SetColumn(DetailTabControl, 0);
            Grid.SetRow(PreviewSplitter,  0); Grid.SetColumn(PreviewSplitter,  1);
            Grid.SetRow(PreviewBorder,    0); Grid.SetColumn(PreviewBorder,    2);

            PreviewSplitter.Width  = 4;
            PreviewSplitter.Height = double.NaN;
            PreviewSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            PreviewSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
            PreviewSplitter.ResizeDirection     = GridResizeDirection.Columns;

            PreviewBorder.BorderThickness = new Thickness(1, 0, 0, 0);
        }
        else
        {
            // Tabs top | splitter | preview bottom
            TabRow.Height      = new GridLength(1, GridUnitType.Star);
            SplitterRow.Height = new GridLength(4);
            PreviewRow.Height  = new GridLength(220, GridUnitType.Pixel);

            TabCol.Width      = new GridLength(1, GridUnitType.Star);
            SplitterCol.Width = new GridLength(0);
            PreviewCol.Width  = new GridLength(0);

            Grid.SetRow(DetailTabControl, 0); Grid.SetColumn(DetailTabControl, 0);
            Grid.SetRow(PreviewSplitter,  1); Grid.SetColumn(PreviewSplitter,  0);
            Grid.SetRow(PreviewBorder,    2); Grid.SetColumn(PreviewBorder,    0);

            PreviewSplitter.Height = 4;
            PreviewSplitter.Width  = double.NaN;
            PreviewSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            PreviewSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
            PreviewSplitter.ResizeDirection     = GridResizeDirection.Rows;

            PreviewBorder.BorderThickness = new Thickness(0, 1, 0, 0);
        }
    }
}
