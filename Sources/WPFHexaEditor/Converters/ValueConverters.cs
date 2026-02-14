//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexaEditor.Core;

namespace WpfHexaEditor.Converters
{
    /// <summary>
    /// Converts bool (IsSelected) to brush
    /// </summary>
    public class BoolToSelectionBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
                return new SolidColorBrush(Color.FromArgb(77, 51, 153, 255)); // #3399FF with alpha
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts ByteAction to brush color using dynamic resources from UserControl
    /// </summary>
    public class ActionToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ByteAction action)
            {
                // Try to get the brush from the UserControl's resources (passed as parameter)
                // The parameter should be the UserControl itself
                if (parameter is FrameworkElement element && element != null)
                {
                    try
                    {
                        return action switch
                        {
                            ByteAction.Modified => element.TryFindResource("ModifiedBrush") as Brush ?? Brushes.Orange,
                            ByteAction.Added => element.TryFindResource("AddedBrush") as Brush ?? Brushes.LightGreen,
                            ByteAction.Deleted => element.TryFindResource("DeletedBrush") as Brush ?? Brushes.Red,
                            _ => Brushes.Transparent
                        };
                    }
                    catch
                    {
                        // Fallback to hardcoded colors
                    }
                }

                // Fallback to hardcoded colors if resources not available
                return action switch
                {
                    ByteAction.Modified => Brushes.Orange,
                    ByteAction.Added => Brushes.LightGreen,
                    ByteAction.Deleted => Brushes.Red,
                    _ => Brushes.Transparent
                };
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
