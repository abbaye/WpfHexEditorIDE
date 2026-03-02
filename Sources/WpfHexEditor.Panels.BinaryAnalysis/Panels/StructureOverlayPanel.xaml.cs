//////////////////////////////////////////////
// Apache 2.0  - 2026
// Structure Overlay Panel - Code-behind
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Text.Json.Nodes;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Models.StructureOverlay;
using WpfHexEditor.Core.Services;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.Panels.BinaryAnalysis
{
    /// <summary>
    /// Panel for managing and displaying structure overlays
    /// </summary>
    public partial class StructureOverlayPanel : UserControl, IStructureOverlayPanel
    {
        private StructureOverlayViewModel _viewModel;
        private StructureOverlayService _service;
        private byte[] _currentFileBytes;

        public StructureOverlayPanel()
        {
            InitializeComponent();

            _viewModel = new StructureOverlayViewModel();
            _service = new StructureOverlayService();
            DataContext = _viewModel;

            // Wire up events
            _viewModel.OnFieldSelected += ViewModel_OnFieldSelected;
            _viewModel.OnStructureSelected += ViewModel_OnStructureSelected;
            StructuresTreeView.SelectedItemChanged += StructuresTreeView_SelectedItemChanged;
        }

        #region Public Properties

        /// <summary>
        /// Get the ViewModel
        /// </summary>
        public StructureOverlayViewModel ViewModel => _viewModel;

        #endregion

        #region Public Methods

        /// <summary>
        /// Update the file bytes for overlay interpretation
        /// </summary>
        public void UpdateFileBytes(byte[] fileBytes)
        {
            _currentFileBytes = fileBytes;
        }

        /// <summary>
        /// Add a structure overlay from format definition
        /// </summary>
        public void AddOverlayFromFormat(JsonObject formatDefinition)
        {
            if (_currentFileBytes == null || formatDefinition == null)
                return;

            var overlay = _service.CreateOverlayFromFormat(formatDefinition, _currentFileBytes);
            if (overlay != null)
            {
                _viewModel.AddStructure(overlay);
                OnOverlayAdded?.Invoke(this, overlay);
            }
        }

        /// <summary>
        /// Add a custom structure overlay
        /// </summary>
        public void AddCustomOverlay(string name, System.Collections.Generic.List<(string name, string type, int length)> fields, long startOffset = 0)
        {
            var overlay = _service.CreateCustomOverlay(name, fields, startOffset);
            if (overlay != null)
            {
                _viewModel.AddStructure(overlay);
                OnOverlayAdded?.Invoke(this, overlay);
            }
        }

        /// <summary>
        /// Clear all overlays
        /// </summary>
        public void ClearAllOverlays()
        {
            _viewModel.ClearAll();
            OnAllOverlaysCleared?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when a new overlay is added
        /// </summary>
        public event EventHandler<OverlayStructure> OnOverlayAdded;

        /// <summary>
        /// Event fired when all overlays are cleared
        /// </summary>
        public event EventHandler OnAllOverlaysCleared;

        /// <summary>
        /// Event fired when a field is selected (for highlighting in hex editor)
        /// </summary>
        public event EventHandler<OverlayField> OnFieldSelectedForHighlight;

        /// <summary>
        /// Event fired when a structure is selected
        /// </summary>
        public event EventHandler<OverlayStructure> OnStructureSelectedForHighlight;

        #endregion

        #region Event Handlers

        private void AddStructureButton_Click(object sender, RoutedEventArgs e)
        {
            // Show dialog to create custom structure
            // For now, create a simple example
            var fields = new System.Collections.Generic.List<(string, string, int)>
            {
                ("Header", "uint32", 4),
                ("Version", "uint16", 2),
                ("Flags", "uint16", 2),
                ("Data Length", "uint32", 4)
            };

            AddCustomOverlay("Custom Structure", fields, 0);
        }

        private void LoadFormatButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Format Definitions (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Load Format Definition"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(dialog.FileName);
                    var formatDef = JsonNode.Parse(json)!.AsObject();
                    AddOverlayFromFormat(formatDef);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load format definition:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.Structures.Count == 0)
                return;

            var result = MessageBox.Show(
                "Remove all structure overlays?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ClearAllOverlays();
            }
        }

        private void StructuresTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is OverlayStructure structure)
            {
                _viewModel.SelectedStructure = structure;
            }
            else if (e.NewValue is OverlayField field)
            {
                _viewModel.SelectedField = field;
            }
        }

        private void ViewModel_OnFieldSelected(object sender, OverlayField field)
        {
            if (field != null)
            {
                OnFieldSelectedForHighlight?.Invoke(this, field);
            }
        }

        private void ViewModel_OnStructureSelected(object sender, OverlayStructure structure)
        {
            if (structure != null)
            {
                OnStructureSelectedForHighlight?.Invoke(this, structure);
            }
        }

        #endregion
    }
}
