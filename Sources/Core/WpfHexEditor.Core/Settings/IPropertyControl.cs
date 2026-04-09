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

namespace WpfHexEditor.Core.Settings
{
    /// <summary>
    /// Interface for property control generation.
    /// Each property type (bool, Color, enum, etc.) has its own implementation.
    /// This enables polymorphism and testability via Strategy pattern.
    /// </summary>
    public interface IPropertyControl
    {
        /// <summary>
        /// Creates the WPF control (CheckBox, ComboBox, Slider, ColorPicker, etc.)
        /// </summary>
        FrameworkElement CreateControl();

        /// <summary>
        /// Creates the TwoWay binding for the property.
        /// The binding will be applied to the appropriate property of the control.
        /// </summary>
        Binding CreateBinding(PropertyMetadata metadata);

        /// <summary>
        /// Creates the label TextBlock for the control (if needed).
        /// Some controls (CheckBox) include the label in their Content.
        /// Returns null if no separate label is needed.
        /// </summary>
        TextBlock CreateLabel(PropertyMetadata metadata);
    }
}
