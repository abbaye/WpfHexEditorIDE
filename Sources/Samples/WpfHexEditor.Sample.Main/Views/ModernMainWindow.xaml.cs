//////////////////////////////////////////////
// Apache 2.0  2026
// HexEditor V2 - Modern Main Window
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Ribbon;
using WpfHexEditor.Sample.Main.ViewModels;
using WpfHexEditor.Sample.Main.Views.Dialogs;

namespace WpfHexEditor.Sample.Main.Views
{
    /// <summary>
    /// Modern Main Window - The ultimate 2026 hex editor experience
    /// </summary>
    public partial class ModernMainWindow : RibbonWindow
    {
        private readonly ModernMainWindowViewModel _viewModel;

        public ModernMainWindow()
        {
            // CRITICAL: Restore culture for this window BEFORE InitializeComponent
            // This ensures the window's resources are loaded with the correct culture
            var cultureName = WpfHexEditor.Sample.Main.Properties.Settings.Default.PreferredCulture;
            if (!string.IsNullOrEmpty(cultureName))
            {
                try
                {
                    var culture = new CultureInfo(cultureName);
                    Thread.CurrentThread.CurrentCulture = culture;
                    Thread.CurrentThread.CurrentUICulture = culture;
                    System.Diagnostics.Debug.WriteLine($"[ModernMainWindow.Constructor] Restored culture to: {culture.Name}");
                }
                catch (CultureNotFoundException)
                {
                    // Fallback to default
                }
            }

            InitializeComponent();

            // Initialize ViewModel
            _viewModel = new ModernMainWindowViewModel();
            DataContext = _viewModel;

            // Wire up components
            _viewModel.SetHexEditor(HexEditorControl);

            // Connect HexEditor to Settings Panel for property bindings
            HexEditorSettingsPanel.HexEditorControl = HexEditorControl;

            // Wire up file operations
            _viewModel.FileOpenRequested += OnFileOpenRequested;
            _viewModel.FileSaveRequested += OnFileSaveRequested;

            // Wire up theme changes
            _viewModel.SettingsViewModel.ThemeChanged += OnThemeChanged;

            // Sync HexEditor colors with current theme (theme loaded by ThemeManager.Initialize())
            Services.ThemeManager.SyncHexEditorColors(HexEditorControl);
        }

        private void OnFileOpenRequested(object sender, string filePath)
        {
            try
            {
                HexEditorControl.FileName = filePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Properties.Resources.Message_FileOpenError, ex.Message),
                    Properties.Resources.Message_ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnFileSaveRequested(object sender, EventArgs e)
        {
            try
            {
                HexEditorControl.SubmitChanges();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Properties.Resources.Message_FileSaveError, ex.Message),
                    Properties.Resources.Message_ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnThemeChanged(object sender, string themeName)
        {
            // Theme loading is now handled by ThemeManager
            // Just sync HexEditor colors with the new theme
            Services.ThemeManager.SyncHexEditorColors(HexEditorControl);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Ribbon.RibbonMenuItem menuItem &&
                menuItem.Tag is string themeName)
            {
                Services.ThemeManager.ChangeTheme(themeName, persistent: true);
            }
        }

        private void LanguageOptions_Click(object sender, RoutedEventArgs e)
        {
            // Open the Options dialog
            // Language changes happen instantly when user selects from the list
            // All culture management is handled by DynamicResourceManager
            var dialog = new OptionsDialog
            {
                Owner = this
            };

            dialog.ShowDialog();
            // Note: No need to check DialogResult or show confirmation MessageBox
            // Language changes are applied instantly as user clicks in the list!
        }

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup
            HexEditorControl?.Close();
            base.OnClosed(e);
        }
    }
}
