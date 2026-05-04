// ==========================================================
// Project: WpfHexEditor.Plugins.CustomParserTemplate
// File: CustomParserTemplatePanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Custom parser template editor panel migrated from Panels.BinaryAnalysis.
//     Manages JSON-persisted templates with field blocks for binary parsing.
// ==========================================================

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Plugins.CustomParserTemplate.Properties;
using WpfHexEditor.Editor.Core.Dialogs;

namespace WpfHexEditor.Plugins.CustomParserTemplate.Views;

/// <summary>
/// Event args raised when the user clicks "Apply Template".
/// </summary>
public sealed class TemplateApplyEventArgs : EventArgs
{
    public CustomTemplate Template { get; }
    public TemplateApplyEventArgs(CustomTemplate template) => Template = template;
}

/// <summary>
/// Panel for creating and managing custom binary parser templates.
/// </summary>
public partial class CustomParserTemplatePanel : UserControl
{
    /// <summary>Raised when the user clicks "Apply Template to File".</summary>
    public event EventHandler<TemplateApplyEventArgs>? TemplateApplyRequested;

    private ObservableCollection<CustomTemplate> _templates = new();
    private CustomTemplate? _currentTemplate;
    private string _templatesDirectory = string.Empty;

    public List<string> ValueTypes { get; } = new()
    {
        "uint8", "uint16", "uint32", "uint64",
        "int8",  "int16",  "int32",  "int64",
        "float", "double",
        "string", "ascii", "utf8", "utf16",
        "hex", "binary", "boolean"
    };

    public CustomParserTemplatePanel()
    {
        InitializeComponent();
        DataContext = this;

        InitializeTemplatesDirectory();
        LoadTemplates();
    }

    // -- Initialisation -------------------------------------------------------

