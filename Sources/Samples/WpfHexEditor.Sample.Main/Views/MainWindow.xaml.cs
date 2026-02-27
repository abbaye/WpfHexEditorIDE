//////////////////////////////////////////////
// Apache 2.0  2026
// HexEditor V2 - Main Window
// Modern interface without Ribbon (VS 2026 inspired design)
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Sample.Main.ViewModels;
using WpfHexEditor.Sample.Main.Views.Dialogs;
using Microsoft.Win32;

namespace WpfHexEditor.Sample.Main.Views
{
    /// <summary>
    /// Main Window - Modern hex editor experience
    /// Features: Classic menu + toolbar, modern panels, VS 2026 inspired design
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ModernMainWindowViewModel _viewModel;
        private double _parsedFieldsPanelSavedWidth = 350;
        private Window _parsedFieldsFloatWindow;

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

            // Connecter le ParsedFieldsPanel externe au HexEditor (via DP)
            HexEditorControl.ConnectParsedFieldsPanel(ParsedFieldsPanelControl);

            // Connecter le BarChartPanel externe au HexEditor (via DP IByteDistributionPanel)
            HexEditorControl.ByteDistributionPanel = ByteDistChartPanel;

            // Tear-off flottant style VS2022
            ParsedFieldsPanelControl.TitleBarDragStarted += ParsedFieldsPanel_TitleBarDragStarted;

            // Synchroniser l'état initial des colonnes ParsedFieldsPanel
            _viewModel.PropertyChanged += ViewModel_ParsedFieldsPanelVisibilityChanged;
            ApplyParsedFieldsPanelVisibility(_viewModel.IsParsedFieldsPanelVisible);

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
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Subscribe to HexEditor operation state changes
            if (HexEditorControl != null)
            {
                HexEditorControl.OperationStateChanged += HexEditor_OperationStateChanged;
            }

