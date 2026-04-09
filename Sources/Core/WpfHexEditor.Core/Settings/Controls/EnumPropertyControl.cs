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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfHexEditor.Core.Settings;
using PropertyMetadata = WpfHexEditor.Core.Settings.PropertyMetadata;

namespace WpfHexEditor.Core.Settings.Controls
{
    /// <summary>
    /// Control generator for enum properties (ComboBox).
    /// Example: EditMode, CaretMode, CopyPasteMode, ByteSpacerPosition
    /// </summary>
    public class EnumPropertyControl : IPropertyControl
    {
        public FrameworkElement CreateControl()
        {
            var comboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 8),
                SelectedValuePath = "Tag"
            };

            return comboBox;
        }

        public Binding CreateBinding(PropertyMetadata metadata)
        {
            // Use EnumToComboBoxConverter from existing Converters.cs
            var binding = new Binding(metadata.PropertyName)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            // Note: Converter will be set in DynamicSettingsGenerator
            // where it can access StaticResource

            return binding;
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
        /// Populates ComboBox with enum values.
        /// Called by DynamicSettingsGenerator after control creation.
        /// </summary>
        public void PopulateEnumValues(ComboBox comboBox, PropertyMetadata metadata)
        {
            if (metadata.Constraints?.EnumType == null)
                return;

            var enumType = metadata.Constraints.EnumType;
            var enumValues = Enum.GetValues(enumType);

            foreach (var value in enumValues)
            {
                var item = new ComboBoxItem
                {
                    Content = GetEnumDisplayName(value),
                    Tag = value  // Store actual enum value, not string
                };
                comboBox.Items.Add(item);
            }
        }

        private string GetEnumDisplayName(object enumValue)
        {
            // Split camelCase: "HexaString" → "Hexa String"
            var name = enumValue.ToString();
            return System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", " $1").Trim();
        }
    }
}
