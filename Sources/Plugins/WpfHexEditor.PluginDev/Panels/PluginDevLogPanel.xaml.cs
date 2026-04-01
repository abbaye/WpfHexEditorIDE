// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Panels/PluginDevLogPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for the Plugin Developer Log panel.
//     Handles toolbar actions (Clear, Export) and auto-scrolling.
// ==========================================================

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;

namespace WpfHexEditor.PluginDev.Panels;

/// <summary>
/// VS-Like dockable panel for plugin development log output.
/// </summary>
public sealed partial class PluginDevLogPanel : UserControl
{
    // -----------------------------------------------------------------------
    // ViewModel
    // -----------------------------------------------------------------------

    private PluginDevLogViewModel? _vm;

    public PluginDevLogViewModel ViewModel
    {
        get => _vm ??= new PluginDevLogViewModel();
        set
        {
            _vm = value;
            DataContext = _vm;
        }
    }

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    public PluginDevLogPanel()
    {
        InitializeComponent();
        DataContext = ViewModel;

        // Wire auto-scroll: scroll ListView to the last item when Entries changes.
        ViewModel.Entries.CollectionChanged += (_, _) =>
        {
            if (ViewModel.AutoScroll && LvLog.Items.Count > 0)
                LvLog.ScrollIntoView(LvLog.Items[LvLog.Items.Count - 1]);
        };
    }

    // -----------------------------------------------------------------------
    // Toolbar handlers
    // -----------------------------------------------------------------------

    private void OnClear(object sender, RoutedEventArgs e) => ViewModel.Clear();

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title            = "Export Plugin Dev Log",
            Filter           = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt       = ".log",
            FileName         = $"plugin-dev-{DateTime.Now:yyyyMMdd-HHmmss}.log",
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dlg.FileName, ViewModel.Export());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Export failed: {ex.Message}",
                    "Plugin Dev Log",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}

// -----------------------------------------------------------------------
// Level → color converter
// -----------------------------------------------------------------------

/// <summary>
/// Converts a <see cref="LogLevel"/> to a VS-palette foreground brush.
/// </summary>
[ValueConversion(typeof(LogLevel), typeof(Brush))]
public sealed class LogLevelToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush _debug   = Brush("#9B9B9B");
    private static readonly SolidColorBrush _info    = Brush("#DCDCDC");
    private static readonly SolidColorBrush _warning = Brush("#DCDCAA");
    private static readonly SolidColorBrush _error   = Brush("#F48771");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is LogLevel l ? l switch
        {
            LogLevel.Debug   => _debug,
            LogLevel.Warning => _warning,
            LogLevel.Error   => _error,
            _                => _info,
        } : _info;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush Brush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
