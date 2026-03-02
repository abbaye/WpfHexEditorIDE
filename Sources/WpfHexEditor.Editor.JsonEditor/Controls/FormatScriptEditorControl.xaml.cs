//////////////////////////////////////////////
// Apache 2.0  - 2026
// FormatScriptEditor - Main Container Control (Phase 8)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Text.Json;
using System.Text.Json.Nodes;
using WpfHexEditor.Definitions;
using WpfHexEditor.Editor.JsonEditor.Models;

namespace WpfHexEditor.Editor.JsonEditor.Controls
{
    /// <summary>
    /// Main container control for Format Script Editor.
    /// Integrates JsonEditor with file management, validation, and testing.
    /// Phase 8: Complete integration with embedded formats + external file support.
    /// </summary>
    public partial class FormatScriptEditorControl : UserControl
    {
        #region Fields

        private string _currentFilePath;
        private bool _isModified;
        private FormatTreeNode _rootNode;

        #endregion

        #region Constructor

        public FormatScriptEditorControl()
        {
            InitializeComponent();

            InitializeUI();
            LoadEmbeddedFormats();
            WireUpEvents();
        }

        #endregion

        #region Initialization

        private void InitializeUI()
        {
            // Set initial status
            UpdateStatusBar();
        }

        private void WireUpEvents()
        {
            // Toolbar buttons
            NewFileButton.Click += NewFileButton_Click;
            OpenFileButton.Click += OpenFileButton_Click;
            SaveFileButton.Click += SaveFileButton_Click;
            UndoButton.Click += UndoButton_Click;
            RedoButton.Click += RedoButton_Click;
            FormatButton.Click += FormatButton_Click;
            ValidateButton.Click += ValidateButton_Click;

            // Tree view selection
            FormatTreeView.SelectedItemChanged += FormatTreeView_SelectedItemChanged;

            // Editor events
            if (JsonEditorControl != null)
            {
                // Update status bar when cursor moves
                JsonEditorControl.PreviewKeyUp += (s, e) => UpdateStatusBar();
                JsonEditorControl.PreviewMouseUp += (s, e) => UpdateStatusBar();
            }
        }

