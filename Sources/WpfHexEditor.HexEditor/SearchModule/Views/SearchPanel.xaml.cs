////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

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
        {
            throw new NotImplementedException();
        }
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
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}
