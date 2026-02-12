using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexaEditor.V2.Models;

namespace HexEditorV2.Sample
{
    public partial class MainWindow : Window
    {
        private string? _currentFilePath;

        public MainWindow()
        {
            InitializeComponent();
            UpdateUIState();
        }

        #region File Menu

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open file",
                Filter = "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    HexEditor.OpenFile(dialog.FileName);
                    _currentFilePath = dialog.FileName;
                    Title = $"HexEditorV2 - {System.IO.Path.GetFileName(dialog.FileName)}";
                    UpdateUIState();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open file: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded)
            {
                MessageBox.Show("No file loaded", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                HexEditor.Save();
                MessageBox.Show("File saved successfully", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded)
            {
                MessageBox.Show("No file loaded", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Save file as",
                Filter = "All files (*.*)|*.*",
                FileName = System.IO.Path.GetFileName(_currentFilePath)
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // TODO: Implement SaveAs in HexEditor
                    MessageBox.Show("SaveAs not yet implemented in HexEditorV2", "Info",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save file: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            HexEditor.Close();
            _currentFilePath = null;
            Title = "HexEditorV2 Sample";
            UpdateUIState();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Edit Menu

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditor.CanUndo)
            {
                HexEditor.Undo();
                UpdateUIState();
            }
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditor.CanRedo)
            {
                HexEditor.Redo();
                UpdateUIState();
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditor.IsFileLoaded)
            {
                HexEditor.SelectAll();
                UpdateUIState();
            }
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditor.HasSelection)
            {
                HexEditor.ClearSelection();
                UpdateUIState();
            }
        }

        private void DeleteSelection_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditor.HasSelection)
            {
                var result = MessageBox.Show(
                    $"Delete {HexEditor.SelectionLength} selected bytes?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    HexEditor.DeleteSelection();
                    UpdateUIState();
                }
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditor.HasSelection)
            {
                if (HexEditor.Copy())
                {
                }
                else
                {
                    MessageBox.Show("Failed to copy to clipboard", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CopyHex_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditor.HasSelection)
            {
                try
                {
                    // Get selection as hex string (e.g., "48 65 6C 6C 6F")
                    var selection = HexEditor.GetSelectionByteArray();
                    if (selection != null && selection.Length > 0)
                    {
                        string hexString = string.Join(" ", selection.Select(b => b.ToString("X2")));
                        Clipboard.SetText(hexString);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy as hex: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CopyAscii_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditor.HasSelection)
            {
                try
                {
                    // Get selection as ASCII string
                    var selection = HexEditor.GetSelectionByteArray();
                    if (selection != null && selection.Length > 0)
                    {
                        string asciiString = System.Text.Encoding.ASCII.GetString(selection);
                        Clipboard.SetText(asciiString);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy as ASCII: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditor.IsFileLoaded && !HexEditor.ReadOnlyMode)
            {
                try
                {
                    if (HexEditor.Paste())
                    {
                        UpdateUIState();
                    }
                    else
                    {
                        MessageBox.Show("Nothing to paste or paste failed", "Info",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to paste: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Position Menu

        private void GoToPosition_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            // Simple input dialog for position
            var dialog = new Window
            {
                Title = "Go to Position",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new System.Windows.Controls.Label { Content = "Position (decimal or 0x hex):", Margin = new Thickness(10) };
            System.Windows.Controls.Grid.SetRow(label, 0);

            var textBox = new System.Windows.Controls.TextBox { Margin = new Thickness(10, 0, 10, 10) };
            System.Windows.Controls.Grid.SetRow(textBox, 1);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
            System.Windows.Controls.Grid.SetRow(buttonPanel, 3);

            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0), IsDefault = true };
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };

            okButton.Click += (s, args) =>
            {
                string input = textBox.Text.Trim();
                long position;

                try
                {
                    // Parse hex (0x prefix) or decimal
                    if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        position = Convert.ToInt64(input.Substring(2), 16);
                    else
                        position = long.Parse(input);

                    // Validate range
                    if (position < 0 || position >= HexEditor.VirtualLength)
                    {
                        MessageBox.Show($"Position must be between 0 and {HexEditor.VirtualLength - 1}", "Invalid Position",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Set position
                    HexEditor.SetPosition(position);
                    dialog.DialogResult = true;
                    dialog.Close();
                }
                catch
                {
                    MessageBox.Show("Invalid position format. Use decimal or hex (0x prefix).", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(label);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            dialog.ShowDialog();
        }

        #endregion

        #region View Menu

        private void InsertMode_Changed(object sender, RoutedEventArgs e)
        {
            if (HexEditor == null || !HexEditor.IsFileLoaded) return;

            HexEditor.EditMode = InsertModeMenuItem.IsChecked
                ? EditMode.Insert
                : EditMode.Overwrite;

            InsertModeToggle.IsChecked = InsertModeMenuItem.IsChecked;
        }

        private void ReadOnly_Changed(object sender, RoutedEventArgs e)
        {
            if (HexEditor == null || !HexEditor.IsFileLoaded) return;

            HexEditor.ReadOnlyMode = ReadOnlyMenuItem.IsChecked;
            ReadOnlyToggle.IsChecked = ReadOnlyMenuItem.IsChecked;
        }

        private void InsertModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (HexEditor == null || !HexEditor.IsFileLoaded) return;

            InsertModeMenuItem.IsChecked = InsertModeToggle.IsChecked == true;
            HexEditor.EditMode = InsertModeToggle.IsChecked == true
                ? EditMode.Insert
                : EditMode.Overwrite;

        }

        private void ReadOnlyToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (HexEditor == null || !HexEditor.IsFileLoaded) return;

            ReadOnlyMenuItem.IsChecked = ReadOnlyToggle.IsChecked == true;
            HexEditor.ReadOnlyMode = ReadOnlyToggle.IsChecked == true;
        }

        private void BytesPerLine_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded || sender is not MenuItem menuItem)
                return;

            // Get the value from Tag
            if (int.TryParse(menuItem.Tag?.ToString(), out int bytesPerLine))
            {
                HexEditor.BytePerLine = bytesPerLine;

                // Update checkmarks (uncheck all, check selected)
                BytesPerLine8MenuItem.IsChecked = false;
                BytesPerLine16MenuItem.IsChecked = false;
                BytesPerLine32MenuItem.IsChecked = false;

                menuItem.IsChecked = true;
            }
        }

        private void ChangeSelectionColor_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            // Cycle through predefined selection colors
            var currentColor = HexEditor.SelectionFirstColor;
            HexEditor.SelectionFirstColor = currentColor == System.Windows.Media.Colors.Blue
                ? System.Windows.Media.Colors.Green
                : currentColor == System.Windows.Media.Colors.Green
                    ? System.Windows.Media.Colors.Red
                    : System.Windows.Media.Colors.Blue;
        }

        private void ChangeModifiedColor_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            // Cycle through predefined colors for modified bytes
            var currentColor = HexEditor.ByteModifiedColor;
            HexEditor.ByteModifiedColor = currentColor == System.Windows.Media.Color.FromRgb(255, 165, 0)
                ? System.Windows.Media.Colors.DarkOrange
                : currentColor == System.Windows.Media.Colors.DarkOrange
                    ? System.Windows.Media.Colors.Coral
                    : System.Windows.Media.Color.FromRgb(255, 165, 0);
        }

        private void ChangeAddedColor_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            // Cycle through predefined colors for added bytes
            var currentColor = HexEditor.ByteAddedColor;
            HexEditor.ByteAddedColor = currentColor == System.Windows.Media.Color.FromRgb(76, 175, 80)
                ? System.Windows.Media.Colors.LimeGreen
                : currentColor == System.Windows.Media.Colors.LimeGreen
                    ? System.Windows.Media.Colors.ForestGreen
                    : System.Windows.Media.Color.FromRgb(76, 175, 80);
        }

        private void ChangeDeletedColor_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            // Cycle through predefined colors for deleted bytes
            var currentColor = HexEditor.ByteDeletedColor;
            HexEditor.ByteDeletedColor = currentColor == System.Windows.Media.Color.FromRgb(244, 67, 54)
                ? System.Windows.Media.Colors.Crimson
                : currentColor == System.Windows.Media.Colors.Crimson
                    ? System.Windows.Media.Colors.DarkRed
                    : System.Windows.Media.Color.FromRgb(244, 67, 54);
        }

        private void ChangeAlternateColor_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            // Cycle through predefined colors for alternate bytes
            var currentColor = HexEditor.ForegroundSecondColor;
            HexEditor.ForegroundSecondColor = currentColor == System.Windows.Media.Colors.Blue
                ? System.Windows.Media.Colors.DarkBlue
                : currentColor == System.Windows.Media.Colors.DarkBlue
                    ? System.Windows.Media.Colors.Navy
                    : System.Windows.Media.Colors.Blue;
        }

        private void ResetColors_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            // Reset all colors to defaults (V1 compatible defaults)
            HexEditor.SelectionFirstColor = System.Windows.Media.Color.FromArgb(102, 0, 120, 212);
            HexEditor.ByteModifiedColor = System.Windows.Media.Color.FromRgb(255, 165, 0);
            HexEditor.ByteAddedColor = System.Windows.Media.Color.FromRgb(76, 175, 80);
            HexEditor.ByteDeletedColor = System.Windows.Media.Color.FromRgb(244, 67, 54);
            HexEditor.ForegroundSecondColor = System.Windows.Media.Colors.Blue;
            HexEditor.MouseOverColor = System.Windows.Media.Color.FromRgb(227, 242, 253);

            MessageBox.Show("Colors reset to defaults", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowStatusBar_Changed(object sender, RoutedEventArgs e)
        {
            if (HexEditor == null || !HexEditor.IsFileLoaded) return;
            HexEditor.ShowStatusBar = ShowStatusBarMenuItem.IsChecked;
        }

        private void ShowHeader_Changed(object sender, RoutedEventArgs e)
        {
            if (HexEditor == null || !HexEditor.IsFileLoaded) return;
            HexEditor.ShowHeader = ShowHeaderMenuItem.IsChecked;
        }

        #endregion

        #region Search Menu

        private byte[]? _lastFindData;
        private long _lastFindPosition = 0;

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            var dialog = CreateHexInputDialog("Find", "Enter hex bytes to find (e.g., 48 65 6C 6C 6F):");
            if (dialog.ShowDialog() == true && dialog.Tag is byte[] findData && findData.Length > 0)
            {
                _lastFindData = findData;
                long position = HexEditor.FindFirst(findData, HexEditor.Position);

                if (position >= 0)
                {
                    _lastFindPosition = position;
                    HexEditor.FindSelect(position, findData.Length);
                    MessageBox.Show($"Found at position {position} (0x{position:X})", "Found",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Pattern not found", "Not Found",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void FindNext_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded || _lastFindData == null) return;

            long position = HexEditor.FindNext(_lastFindData, _lastFindPosition);
            if (position >= 0)
            {
                _lastFindPosition = position;
                HexEditor.FindSelect(position, _lastFindData.Length);
            }
            else
            {
                MessageBox.Show("No more occurrences found", "Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void FindPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded || _lastFindData == null) return;

            // Find last occurrence before current position
            long position = HexEditor.FindLast(_lastFindData, _lastFindPosition - 1);
            if (position >= 0)
            {
                _lastFindPosition = position;
                HexEditor.FindSelect(position, _lastFindData.Length);
            }
            else
            {
                MessageBox.Show("No previous occurrences found", "Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded || HexEditor.ReadOnlyMode) return;

            var findDialog = CreateHexInputDialog("Replace - Find", "Enter hex bytes to find:");
            if (findDialog.ShowDialog() != true || !(findDialog.Tag is byte[] findData) || findData.Length == 0)
                return;

            var replaceDialog = CreateHexInputDialog("Replace - Replace With", "Enter replacement hex bytes:");
            if (replaceDialog.ShowDialog() != true || !(replaceDialog.Tag is byte[] replaceData))
                return;

            var result = MessageBox.Show(
                "Replace All occurrences?\n\nYes = Replace All\nNo = Replace First Only\nCancel = Cancel",
                "Replace Scope",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return;

            try
            {
                if (result == MessageBoxResult.Yes)
                {
                    int count = HexEditor.ReplaceAll(findData, replaceData);
                    MessageBox.Show($"Replaced {count} occurrence(s)", "Replace Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    long position = HexEditor.ReplaceFirst(findData, replaceData, HexEditor.Position);
                    if (position >= 0)
                    {
                        MessageBox.Show($"Replaced at position {position} (0x{position:X})", "Replaced",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Pattern not found", "Not Found",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Replace failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Window CreateHexInputDialog(string title, string prompt)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new System.Windows.Controls.Label { Content = prompt, Margin = new Thickness(10) };
            System.Windows.Controls.Grid.SetRow(label, 0);

            var textBox = new System.Windows.Controls.TextBox { Margin = new Thickness(10, 0, 10, 5) };
            System.Windows.Controls.Grid.SetRow(textBox, 1);

            var helpText = new System.Windows.Controls.TextBlock
            {
                Text = "Formats: \"48 65 6C\" or \"48656C\" or \"Hello\" (ASCII)",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 11,
                Margin = new Thickness(10, 0, 10, 10)
            };
            System.Windows.Controls.Grid.SetRow(helpText, 2);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
            System.Windows.Controls.Grid.SetRow(buttonPanel, 3);

            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0), IsDefault = true };
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };

            okButton.Click += (s, args) =>
            {
                string input = textBox.Text.Trim();
                try
                {
                    byte[] bytes = ParseHexInput(input);
                    dialog.Tag = bytes;
                    dialog.DialogResult = true;
                    dialog.Close();
                }
                catch
                {
                    MessageBox.Show("Invalid hex format. Use space-separated hex bytes (e.g., 48 65 6C) or ASCII text.",
                        "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(label);
            grid.Children.Add(textBox);
            grid.Children.Add(helpText);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            return dialog;
        }

        private byte[] ParseHexInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be empty");

            // Try hex format first (e.g., "48 65 6C" or "48656C")
            string hexOnly = input.Replace(" ", "").Replace("-", "");
            if (hexOnly.All(c => "0123456789ABCDEFabcdef".Contains(c)) && hexOnly.Length % 2 == 0)
            {
                byte[] bytes = new byte[hexOnly.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                    bytes[i] = Convert.ToByte(hexOnly.Substring(i * 2, 2), 16);
                return bytes;
            }

            // Otherwise treat as ASCII
            return System.Text.Encoding.ASCII.GetBytes(input);
        }

        #endregion

        #region Tools > Byte Operations

        private void FillWithByte_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded || HexEditor.ReadOnlyMode) return;

            var dialog = CreateByteInputDialog("Fill with Byte", "Enter byte value (hex):", "Start position:", "Length:");
            if (dialog.ShowDialog() == true && dialog.Tag is (byte byteValue, long startPos, long length))
            {
                try
                {
                    HexEditor.FillWithByte(byteValue, startPos, length);
                    MessageBox.Show($"Filled {length} bytes with 0x{byteValue:X2} starting at position {startPos}",
                        "Fill Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fill failed: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void GetByte_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            var dialog = CreatePositionInputDialog("Get Byte", "Enter position:");
            if (dialog.ShowDialog() == true && dialog.Tag is long position)
            {
                try
                {
                    byte value = HexEditor.GetByte(position);
                    MessageBox.Show(
                        $"Position: {position} (0x{position:X})\n" +
                        $"Value: 0x{value:X2} ({value})\n" +
                        $"ASCII: {(value >= 32 && value < 127 ? ((char)value).ToString() : ".")}",
                        "Byte Value",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to get byte: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SetByte_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded || HexEditor.ReadOnlyMode) return;

            var dialog = CreateSetByteDialog("Set Byte", "Position:", "New value (hex):");
            if (dialog.ShowDialog() == true && dialog.Tag is (long position, byte value))
            {
                try
                {
                    HexEditor.SetByte(position, value);
                    MessageBox.Show($"Set byte at position {position} to 0x{value:X2}", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to set byte: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private Window CreatePositionInputDialog(string title, string label)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelControl = new System.Windows.Controls.Label { Content = label, Margin = new Thickness(10) };
            System.Windows.Controls.Grid.SetRow(labelControl, 0);

            var textBox = new System.Windows.Controls.TextBox { Margin = new Thickness(10, 0, 10, 10), Text = HexEditor.Position.ToString() };
            System.Windows.Controls.Grid.SetRow(textBox, 1);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
            System.Windows.Controls.Grid.SetRow(buttonPanel, 3);

            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0), IsDefault = true };
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };

            okButton.Click += (s, args) =>
            {
                try
                {
                    string input = textBox.Text.Trim();
                    long position = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? Convert.ToInt64(input.Substring(2), 16)
                        : long.Parse(input);

                    if (position < 0 || position >= HexEditor.VirtualLength)
                    {
                        MessageBox.Show($"Position must be between 0 and {HexEditor.VirtualLength - 1}",
                            "Invalid Position", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    dialog.Tag = position;
                    dialog.DialogResult = true;
                    dialog.Close();
                }
                catch
                {
                    MessageBox.Show("Invalid position format", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(labelControl);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            return dialog;
        }

        private Window CreateSetByteDialog(string title, string posLabel, string valueLabel)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 350,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var posLabelControl = new System.Windows.Controls.Label { Content = posLabel, Margin = new Thickness(10, 10, 10, 0) };
            System.Windows.Controls.Grid.SetRow(posLabelControl, 0);

            var posTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(10, 0, 10, 10), Text = HexEditor.Position.ToString() };
            System.Windows.Controls.Grid.SetRow(posTextBox, 1);

            var valueLabelControl = new System.Windows.Controls.Label { Content = valueLabel, Margin = new Thickness(10, 0, 10, 0) };
            System.Windows.Controls.Grid.SetRow(valueLabelControl, 2);

            var valueTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(10, 0, 10, 10) };
            System.Windows.Controls.Grid.SetRow(valueTextBox, 3);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
            System.Windows.Controls.Grid.SetRow(buttonPanel, 5);

            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0), IsDefault = true };
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };

            okButton.Click += (s, args) =>
            {
                try
                {
                    string posInput = posTextBox.Text.Trim();
                    long position = posInput.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? Convert.ToInt64(posInput.Substring(2), 16)
                        : long.Parse(posInput);

                    if (position < 0 || position >= HexEditor.VirtualLength)
                    {
                        MessageBox.Show($"Position must be between 0 and {HexEditor.VirtualLength - 1}",
                            "Invalid Position", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string valueInput = valueTextBox.Text.Trim().Replace("0x", "");
                    byte value = Convert.ToByte(valueInput, 16);

                    dialog.Tag = (position, value);
                    dialog.DialogResult = true;
                    dialog.Close();
                }
                catch
                {
                    MessageBox.Show("Invalid input format", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(posLabelControl);
            grid.Children.Add(posTextBox);
            grid.Children.Add(valueLabelControl);
            grid.Children.Add(valueTextBox);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            return dialog;
        }

        private Window CreateByteInputDialog(string title, string valueLabel, string posLabel, string lengthLabel)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 350,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var valueLabelControl = new System.Windows.Controls.Label { Content = valueLabel, Margin = new Thickness(10, 10, 10, 0) };
            System.Windows.Controls.Grid.SetRow(valueLabelControl, 0);

            var valueTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(10, 0, 10, 10) };
            System.Windows.Controls.Grid.SetRow(valueTextBox, 1);

            var posLabelControl = new System.Windows.Controls.Label { Content = posLabel, Margin = new Thickness(10, 0, 10, 0) };
            System.Windows.Controls.Grid.SetRow(posLabelControl, 2);

            var posTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(10, 0, 10, 10), Text = HexEditor.Position.ToString() };
            System.Windows.Controls.Grid.SetRow(posTextBox, 3);

            var lengthLabelControl = new System.Windows.Controls.Label { Content = lengthLabel, Margin = new Thickness(10, 0, 10, 0) };
            System.Windows.Controls.Grid.SetRow(lengthLabelControl, 4);

            var lengthTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(10, 0, 10, 10), Text = "1" };
            System.Windows.Controls.Grid.SetRow(lengthTextBox, 5);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
            System.Windows.Controls.Grid.SetRow(buttonPanel, 7);

            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0), IsDefault = true };
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };

            okButton.Click += (s, args) =>
            {
                try
                {
                    string valueInput = valueTextBox.Text.Trim().Replace("0x", "");
                    byte byteValue = Convert.ToByte(valueInput, 16);

                    string posInput = posTextBox.Text.Trim();
                    long position = posInput.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? Convert.ToInt64(posInput.Substring(2), 16)
                        : long.Parse(posInput);

                    long length = long.Parse(lengthTextBox.Text.Trim());

                    if (position < 0 || position >= HexEditor.VirtualLength)
                    {
                        MessageBox.Show($"Position must be between 0 and {HexEditor.VirtualLength - 1}",
                            "Invalid Position", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (length <= 0)
                    {
                        MessageBox.Show("Length must be greater than 0",
                            "Invalid Length", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    dialog.Tag = (byteValue, position, length);
                    dialog.DialogResult = true;
                    dialog.Close();
                }
                catch
                {
                    MessageBox.Show("Invalid input format", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(valueLabelControl);
            grid.Children.Add(valueTextBox);
            grid.Children.Add(posLabelControl);
            grid.Children.Add(posTextBox);
            grid.Children.Add(lengthLabelControl);
            grid.Children.Add(lengthTextBox);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            return dialog;
        }

        #endregion

        #region Tools > Bookmarks

        private void ToggleBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            long currentPos = HexEditor.Position;
            if (HexEditor.IsBookmarked(currentPos))
            {
                HexEditor.RemoveBookmark(currentPos);
                MessageBox.Show($"Bookmark removed at position {currentPos}", "Bookmark",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                HexEditor.SetBookmark(currentPos);
                MessageBox.Show($"Bookmark set at position {currentPos}", "Bookmark",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearBookmarks_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            var result = MessageBox.Show("Clear all bookmarks?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                HexEditor.ClearAllBookmarks();
                MessageBox.Show("All bookmarks cleared", "Bookmarks",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void NextBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            long nextPos = HexEditor.GetNextBookmark(HexEditor.Position);
            if (nextPos >= 0)
            {
                HexEditor.SetPosition(nextPos);
            }
            else
            {
                MessageBox.Show("No next bookmark found", "Bookmarks",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void PreviousBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            long prevPos = HexEditor.GetPreviousBookmark(HexEditor.Position);
            if (prevPos >= 0)
            {
                HexEditor.SetPosition(prevPos);
            }
            else
            {
                MessageBox.Show("No previous bookmark found", "Bookmarks",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowBookmarks_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            long[] bookmarks = HexEditor.GetBookmarks();
            if (bookmarks.Length == 0)
            {
                MessageBox.Show("No bookmarks set", "Bookmarks",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string list = string.Join("\n", bookmarks.Select(b => $"  Position: {b} (0x{b:X})"));
            MessageBox.Show($"Bookmarks ({bookmarks.Length}):\n\n{list}", "All Bookmarks",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Tools > Character Table (TBL)

        private void LoadTBL_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            var dialog = new OpenFileDialog
            {
                Title = "Load TBL File",
                Filter = "TBL files (*.tbl)|*.tbl|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    HexEditor.LoadTBLFile(dialog.FileName);
                    MessageBox.Show($"TBL file loaded: {System.IO.Path.GetFileName(dialog.FileName)}", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    CloseTBLMenuItem.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load TBL file: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseTBL_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded) return;

            HexEditor.CloseTBL();
            MessageBox.Show("TBL file closed", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
            CloseTBLMenuItem.IsEnabled = false;
        }

        private void TblType_Click(object sender, RoutedEventArgs e)
        {
            if (!HexEditor.IsFileLoaded || sender is not MenuItem menuItem) return;

            if (int.TryParse(menuItem.Tag?.ToString(), out int typeValue))
            {
                HexEditor.TypeOfCharacterTable = (WpfHexaEditor.Core.CharacterTableType)typeValue;

                // Update checkmarks
                TblAsciiMenuItem.IsChecked = false;
                TblEbcdicSpecialMenuItem.IsChecked = false;
                TblEbcdicNoSpecialMenuItem.IsChecked = false;

                menuItem.IsChecked = true;
            }
        }

        #endregion

        #region Help Menu

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "HexEditorV2 Sample Application\n\n" +
                "Version: 2.0 Alpha\n" +
                "Architecture: MVVM with native insert mode\n" +
                "Author: Derek Tremblay\n" +
                "Contributors: Claude Sonnet 4.5\n\n" +
                "Copyright © 2026\n" +
                "Licensed under Apache 2.0",
                "About HexEditorV2",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void KeyboardShortcuts_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Keyboard Shortcuts:\n\n" +
                "File Operations:\n" +
                "  Ctrl+O - Open file\n" +
                "  Ctrl+S - Save file\n\n" +
                "Edit Operations:\n" +
                "  Ctrl+Z - Undo\n" +
                "  Ctrl+Y - Redo\n" +
                "  Ctrl+A - Select all\n" +
                "  Ctrl+C - Copy selection\n" +
                "  Ctrl+X - Cut selection\n" +
                "  Ctrl+V - Paste\n" +
                "  Del - Delete selection\n\n" +
                "Navigation:\n" +
                "  Arrow keys - Move cursor\n" +
                "  Page Up/Down - Scroll page\n" +
                "  Home/End - Start/End of line\n" +
                "  Ctrl+Home/End - Start/End of file\n\n" +
                "Editing:\n" +
                "  0-9, A-F - Enter hex values\n" +
                "  Insert - Toggle Insert/Overwrite mode\n" +
                "  Shift+Navigation - Extend selection",
                "Keyboard Shortcuts",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion

        #region Keyboard Handling

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle F3 (Find Next) and Shift+F3 (Find Previous)
            if (e.Key == Key.F3)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (HexEditor.IsFileLoaded && _lastFindData != null)
                        FindPrevious_Click(sender, e);
                }
                else if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    if (HexEditor.IsFileLoaded && _lastFindData != null)
                        FindNext_Click(sender, e);
                }
                e.Handled = true;
                return;
            }

            // Handle keyboard shortcuts with Ctrl modifier
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                switch (e.Key)
                {
                    case Key.O:
                        Open_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.S:
                        if (HexEditor.IsFileLoaded)
                            Save_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.Z:
                        if (HexEditor.CanUndo)
                            Undo_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.Y:
                        if (HexEditor.CanRedo)
                            Redo_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.A:
                        if (HexEditor.IsFileLoaded)
                            SelectAll_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.C:
                        if (HexEditor.HasSelection)
                            Copy_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.V:
                        if (HexEditor.IsFileLoaded && !HexEditor.ReadOnlyMode)
                            Paste_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.X:
                        if (HexEditor.HasSelection && !HexEditor.ReadOnlyMode)
                        {
                            if (HexEditor.Cut())
                            {
                                    UpdateUIState();
                            }
                        }
                        e.Handled = true;
                        break;
                    case Key.F:
                        if (HexEditor.IsFileLoaded)
                            Find_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.H:
                        if (HexEditor.IsFileLoaded && !HexEditor.ReadOnlyMode)
                            Replace_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.G:
                        if (HexEditor.IsFileLoaded)
                            GoToPosition_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.B:
                        if (HexEditor.IsFileLoaded)
                            ToggleBookmark_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.N:
                        if (HexEditor.IsFileLoaded)
                            NextBookmark_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.P:
                        if (HexEditor.IsFileLoaded)
                            PreviousBookmark_Click(sender, e);
                        e.Handled = true;
                        break;
                }
            }
        }

        #endregion

        #region UI State Management

        private void UpdateUIState()
        {
            bool fileLoaded = HexEditor.IsFileLoaded;
            bool hasSelection = HexEditor.HasSelection;
            bool canUndo = HexEditor.CanUndo;
            bool canRedo = HexEditor.CanRedo;
            bool isReadOnly = HexEditor.ReadOnlyMode;

            // File menu
            SaveMenuItem.IsEnabled = fileLoaded;
            SaveAsMenuItem.IsEnabled = fileLoaded;
            CloseMenuItem.IsEnabled = fileLoaded;

            // Edit menu
            UndoMenuItem.IsEnabled = canUndo;
            RedoMenuItem.IsEnabled = canRedo;
            SelectAllMenuItem.IsEnabled = fileLoaded;
            ClearSelectionMenuItem.IsEnabled = hasSelection;
            DeleteSelectionMenuItem.IsEnabled = hasSelection && !isReadOnly;
            CopyMenuItem.IsEnabled = hasSelection;
            CopyHexMenuItem.IsEnabled = hasSelection;
            CopyAsciiMenuItem.IsEnabled = hasSelection;
            PasteMenuItem.IsEnabled = fileLoaded && !isReadOnly;

            // Search menu
            FindMenuItem.IsEnabled = fileLoaded;
            FindNextMenuItem.IsEnabled = fileLoaded && _lastFindData != null;
            FindPreviousMenuItem.IsEnabled = fileLoaded && _lastFindData != null;
            ReplaceMenuItem.IsEnabled = fileLoaded && !isReadOnly;

            // Tools > Byte Operations
            FillWithByteMenuItem.IsEnabled = fileLoaded && !isReadOnly;
            GetByteMenuItem.IsEnabled = fileLoaded;
            SetByteMenuItem.IsEnabled = fileLoaded && !isReadOnly;

            // Tools > Bookmarks
            ToggleBookmarkMenuItem.IsEnabled = fileLoaded;
            ClearBookmarksMenuItem.IsEnabled = fileLoaded;
            NextBookmarkMenuItem.IsEnabled = fileLoaded;
            PreviousBookmarkMenuItem.IsEnabled = fileLoaded;
            ShowBookmarksMenuItem.IsEnabled = fileLoaded;

            // Tools > Character Table
            LoadTBLMenuItem.IsEnabled = fileLoaded;
            // CloseTBLMenuItem is enabled dynamically when TBL is loaded

            // Position menu
            GoToPositionMenuItem.IsEnabled = fileLoaded;

            // View menu
            InsertModeMenuItem.IsEnabled = fileLoaded;
            ReadOnlyMenuItem.IsEnabled = fileLoaded;
            ShowStatusBarMenuItem.IsEnabled = fileLoaded;
            ShowHeaderMenuItem.IsEnabled = fileLoaded;
            ShowOffsetMenuItem.IsEnabled = fileLoaded;
            ShowAsciiMenuItem.IsEnabled = fileLoaded;

            // Toolbar
            SaveButton.IsEnabled = fileLoaded;
            UndoButton.IsEnabled = canUndo;
            RedoButton.IsEnabled = canRedo;
            InsertModeToggle.IsEnabled = fileLoaded;
            ReadOnlyToggle.IsEnabled = fileLoaded;
        }

        #endregion
    }
}
