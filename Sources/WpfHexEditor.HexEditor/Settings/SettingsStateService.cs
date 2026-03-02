//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// Apache 2.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows.Media;

namespace WpfHexEditor.Core.Settings
{
    /// <summary>
    /// Service for automatic save/load of HexEditor settings via reflection.
    /// Replaces manual 60+ line SaveState/LoadState methods with automatic discovery.
    /// </summary>
    public class SettingsStateService
    {
        private readonly PropertyDiscoveryService _discoveryService;

        public SettingsStateService(Type targetType)
        {
            _discoveryService = new PropertyDiscoveryService(targetType);
        }

        /// <summary>
        /// Saves all discovered properties to JSON string.
        /// Automatically serializes all properties with [Category] attribute.
        /// </summary>
        public string SaveState(object hexEditorInstance)
        {
            System.Diagnostics.Debug.WriteLine(">>> [SettingsStateService.SaveState] METHOD BODY ENTERED <<<");
            if (hexEditorInstance == null)
                throw new ArgumentNullException(nameof(hexEditorInstance));

            var properties = _discoveryService.DiscoverProperties();
            var state = new Dictionary<string, object>();

            foreach (var prop in properties)
            {
                try
                {
                    var propInfo = hexEditorInstance.GetType().GetProperty(prop.PropertyName);
                    if (propInfo == null || !propInfo.CanRead)
                        continue;

                    var value = propInfo.GetValue(hexEditorInstance);
                    if (value == null)
                        continue;

                    // Serialize based on type
                    if (prop.PropertyType == typeof(Color))
                    {
                        state[prop.PropertyName] = ColorToHex((Color)value);
                    }
                    else if (prop.PropertyType.IsEnum)
                    {
                        state[prop.PropertyName] = value.ToString();
                    }
                    else if (prop.PropertyType == typeof(bool) ||
                             prop.PropertyType == typeof(int) ||
                             prop.PropertyType == typeof(double) ||
                             prop.PropertyType == typeof(long))
                    {
                        state[prop.PropertyName] = value;
                    }
                    else if (prop.PropertyType == typeof(string))
                    {
                        state[prop.PropertyName] = value.ToString();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save {prop.PropertyName}: {ex.Message}");
                }
            }

            return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Loads settings from JSON string and applies them to HexEditor instance.
        /// Automatically deserializes all discovered properties.
        /// </summary>
        public void LoadState(object hexEditorInstance, string json)
        {
            System.Diagnostics.Debug.WriteLine(">>> [SettingsStateService.LoadState] METHOD BODY ENTERED <<<");
            if (hexEditorInstance == null)
                throw new ArgumentNullException(nameof(hexEditorInstance));

            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (settings == null)
                    return;

                var properties = _discoveryService.DiscoverProperties();

                foreach (var prop in properties)
                {
                    if (!settings.TryGetValue(prop.PropertyName, out var jsonValue))
                        continue;

                    try
                    {
                        var propInfo = hexEditorInstance.GetType().GetProperty(prop.PropertyName);
                        if (propInfo == null || !propInfo.CanWrite)
                            continue;

                        object value = null;

                        // Deserialize based on type
                        if (prop.PropertyType == typeof(bool))
                        {
                            value = jsonValue.GetBoolean();
                        }
                        else if (prop.PropertyType == typeof(int))
                        {
                            value = jsonValue.GetInt32();
                        }
                        else if (prop.PropertyType == typeof(double))
                        {
                            value = jsonValue.GetDouble();
                        }
                        else if (prop.PropertyType == typeof(long))
                        {
                            value = jsonValue.GetInt64();
                        }
                        else if (prop.PropertyType == typeof(Color))
                        {
                            value = HexToColor(jsonValue.GetString());
                        }
                        else if (prop.PropertyType.IsEnum)
                        {
                            value = Enum.Parse(prop.PropertyType, jsonValue.GetString());
                        }
                        else if (prop.PropertyType == typeof(string))
                        {
                            value = jsonValue.GetString();
                        }

                        if (value != null)
                        {
                            propInfo.SetValue(hexEditorInstance, value);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load {prop.PropertyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse settings JSON: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Converts Color to hex string (#AARRGGBB format).
        /// </summary>
        private string ColorToHex(Color color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// Converts hex string to Color (#AARRGGBB or #RRGGBB format).
        /// </summary>
        private Color HexToColor(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return Colors.Black; // Fallback
            }
        }
    }
}
