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

            // Load default theme
            LoadTheme(_viewModel.SettingsViewModel.SelectedTheme);
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
            LoadTheme(themeName);
        }

        private void LoadTheme(string themeName)
        {
            try
            {
                // Clear existing theme
                var existingTheme = Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/Themes/"));

                if (existingTheme != null)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(existingTheme);
                }

                // Load new theme
                var themeUri = new Uri($"pack://application:,,,/WpfHexEditor.Sample.Main;component/Resources/Themes/{themeName}.xaml", UriKind.Absolute);
                var themeDictionary = new ResourceDictionary { Source = themeUri };
                Application.Current.Resources.MergedDictionaries.Add(themeDictionary);

                // Force refresh of all bindings
                InvalidateVisual();
                UpdateLayout();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load theme {themeName}: {ex.Message}");
            }
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
                LoadTheme(themeName);
            }
        }

        private void LanguageOptions_Click(object sender, RoutedEventArgs e)
        {
            var currentCulture = Thread.CurrentThread.CurrentUICulture;

            var dialog = new OptionsDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                var selectedCulture = dialog.SelectedCulture;

                // Check if language actually changed
                if (selectedCulture.Name == currentCulture.Name)
                {
                    MessageBox.Show(
                        string.Format(Properties.Resources.Message_LanguageUnchanged_Text, selectedCulture.NativeName),
                        Properties.Resources.Message_LanguageUnchanged_Title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Save the selected language
                System.Diagnostics.Debug.WriteLine($"[LanguageOptions_Click] Saving culture: '{selectedCulture.Name}'");
                WpfHexEditor.Sample.Main.Properties.Settings.Default.PreferredCulture = selectedCulture.Name;
                WpfHexEditor.Sample.Main.Properties.Settings.Default.Save();
                System.Diagnostics.Debug.WriteLine($"[LanguageOptions_Click] Settings saved successfully");

                // Inform the user
                MessageBox.Show(
                    string.Format(Properties.Resources.Message_LanguageChanged_Text, selectedCulture.NativeName),
                    Properties.Resources.Message_LanguageChanged_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup
            HexEditorControl?.Close();
            base.OnClosed(e);
        }
    }
}
