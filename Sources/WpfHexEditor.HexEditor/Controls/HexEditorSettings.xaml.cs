//////////////////////////////////////////////
// Apache 2.0  - 2026
// HexEditor Settings Panel - Auto-generated Configuration UI
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
// Pattern: Uses unified BaseEditorSettings<T> helper (composition)
//////////////////////////////////////////////

using System.Windows.Controls;

namespace WpfHexEditor.HexEditor.Controls
{
    /// <summary>
    /// Complete HexEditor Settings Panel - Auto-generated via Reflection.
    /// Uses unified BaseEditorSettings helper with DynamicSettingsGenerator.
    ///
    /// <para><b>Usage:</b></para>
    /// <para>
    /// 1. Set HexEditorControl property to the HexEditor instance you want to configure
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
    public partial class HexEditorSettings : UserControl
    {
        private HexEditor _hexEditorControl;
        private BaseEditorSettings<HexEditor> _baseHelper;

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
                    _baseHelper.RegenerateUI(value);
                }
            }
        }

        public HexEditorSettings()
        {
            InitializeComponent();

            // Initialize helper with composition
            _baseHelper = new BaseEditorSettings<HexEditor>(
                this,
                typeof(HexEditor),
                () => _hexEditorControl,
                () => SettingsScrollViewer);
        }

        #region Public API - Delegate to Helper

        /// <summary>
        /// Gets the current HexEditor settings as JSON string.
        /// The consumer is responsible for persisting this (file, database, registry, etc.)
        /// </summary>
        /// <returns>JSON string containing all settings</returns>
        public string GetSettingsJson() => _baseHelper.GetSettingsJson();

        /// <summary>
        /// Loads HexEditor settings from JSON string.
        /// </summary>
        /// <param name="json">JSON string containing settings (obtained from GetSettingsJson)</param>
        public void LoadSettingsJson(string json) => _baseHelper.LoadSettingsJson(json);

        #endregion
    }
}
