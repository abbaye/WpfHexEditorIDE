//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

//////////////////////////////////////////////
// Apache 2.0  - 2025
// Localization infrastructure for dynamic language switching
//////////////////////////////////////////////

using System;
using System.Collections;
using System.Globalization;
using System.Resources;
using System.Windows;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// A ResourceDictionary that automatically updates its resources when the culture changes.
    /// This enables instant language switching without application restart.
    /// </summary>
    public class LocalizedResourceDictionary : ResourceDictionary
    {
        private readonly ResourceManager _resourceManager;

        /// <summary>
        /// Event fired when culture is changed, allowing control to refresh UI
        /// </summary>
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        public static event EventHandler<CultureChangedEventArgs>? CultureChanged;
#pragma warning restore CS8632

        private static CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

        /// <summary>
        /// Gets or sets the current culture for the control
        /// </summary>
        public static CultureInfo CurrentCulture
        {
            get => _currentCulture;
            set
            {
                if (_currentCulture.Name != value.Name)
                {
                    var oldCulture = _currentCulture;
                    _currentCulture = value;

                    CultureChanged?.Invoke(null, new CultureChangedEventArgs(oldCulture, value));
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the LocalizedResourceDictionary class
        /// </summary>
        public LocalizedResourceDictionary()
        {
            _resourceManager = Properties.Resources.ResourceManager;

            // Subscribe to our own culture change events
            CultureChanged += OnCultureChanged;

            // Load initial resources
            UpdateResources();
        }

        /// <summary>
        /// Handles culture change events by updating all resources
        /// </summary>
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        private void OnCultureChanged(object? sender, CultureChangedEventArgs e)
#pragma warning restore CS8632
        {
            UpdateResources();
        }

        /// <summary>
        /// Updates all resources from the resource manager for the current culture.
        /// Uses the invariant culture as the master key list, then resolves each key
        /// with proper per-key fallback (e.g., fr-CA → fr → invariant).
        /// This ensures all keys are present even when a translation file is incomplete.
        /// </summary>
        private void UpdateResources()
        {
            // Clear existing resources
            Clear();

            try
            {
                var culture = CurrentCulture;

                // Get the invariant ResourceSet as the master list of all available keys.
                // This guarantees every key (all 510 in Resources.resx) is enumerated,
                // even for cultures with partial translations (e.g., fr-CA has 342/510).
                var invariantSet = _resourceManager.GetResourceSet(
                    CultureInfo.InvariantCulture,
                    createIfNotExists: true,
                    tryParents: false);

                if (invariantSet == null)
                {
                    System.Diagnostics.Debug.WriteLine("[LocalizedResourceDictionary] WARNING: No invariant resource set found");
                    return;
                }

                int count = 0;
                foreach (DictionaryEntry entry in invariantSet)
                {
                    if (entry.Key is string key)
                    {
                        // GetString() walks the fallback chain per-key: fr-CA → fr → invariant
                        var value = _resourceManager.GetString(key, culture);
                        if (!string.IsNullOrEmpty(value))
                        {
                            this[key] = value;
                            count++;
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[LocalizedResourceDictionary] Loaded {count} resources for culture '{culture.Name}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalizedResourceDictionary] ERROR updating resources: {ex.Message}");
            }
        }

        /// <summary>
        /// Changes the current culture and notifies all LocalizedResourceDictionary instances
        /// </summary>
        /// <param name="newCulture">The new culture to switch to</param>
        public static void ChangeCulture(CultureInfo newCulture)
        {
            CurrentCulture = newCulture;
        }
    }

    /// <summary>
    /// Event arguments for culture change events
    /// </summary>
    public class CultureChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous culture
        /// </summary>
        public CultureInfo OldCulture { get; }

        /// <summary>
        /// Gets the new culture
        /// </summary>
        public CultureInfo NewCulture { get; }

        /// <summary>
        /// Initializes a new instance of the CultureChangedEventArgs class
        /// </summary>
        public CultureChangedEventArgs(CultureInfo oldCulture, CultureInfo newCulture)
        {
            OldCulture = oldCulture;
            NewCulture = newCulture;
        }
    }
}
