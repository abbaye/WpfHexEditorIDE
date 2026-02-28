// Apache 2.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfHexEditor.Core.Settings;
using PropertyMetadata = WpfHexEditor.Core.Settings.PropertyMetadata;

namespace WpfHexEditor.HexEditor.Settings.Controls
{
    /// <summary>
    /// Control generator for boolean properties (CheckBox).
    /// Example: ShowOffset, ReadOnlyMode, AllowContextMenu
    /// </summary>
    public class BoolPropertyControl : IPropertyControl
    {
        public FrameworkElement CreateControl()
        {
            return new CheckBox
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        public Binding CreateBinding(PropertyMetadata metadata)
        {
            return new Binding(metadata.PropertyName)
            {
                Mode = metadata.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
        }

        public TextBlock CreateLabel(PropertyMetadata metadata)
        {
            // CheckBox includes label in Content, so no separate label needed
            return null;
        }
    }
}
