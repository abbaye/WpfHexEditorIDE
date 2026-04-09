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
using WpfHexEditor.Core.Settings.Converters;

namespace WpfHexEditor.Core.Settings.Controls
{
    /// <summary>
    /// Control generator for long properties (TextBox).
    /// Example: ByteShiftLeft
    /// </summary>
    public class LongTextBoxPropertyControl : IPropertyControl
    {
        public FrameworkElement CreateControl()
        {
            return new TextBox
            {
                Margin = new Thickness(0, 0, 0, 8),
                MinWidth = 100
            };
        }

        public Binding CreateBinding(PropertyMetadata metadata)
        {
            return new Binding(metadata.PropertyName)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus // Update when TextBox loses focus
                // Note: WPF handles long ↔ string conversion automatically, no converter needed
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
}
