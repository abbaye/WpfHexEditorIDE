//////////////////////////////////////////////
// Apache 2.0  - 2026
// File Diff Dialog - Side-by-side comparison window
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.HexEditor;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.Models.Comparison;
using WpfHexEditor.Core.Services;

namespace WpfHexEditor.Panels.FileOps
{
    /// <summary>
    /// Dialog for side-by-side file comparison
    /// </summary>
    public partial class FileDiffDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
    {
        private WpfHexEditor.HexEditor.HexEditor _editor1;
        private WpfHexEditor.HexEditor.HexEditor _editor2;
        private List<FileDifference> _differences;
        private int _currentDiffIndex = -1;
        private bool _syncScrolling = true;
        private FileDiffService _diffService;

        public FileDiffDialog()
        {
            InitializeComponent();
            _diffService = new FileDiffService();

            // Create hex editors
            CreateHexEditors();

            // Register keyboard shortcuts
            RegisterCommands();
        }

        #region Initialization

        private void CreateHexEditors()
        {
            // Create editor 1
            _editor1 = new WpfHexEditor.HexEditor.HexEditor
            {
                ReadOnlyMode = true,
                BytePerLine = 16
            };
            File1EditorContainer.Children.Add(_editor1);

            // Create editor 2
            _editor2 = new WpfHexEditor.HexEditor.HexEditor
            {
                ReadOnlyMode = true,
                BytePerLine = 16
            };
            File2EditorContainer.Children.Add(_editor2);

            // Note: Scroll synchronization via SelectionChanged event
            // ScrollChanged event doesn't exist in HexEditor
        }

