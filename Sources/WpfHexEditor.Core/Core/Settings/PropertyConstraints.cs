// Apache 2.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;
using System.Collections.Generic;
using System.Windows.Data;

namespace WpfHexaEditor.Core.Settings
{
    /// <summary>
    /// Validation constraints for a property.
    /// Used to configure UI controls (e.g., Slider min/max, ComboBox allowed values).
    /// </summary>
    public class PropertyConstraints
    {
        /// <summary>
        /// Minimum value for int/double properties (used for Slider.Minimum)
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Maximum value for int/double properties (used for Slider.Maximum)
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Step value for int/double properties (used for Slider.TickFrequency)
        /// </summary>
        public double? StepValue { get; set; }

        /// <summary>
        /// Enum type for enum properties (used to generate ComboBox items)
        /// </summary>
        public Type EnumType { get; set; }

        /// <summary>
        /// Allowed values for properties with fixed options.
        /// Example: BytePerLine can only be [8, 16, 32, 64]
        /// </summary>
        public List<int> AllowedValues { get; set; }

        /// <summary>
        /// Custom converter for special binding scenarios.
        /// Example: BytesPerLineToIndexConverter for BytePerLine property
        /// </summary>
        public IValueConverter CustomConverter { get; set; }
    }
}
