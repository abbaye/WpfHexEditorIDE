// Apache 2.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace WpfHexaEditor.Core.Settings
{
    /// <summary>
    /// Service that discovers properties via reflection.
    /// Scans a type (HexEditor) for properties with [Category] attribute and DependencyProperty backing.
    /// </summary>
    public class PropertyDiscoveryService
    {
        private readonly Type _targetType;

        public PropertyDiscoveryService(Type targetType)
        {
            _targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        }

        /// <summary>
        /// Discovers all properties with [Category] attribute that have a DependencyProperty backing.
        /// Returns sorted list: by Category, then DisplayOrder, then PropertyName.
        /// </summary>
        public List<PropertyMetadata> DiscoverProperties()
        {
            var properties = new List<PropertyMetadata>();

            // 1. Get all public instance properties
            var propInfos = _targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var propInfo in propInfos)
            {
                // 2. Check if property has [Category] attribute
                var categoryAttr = propInfo.GetCustomAttribute<CategoryAttribute>();
                if (categoryAttr == null)
                    continue;

                // 3. Verify there's a corresponding DependencyProperty static field
                // DependencyProperty fields are named "{PropertyName}Property" (e.g., ShowOffsetProperty)
                var dpField = _targetType.GetField(
                    $"{propInfo.Name}Property",
                    BindingFlags.Public | BindingFlags.Static);

                if (dpField == null || !typeof(DependencyProperty).IsAssignableFrom(dpField.FieldType))
                    continue; // Skip properties without DP backing

                // Get the actual DependencyProperty instance
                var dp = dpField.GetValue(null) as DependencyProperty;
                if (dp == null)
                    continue;

                // 3.5. Skip Brush properties (too complex to handle with simple ColorPicker)
                // Brush can be SolidColorBrush, LinearGradientBrush, etc.
                if (typeof(System.Windows.Media.Brush).IsAssignableFrom(propInfo.PropertyType))
                    continue;

                // 4. Check if this is a read-only dependency property
                // For WPF, we need to check: CLR property setter, DependencyPropertyKey, AND [ReadOnly] attribute
                bool isReadOnly = !propInfo.CanWrite ||
                                  IsReadOnlyDependencyProperty(dp, _targetType) ||
                                  HasReadOnlyAttribute(propInfo);

                // 4. Create metadata
                var metadata = new PropertyMetadata
                {
                    PropertyName = propInfo.Name,
                    Category = categoryAttr.Category,
                    PropertyType = propInfo.PropertyType,
                    IsReadOnly = isReadOnly,
                    DefaultValue = GetDefaultValue(dpField),
                    DisplayOrder = GetDisplayOrder(propInfo),
                    Constraints = BuildConstraints(propInfo)
                };

                properties.Add(metadata);
            }

            // 5. Sort: by category, then display order, then property name
            return properties
                .OrderBy(p => GetCategoryOrder(p.Category)) // Custom category ordering
                .ThenBy(p => p.DisplayOrder)
                .ThenBy(p => p.PropertyName)
                .ToList();
        }

        /// <summary>
        /// Groups discovered properties by category.
        /// Returns Dictionary where key=category name, value=list of properties in that category.
        /// Supports hierarchical categories using dot notation (e.g., "Colors.Selection").
        /// Top-level categories are returned with their full path.
        /// </summary>
        public Dictionary<string, List<PropertyMetadata>> GroupByCategory()
        {
            return DiscoverProperties()
                .GroupBy(p => p.Category)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Parses a category string into parent and subcategory.
        /// E.g., "Colors.Selection" → ("Colors", "Selection")
        ///       "Display" → ("Display", null)
        /// </summary>
        public (string Parent, string Subcategory) ParseCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
                return (string.Empty, null);

            var parts = category.Split(new[] { '.' }, 2);
            if (parts.Length == 2)
                return (parts[0], parts[1]);

            return (parts[0], null);
        }

        /// <summary>
        /// Groups properties by parent category and subcategory hierarchy.
        /// Returns nested dictionary: ParentCategory → Subcategory → Properties
        /// Properties without subcategories are stored with null subcategory key.
        /// </summary>
        public Dictionary<string, Dictionary<string, List<PropertyMetadata>>> GroupByHierarchy()
        {
            var result = new Dictionary<string, Dictionary<string, List<PropertyMetadata>>>();
            var properties = DiscoverProperties();

            foreach (var prop in properties)
            {
                var (parent, subcategory) = ParseCategory(prop.Category);

                // Ensure parent category exists
                if (!result.ContainsKey(parent))
                    result[parent] = new Dictionary<string, List<PropertyMetadata>>();

                // Use empty string instead of null for direct properties (easier to work with)
                var subKey = subcategory ?? string.Empty;

                // Ensure subcategory list exists
                if (!result[parent].ContainsKey(subKey))
                    result[parent][subKey] = new List<PropertyMetadata>();

                result[parent][subKey].Add(prop);
            }

            return result;
        }

        /// <summary>
        /// Gets the default value from DependencyProperty.DefaultMetadata
        /// </summary>
        private object GetDefaultValue(FieldInfo dpField)
        {
            try
            {
                var dp = (DependencyProperty)dpField.GetValue(null);
                return dp?.DefaultMetadata?.DefaultValue;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets display order from [DisplayOrder] attribute if present, otherwise 999.
        /// Note: DisplayOrder attribute is optional and will be created in Phase 6.
        /// For now, returns 999 for all properties (alphabetical sorting).
        /// </summary>
        private int GetDisplayOrder(PropertyInfo propInfo)
        {
            // Optional: Check for [DisplayOrder(n)] attribute
            // For Phase 1-5, we don't have this attribute yet, so return default
            // This can be implemented in Phase 6

            return 999; // Default order = alphabetical
        }

        /// <summary>
        /// Builds PropertyConstraints based on property type and name.
        /// Handles special cases like BytePerLine, enums, and numeric ranges.
        /// </summary>
        private PropertyConstraints BuildConstraints(PropertyInfo propInfo)
        {
            var constraints = new PropertyConstraints();

            // Special case: BytePerLine has fixed allowed values
            if (propInfo.Name == "BytePerLine")
            {
                constraints.AllowedValues = new List<int> { 8, 16, 32, 64 };
                // Note: BytesPerLineToIndexConverter will be added in control generation
                return constraints;
            }

            // Enum types
            if (propInfo.PropertyType.IsEnum)
            {
                constraints.EnumType = propInfo.PropertyType;
                return constraints;
            }

            // Numeric types: look for range constraints
            if (propInfo.PropertyType == typeof(int) || propInfo.PropertyType == typeof(double))
            {
                // Check for [Range] attribute first
                var rangeAttr = propInfo.GetCustomAttribute<RangeAttribute>();
                if (rangeAttr != null)
                {
                    constraints.MinValue = rangeAttr.Minimum;
                    constraints.MaxValue = rangeAttr.Maximum;
                    if (rangeAttr.Step.HasValue)
                        constraints.StepValue = rangeAttr.Step.Value;
                    return constraints;
                }

                // Default ranges for common properties (if no [Range] attribute)
                if (propInfo.Name == "ZoomScale" || propInfo.Name.Contains("Zoom"))
                {
                    constraints.MinValue = 0.5;
                    constraints.MaxValue = 3.0;
                    constraints.StepValue = 0.1;
                }
                else if (propInfo.Name.Contains("Progress"))
                {
                    constraints.MinValue = 0.0;
                    constraints.MaxValue = 1.0;
                    constraints.StepValue = 0.01;
                }
            }

            return constraints;
        }

        /// <summary>
        /// Custom category ordering for better UX.
        /// StatusBar and Display first, then others alphabetically.
        /// </summary>
        private int GetCategoryOrder(string category)
        {
            return category switch
            {
                "StatusBar" => 1,
                "Display" => 2,
                "Colors" => 3,
                "ScrollMarkers" => 4,
                "Behavior" => 5,
                "Data" => 6,
                "Visual" => 7,
                "Keyboard" => 8,
                _ => 99 // Unknown categories last
            };
        }

        /// <summary>
        /// Checks if a DependencyProperty is read-only by looking for a DependencyPropertyKey field.
        /// Read-only DependencyProperties are registered with DependencyProperty.RegisterReadOnly()
        /// and have a corresponding {PropertyName}PropertyKey field.
        /// </summary>
        private bool IsReadOnlyDependencyProperty(DependencyProperty dp, Type targetType)
        {
            if (dp == null) return false;

            // Try to find a DependencyPropertyKey field with the pattern {PropertyName}PropertyKey
            // For example: IsFileOrStreamLoadedPropertyKey for IsFileOrStreamLoadedProperty
            var dpName = dp.Name;
            var keyFieldName = $"{dpName}PropertyKey";

            var keyField = targetType.GetField(
                keyFieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            // If a DependencyPropertyKey field exists, this is a read-only DP
            if (keyField != null && keyField.FieldType.Name == "DependencyPropertyKey")
            {
                return true;
            }

            // Alternative: Check if the DependencyProperty's metadata indicates read-only
            // WPF doesn't have a direct IsReadOnly property, but we can check the registration
            return false;
        }

        /// <summary>
        /// Checks if a property has the [ReadOnly(true)] attribute.
        /// </summary>
        private bool HasReadOnlyAttribute(PropertyInfo propInfo)
        {
            var readOnlyAttr = propInfo.GetCustomAttribute<System.ComponentModel.ReadOnlyAttribute>();
            return readOnlyAttr != null && readOnlyAttr.IsReadOnly;
        }
    }
}
