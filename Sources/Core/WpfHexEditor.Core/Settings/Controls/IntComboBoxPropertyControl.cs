//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// GNU Affero General Public License v3.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfHexEditor.Core.Settings;
using PropertyMetadata = WpfHexEditor.Core.Settings.PropertyMetadata;

namespace WpfHexEditor.Core.Settings.Controls
{
    /// <summary>
    /// Control generator for int properties with fixed allowed values (ComboBox).
    /// Example: BytePerLine with values [8, 16, 32, 64]
    /// </summary>
    public class IntComboBoxPropertyControl : IPropertyControl
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
            var binding = new Binding(metadata.PropertyName)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            // Note: Converter (BytesPerLineToIndexConverter) will be set in DynamicSettingsGenerator
            // if needed for the specific property

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
        /// Populates ComboBox with allowed values from constraints.
        /// Called by DynamicSettingsGenerator after control creation.
        /// </summary>
        public void PopulateAllowedValues(ComboBox comboBox, PropertyMetadata metadata)
        {
            if (metadata.Constraints?.AllowedValues == null)
                return;

            foreach (var value in metadata.Constraints.AllowedValues)
            {
                var item = new ComboBoxItem
                {
                    Content = value.ToString(),
                    Tag = value  // CRITICAL FIX: Keep as int object, not string, for proper converter binding
                };
                comboBox.Items.Add(item);
            }
        }
    }
}
