//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// File Comparison Side-by-Side Diff Panel
// Author : Claude Sonnet 4.5
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Services;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Panels.FileOps;

namespace WpfHexEditor.Panels.FileOps
{
    public partial class FileComparisonPanel : UserControl
    {
        private List<ParsedField> _file1Fields;
        private List<ParsedField> _file2Fields;
        private string _file1Path;
        private string _file2Path;

        public FileComparisonPanel()
        {
            InitializeComponent();
        }

        private void LoadFile1_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select File 1 to Compare",
                Filter = "All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _file1Path = dialog.FileName;
                File1NameText.Text = $"File 1: {Path.GetFileName(_file1Path)}";
                LoadFieldsFromFile(_file1Path, out _file1Fields);
                UpdateComparison();
            }
        }

        private void LoadFile2_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select File 2 to Compare",
                Filter = "All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _file2Path = dialog.FileName;
                File2NameText.Text = $"File 2: {Path.GetFileName(_file2Path)}";
                LoadFieldsFromFile(_file2Path, out _file2Fields);
                UpdateComparison();
            }
        }

        private void LoadFieldsFromFile(string filePath, out List<ParsedField> fields)
        {
            fields = new List<ParsedField>();

            try
            {
                // Use FileShare.ReadWrite so this read succeeds even while the HexEditor
                // holds the file open with FileAccess.ReadWrite + FileShare.Read.
                byte[] fileBytes;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fileBytes = new byte[fs.Length];
                    _ = fs.Read(fileBytes, 0, fileBytes.Length);
                }

                var detectionService = new FormatDetectionService();

                // Load format definitions (you may want to cache this)
                var formatDefsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FormatDefinitions");
                if (Directory.Exists(formatDefsPath))
                {
                    detectionService.LoadFormatDefinitionsFromDirectory(formatDefsPath);
                }

                var result = detectionService.DetectFormat(fileBytes, Path.GetFileName(filePath));

                // Handle ambiguous detection - prompt user to select format
                if (result.Success && result.RequiresUserSelection && result.Candidates != null && result.Candidates.Count > 1)
                {
                    var selectedCandidate = ShowFormatSelectionDialog(result.Candidates);
                    if (selectedCandidate != null)
                    {
                        result.Format = selectedCandidate.Format;
                        result.Blocks = selectedCandidate.Blocks;
                    }
                }

                if (result.Success && result.Format != null && result.Format.Blocks != null)
                {
                    // Use variables from detection result (includes function execution results)
                    var variables = result.Variables ?? result.Format.Variables ?? new Dictionary<string, object>();

                    System.Diagnostics.Debug.WriteLine($"[FileComparisonPanel] Format: {result.Format.FormatName}, Variables count: {variables.Count}");
                    foreach (var v in variables)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Variable: {v.Key} = {v.Value}");
                    }

                    foreach (var block in result.Format.Blocks)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileComparisonPanel] Block: Name={block.Name}, Type={block.Type}, Variable={block.Variable}");

                        // Handle metadata blocks differently
                        if (block.Type != null && block.Type.Equals("metadata", StringComparison.OrdinalIgnoreCase))
                        {
                            System.Diagnostics.Debug.WriteLine($"  -> Metadata block detected");

                            // For metadata blocks, get value from variable
                            if (!string.IsNullOrWhiteSpace(block.Variable) && variables.TryGetValue(block.Variable, out var varValue))
                            {
                                System.Diagnostics.Debug.WriteLine($"  -> Variable '{block.Variable}' = '{varValue}'");
                                fields.Add(new ParsedField
                                {
                                    Name = block.Name,
                                    Value = varValue?.ToString() ?? "",
                                    ValueType = "metadata",
                                    Offset = 0,
                                    Length = 0
                                });
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"  -> Variable '{block.Variable}' NOT FOUND");
                            }
                            continue;
                        }

                        // Handle regular field blocks
                        var offset = block.GetOffsetValue(variables);
                        var length = block.GetLengthValue(variables);

                        if (!offset.HasValue || !length.HasValue) continue;
                        if (offset.Value >= fileBytes.Length) continue;

                        var actualLength = Math.Min(length.Value, fileBytes.Length - (int)offset.Value);
                        var data = new byte[actualLength];
                        Array.Copy(fileBytes, offset.Value, data, 0, actualLength);

                        var value = ParseBlockValue(data, block.ValueType);

                        fields.Add(new ParsedField
                        {
                            Name = block.Name,
                            Value = value,
                            ValueType = block.ValueType,
                            Offset = (int)offset.Value,
                            Length = actualLength
                        });
                    }
                }
                else
                {
                    // No format detected - this is expected for plain text files or unknown formats
                    // Fields list is already empty, which is correct
                    var fileName = Path.GetFileName(filePath);

                    // Check if it's likely a text file
                    if (result.ContentAnalysis != null && result.ContentAnalysis.IsLikelyText)
                    {
                        // Informational message for text files
                        System.Diagnostics.Debug.WriteLine($"No structured format detected for {fileName} - appears to be plain text");
                    }
                    else if (!result.Success)
                    {
                        // Detection failed
                        System.Diagnostics.Debug.WriteLine($"Format detection failed for {fileName}: {result.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileComparisonPanel] Error loading file '{filePath}': {ex.Message}");
                fields = new List<ParsedField>();
            }
        }

        private string ParseBlockValue(byte[] data, string valueType)
        {
            if (data == null || data.Length == 0) return "";

            try
            {
                return valueType?.ToLowerInvariant() switch
                {
                    "uint8" or "byte" when data.Length >= 1 => data[0].ToString(),
                    "uint16" or "ushort" when data.Length >= 2 => BitConverter.ToUInt16(data, 0).ToString(),
                    "uint32" or "uint" when data.Length >= 4 => BitConverter.ToUInt32(data, 0).ToString(),
                    "int8" or "sbyte" when data.Length >= 1 => ((sbyte)data[0]).ToString(),
                    "int16" or "short" when data.Length >= 2 => BitConverter.ToInt16(data, 0).ToString(),
                    "int32" or "int" when data.Length >= 4 => BitConverter.ToInt32(data, 0).ToString(),
                    "string" or "ascii" => System.Text.Encoding.ASCII.GetString(data).TrimEnd('\0'),
                    "utf8" => System.Text.Encoding.UTF8.GetString(data).TrimEnd('\0'),
                    "hex" => BitConverter.ToString(data).Replace("-", " "),
                    _ => BitConverter.ToString(data).Replace("-", " ")
                };
            }
            catch
            {
                return BitConverter.ToString(data).Replace("-", " ");
            }
        }

        private void UpdateComparison()
        {
            if (_file1Fields == null && _file2Fields == null)
            {
                ComparisonInfoText.Text = "Select files to compare";
                File1Fields.ItemsSource = null;
                File2Fields.ItemsSource = null;
                UpdateStatistics(0, 0, 0, 0);
                return;
            }

            if (_file1Fields == null || _file2Fields == null)
            {
                ComparisonInfoText.Text = "Select both files to compare";

                // Update individual file displays even if only one is loaded
                if (_file1Fields != null)
                {
                    var file1Display = _file1Fields.Select(f => new DiffField
                    {
                        Name = f.Name,
                        Value = f.Value,
                        DiffStatus = DiffStatus.Unchanged
                    }).ToList();
                    File1Fields.ItemsSource = file1Display;

                    // Show message if no fields detected
                    if (_file1Fields.Count == 0 && !string.IsNullOrEmpty(_file1Path))
                    {
                        File1NameText.Text = $"File 1: {Path.GetFileName(_file1Path)} (No structured format detected)";
                    }
                }
                else
                {
                    File1Fields.ItemsSource = null;
                }

                if (_file2Fields != null)
                {
                    var file2Display = _file2Fields.Select(f => new DiffField
                    {
                        Name = f.Name,
                        Value = f.Value,
                        DiffStatus = DiffStatus.Unchanged
                    }).ToList();
                    File2Fields.ItemsSource = file2Display;

                    // Show message if no fields detected
                    if (_file2Fields.Count == 0 && !string.IsNullOrEmpty(_file2Path))
                    {
                        File2NameText.Text = $"File 2: {Path.GetFileName(_file2Path)} (No structured format detected)";
                    }
                }
                else
                {
                    File2Fields.ItemsSource = null;
                }

                UpdateStatistics(0, 0, 0, 0);
                return;
            }

            // Perform comparison
            var file1Dict = _file1Fields.ToDictionary(f => f.Name, f => f);
            var file2Dict = _file2Fields.ToDictionary(f => f.Name, f => f);

            var file1DiffFields = new List<DiffField>();
            var file2DiffFields = new List<DiffField>();

            int matching = 0;
            int modified = 0;
            int added = 0;
            int removed = 0;

            // Process File 1 fields
            foreach (var field in _file1Fields)
            {
                if (file2Dict.TryGetValue(field.Name, out var field2))
                {
                    // Field exists in both
                    if (field.Value == field2.Value)
                    {
                        // Matching
                        file1DiffFields.Add(new DiffField
                        {
                            Name = field.Name,
                            Value = field.Value,
                            DiffStatus = DiffStatus.Unchanged
                        });
                        matching++;
                    }
                    else
                    {
                        // Modified
                        file1DiffFields.Add(new DiffField
                        {
                            Name = field.Name,
                            Value = field.Value,
                            DiffStatus = DiffStatus.Modified
                        });
                        modified++;
                    }
                }
                else
                {
                    // Removed in File 2
                    file1DiffFields.Add(new DiffField
                    {
                        Name = field.Name,
                        Value = field.Value,
                        DiffStatus = DiffStatus.Removed
                    });
                    removed++;
                }
            }

            // Process File 2 fields
            foreach (var field in _file2Fields)
            {
                if (file1Dict.TryGetValue(field.Name, out var field1))
                {
                    // Field exists in both
                    if (field.Value == field1.Value)
                    {
                        // Matching
                        file2DiffFields.Add(new DiffField
                        {
                            Name = field.Name,
                            Value = field.Value,
                            DiffStatus = DiffStatus.Unchanged
                        });
                    }
                    else
                    {
                        // Modified
                        file2DiffFields.Add(new DiffField
                        {
                            Name = field.Name,
                            Value = field.Value,
                            DiffStatus = DiffStatus.Modified
                        });
                    }
                }
                else
                {
                    // Added in File 2
                    file2DiffFields.Add(new DiffField
                    {
                        Name = field.Name,
                        Value = field.Value,
                        DiffStatus = DiffStatus.Added
                    });
                    added++;
                }
            }

            // Update UI
            File1Fields.ItemsSource = file1DiffFields;
            File2Fields.ItemsSource = file2DiffFields;

            ComparisonInfoText.Text = $"Comparing {Path.GetFileName(_file1Path)} ↔ {Path.GetFileName(_file2Path)}";
            UpdateStatistics(matching, added, modified, removed);
        }

        private void UpdateStatistics(int matching, int added, int modified, int removed)
        {
            MatchingFieldsText.Text = $"Matching: {matching}";
            AddedFieldsText.Text = $"Added: {added}";
            ModifiedFieldsText.Text = $"Modified: {modified}";
            RemovedFieldsText.Text = $"Removed: {removed}";
        }

        /// <summary>
        /// Shows dialog for user to select format when detection is ambiguous
        /// </summary>
        private FormatMatchCandidate ShowFormatSelectionDialog(List<FormatMatchCandidate> candidates)
        {
            try
            {
                var dialog = new FormatSelectionDialog
                {
                    Owner = Window.GetWindow(this),
                    Title = "Multiple Formats Detected - File Comparison",
                    Message = "Multiple file formats match this file. Please select the most appropriate format:",
                    Candidates = candidates.Take(5).ToList() // Show top 5 candidates
                };

                return dialog.ShowDialog() == true ? dialog.SelectedCandidate : candidates[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing format selection: {ex.Message}\nUsing default format.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return candidates.FirstOrDefault();
            }
        }
    }

    public class ParsedField
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string ValueType { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
    }

    public class DiffField : INotifyPropertyChanged
    {
        private string _name;
        private string _value;
        private DiffStatus _diffStatus;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public DiffStatus DiffStatus
        {
            get => _diffStatus;
            set { _diffStatus = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public enum DiffStatus
    {
        Unchanged,
        Added,
        Removed,
        Modified
    }
}
