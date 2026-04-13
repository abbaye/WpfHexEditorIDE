// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: SearchPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Code-behind for the SearchPanel UserControl — the full-featured search panel
//     dockable in the IDE. Binds to SearchViewModel, shows search results in a list,
//     and supports navigation, result previews, and search type selection.
//
// Architecture Notes:
//     MVVM view in the Search module. Contains internal converters for result formatting.
//     Conforms to PanelCommon.xaml theme styles for toolbar and layout.
//
// ==========================================================

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfHexEditor.Core.Search.Models;
using WpfHexEditor.HexEditor.Search.ViewModels;

namespace WpfHexEditor.HexEditor.Search.Views
{
    /// <summary>
    /// Interaction logic for SearchPanel.xaml
    /// </summary>
    public partial class SearchPanel : UserControl
    {
        public SearchPanel()
        {
            InitializeComponent();

            // Set default DataContext if not set
            if (DataContext == null)
            {
                DataContext = new SearchViewModel();
            }
        }

        /// <summary>
        /// Gets or sets the SearchViewModel.
        /// </summary>
        public SearchViewModel ViewModel
        {
            get => DataContext as SearchViewModel;
            set => DataContext = value;
        }
    }

    #region Value Converters

    /// <summary>
    /// Converts SearchMode enum to Visibility based on parameter.
    /// </summary>
    public class SearchModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SearchMode mode && parameter is string targetMode)
            {
                if (Enum.TryParse<SearchMode>(targetMode, out var target))
                {
                    return mode == target ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>
    /// Converts enum to bool for RadioButton binding.
    /// </summary>
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            return value.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
            {
                return Enum.Parse(targetType, parameter.ToString());
            }
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converts bool to Visibility.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>
    /// Returns Visible when the string is null or empty (used for placeholder text).
    /// Returns Collapsed when the string has content.
    /// </summary>
    public class StringEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    #endregion
}
