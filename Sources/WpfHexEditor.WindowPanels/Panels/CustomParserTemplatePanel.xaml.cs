//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom Parser Templates System
// Author : Claude Sonnet 4.5
//////////////////////////////////////////////

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

namespace WpfHexEditor.WindowPanels.Panels
{
    public partial class CustomParserTemplatePanel : UserControl
    {
        private ObservableCollection<CustomTemplate> _templates;
        private CustomTemplate _currentTemplate;
        private string _templatesDirectory;

        public List<string> ValueTypes { get; set; }

        public CustomParserTemplatePanel()
        {
            InitializeComponent();
            DataContext = this;

            InitializeValueTypes();
            InitializeTemplatesDirectory();
            LoadTemplates();
        }

        private void InitializeValueTypes()
        {
            ValueTypes = new List<string>
            {
                "uint8", "uint16", "uint32", "uint64",
                "int8", "int16", "int32", "int64",
                "float", "double",
                "string", "ascii", "utf8", "utf16",
                "hex", "binary", "boolean"
            };
        }

        private void InitializeTemplatesDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _templatesDirectory = Path.Combine(appData, "WpfHexaEditor", "CustomTemplates");

            if (!Directory.Exists(_templatesDirectory))
                Directory.CreateDirectory(_templatesDirectory);
        }