        /// <summary>
        /// When Editor tab is selected, automatically focus the JsonEditor
        /// This allows immediate keyboard input without needing to click twice
        /// </summary>
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl.SelectedItem == EditorTab && JsonEditorControl != null)
            {
                // Use Dispatcher to ensure the tab switch animation completes first
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    JsonEditorControl.Focus();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        #endregion

        #region Load Embedded Formats

        /// <summary>
        /// Load all embedded format definitions from FormatDefinitions directory
        /// </summary>
        private void LoadEmbeddedFormats()
        {
            try
            {
                _rootNode = new FormatTreeNode
                {
                    Name = "Embedded Formats",
                    Icon = "📦",
                    IsCategory = true,
                    Children = new ObservableCollection<FormatTreeNode>()
                };

                var categories = new Dictionary<string, FormatTreeNode>();

                foreach (var entry in EmbeddedFormatCatalog.Instance.GetAll())
                {
                    if (!categories.TryGetValue(entry.Category, out var categoryNode))
                    {
                        categoryNode = new FormatTreeNode
                        {
                            Name = entry.Category,
                            Icon = "📁",
                            IsCategory = true,
                            Children = new ObservableCollection<FormatTreeNode>()
                        };
                        categories[entry.Category] = categoryNode;
                        _rootNode.Children.Add(categoryNode);
                    }

                    categoryNode.Children.Add(new FormatTreeNode
                    {
                        Name = entry.Name,
                        Icon = "📄",
                        IsCategory = false,
                        ResourceName = entry.ResourceKey
                    });
                }

                // Set tree view root
                FormatTreeView.Items.Clear();
                FormatTreeView.Items.Add(_rootNode);

                // Expand root
                var rootItem = FormatTreeView.ItemContainerGenerator.ContainerFromItem(_rootNode) as TreeViewItem;
                if (rootItem != null)
                    rootItem.IsExpanded = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load embedded formats: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Event Handlers - Toolbar

        private void NewFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckSaveChanges())
            {
                // Create new empty format definition template
                var template = @"{
  ""formatName"": ""New Format"",
  ""version"": ""1.0"",
  ""description"": ""Description of the file format"",
  ""extensions"": ["".ext""],
  ""author"": """",
  ""website"": """",
  ""detection"": {
    ""signature"": """"
  },
  ""blocks"": [
    {
      ""type"": ""signature"",
      ""name"": ""File Header"",
      ""fields"": [
        {
          ""type"": ""ascii"",
          ""name"": ""Magic"",
          ""length"": 4,
          ""value"": """"
        }
      ]
    }
  ]
}";

                JsonEditorControl.LoadText(template);
                _currentFilePath = null;
                _isModified = true;
                FileNameText.Text = "Untitled.json";
                UpdateStatusBar();
            }
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckSaveChanges())
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Open Format Definition"
                };

                if (dialog.ShowDialog() == true)
                {
                    LoadFile(dialog.FileName);
                }
            }
        }

        private void SaveFileButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFile();
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            // JsonEditor handles undo internally via Ctrl+Z
            if (JsonEditorControl.CanUndo)
            {
                // Trigger undo programmatically
                // (In actual implementation, expose Undo() method on JsonEditor)
            }
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            // JsonEditor handles redo internally via Ctrl+Y
            if (JsonEditorControl.CanRedo)
            {
                // Trigger redo programmatically
            }
        }

        private void FormatButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var jsonText = JsonEditorControl.GetText();
                var parsed = JsonNode.Parse(jsonText);
                var formatted = parsed!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                JsonEditorControl.LoadText(formatted);
                _isModified = true;
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to format JSON: {ex.Message}",
                    "Format Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            // Trigger validation
            JsonEditorControl.TriggerValidation();

            // Update validation tab
            UpdateValidationTab();

            // Show validation tab
            var tabControl = FindParent<TabControl>(JsonEditorControl);
            if (tabControl != null)
            {
                tabControl.SelectedIndex = 2; // Validation tab
            }
        }

        #endregion

        #region Event Handlers - Tree View

        private void FormatTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FormatTreeNode node && !node.IsCategory && !string.IsNullOrEmpty(node.ResourceName))
            {
                if (CheckSaveChanges())
                {
                    LoadEmbeddedFormat(node.ResourceName);
                }
            }
        }

        #endregion

        #region File Operations

        private void LoadFile(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                JsonEditorControl.LoadText(content);
                _currentFilePath = filePath;
                _isModified = false;
                FileNameText.Text = Path.GetFileName(filePath);
                UpdateStatusBar();
                UpdateValidationTab();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadEmbeddedFormat(string resourceName)
        {
            try
            {
                var content = EmbeddedFormatCatalog.Instance.GetJson(resourceName);
                JsonEditorControl.LoadText(content);
                _currentFilePath = null; // Embedded format
                _isModified = false;
                var parts = resourceName.Split('.');
                FileNameText.Text = $"[Embedded] {(parts.Length >= 2 ? parts[parts.Length - 2] : resourceName)}";
                UpdateStatusBar();
                UpdateValidationTab();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load embedded format: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveFile()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                // Save As
                var dialog = new SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Save Format Definition",
                    DefaultExt = ".json"
                };

                if (dialog.ShowDialog() == true)
                {
                    _currentFilePath = dialog.FileName;
                }
                else
                {
                    return;
                }
            }

            try
            {
                var content = JsonEditorControl.GetText();
                File.WriteAllText(_currentFilePath, content);
                _isModified = false;
                FileNameText.Text = Path.GetFileName(_currentFilePath);
                MessageBox.Show("File saved successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CheckSaveChanges()
        {
            if (_isModified)
            {
                var result = MessageBox.Show(
                    "Do you want to save changes to the current file?",
                    "Save Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveFile();
                    return !_isModified; // Return true only if save succeeded
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region UI Updates

        private void UpdateStatusBar()
        {
            if (JsonEditorControl == null) return;

            try
            {
                // Update line/column
                var pos = JsonEditorControl.CursorPosition;
                LineColumnText.Text = $"Ln {pos.Line + 1}, Col {pos.Column + 1}";

                // Update errors/warnings
                ErrorCountText.Text = $"{JsonEditorControl.ValidationErrorCount} Errors";
                WarningCountText.Text = $"{JsonEditorControl.ValidationWarningCount} Warnings";

                // Update undo/redo buttons
                UndoButton.IsEnabled = JsonEditorControl.CanUndo;
                RedoButton.IsEnabled = JsonEditorControl.CanRedo;
            }
            catch
            {
                // Silently ignore
            }
        }

        private void UpdateValidationTab()
        {
            if (JsonEditorControl == null || ValidationListBox == null)
                return;

            try
            {
                var errors = JsonEditorControl.ValidationErrors;

                var items = errors.Select(e => new
                {
                    Icon = e.Severity == ValidationSeverity.Error ? "❌" :
                          e.Severity == ValidationSeverity.Warning ? "⚠️" : "ℹ️",
                    Message = e.Message,
                    Location = $"Line {e.Line + 1}, Column {e.Column + 1} ({e.Layer})"
                }).ToList();

                ValidationListBox.ItemsSource = items;
            }
            catch
            {
                // Silently ignore
            }
        }

        #endregion

        #region Helper Methods

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            T parent = parentObject as T;
            if (parent != null)
            {
                return parent;
            }
            else
            {
                return FindParent<T>(parentObject);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the JsonEditor control
        /// </summary>
        public JsonEditor Editor => JsonEditorControl;

        /// <summary>
        /// Load format definition from file path
        /// </summary>
        public void LoadFormat(string filePath)
        {
            LoadFile(filePath);
        }

        /// <summary>
        /// Get current format JSON
        /// </summary>
        public string GetFormatJson()
        {
            return JsonEditorControl?.GetText() ?? string.Empty;
        }

        #endregion
    }

    #region Tree Node Model

    /// <summary>
    /// Tree node for format file tree
    /// </summary>
    public class FormatTreeNode
    {
        public string Name { get; set; }
        public string Icon { get; set; }
        public bool IsCategory { get; set; }
        public string ResourceName { get; set; }
        public ObservableCollection<FormatTreeNode> Children { get; set; }

        public FormatTreeNode()
        {
            Children = new ObservableCollection<FormatTreeNode>();
        }
    }

    #endregion
}
