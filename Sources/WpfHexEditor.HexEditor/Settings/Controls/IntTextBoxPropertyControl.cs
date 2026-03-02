//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

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
    /// Control generator for int properties without fixed values (TextBox).
    /// Example: VisibleLines, generic int input
    /// </summary>
    public class IntTextBoxPropertyControl : IPropertyControl
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
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
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
