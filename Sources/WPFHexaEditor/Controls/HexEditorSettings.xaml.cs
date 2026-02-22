//////////////////////////////////////////////
// Apache 2.0  - 2026
// HexEditor V2 - Complete Settings Panel
// All HexEditor properties exposed for configuration
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexaEditor.Core.Settings;

namespace WpfHexaEditor.Controls
{
    /// <summary>
    /// Complete HexEditor Settings Panel - Auto-generated via Reflection.
    /// Uses DynamicSettingsGenerator to create UI from [Category] attributes.
    ///
    /// <para><b>Persistence:</b></para>
    /// <para>
    /// Use GetSettingsJson() to retrieve settings as JSON string, then persist it
    /// (file, database, registry, etc.) according to your application's requirements.
    /// Use LoadSettingsJson(json) to restore settings from a saved JSON string.
    /// </para>
    /// </summary>
    public partial class HexEditorSettings : UserControl
    {
        private HexEditor _hexEditorControl;
        private SettingsStateService _stateService;
        private DynamicSettingsGenerator _generator;

        /// <summary>
        /// Reference to the HexEditor control to configure
        /// </summary>
        public HexEditor HexEditorControl
        {
            get => _hexEditorControl;
            set
            {
                _hexEditorControl = value;

                // Generate UI if control is already loaded
                if (value != null && IsLoaded)
                {
                    System.Diagnostics.Debug.WriteLine("[HexEditorSettings] Control already loaded, regenerating UI immediately");
                    RegenerateUI();
                }
                else if (value != null)
                {
                    System.Diagnostics.Debug.WriteLine("[HexEditorSettings] Control not yet loaded, will regenerate UI in Loaded event");
                }
            }
        }

        public HexEditorSettings()
        {
            InitializeComponent();

            // Initialize services
            _stateService = new SettingsStateService(typeof(HexEditor));
            _generator = new DynamicSettingsGenerator(typeof(HexEditor));

            // Generate UI when control is loaded
            Loaded += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[HexEditorSettings] Loaded event fired");
                if (HexEditorControl != null)
                {
                    System.Diagnostics.Debug.WriteLine("[HexEditorSettings] HexEditorControl exists, calling RegenerateUI from Loaded event");
                    RegenerateUI();
                }
            };
        }

        /// <summary>
        /// Regenerates the entire settings panel UI using reflection-based discovery.
        /// Called when HexEditorControl is set or when Loaded event fires.
        /// </summary>
        private void RegenerateUI()
        {
            if (HexEditorControl == null)
            {
                System.Diagnostics.Debug.WriteLine("[HexEditorSettings] RegenerateUI called but HexEditorControl is null");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[HexEditorSettings] Generating settings panel...");

                // Generate the complete panel
                var panel = _generator.GenerateSettingsPanel();
                panel.DataContext = HexEditorControl;

                // Set as content of ScrollViewer
                SettingsScrollViewer.Content = panel;
                SettingsScrollViewer.UpdateLayout();

                // Connect button handlers
                WireUpButtonHandlers(panel);

                System.Diagnostics.Debug.WriteLine("[HexEditorSettings] UI generation complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HexEditorSettings] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Wires up button click handlers for Save/Load/Reset buttons.
        /// </summary>
        private void WireUpButtonHandlers(Panel rootPanel)
        {
            // Find buttons by name
            var saveButton = FindElementByName<Button>(rootPanel, "SaveStateButton");
            var loadButton = FindElementByName<Button>(rootPanel, "LoadStateButton");
            var resetButton = FindElementByName<Button>(rootPanel, "ResetButton");

            if (saveButton != null)
            {
                saveButton.Click += SaveStateButton_Click;
                System.Diagnostics.Debug.WriteLine("  Wired SaveStateButton");
            }

            if (loadButton != null)
            {
                loadButton.Click += LoadStateButton_Click;
                System.Diagnostics.Debug.WriteLine("  Wired LoadStateButton");
            }

            if (resetButton != null)
            {
                resetButton.Click += ResetButton_Click;
                System.Diagnostics.Debug.WriteLine("  Wired ResetButton");
            }
        }

        /// <summary>
        /// Finds a control by name in the visual tree.
        /// </summary>
        private T FindElementByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is T element && element.Name == name)
                    return element;

                var result = FindElementByName<T>(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        #region Public API for Settings Persistence

        /// <summary>
        /// Gets the current HexEditor settings as JSON string.
        /// The consumer is responsible for persisting this (file, database, registry, etc.)
        /// </summary>
        /// <returns>JSON string containing all settings</returns>
        /// <exception cref="InvalidOperationException">If HexEditorControl is not set</exception>
        public string GetSettingsJson()
        {
            if (HexEditorControl == null)
                throw new InvalidOperationException("HexEditorControl is not set");

            return _stateService.SaveState(HexEditorControl);
        }

        /// <summary>
        /// Loads HexEditor settings from JSON string.
        /// </summary>
        /// <param name="json">JSON string containing settings (obtained from GetSettingsJson)</param>
        /// <exception cref="InvalidOperationException">If HexEditorControl is not set</exception>
        /// <exception cref="ArgumentException">If JSON is null or empty</exception>
        public void LoadSettingsJson(string json)
        {
            if (HexEditorControl == null)
                throw new InvalidOperationException("HexEditorControl is not set");

            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("JSON string cannot be null or empty", nameof(json));

            _stateService.LoadState(HexEditorControl, json);
        }

        #endregion

        #region Button Event Handlers

        private void SaveStateButton_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditorControl == null) return;

            try
            {
                var json = GetSettingsJson();

                // For the core library, we just show a message
                // Consumer applications should implement their own persistence
                MessageBox.Show(
                    $"Settings JSON ready ({json.Length} chars).\n\n" +
                    "Use GetSettingsJson() to retrieve and persist this configuration.",
                    "Settings Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                System.Diagnostics.Debug.WriteLine($"[SaveState] Generated JSON ({json.Length} chars)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SaveState] ERROR: {ex.Message}");
                MessageBox.Show(
                    $"Failed to generate settings JSON:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadStateButton_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditorControl == null) return;

            try
            {
                // For the core library, we show instructions
                // Consumer applications should implement their own persistence loading
                MessageBox.Show(
                    "In your application, load the JSON and call LoadSettingsJson(json).",
                    "Load Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load settings:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditorControl == null) return;

            var result = MessageBox.Show(
                "Reset all settings to defaults?\n\nThis will reset all properties to their DependencyProperty default values.",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Get all properties with [Category] attribute
                    var discoveryService = new PropertyDiscoveryService(typeof(HexEditor));
                    var properties = discoveryService.DiscoverProperties();

                    // Reset each property to its default value
                    foreach (var metadata in properties)
                    {
                        try
                        {
                            var propInfo = HexEditorControl.GetType().GetProperty(metadata.PropertyName);
                            if (propInfo != null && propInfo.CanWrite && metadata.DefaultValue != null)
                            {
                                propInfo.SetValue(HexEditorControl, metadata.DefaultValue);
                                System.Diagnostics.Debug.WriteLine($"  Reset {metadata.PropertyName} to {metadata.DefaultValue}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"  Failed to reset {metadata.PropertyName}: {ex.Message}");
                        }
                    }

                    MessageBox.Show(
                        "Settings reset to defaults",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to reset settings:\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}
