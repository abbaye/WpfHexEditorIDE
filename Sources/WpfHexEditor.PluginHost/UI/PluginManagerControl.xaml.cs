//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>Converts a null/non-null object to Visibility. Invert=true → null=Visible, non-null=Collapsed.</summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value is null;
        bool visible = Invert ? !isNull : isNull;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Converts a count (int) to Visibility.
/// Default (no parameter): count == 0 → Visible, count > 0 → Collapsed (shows empty-state).
/// Parameter "invert": count == 0 → Collapsed, count > 0 → Visible (shows list).
/// </summary>
[ValueConversion(typeof(int), typeof(Visibility))]
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isEmpty = value is int count && count == 0;
        bool invert = parameter is string s && s == "invert";
        bool visible = invert ? !isEmpty : isEmpty;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Plugin Manager document tab â€” lists all plugins with live metrics and lifecycle actions.
/// </summary>
public sealed partial class PluginManagerControl : UserControl
{
    public PluginManagerControl(PluginManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        // Theme-aware foreground (rule 7b)
        SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IDisposable d) d.Dispose();
        Unloaded -= OnUnloaded;
    }
}
