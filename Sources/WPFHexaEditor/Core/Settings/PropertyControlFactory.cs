// Apache 2.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;
using System.Windows.Media;
using WpfHexaEditor.Core.Settings.Controls;

namespace WpfHexaEditor.Core.Settings
{
    /// <summary>
    /// Factory that creates the appropriate IPropertyControl based on property type.
    /// Implements Factory pattern for control generation.
    /// </summary>
    public static class PropertyControlFactory
    {
        /// <summary>
        /// Creates the appropriate control implementation for the given property metadata.
        /// </summary>
        /// <param name="metadata">Property metadata containing type and constraints</param>
        /// <returns>IPropertyControl implementation for the property type</returns>
        /// <exception cref="NotSupportedException">Thrown if property type is not supported</exception>
        public static IPropertyControl Create(PropertyMetadata metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            // 1. bool → CheckBox
            if (metadata.PropertyType == typeof(bool))
                return new BoolPropertyControl();

            // 2. Color → ColorPicker (custom UserControl)
            if (metadata.PropertyType == typeof(Color))
                return new ColorPropertyControl();

            // 2b. Brush → BrushPicker (wraps ColorPicker with Brush conversion)
            if (metadata.PropertyType == typeof(Brush) || metadata.PropertyType.IsSubclassOf(typeof(Brush)))
                return new BrushPropertyControl();

            // 3. enum → ComboBox with enum values
            if (metadata.PropertyType.IsEnum)
                return new EnumPropertyControl();

            // 4. int → ComboBox (if AllowedValues) or TextBox (otherwise)
            if (metadata.PropertyType == typeof(int))
            {
                if (metadata.Constraints?.AllowedValues != null && metadata.Constraints.AllowedValues.Count > 0)
                    return new IntComboBoxPropertyControl(); // BytePerLine case
                else
                    return new IntTextBoxPropertyControl(); // Generic int input
            }

            // 5. double → Slider + TextBlock display
            if (metadata.PropertyType == typeof(double))
                return new DoubleSliderPropertyControl();

            // 6. long → TextBox
            if (metadata.PropertyType == typeof(long))
                return new LongTextBoxPropertyControl();

            // 7. string → TextBox
            if (metadata.PropertyType == typeof(string))
                return new StringTextBoxPropertyControl();

            // Unsupported type
            throw new NotSupportedException(
                $"Property type '{metadata.PropertyType.Name}' is not supported. " +
                $"Supported types: bool, Color, Brush, enum, int, double, long, string.");
        }
    }
}
