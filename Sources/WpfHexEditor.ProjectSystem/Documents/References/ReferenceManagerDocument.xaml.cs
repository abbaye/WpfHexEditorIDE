// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/References/ReferenceManagerDocument.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-24
// Description:
//     Code-behind for the VS-like Reference Manager document tab.
//     Minimal: all logic lives in ReferenceManagerViewModel.
//
// Architecture Notes:
//     Pattern:  MVVM — DataContext set by the host (MainWindow).
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WpfHexEditor.ProjectSystem.Documents.References;

/// <summary>VS-like Reference Manager document tab.</summary>
public partial class ReferenceManagerDocument : UserControl
{
    public ReferenceManagerDocument()
    {
        InitializeComponent();
    }
}

/// <summary>Simple bool → Visibility converter used in the Reference Manager DataTemplate.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
