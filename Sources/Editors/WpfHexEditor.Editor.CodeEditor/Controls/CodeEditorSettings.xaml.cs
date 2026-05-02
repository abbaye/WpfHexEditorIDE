//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// CodeEditor Settings Panel - Auto-generated Configuration UI
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
// Pattern: Uses unified BaseEditorSettings<T> helper (composition)
//////////////////////////////////////////////

using System.Linq;
using System.Windows.Controls;
using WpfHexEditor.Core.Settings;
using WpfHexEditor.Editor.CodeEditor.Properties;

namespace WpfHexEditor.Editor.CodeEditor.Controls
{
    /// <summary>
    /// Complete CodeEditor Settings Panel - Auto-generated via Reflection.
    /// Uses unified BaseEditorSettings helper with DynamicSettingsGenerator.
    ///
    /// <para><b>Usage:</b></para>
    /// <para>
    /// 1. Set CodeEditorControl property to the CodeEditor instance you want to configure
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
    public partial class CodeEditorSettings : UserControl
    {
        private CodeEditor _codeEditorControl;
        private BaseEditorSettings<CodeEditor> _baseHelper;

        /// <summary>
        /// Reference to the CodeEditor control to configure
        /// </summary>
        public CodeEditor CodeEditorControl
        {
            get => _codeEditorControl;
            set
            {
                _codeEditorControl = value;

                // Generate UI if control is already loaded
                if (value != null && IsLoaded)
                {
                    _baseHelper.RegenerateUI(value);
                }
            }
        }

        public CodeEditorSettings()
        {
            InitializeComponent();

            // Initialize helper with composition
            _baseHelper = new BaseEditorSettings<CodeEditor>(
                this,
                typeof(CodeEditor),
                () => _codeEditorControl,
                () => SettingsScrollViewer);

            // Hide preview placeholder by default
            if (PreviewBorder != null)
                PreviewBorder.Visibility = System.Windows.Visibility.Collapsed;
        }

        #region Public API - Delegate to Helper

        /// <summary>
        /// Gets the current CodeEditor settings as JSON string.
        /// The consumer is responsible for persisting this (file, database, registry, etc.)
        /// </summary>
        /// <returns>JSON string containing all settings</returns>
        public string GetSettingsJson() => _baseHelper.GetSettingsJson();

        /// <summary>
        /// Loads CodeEditor settings from JSON string.
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
            if (_codeEditorControl == null)
            {
                System.Windows.MessageBox.Show(CodeEditorResources.CodeEditorSettings_NoEditorMessage, CodeEditorResources.CodeEditorSettings_NoEditorTitle,
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                CodeEditorResources.CodeEditorSettings_ResetConfirmMessage,
                CodeEditorResources.CodeEditorSettings_ResetConfirmTitle,
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
                    FileName = "CodeEditorSettings.json",
                    Title = CodeEditorResources.CodeEditorSettings_ExportDialogTitle
                };

                if (saveDialog.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(saveDialog.FileName, json);
                    System.Windows.MessageBox.Show(string.Format(CodeEditorResources.CodeEditorSettings_ExportSuccessMessage, saveDialog.FileName),
                        CodeEditorResources.CodeEditorSettings_ExportSuccessTitle, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(CodeEditorResources.CodeEditorSettings_ExportFailedMessage, ex.Message),
                    CodeEditorResources.CodeEditorSettings_ExportFailedTitle, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                    Title = CodeEditorResources.CodeEditorSettings_ImportDialogTitle
                };

                if (openDialog.ShowDialog() == true)
                {
                    var json = System.IO.File.ReadAllText(openDialog.FileName);
                    LoadSettingsJson(json);
                    System.Windows.MessageBox.Show(string.Format(CodeEditorResources.CodeEditorSettings_ImportSuccessMessage, openDialog.FileName),
                        CodeEditorResources.CodeEditorSettings_ImportSuccessTitle, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(CodeEditorResources.CodeEditorSettings_ImportFailedMessage, ex.Message),
                    CodeEditorResources.CodeEditorSettings_ImportFailedTitle, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Reset all CodeEditor properties to their default values
        /// </summary>
        private void ResetToDefaults()
        {
            if (_codeEditorControl == null)
                return;

            // Get all DPs from the CodeEditor type
            var type = typeof(CodeEditor);
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(System.Windows.DependencyProperty));

            foreach (var field in fields)
            {
                var dp = (System.Windows.DependencyProperty)field.GetValue(null);
                if (dp != null)
                {
                    // Clear local value to restore default
                    _codeEditorControl.ClearValue(dp);
                }
            }

            System.Windows.MessageBox.Show(CodeEditorResources.CodeEditorSettings_ResetCompleteMessage,
                CodeEditorResources.CodeEditorSettings_ResetCompleteTitle, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        #endregion
    }
}
