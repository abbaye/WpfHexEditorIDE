// Apache 2.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.ColorPicker.Controls;
using WpfHexEditor.Core.Settings;
using PropertyMetadata = WpfHexEditor.Core.Settings.PropertyMetadata;

namespace WpfHexEditor.HexEditor.Settings.Controls
{
    /// <summary>
    /// Control generator for Brush properties (using ColorPicker with conversion).
    /// Example: SyntaxBraceColor, EditorBackground, SelectionBackground
    /// Converts SolidColorBrush to/from Color for ColorPicker binding
    /// </summary>
    public class BrushPropertyControl : IPropertyControl
    {
        public FrameworkElement CreateControl()
        {
            // Create ColorPicker instance (same as ColorPropertyControl)
            return new WpfHexEditor.ColorPicker.Controls.ColorPicker
            {
                Margin = new Thickness(0, 0, 0, 8),
                ShowAlphaChannel = true // Brush properties may use alpha
            };
        }

        public Binding CreateBinding(PropertyMetadata metadata)
        {
            return new Binding(metadata.PropertyName)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Converter = new BrushToColorConverter() // Convert Brush ↔ Color
            };
        }

        public TextBlock CreateLabel(PropertyMetadata metadata)
        {
            return new TextBlock
            {
                Text = metadata.GetDisplayName(),
                FontSize = 11,
                Margin = new Thickness(0, 8, 0, 4)
            };
        }
    }

    /// <summary>
    /// Converter that converts between Brush (SolidColorBrush) and Color.
    /// Used to bind Brush properties to ColorPicker (which expects Color).
    /// </summary>
    public class BrushToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Brush → Color (for ColorPicker display)
            if (value is SolidColorBrush solidBrush)
            {
                return solidBrush.Color;
            }

            // Fallback for other Brush types (gradient, etc.) - return transparent
            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Color → Brush (from ColorPicker selection)
            if (value is Color color)
            {
                return new SolidColorBrush(color);
            }

            return Brushes.Transparent;
        }
    }
}
