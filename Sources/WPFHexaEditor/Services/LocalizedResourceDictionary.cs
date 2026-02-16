//////////////////////////////////////////////
// Apache 2.0  - 2025
// Localization infrastructure for dynamic language switching
//////////////////////////////////////////////

using System;
using System.Collections;
using System.Globalization;
using System.Resources;
using System.Windows;

namespace WpfHexaEditor.Services
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
        public static event EventHandler<CultureChangedEventArgs>? CultureChanged;

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

                    System.Diagnostics.Debug.WriteLine(
                        $"[LocalizedResourceDictionary] Culture changed from '{oldCulture.Name}' to '{value.Name}'");

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

            System.Diagnostics.Debug.WriteLine(
                $"[LocalizedResourceDictionary] Initialized with culture: {CurrentCulture.Name}");
        }

        /// <summary>
        /// Handles culture change events by updating all resources
        /// </summary>
        private void OnCultureChanged(object? sender, CultureChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LocalizedResourceDictionary] OnCultureChanged: '{e.OldCulture.Name}' -> '{e.NewCulture.Name}'");

            UpdateResources();
        }

        /// <summary>
        /// Updates all resources from the resource manager for the current culture
        /// </summary>
        private void UpdateResources()
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LocalizedResourceDictionary] UpdateResources called for culture: {CurrentCulture.Name}");

            // Clear existing resources
            Clear();

            try
            {
                // Get the resource set for the current culture
                var resourceSet = _resourceManager.GetResourceSet(CurrentCulture, createIfNotExists: true, tryParents: true);

                if (resourceSet == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[LocalizedResourceDictionary] WARNING: ResourceSet is null for culture {CurrentCulture.Name}");
                    return;
                }

                // Add all resources to the dictionary
                var count = 0;
                foreach (DictionaryEntry entry in resourceSet)
                {
                    if (entry.Key is string key && entry.Value is string value)
                    {
                        this[key] = value;
                        count++;
                    }
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[LocalizedResourceDictionary] Loaded {count} resources for culture {CurrentCulture.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LocalizedResourceDictionary] ERROR loading resources: {ex.Message}");
            }
        }

        /// <summary>
        /// Changes the current culture and notifies all LocalizedResourceDictionary instances
        /// </summary>
        /// <param name="newCulture">The new culture to switch to</param>
        public static void ChangeCulture(CultureInfo newCulture)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LocalizedResourceDictionary.ChangeCulture] Changing culture to: {newCulture.Name}");

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
