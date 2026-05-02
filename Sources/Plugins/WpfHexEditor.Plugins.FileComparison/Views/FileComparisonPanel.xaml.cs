// ==========================================================
// Project: WpfHexEditor.Plugins.FileComparison
// File: FileComparisonPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Side-by-side file comparison panel migrated from Panels.FileOps.
//     Detects format, parses fields, and performs structural diff.
// ==========================================================

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Services;
using WpfHexEditor.Plugins.FileComparison.Dialogs;
using WpfHexEditor.Plugins.FileComparison.Properties;

namespace WpfHexEditor.Plugins.FileComparison.Views;

/// <summary>
/// Side-by-side structural diff panel for two binary files.
/// </summary>
public partial class FileComparisonPanel : UserControl
{
    private List<ParsedField>? _file1Fields;
    private List<ParsedField>? _file2Fields;
    private string? _file1Path;
    private string? _file2Path;

    public FileComparisonPanel()
    {
        InitializeComponent();
    }

    // -- Public API -----------------------------------------------------------

    /// <summary>
    /// Pre-loads <paramref name="filePath"/> as File 1 if no file is already loaded.
    /// Called automatically by the plugin when a hex file is opened.
    /// </summary>
    public void SuggestFile1(string filePath)
    {
        if (_file1Path is not null || !File.Exists(filePath)) return;
        _file1Path = filePath;
        File1NameText.Text = $"File 1: {Path.GetFileName(_file1Path)}";
        LoadFieldsFromFile(_file1Path, out _file1Fields);
        UpdateComparison();
    }

    // -- Toolbar handlers -----------------------------------------------------

