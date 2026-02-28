//////////////////////////////////////////////
// Apache 2.0  - 2026
// JsonEditor Settings Panel - Auto-generated Configuration UI
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
// Pattern: Uses unified BaseEditorSettings<T> helper (composition)
//////////////////////////////////////////////

using System.Linq;
using System.Windows.Controls;
using WpfHexEditor.HexEditor.Controls;

namespace WpfHexEditor.JsonEditor.Controls
{
    /// <summary>
    /// Complete JsonEditor Settings Panel - Auto-generated via Reflection.
    /// Uses unified BaseEditorSettings helper with DynamicSettingsGenerator.
    ///
    /// <para><b>Usage:</b></para>
    /// <para>
    /// 1. Set JsonEditorControl property to the JsonEditor instance you want to configure
    /// 2. The UI will be auto-generated based on [Category] attributes on Dependency Properties
    /// 3. Use GetSettingsJson() to save settings, LoadSettingsJson(json) to restore
    /// </para>
    ///
    /// <para><b>Persistence:</b></para>
    /// <para>
    /// Use GetSettingsJson() to retrieve settings as JSON string, then persist it
    /// (file, database, registry, etc.) according to your application's requirements.
    /// Use LoadSettingsJson(json) to restore settings from a saved JSON string.
    /// </para>
    /// </summary>
    public partial class JsonEditorSettings : UserControl
    {
        private JsonEditor _jsonEditorControl;
        private BaseEditorSettings<JsonEditor> _baseHelper;

        /// <summary>
        /// Reference to the JsonEditor control to configure
        /// </summary>
        public JsonEditor JsonEditorControl
        {
            get => _jsonEditorControl;
            set
            {
                _jsonEditorControl = value;

                // Generate UI if control is already loaded
                if (value != null && IsLoaded)
                {
                    _baseHelper.RegenerateUI(value);
                }
            }
        }

        public JsonEditorSettings()
        {
            InitializeComponent();

            // Initialize helper with composition
            _baseHelper = new BaseEditorSettings<JsonEditor>(
                this,
                typeof(JsonEditor),
                () => _jsonEditorControl,
                () => SettingsScrollViewer);

            // Hide preview placeholder by default
            if (PreviewBorder != null)
                PreviewBorder.Visibility = System.Windows.Visibility.Collapsed;
        }

        #region Public API - Delegate to Helper

        /// <summary>
        /// Gets the current JsonEditor settings as JSON string.
        /// The consumer is responsible for persisting this (file, database, registry, etc.)
        /// </summary>
        /// <returns>JSON string containing all settings</returns>
        public string GetSettingsJson() => _baseHelper.GetSettingsJson();

        /// <summary>
        /// Loads JsonEditor settings from JSON string.
        /// </summary>
        /// <param name="json">JSON string containing settings (obtained from GetSettingsJson)</param>
        public void LoadSettingsJson(string json) => _baseHelper.LoadSettingsJson(json);

        #endregion

        #region Button Handlers

        /// <summary>
        /// Reset all settings to default values
        /// </summary>
        private void ResetButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_jsonEditorControl == null)
            {
                System.Windows.MessageBox.Show("No JsonEditor control is connected.", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to reset all settings to their default values?",
                "Confirm Reset",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                // Reset all properties to their default values
                ResetToDefaults();
            }
        }

        /// <summary>
        /// Export settings to JSON file
        /// </summary>
        private void ExportButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                var json = GetSettingsJson();

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".json",
                    FileName = "JsonEditorSettings.json",
                    Title = "Export Settings"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(saveDialog.FileName, json);
                    System.Windows.MessageBox.Show($"Settings exported successfully to:\n{saveDialog.FileName}",
                        "Export Successful", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to export settings:\n{ex.Message}",
                    "Export Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Import settings from JSON file
        /// </summary>
        private void ImportButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Import Settings"
                };

                if (openDialog.ShowDialog() == true)
                {
                    var json = System.IO.File.ReadAllText(openDialog.FileName);
                    LoadSettingsJson(json);
                    System.Windows.MessageBox.Show($"Settings imported successfully from:\n{openDialog.FileName}",
                        "Import Successful", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to import settings:\n{ex.Message}",
                    "Import Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Reset all JsonEditor properties to their default values
        /// </summary>
        private void ResetToDefaults()
        {
            if (_jsonEditorControl == null)
                return;

            // Get all DPs from the JsonEditor type
            var type = typeof(JsonEditor);
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(System.Windows.DependencyProperty));

            foreach (var field in fields)
            {
                var dp = (System.Windows.DependencyProperty)field.GetValue(null);
                if (dp != null)
                {
                    // Clear local value to restore default
                    _jsonEditorControl.ClearValue(dp);
                }
            }

            System.Windows.MessageBox.Show("All settings have been reset to their default values.",
                "Reset Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        #endregion
    }
}
