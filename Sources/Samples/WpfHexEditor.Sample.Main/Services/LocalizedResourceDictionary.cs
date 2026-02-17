using System;
using System.Collections;
using System.Resources;
using System.Windows;
using WpfHexEditor.Sample.Main.Properties;

namespace WpfHexEditor.Sample.Main.Services
{
    /// <summary>
    /// A dynamic ResourceDictionary that populates itself with localized strings
    /// from the application's Resources.resx files and automatically updates
    /// when the culture changes.
    ///
    /// Usage in XAML:
    /// &lt;Text="{DynamicResource Menu_File_Open}"/&gt;
    /// </summary>
    public class LocalizedResourceDictionary : ResourceDictionary
    {
        private readonly ResourceManager _resourceManager;

        public LocalizedResourceDictionary()
        {
            _resourceManager = Resources.ResourceManager;

            // Subscribe to culture changes
            DynamicResourceManager.CultureChanged += OnCultureChanged;

            // Initial population with current culture
            UpdateResources();

            System.Diagnostics.Debug.WriteLine("[LocalizedResourceDictionary] Created and populated with current culture resources");
        }

        /// <summary>
        /// Called when the application culture changes.
        /// Clears and repopulates the dictionary with the new culture's strings.
        /// </summary>
        private void OnCultureChanged(object? sender, CultureChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalizedResourceDictionary] Culture changed from '{e.OldCulture.Name}' to '{e.NewCulture.Name}', updating resources...");
            UpdateResources();
        }

        /// <summary>
        /// Populates the ResourceDictionary with all localized strings from the current culture.
        /// Uses proper fallback chain to parent cultures.
        /// </summary>
        private void UpdateResources()
        {
            // Clear existing resources
            Clear();

            try
            {
                var culture = DynamicResourceManager.CurrentCulture;

                // IMPORTANT: Get the resource set from the INVARIANT culture to get ALL possible keys
                // This ensures we have a complete list of resource keys
                var invariantResourceSet = _resourceManager.GetResourceSet(
                    System.Globalization.CultureInfo.InvariantCulture,
                    createIfNotExists: true,
                    tryParents: false);

                if (invariantResourceSet != null)
                {
                    int count = 0;
                    // For each key in the invariant culture, use GetString() which properly handles fallback
                    foreach (DictionaryEntry entry in invariantResourceSet)
                    {
                        if (entry.Key is string key)
                        {
                            // GetString() with the specific culture will automatically fall back to parent cultures
                            var value = _resourceManager.GetString(key, culture);
                            if (!string.IsNullOrEmpty(value))
                            {
                                this[key] = value;
                                count++;
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[LocalizedResourceDictionary] Loaded {count} resources for culture '{culture.Name}' (with fallback)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalizedResourceDictionary] WARNING: No invariant resource set found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalizedResourceDictionary] ERROR updating resources: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[LocalizedResourceDictionary] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Cleanup: unsubscribe from events when the dictionary is disposed.
        /// </summary>
        ~LocalizedResourceDictionary()
        {
            DynamicResourceManager.CultureChanged -= OnCultureChanged;
        }
    }
}