    private void LoadFile1_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = FileComparisonResources.FileComparison_SelectFile1, Filter = "All Files (*.*)|*.*" };
        if (dlg.ShowDialog() != true) return;

        _file1Path = dlg.FileName;
        File1NameText.Text = $"File 1: {Path.GetFileName(_file1Path)}";
        LoadFieldsFromFile(_file1Path, out _file1Fields);
        UpdateComparison();
    }

    private void LoadFile2_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = FileComparisonResources.FileComparison_SelectFile2, Filter = "All Files (*.*)|*.*" };
        if (dlg.ShowDialog() != true) return;

        _file2Path = dlg.FileName;
        File2NameText.Text = $"File 2: {Path.GetFileName(_file2Path)}";
        LoadFieldsFromFile(_file2Path, out _file2Fields);
        UpdateComparison();
    }

    // -- Format parsing -------------------------------------------------------

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

            var formatDefsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FormatDefinitions");
            if (Directory.Exists(formatDefsPath))
                detectionService.LoadFormatDefinitionsFromDirectory(formatDefsPath);

            var result = detectionService.DetectFormat(fileBytes, Path.GetFileName(filePath));

            if (result.Success && result.RequiresUserSelection &&
                result.Candidates?.Count > 1)
            {
                var selected = ShowFormatSelectionDialog(result.Candidates);
                if (selected != null)
                {
                    result.Format = selected.Format;
                    result.Blocks = selected.Blocks;
                }
            }

            if (result.Success && result.Format?.Blocks != null)
            {
                var variables = result.Variables ?? result.Format.Variables ?? new Dictionary<string, object>();

                foreach (var block in result.Format.Blocks)
                {
                    if (block.Type != null &&
                        block.Type.Equals("metadata", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(block.Variable) &&
                            variables.TryGetValue(block.Variable, out var v))
                            fields.Add(new ParsedField { Name = block.Name, Value = v?.ToString() ?? "", ValueType = "metadata" });
                        continue;
                    }

                    var offset = block.GetOffsetValue(variables);
                    var length = block.GetLengthValue(variables);
                    if (!offset.HasValue || !length.HasValue) continue;
                    if (offset.Value >= fileBytes.Length) continue;

                    var actualLen = Math.Min(length.Value, fileBytes.Length - (int)offset.Value);
                    var data      = new byte[actualLen];
                    Array.Copy(fileBytes, offset.Value, data, 0, actualLen);

                    fields.Add(new ParsedField
                    {
                        Name      = block.Name,
                        Value     = ParseBlockValue(data, block.ValueType),
                        ValueType = block.ValueType,
                        Offset    = (int)offset.Value,
                        Length    = actualLen
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileComparisonPanel] Error loading '{filePath}': {ex.Message}");
            fields = new List<ParsedField>();
        }
    }

    private static string ParseBlockValue(byte[] data, string? valueType)
    {
        if (data.Length == 0) return "";
        try
        {
            return valueType?.ToLowerInvariant() switch
            {
                "uint8"  or "byte"   when data.Length >= 1 => data[0].ToString(),
                "uint16" or "ushort" when data.Length >= 2 => BitConverter.ToUInt16(data, 0).ToString(),
                "uint32" or "uint"   when data.Length >= 4 => BitConverter.ToUInt32(data, 0).ToString(),
                "int16"  or "short"  when data.Length >= 2 => BitConverter.ToInt16(data, 0).ToString(),
                "int32"  or "int"    when data.Length >= 4 => BitConverter.ToInt32(data, 0).ToString(),
                "string" or "ascii"                        => System.Text.Encoding.ASCII.GetString(data).TrimEnd('\0'),
                "utf8"                                     => System.Text.Encoding.UTF8.GetString(data).TrimEnd('\0'),
                _                                          => BitConverter.ToString(data).Replace("-", " ")
            };
        }
        catch { return BitConverter.ToString(data).Replace("-", " "); }
    }

    // -- Diff logic -----------------------------------------------------------

    private void UpdateComparison()
    {
        if (_file1Fields == null && _file2Fields == null)
        {
            ComparisonInfoText.Text  = FileComparisonResources.FileComparison_SelectFilesHint;
            File1Fields.ItemsSource  = null;
            File2Fields.ItemsSource  = null;
            UpdateStatistics(0, 0, 0, 0);
            return;
        }

        if (_file1Fields == null || _file2Fields == null)
        {
            ComparisonInfoText.Text = FileComparisonResources.FileComparison_SelectBothFiles;
            File1Fields.ItemsSource = _file1Fields?.Select(f => new DiffField { Name = f.Name, Value = f.Value, DiffStatus = DiffStatus.Unchanged }).ToList();
            File2Fields.ItemsSource = _file2Fields?.Select(f => new DiffField { Name = f.Name, Value = f.Value, DiffStatus = DiffStatus.Unchanged }).ToList();
            UpdateStatistics(0, 0, 0, 0);
            return;
        }

        var f1Dict = _file1Fields.ToDictionary(f => f.Name, f => f);
        var f2Dict = _file2Fields.ToDictionary(f => f.Name, f => f);
        var f1Diff = new List<DiffField>();
        var f2Diff = new List<DiffField>();
        int matching = 0, modified = 0, added = 0, removed = 0;

        foreach (var f in _file1Fields)
        {
            if (f2Dict.TryGetValue(f.Name, out var f2))
            {
                var status = f.Value == f2.Value ? DiffStatus.Unchanged : DiffStatus.Modified;
                f1Diff.Add(new DiffField { Name = f.Name, Value = f.Value, DiffStatus = status });
                if (status == DiffStatus.Unchanged) matching++; else modified++;
            }
            else
            {
                f1Diff.Add(new DiffField { Name = f.Name, Value = f.Value, DiffStatus = DiffStatus.Removed });
                removed++;
            }
        }

        foreach (var f in _file2Fields)
        {
            if (f1Dict.TryGetValue(f.Name, out var f1))
            {
                var status = f.Value == f1.Value ? DiffStatus.Unchanged : DiffStatus.Modified;
                f2Diff.Add(new DiffField { Name = f.Name, Value = f.Value, DiffStatus = status });
            }
            else
            {
                f2Diff.Add(new DiffField { Name = f.Name, Value = f.Value, DiffStatus = DiffStatus.Added });
                added++;
            }
        }

        File1Fields.ItemsSource = f1Diff;
        File2Fields.ItemsSource = f2Diff;
        ComparisonInfoText.Text = $"Comparing {Path.GetFileName(_file1Path)} \u2194 {Path.GetFileName(_file2Path)}";
        UpdateStatistics(matching, added, modified, removed);
    }

    private void UpdateStatistics(int matching, int added, int modified, int removed)
    {
        MatchingFieldsText.Text  = $"Matching: {matching}";
        AddedFieldsText.Text     = $"Added: {added}";
        ModifiedFieldsText.Text  = $"Modified: {modified}";
        RemovedFieldsText.Text   = $"Removed: {removed}";
    }

    private FormatMatchCandidate? ShowFormatSelectionDialog(List<FormatMatchCandidate> candidates)
    {
        try
        {
            var dialog = new FormatSelectionDialog
            {
                Owner      = Window.GetWindow(this),
                Title      = FileComparisonResources.FileComparison_MultipleFormats,
                Message    = "Multiple formats match this file. Select the most appropriate one:",
                Candidates = candidates.Take(5).ToList()
            };
            return dialog.ShowDialog() == true ? dialog.SelectedCandidate : candidates[0];
        }
        catch
        {
            return candidates.FirstOrDefault();
        }
    }
}

// -- Supporting types ---------------------------------------------------------

public class ParsedField
{
    public string? Name      { get; set; }
    public string? Value     { get; set; }
    public string? ValueType { get; set; }
    public int     Offset    { get; set; }
    public int     Length    { get; set; }
}

public class DiffField : INotifyPropertyChanged
{
    private string? _name, _value;
    private DiffStatus _status;

    public string?    Name       { get => _name;   set { _name   = value; OnPropertyChanged(); } }
    public string?    Value      { get => _value;  set { _value  = value; OnPropertyChanged(); } }
    public DiffStatus DiffStatus { get => _status; set { _status = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public enum DiffStatus { Unchanged, Added, Removed, Modified }
