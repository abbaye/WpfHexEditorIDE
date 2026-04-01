//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// GNU Affero General Public License v3.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfHexEditor.Core.Settings;
using PropertyMetadata = WpfHexEditor.Core.Settings.PropertyMetadata;

namespace WpfHexEditor.HexEditor.Settings.Controls
{
    /// <summary>
    /// Control generator for FontWeight properties.
    /// Creates ComboBox with common font weights (Normal, Bold, etc.)
    /// </summary>
    public class FontWeightPropertyControl : IPropertyControl
    {
        public FrameworkElement CreateControl()
        {
            var comboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 8),
                ItemsSource = GetFontWeights(),
                DisplayMemberPath = "Name"
            };

            return comboBox;
        }

        public Binding CreateBinding(PropertyMetadata metadata)
        {
            return new Binding(metadata.PropertyName)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Converter = new FontWeightConverter()
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

        /// <summary>
        /// Returns list of common font weights with display names
        /// </summary>
        private List<FontWeightItem> GetFontWeights()
        {
            return new List<FontWeightItem>
            {
                new FontWeightItem { Name = "Thin (100)", Weight = FontWeights.Thin },
                new FontWeightItem { Name = "Extra Light (200)", Weight = FontWeights.ExtraLight },
                new FontWeightItem { Name = "Light (300)", Weight = FontWeights.Light },
                new FontWeightItem { Name = "Normal (400)", Weight = FontWeights.Normal },
                new FontWeightItem { Name = "Medium (500)", Weight = FontWeights.Medium },
                new FontWeightItem { Name = "Semi Bold (600)", Weight = FontWeights.SemiBold },
                new FontWeightItem { Name = "Bold (700)", Weight = FontWeights.Bold },
                new FontWeightItem { Name = "Extra Bold (800)", Weight = FontWeights.ExtraBold },
                new FontWeightItem { Name = "Black (900)", Weight = FontWeights.Black }
            };
        }
    }

    /// <summary>
    /// Helper class to hold font weight with display name
    /// </summary>
    public class FontWeightItem
    {
        public string Name { get; set; }
        public FontWeight Weight { get; set; }
    }

    /// <summary>
    /// Converter between FontWeight struct and FontWeightItem
    /// </summary>
    public class FontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // FontWeight → FontWeightItem (for ComboBox display)
            if (value is FontWeight fontWeight)
            {
                // Find matching item by weight value
                var items = new List<FontWeightItem>
                {
                    new FontWeightItem { Name = "Thin (100)", Weight = FontWeights.Thin },
                    new FontWeightItem { Name = "Extra Light (200)", Weight = FontWeights.ExtraLight },
                    new FontWeightItem { Name = "Light (300)", Weight = FontWeights.Light },
                    new FontWeightItem { Name = "Normal (400)", Weight = FontWeights.Normal },
                    new FontWeightItem { Name = "Medium (500)", Weight = FontWeights.Medium },
                    new FontWeightItem { Name = "Semi Bold (600)", Weight = FontWeights.SemiBold },
                    new FontWeightItem { Name = "Bold (700)", Weight = FontWeights.Bold },
                    new FontWeightItem { Name = "Extra Bold (800)", Weight = FontWeights.ExtraBold },
                    new FontWeightItem { Name = "Black (900)", Weight = FontWeights.Black }
                };

                return items.Find(item => item.Weight == fontWeight) ??
                       items.Find(item => item.Weight == FontWeights.Normal);
            }

            // Default to Normal
            return new FontWeightItem { Name = "Normal (400)", Weight = FontWeights.Normal };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // FontWeightItem → FontWeight (from ComboBox selection)
            if (value is FontWeightItem item)
            {
                return item.Weight;
            }

            return FontWeights.Normal; // Default
        }
    }
}