            // Auto-load HexEditor settings from previous session
            try
            {
                var json = Properties.Settings.Default.HexEditorSettings;
                System.Diagnostics.Debug.WriteLine($"[MainWindow_Loaded] HexEditorSettings JSON length: {json?.Length ?? 0}");

                if (!string.IsNullOrEmpty(json) && HexEditorSettingsPanel != null)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow_Loaded] Calling LoadSettingsJson...");
                    HexEditorSettingsPanel.LoadSettingsJson(json);
                    System.Diagnostics.Debug.WriteLine("[MainWindow_Loaded] LoadSettingsJson completed");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow_Loaded] Skipping load: json empty={string.IsNullOrEmpty(json)}, panel null={HexEditorSettingsPanel == null}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow_Loaded] ERROR loading settings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"  Stack trace: {ex.StackTrace}");
            }
        }

        private void ParsedFieldsPanel_TitleBarDragStarted(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_parsedFieldsFloatWindow != null) return; // déjà flottant

            // Position de la souris relative au panel et à l'écran
            var mouseInPanel = e.GetPosition(ParsedFieldsPanelControl);
            var screenPos    = ParsedFieldsPanelControl.PointToScreen(mouseInPanel);

            // Masquer la colonne dans le grid
            ApplyParsedFieldsPanelVisibility(false);

            // Détacher le panel de son parent (grid)
            var parentGrid = ParsedFieldsPanelControl.Parent as Grid;
            parentGrid?.Children.Remove(ParsedFieldsPanelControl);

            // Créer la float window positionnée sous le curseur (header aligné)
            _parsedFieldsFloatWindow = new Window
            {
                Title         = "Parsed Fields",
                Content       = ParsedFieldsPanelControl,
                Width         = Math.Max(_parsedFieldsPanelSavedWidth, 350),
                Height        = ActualHeight,
                WindowStyle   = WindowStyle.SingleBorderWindow,
                ResizeMode    = ResizeMode.CanResizeWithGrip,
                ShowInTaskbar = false,
                Owner         = this,
                Left          = screenPos.X - mouseInPanel.X,
                Top           = screenPos.Y - mouseInPanel.Y,
            };

            _parsedFieldsFloatWindow.Closed += ParsedFieldsFloatWindow_Closed;
            _parsedFieldsFloatWindow.Show();

            // DragMove immédiat — le bouton souris est encore pressé
            _parsedFieldsFloatWindow.DragMove();
        }

        private void ParsedFieldsFloatWindow_Closed(object sender, EventArgs e)
        {
            _parsedFieldsFloatWindow.Closed -= ParsedFieldsFloatWindow_Closed;

            // Détacher le panel de la float window
            _parsedFieldsFloatWindow.Content = null;
            _parsedFieldsFloatWindow = null;

            // Remettre le panel dans le grid (colonne 2)
            var contentGrid = HexEditorControl.Parent as Grid;
            if (contentGrid != null)
            {
                contentGrid.Children.Add(ParsedFieldsPanelControl);
                Grid.SetColumn(ParsedFieldsPanelControl, 2);
            }

            ApplyParsedFieldsPanelVisibility(true);
        }

        private void ViewModel_ParsedFieldsPanelVisibilityChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModernMainWindowViewModel.IsParsedFieldsPanelVisible))
                ApplyParsedFieldsPanelVisibility(_viewModel.IsParsedFieldsPanelVisible);
        }

        private void ApplyParsedFieldsPanelVisibility(bool visible)
        {
            // Si le panel est en float, ne pas toucher à la colonne
            if (_parsedFieldsFloatWindow != null) return;

            if (visible)
            {
                ParsedFieldsColumn.Width = new GridLength(_parsedFieldsPanelSavedWidth);
                ParsedFieldsColumn.MinWidth = 150;
                SplitterColumn.Width = new GridLength(5);
                ParsedFieldsPanelControl.Visibility = Visibility.Visible;
                ParsedFieldsSplitter.Visibility = Visibility.Visible;
            }
            else
            {
                // Sauvegarder la largeur actuelle avant de masquer
                if (ParsedFieldsColumn.ActualWidth > 0)
                    _parsedFieldsPanelSavedWidth = ParsedFieldsColumn.ActualWidth;
                ParsedFieldsColumn.Width = new GridLength(0);
                ParsedFieldsColumn.MinWidth = 0;
                SplitterColumn.Width = new GridLength(0);
                ParsedFieldsPanelControl.Visibility = Visibility.Collapsed;
                ParsedFieldsSplitter.Visibility = Visibility.Collapsed;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save UI state BEFORE auto-saving HexEditor settings
            _viewModel?.SaveUIState();

            // Auto-save HexEditor settings for next session
            try
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow_Closing] Starting HexEditor settings save...");

                if (HexEditorSettingsPanel != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow_Closing] Calling GetSettingsJson...");
                    var json = HexEditorSettingsPanel.GetSettingsJson();
                    System.Diagnostics.Debug.WriteLine($"[MainWindow_Closing] Got JSON ({json?.Length ?? 0} chars)");

                    Properties.Settings.Default.HexEditorSettings = json;
                    Properties.Settings.Default.Save();
                    System.Diagnostics.Debug.WriteLine($"[MainWindow_Closing] Settings saved successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow_Closing] HexEditorSettingsPanel is null!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow_Closing] ERROR saving settings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"  Stack trace: {ex.StackTrace}");
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

        #region TBL Character Table Support

        /// <summary>
        /// Load a TBL (Character Table) file for custom encoding
        /// </summary>
        private void LoadTblMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select a TBL file",
                Filter = "TBL Files (*.tbl)|*.tbl|All Files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Load the TBL file (HexEditor will update status bar automatically)
                HexEditorControl.LoadTBLFile(openFileDialog.FileName);

                // Enable the Close TBL menu item
                CloseTblMenuItem.IsEnabled = true;
            }
        }

        /// <summary>
        /// Close the current TBL file and return to ASCII encoding
        /// </summary>
        private void CloseTblMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Close the TBL (HexEditor will update status bar automatically)
            HexEditorControl.CloseTBL();

            // Disable the Close TBL menu item
            CloseTblMenuItem.IsEnabled = false;
        }

        /// <summary>
        /// Opens the Relative Search dialog for ROM encoding discovery
        /// </summary>
        private void RelativeSearchMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Show the Relative Search dialog (HexEditor API)
            HexEditorControl.ShowRelativeSearchDialog();
        }

        /// <summary>
        /// Opens the Advanced Search dialog with 5 modes (TEXT, HEX, WILDCARD, TBL TEXT, RELATIVE)
        /// </summary>
        private void AdvancedSearchMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Show the Advanced Search dialog (HexEditor API)
            // Supports ultra-performant searching with TBL support and encoding discovery
            HexEditorControl.ShowAdvancedSearchDialog(this);
        }

        /// <summary>
        /// Opens the JSON Editor Demo window with JsonEditor and Settings Panel
        /// </summary>
        private void JsonEditorDemoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var demoWindow = new JsonEditorDemoWindow
            {
                Owner = this
            };
            demoWindow.Show();
        }

        #endregion
    }
}