    private void InitializeTemplatesDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _templatesDirectory = Path.Combine(appData, "WpfHexaEditor", "CustomTemplates");
        Directory.CreateDirectory(_templatesDirectory);
    }

    private void LoadTemplates()
    {
        _templates.Clear();
        TemplateListBox.ItemsSource = _templates;

        try
        {
            foreach (var file in Directory.GetFiles(_templatesDirectory, "*.json"))
            {
                try
                {
                    var template = JsonSerializer.Deserialize<CustomTemplate>(File.ReadAllText(file));
                    if (template != null)
                    {
                        template.FilePath = file;
                        _templates.Add(template);
                    }
                }
                catch (Exception ex)
                {
                    IdeMessageBox.Show($"Error loading template {Path.GetFileName(file)}: {ex.Message}",
                        CustomParserTemplateResources.CustomParser_Dialog_LoadError, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            IdeMessageBox.Show($"Error loading templates: {ex.Message}",
                CustomParserTemplateResources.CustomParser_Dialog_LoadError, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // -- Toolbar handlers -----------------------------------------------------

    private void NewTemplate_Click(object sender, RoutedEventArgs e)
    {
        var t = new CustomTemplate
        {
            Name        = $"New Template {DateTime.Now:yyyyMMdd_HHmmss}",
            Description = "Enter description here",
            Extensions  = new List<string> { ".bin" },
            Blocks      = new ObservableCollection<CustomBlock>()
        };
        _templates.Add(t);
        TemplateListBox.SelectedItem = t;
    }

    private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateListBox.SelectedItem is not CustomTemplate template) return;

        if (IdeMessageBox.Show($"Delete template '{template.Name}'?",
                CustomParserTemplateResources.CustomParser_Dialog_ConfirmDelete, MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes)
            return;

        if (!string.IsNullOrEmpty(template.FilePath) && File.Exists(template.FilePath))
        {
            try   { File.Delete(template.FilePath); }
            catch (Exception ex)
            {
                IdeMessageBox.Show($"Error deleting file: {ex.Message}", CustomParserTemplateResources.CustomParser_Dialog_DeleteError,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        _templates.Remove(template);
        ClearEditor();
    }

    private void TemplateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateListBox.SelectedItem is CustomTemplate template)
            LoadTemplateIntoEditor(template);
    }

    private void LoadTemplateIntoEditor(CustomTemplate template)
    {
        _currentTemplate            = template;
        TemplateNameBox.Text        = template.Name;
        TemplateDescriptionBox.Text = template.Description;
        TemplateExtensionsBox.Text  = string.Join(", ", template.Extensions ?? new List<string>());
        BlocksDataGrid.ItemsSource  = template.Blocks;
    }

    private void ClearEditor()
    {
        _currentTemplate            = null;
        TemplateNameBox.Text        = "";
        TemplateDescriptionBox.Text = "";
        TemplateExtensionsBox.Text  = "";
        BlocksDataGrid.ItemsSource  = null;
    }

    private void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTemplate is null)
        {
            IdeMessageBox.Show("No template selected to save.", CustomParserTemplateResources.CustomParser_Dialog_SaveTemplate,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _currentTemplate.Name        = TemplateNameBox.Text;
        _currentTemplate.Description = TemplateDescriptionBox.Text;
        _currentTemplate.Extensions  = TemplateExtensionsBox.Text
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        try
        {
            if (string.IsNullOrEmpty(_currentTemplate.FilePath))
                _currentTemplate.FilePath = Path.Combine(_templatesDirectory,
                    $"{_currentTemplate.Name.Replace(" ", "_")}.json");

            var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            File.WriteAllText(_currentTemplate.FilePath, JsonSerializer.Serialize(_currentTemplate, options));

            IdeMessageBox.Show($"Template '{_currentTemplate.Name}' saved successfully!",
                CustomParserTemplateResources.CustomParser_Dialog_SaveTemplate, MessageBoxButton.OK, MessageBoxImage.Information);

            TemplateListBox.Items.Refresh();
        }
        catch (Exception ex)
        {
            IdeMessageBox.Show($"Error saving template: {ex.Message}", CustomParserTemplateResources.CustomParser_Dialog_SaveError,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddBlock_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTemplate is null)
        {
            IdeMessageBox.Show("Select or create a template first.", CustomParserTemplateResources.CustomParser_Dialog_AddBlock,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _currentTemplate.Blocks.Add(new CustomBlock
        {
            Name        = $"Field{_currentTemplate.Blocks.Count + 1}",
            Offset      = 0,
            Length      = 4,
            ValueType   = "uint32",
            Color       = "#4ECDC4",
            Description = "New field"
        });
    }

    private void RemoveBlock_Click(object sender, RoutedEventArgs e)
    {
        if (BlocksDataGrid.SelectedItem is CustomBlock block && _currentTemplate != null)
            _currentTemplate.Blocks.Remove(block);
    }

    private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTemplate is null)
        {
            IdeMessageBox.Show("No template selected to apply.", CustomParserTemplateResources.CustomParser_Dialog_ApplyTemplate,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (TemplateApplyRequested != null)
            TemplateApplyRequested(this, new TemplateApplyEventArgs(_currentTemplate));
        else
            IdeMessageBox.Show(
                $"Template '{_currentTemplate.Name}' is ready ({_currentTemplate.Blocks.Count} blocks).\n" +
                "Connect this panel to the active HexEditor via the plugin host.",
                CustomParserTemplateResources.CustomParser_Dialog_ApplyTemplate, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTemplate is null)
        {
            IdeMessageBox.Show("No template selected.", CustomParserTemplateResources.CustomParser_Dialog_Export, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new SaveFileDialog { Title = CustomParserTemplateResources.CustomParser_Dialog_Export, Filter = "JSON Files (*.json)|*.json", FileName = $"{_currentTemplate.Name}.json" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(_currentTemplate, options));
            IdeMessageBox.Show($"Exported to:\n{dlg.FileName}", CustomParserTemplateResources.CustomParser_Dialog_ExportSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            IdeMessageBox.Show($"Error exporting: {ex.Message}", CustomParserTemplateResources.CustomParser_Dialog_ExportError, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = CustomParserTemplateResources.CustomParser_Dialog_ImportTemplate, Filter = "JSON Files (*.json)|*.json" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var template = JsonSerializer.Deserialize<CustomTemplate>(File.ReadAllText(dlg.FileName));
            if (template is null) return;

            template.FilePath = Path.Combine(_templatesDirectory, Path.GetFileName(dlg.FileName));
            var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            File.WriteAllText(template.FilePath, JsonSerializer.Serialize(template, options));

            _templates.Add(template);
            TemplateListBox.SelectedItem = template;
            IdeMessageBox.Show($"Template '{template.Name}' imported.", CustomParserTemplateResources.CustomParser_Dialog_ImportSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            IdeMessageBox.Show($"Error importing: {ex.Message}", CustomParserTemplateResources.CustomParser_Dialog_ImportError, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// -- Data models --------------------------------------------------------------

public class CustomTemplate : INotifyPropertyChanged
{
    private string? _name, _description;
    private List<string>? _extensions;
    private ObservableCollection<CustomBlock>? _blocks;

    public string? Name        { get => _name;        set { _name = value;        OnPropertyChanged(); } }
    public string? Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    public List<string>? Extensions { get => _extensions; set { _extensions = value; OnPropertyChanged(); } }
    public ObservableCollection<CustomBlock>? Blocks { get => _blocks; set { _blocks = value; OnPropertyChanged(); } }

    [JsonIgnore] public string? FilePath { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class CustomBlock : INotifyPropertyChanged
{
    private string? _name, _valueType, _color, _description;
    private int _offset, _length;

    public string? Name        { get => _name;        set { _name = value;        OnPropertyChanged(); } }
    public int     Offset      { get => _offset;      set { _offset = value;      OnPropertyChanged(); } }
    public int     Length      { get => _length;      set { _length = value;      OnPropertyChanged(); } }
    public string? ValueType   { get => _valueType;   set { _valueType = value;   OnPropertyChanged(); } }
    public string? Color       { get => _color;       set { _color = value;       OnPropertyChanged(); } }
    public string? Description { get => _description; set { _description = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
