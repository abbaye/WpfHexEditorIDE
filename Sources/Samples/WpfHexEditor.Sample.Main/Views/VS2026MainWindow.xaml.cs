//////////////////////////////////////////////
// Apache 2.0  2026
// HexEditor V2 - VS 2026 Preview Style Main Window
// Modern futuristic interface without Ribbon
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using WpfHexEditor.Sample.Main.ViewModels;
using WpfHexEditor.Sample.Main.Views.Dialogs;

namespace WpfHexEditor.Sample.Main.Views
{
    /// <summary>
    /// VS 2026 Preview Style Main Window - The futuristic hex editor experience
    /// Features: Floating command bar, neumorphic sidebar, glassmorphism, fluid animations
    /// </summary>
    public partial class VS2026MainWindow : Window
    {
        private readonly ModernMainWindowViewModel _viewModel;

        public VS2026MainWindow()
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
                    System.Diagnostics.Debug.WriteLine($"[VS2026MainWindow.Constructor] Restored culture to: {culture.Name}");
                }
                catch (CultureNotFoundException)
                {
                    // Fallback to default
                }
            }

            InitializeComponent();

            // Initialize ViewModel (reusing existing ModernMainWindowViewModel for compatibility)
            _viewModel = new ModernMainWindowViewModel();
            DataContext = _viewModel;

            // Wire up components
            _viewModel.SetHexEditor(HexEditorControl);

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

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup
            HexEditorControl?.Close();
            base.OnClosed(e);
        }
    }
}
