/*
    Apache 2.0  2026
    Author : Derek Tremblay (derektremblay666@gmail.com)
    Contributors: Claude Sonnet 4.5
*/

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using WpfHexEditor.HexEditor.Search.ViewModels;

namespace WpfHexEditor.HexEditor.Search.Views
{
    /// <summary>
    /// Advanced Search Dialog - Code-behind (minimal).
    /// </summary>
    public partial class AdvancedSearchDialog : Window
    {
        public AdvancedSearchDialog()
        {
            InitializeComponent();
            RegisterKeyboardShortcuts();
        }

        private void RegisterKeyboardShortcuts()
        {
            PreviewKeyDown += (s, e) =>
            {
                var vm = DataContext as AdvancedSearchViewModel;
                if (vm == null) return;

                // F3: Find Next
                if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.None)
                {
                    vm.FindNextCommand.Execute(null);
                    e.Handled = true;
                }

                // Shift+F3: Find Previous
                if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    vm.FindPreviousCommand.Execute(null);
                    e.Handled = true;
                }

                // Escape: Cancel search if running, otherwise close dialog
                if (e.Key == Key.Escape)
                {
                    if (vm.IsSearching)
                    {
                        vm.CancelCommand.Execute(null);
                        e.Handled = true;
                    }
                    else
                    {
                        Close();
                    }
                }

                // Ctrl+F: Focus search input
                if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    // Focus the ComboBox in Find section
                    var comboBox = FindName("SearchInputComboBox") as System.Windows.Controls.ComboBox;
                    comboBox?.Focus();
                    e.Handled = true;
                }
            };
        }
    }

    #region Value Converters

    /// <summary>
    /// Inverse bool to visibility converter.
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts AlternationIndex to 1-based index for display.
    /// </summary>
    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return (index + 1).ToString();
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}
