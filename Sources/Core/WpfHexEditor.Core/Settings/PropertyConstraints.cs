// ==========================================================
// Project: WpfHexEditor.Core
// File: PropertyConstraints.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Defines validation constraints for a discovered property in the dynamic settings
//     UI system. Carries min/max value, step, allowed enum type, and a list of
//     valid string values used to configure sliders and combo boxes.
//
// Architecture Notes:
//     Pure data model — no WPF dependencies in the constraints themselves.
//     Populated by PropertyDiscoveryService from [Range] and [ValidValues] attributes.
//     Consumed by the auto-generated settings panel builder.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows.Data;

namespace WpfHexEditor.Core.Settings
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
