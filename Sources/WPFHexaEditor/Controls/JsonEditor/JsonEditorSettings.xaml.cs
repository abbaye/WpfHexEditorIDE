//////////////////////////////////////////////
// Apache 2.0  - 2026
// JsonEditor Settings Panel - Auto-generated Configuration UI
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
// Pattern: Uses unified BaseEditorSettings<T> helper (composition)
//////////////////////////////////////////////

using System.Windows.Controls;
using WpfHexaEditor.Controls;

namespace WpfHexaEditor.Controls.JsonEditor
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
    }
}
