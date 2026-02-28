// Apache 2.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;
using System.ComponentModel;
using System.Windows.Data;

namespace WpfHexEditor.Core.Settings
{
    /// <summary>
    /// Metadata for a discovered property via reflection.
    /// Encapsulates all information needed to generate UI controls dynamically.
    /// </summary>
    public class PropertyMetadata
    {
        /// <summary>
        /// Property name (e.g., "ShowOffset", "SelectionFirstColor")
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Category from [Category] attribute (e.g., "Display", "Colors")
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Property type (bool, Color, enum, int, double)
        /// </summary>
        public Type PropertyType { get; set; }

        /// <summary>
        /// Default value from DependencyProperty metadata
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Display name for UI (supports i18n)
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Description/tooltip text
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Display order within category (lower = earlier)
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Is the property read-only? (from PropertyInfo.CanWrite)
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Validation constraints (Min/Max, AllowedValues, CustomConverter)
        /// </summary>
        public PropertyConstraints Constraints { get; set; }

        /// <summary>
        /// Creates a human-readable display name by splitting camelCase
        /// </summary>
        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(DisplayName))
                return DisplayName;

            // Split camelCase: "ShowOffset" → "Show Offset"
            return System.Text.RegularExpressions.Regex.Replace(
                PropertyName,
                "([A-Z])",
                " $1").Trim();
        }
    }
}
