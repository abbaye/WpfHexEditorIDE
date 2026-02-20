//////////////////////////////////////////////
// Apache 2.0  2026
// HexEditor V2 - Main Window
// Modern interface without Ribbon (VS 2026 inspired design)
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
    /// Main Window - Modern hex editor experience
    /// Features: Classic menu + toolbar, modern panels, VS 2026 inspired design
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ModernMainWindowViewModel _viewModel;

        public MainWindow()
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
                    System.Diagnostics.Debug.WriteLine($"[MainWindow.Constructor] Restored culture to: {culture.Name}");
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

            // Connect HexEditor to Settings Panel for property bindings
            HexEditorSettingsPanel.HexEditorControl = HexEditorControl;

            // Wire up file operations
            _viewModel.FileOpenRequested += OnFileOpenRequested;
            _viewModel.FileSaveRequested += OnFileSaveRequested;

            // Wire up theme changes
            _viewModel.SettingsViewModel.ThemeChanged += OnThemeChanged;

            // Wire up language changes to open Options dialog
            _viewModel.SettingsViewModel.LanguageChanged += OnLanguageChanged;

            // Sync HexEditor colors with current theme (theme loaded by ThemeManager.Initialize())
            Services.ThemeManager.SyncHexEditorColors(HexEditorControl);

            // CRITICAL: Subscribe to operation state changes to disable UI during async operations
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Subscribe to HexEditor operation state changes
            if (HexEditorControl != null)
            {
                HexEditorControl.OperationStateChanged += HexEditor_OperationStateChanged;
            }
        }

        private void HexEditor_OperationStateChanged(object sender, WpfHexaEditor.Events.OperationStateChangedEventArgs e)
        {
            // Notify ViewModel to update command states
            _viewModel?.OnOperationStateChanged(e.IsActive);
        }

        private void OnLanguageChanged(object sender, string languageCode)
        {
            // Open the Options dialog for language selection
            ShowOptionsDialog();
        }

        private void ShowOptionsDialog_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Event handler for menu item click
            ShowOptionsDialog();
        }

        private void ShowOptionsDialog()
        {
            // Open the Options dialog (Thème, Langue)
            // Changes happen instantly when user selects from the lists
            var dialog = new OptionsDialog
            {
                Owner = this
            };
            dialog.ShowDialog();
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
