// Apache 2.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;

namespace WpfHexEditor.Core.Settings
{
    /// <summary>
    /// Attribute to specify min/max range for numeric properties.
    /// Used by PropertyDiscoveryService to populate PropertyConstraints.
    /// Applied to properties to control Slider min/max values in generated settings UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RangeAttribute : Attribute
    {
        /// <summary>
        /// Minimum value
        /// </summary>
        public double Minimum { get; }

        /// <summary>
        /// Maximum value
        /// </summary>
        public double Maximum { get; }

        /// <summary>
        /// Optional step value for slider tick frequency (0 or negative = not set)
        /// </summary>
        public double Step { get; set; } = 0.0;

        /// <summary>
        /// Creates a range constraint for numeric properties
        /// </summary>
        /// <param name="minimum">Minimum allowed value</param>
        /// <param name="maximum">Maximum allowed value</param>
        public RangeAttribute(double minimum, double maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }
    }
}
