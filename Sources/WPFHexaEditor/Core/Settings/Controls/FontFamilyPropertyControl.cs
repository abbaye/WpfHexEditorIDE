// Apache 2.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfHexaEditor.Core.Settings.Controls
{
    /// <summary>
    /// Control generator for FontFamily properties.
    /// Creates ComboBox with common monospace and system fonts.
    /// </summary>
    public class FontFamilyPropertyControl : IPropertyControl
    {
        public FrameworkElement CreateControl()
        {
            var comboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 8),
                ItemsSource = GetCommonFonts()
            };

            // Custom item template to show font name in its own font
            comboBox.ItemTemplate = CreateFontItemTemplate();

            return comboBox;
        }

        public Binding CreateBinding(PropertyMetadata metadata)
        {
            return new Binding(metadata.PropertyName)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Converter = new FontFamilyConverter()
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
        /// Returns list of common monospace and system fonts
        /// </summary>
        private List<string> GetCommonFonts()
        {
            var commonFonts = new List<string>
            {
                // Monospace fonts (best for code editing)
                "Consolas",
                "Courier New",
                "Lucida Console",
                "Monaco",
                "Menlo",
                "DejaVu Sans Mono",
                "Ubuntu Mono",
                "Fira Code",
                "JetBrains Mono",
                "Cascadia Code",
                "Source Code Pro",

                // Common system fonts
                "Segoe UI",
                "Arial",
                "Tahoma",
                "Verdana",
                "Times New Roman",
                "Georgia"
            };

            // Filter to only fonts installed on system
            var installedFonts = Fonts.SystemFontFamilies.Select(f => f.Source).ToHashSet();
            return commonFonts.Where(f => installedFonts.Contains(f)).ToList();
        }

        /// <summary>
        /// Creates DataTemplate that shows each font in its own typeface
        /// </summary>
        private DataTemplate CreateFontItemTemplate()
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextBlock));

            // Bind Text to the font name string
            factory.SetBinding(TextBlock.TextProperty, new Binding("."));

            // Bind FontFamily to the font name (so it displays in its own font)
            factory.SetBinding(TextBlock.FontFamilyProperty, new Binding(".")
            {
                Converter = new StringToFontFamilyConverter()
            });

            template.VisualTree = factory;
            return template;
        }
    }

    /// <summary>
    /// Converter between FontFamily object and string name
    /// </summary>
    public class FontFamilyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // FontFamily → string (for ComboBox display)
            if (value is FontFamily fontFamily)
            {
                return fontFamily.Source;
            }
            return "Consolas"; // Default
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // string → FontFamily (from ComboBox selection)
            if (value is string fontName)
            {
                return new FontFamily(fontName);
            }
            return new FontFamily("Consolas"); // Default
        }
    }

    /// <summary>
    /// Converter from string to FontFamily (for item template binding)
    /// </summary>
    public class StringToFontFamilyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string fontName)
            {
                return new FontFamily(fontName);
            }
            return new FontFamily("Consolas");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