        private void LoadTemplates()
        {
            _templates = new ObservableCollection<CustomTemplate>();
            TemplateListBox.ItemsSource = _templates;

            try
            {
                var files = Directory.GetFiles(_templatesDirectory, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var template = JsonSerializer.Deserialize<CustomTemplate>(json);
                        if (template != null)
                        {
                            template.FilePath = file;
                            _templates.Add(template);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading template {Path.GetFileName(file)}: {ex.Message}",
                            "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading templates: {ex.Message}",
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NewTemplate_Click(object sender, RoutedEventArgs e)
        {
            var newTemplate = new CustomTemplate
            {
                Name = $"New Template {DateTime.Now:yyyyMMdd_HHmmss}",
                Description = "Enter description here",
                Extensions = new List<string> { ".bin" },
                Blocks = new ObservableCollection<CustomBlock>()
            };

            _templates.Add(newTemplate);
            TemplateListBox.SelectedItem = newTemplate;
        }

        private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateListBox.SelectedItem is CustomTemplate template)
            {
                var result = MessageBox.Show($"Delete template '{template.Name}'?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (!string.IsNullOrEmpty(template.FilePath) && File.Exists(template.FilePath))
                    {
                        try
                        {
                            File.Delete(template.FilePath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error deleting file: {ex.Message}",
                                "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }

                    _templates.Remove(template);
                    ClearEditor();
                }
            }
        }

        private void TemplateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TemplateListBox.SelectedItem is CustomTemplate template)
            {
                LoadTemplateIntoEditor(template);
            }
        }

        private void LoadTemplateIntoEditor(CustomTemplate template)
        {
            _currentTemplate = template;
            TemplateNameBox.Text = template.Name;
            TemplateDescriptionBox.Text = template.Description;
            TemplateExtensionsBox.Text = string.Join(", ", template.Extensions ?? new List<string>());
            BlocksDataGrid.ItemsSource = template.Blocks;
        }

        private void ClearEditor()
        {
            _currentTemplate = null;
            TemplateNameBox.Text = "";
            TemplateDescriptionBox.Text = "";
            TemplateExtensionsBox.Text = "";
            BlocksDataGrid.ItemsSource = null;
        }

        private void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTemplate == null)
            {
                MessageBox.Show("No template selected to save.", "Save Template",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Update template from editor
            _currentTemplate.Name = TemplateNameBox.Text;
            _currentTemplate.Description = TemplateDescriptionBox.Text;
            _currentTemplate.Extensions = TemplateExtensionsBox.Text
                .Split(',')
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

            try
            {
                // Determine file path
                if (string.IsNullOrEmpty(_currentTemplate.FilePath))
                {
                    var fileName = $"{_currentTemplate.Name.Replace(" ", "_")}.json";
                    _currentTemplate.FilePath = Path.Combine(_templatesDirectory, fileName);
                }

                // Serialize and save
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var json = JsonSerializer.Serialize(_currentTemplate, options);
                File.WriteAllText(_currentTemplate.FilePath, json);

                MessageBox.Show($"Template '{_currentTemplate.Name}' saved successfully!",
                    "Save Template", MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh list
                TemplateListBox.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving template: {ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddBlock_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTemplate == null)
            {
                MessageBox.Show("Select or create a template first.", "Add Block",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newBlock = new CustomBlock
            {
                Name = $"Field{_currentTemplate.Blocks.Count + 1}",
                Offset = 0,
                Length = 4,
                ValueType = "uint32",
                Color = "#4ECDC4",
                Description = "New field"
            };

            _currentTemplate.Blocks.Add(newBlock);
        }

        private void RemoveBlock_Click(object sender, RoutedEventArgs e)
        {
            if (BlocksDataGrid.SelectedItem is CustomBlock block && _currentTemplate != null)
            {
                _currentTemplate.Blocks.Remove(block);
            }
        }

        private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTemplate == null)
            {
                MessageBox.Show("No template selected to apply.", "Apply Template",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // This would integrate with the main HexEditor
            // For now, show a message
            MessageBox.Show(
                $"Applying template '{_currentTemplate.Name}' to the current file.\n\n" +
                $"This will parse {_currentTemplate.Blocks.Count} blocks according to your template definition.\n\n" +
                $"Note: Full integration with HexEditor requires connecting to the main view model.",
                "Apply Template", MessageBoxButton.OK, MessageBoxImage.Information);

            // TODO: Implement actual application to HexEditor
            // This would require:
            // 1. Access to the current HexEditor instance
            // 2. Get the loaded file bytes
            // 3. Parse according to template blocks
            // 4. Update the ParsedFieldsPanel with results
        }

        private void ExportTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTemplate == null)
            {
                MessageBox.Show("No template selected to export.", "Export Template",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Template as JSON",
                Filter = "JSON Files (*.json)|*.json",
                FileName = $"{_currentTemplate.Name}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    var json = JsonSerializer.Serialize(_currentTemplate, options);
                    File.WriteAllText(dialog.FileName, json);

                    MessageBox.Show($"Template exported to:\n{dialog.FileName}",
                        "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting template: {ex.Message}",
                        "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import Template from JSON",
                Filter = "JSON Files (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var template = JsonSerializer.Deserialize<CustomTemplate>(json);

                    if (template != null)
                    {
                        // Save to templates directory
                        var fileName = Path.GetFileName(dialog.FileName);
                        template.FilePath = Path.Combine(_templatesDirectory, fileName);

                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                        };

                        var saveJson = JsonSerializer.Serialize(template, options);
                        File.WriteAllText(template.FilePath, saveJson);

                        _templates.Add(template);
                        TemplateListBox.SelectedItem = template;

                        MessageBox.Show($"Template '{template.Name}' imported successfully!",
                            "Import Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing template: {ex.Message}",
                        "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class CustomTemplate : INotifyPropertyChanged
    {
        private string _name;
        private string _description;
        private List<string> _extensions;
        private ObservableCollection<CustomBlock> _blocks;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public List<string> Extensions
        {
            get => _extensions;
            set { _extensions = value; OnPropertyChanged(); }
        }

        public ObservableCollection<CustomBlock> Blocks
        {
            get => _blocks;
            set { _blocks = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public string FilePath { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class CustomBlock : INotifyPropertyChanged
    {
        private string _name;
        private int _offset;
        private int _length;
        private string _valueType;
        private string _color;
        private string _description;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public int Offset
        {
            get => _offset;
            set { _offset = value; OnPropertyChanged(); }
        }

        public int Length
        {
            get => _length;
            set { _length = value; OnPropertyChanged(); }
        }

        public string ValueType
        {
            get => _valueType;
            set { _valueType = value; OnPropertyChanged(); }
        }

        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