        private void RegisterCommands()
        {
            // F7 = Previous Difference
            CommandBindings.Add(new CommandBinding(
                new RoutedCommand("PrevDiff", typeof(FileDiffDialog)),
                (s, e) => NavigateToPreviousDiff()));

            InputBindings.Add(new KeyBinding(
                new RoutedCommand("PrevDiff", typeof(FileDiffDialog)),
                Key.F7, ModifierKeys.None));

            // F8 = Next Difference
            CommandBindings.Add(new CommandBinding(
                new RoutedCommand("NextDiff", typeof(FileDiffDialog)),
                (s, e) => NavigateToNextDiff()));

            InputBindings.Add(new KeyBinding(
                new RoutedCommand("NextDiff", typeof(FileDiffDialog)),
                Key.F8, ModifierKeys.None));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Load two files for comparison
        /// </summary>
        public void LoadFiles(string file1Path, string file2Path)
        {
            try
            {
                // Load files
                _editor1.FileName = file1Path;
                _editor2.FileName = file2Path;

                // Update headers
                File1NameTextBlock.Text = $"File 1: {Path.GetFileName(file1Path)}";
                File2NameTextBlock.Text = $"File 2: {Path.GetFileName(file2Path)}";

                // Start comparison
                CompareFiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load files:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Load two byte providers for comparison
        /// </summary>
        public void LoadProviders(ByteProvider provider1, ByteProvider provider2, string name1 = "File 1", string name2 = "File 2")
        {
            try
            {
                // Open streams
                _editor1.OpenStream(provider1.Stream);
                _editor2.OpenStream(provider2.Stream);

                // Update headers
                File1NameTextBlock.Text = name1;
                File2NameTextBlock.Text = name2;

                // Start comparison
                CompareFiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load data:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void CompareFiles()
        {
            // Check if both editors have data loaded
            if (_editor1?.GetByteProvider() == null || _editor2?.GetByteProvider() == null)
                return;

            // Show progress
            this.Cursor = Cursors.Wait;

            try
            {
                // Perform comparison
                _differences = _diffService.CompareFiles(
                    _editor1.GetByteProvider(),
                    _editor2.GetByteProvider());

                // Update UI
                DifferencesDataGrid.ItemsSource = _differences;

                // Update statistics
                var stats = _diffService.GetStatistics(_differences);
                ModifiedCountRun.Text = stats.ModifiedCount.ToString();
                DeletedCountRun.Text = stats.DeletedCount.ToString();
                AddedCountRun.Text = stats.AddedCount.ToString();

                // Highlight differences on both editors
                HighlightDifferences();

                // Update subtitle
                SubtitleTextBlock.Text = $"Found {_differences.Count} difference(s)";

                // Navigate to first difference
                if (_differences.Count > 0)
                {
                    _currentDiffIndex = 0;
                    NavigateToDifference(_currentDiffIndex);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Comparison failed:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void HighlightDifferences()
        {
            // Clear existing custom background blocks
            _editor1.CustomBackgroundService.ClearAll();
            _editor2.CustomBackgroundService.ClearAll();

            // Add highlights for each difference using custom background blocks
            foreach (var diff in _differences)
            {
                var color = GetColorForDiffType(diff.Type);

                switch (diff.Type)
                {
                    case DifferenceType.Modified:
                        _editor1.AddCustomBackgroundBlock(new CustomBackgroundBlock
                        {
                            StartOffset = diff.Offset,
                            Length = diff.Length,
                            Color = new System.Windows.Media.SolidColorBrush(color)
                        });
                        _editor2.AddCustomBackgroundBlock(new CustomBackgroundBlock
                        {
                            StartOffset = diff.Offset,
                            Length = diff.Length,
                            Color = new System.Windows.Media.SolidColorBrush(color)
                        });
                        break;

                    case DifferenceType.DeletedInSecond:
                        _editor1.AddCustomBackgroundBlock(new CustomBackgroundBlock
                        {
                            StartOffset = diff.Offset,
                            Length = diff.Length,
                            Color = new System.Windows.Media.SolidColorBrush(color)
                        });
                        break;

                    case DifferenceType.AddedInSecond:
                        _editor2.AddCustomBackgroundBlock(new CustomBackgroundBlock
                        {
                            StartOffset = diff.Offset,
                            Length = diff.Length,
                            Color = new System.Windows.Media.SolidColorBrush(color)
                        });
                        break;
                }
            }

            _editor1.RefreshView(true);
            _editor2.RefreshView(true);
        }

        private System.Windows.Media.Color GetColorForDiffType(DifferenceType type)
        {
            switch (type)
            {
                case DifferenceType.Modified:
                    return System.Windows.Media.Color.FromRgb(255, 250, 205); // Yellow
                case DifferenceType.DeletedInSecond:
                    return System.Windows.Media.Color.FromRgb(255, 182, 193); // Pink
                case DifferenceType.AddedInSecond:
                    return System.Windows.Media.Color.FromRgb(152, 251, 152); // Green
                default:
                    return System.Windows.Media.Colors.Transparent;
            }
        }

        private void NavigateToDifference(int index)
        {
            if (index < 0 || index >= _differences.Count)
                return;

            var diff = _differences[index];

            // Navigate both editors to the difference
            _editor1.SetPosition(diff.Offset, 1);
            _editor1.SelectionStart = diff.Offset;
            _editor1.SelectionStop = diff.Offset + diff.Length - 1;

            _editor2.SetPosition(diff.Offset, 1);
            _editor2.SelectionStart = diff.Offset;
            _editor2.SelectionStop = diff.Offset + diff.Length - 1;

            // Update UI
            DiffCounterTextBlock.Text = $"Difference {index + 1} of {_differences.Count}";
            DifferencesDataGrid.SelectedIndex = index;
            DifferencesDataGrid.ScrollIntoView(_differences[index]);

            // Update button states
            PrevDiffButton.IsEnabled = index > 0;
            NextDiffButton.IsEnabled = index < _differences.Count - 1;
        }

        private void NavigateToNextDiff()
        {
            if (_currentDiffIndex < _differences.Count - 1)
            {
                _currentDiffIndex++;
                NavigateToDifference(_currentDiffIndex);
            }
        }

        private void NavigateToPreviousDiff()
        {
            if (_currentDiffIndex > 0)
            {
                _currentDiffIndex--;
                NavigateToDifference(_currentDiffIndex);
            }
        }

        #endregion

        #region Event Handlers

        private void PrevDiffButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPreviousDiff();
        }

        private void NextDiffButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToNextDiff();
        }

        private void DifferencesDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DifferencesDataGrid.SelectedIndex >= 0)
            {
                _currentDiffIndex = DifferencesDataGrid.SelectedIndex;
                NavigateToDifference(_currentDiffIndex);
            }
        }

        private void SyncScrollingCheckBox_Click(object sender, RoutedEventArgs e)
        {
            _syncScrolling = !_syncScrolling;
            var button = sender as System.Windows.Controls.Button;
            if (button != null)
            {
                button.Content = _syncScrolling ? "🔗 Sync Scrolling" : "⛓️‍💥 Independent Scrolling";
            }
        }

        private void ExportReportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_differences == null || _differences.Count == 0)
            {
                MessageBox.Show("No differences to export.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = ".txt",
                FileName = "diff_report.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var stats = _diffService.GetStatistics(_differences);
                    var report = _diffService.ExportDiffReport(_differences, stats);
                    File.WriteAllText(dialog.FileName, report);

                    MessageBox.Show("Report exported successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export report:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion
    }
}
