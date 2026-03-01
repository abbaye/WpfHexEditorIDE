//////////////////////////////////////////////
// Apache 2.0  - 2026
// HexEditor - Structure Overlay Integration (Partial Class)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Models.StructureOverlay;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Structure Overlay integration
    /// Provides visual overlay of data structures on hex view
    /// </summary>
    public partial class HexEditor
    {
        #region Fields

        private IStructureOverlayPanel _structureOverlayPanel;
        private List<(long offset, int length, Color color)> _activeOverlays;

        #endregion

        #region Dependency Properties

        /// <summary>
        /// Dependency Property for StructureOverlayVisibility
        /// </summary>
        public static readonly DependencyProperty StructureOverlayVisibilityProperty =
            DependencyProperty.Register(
                nameof(StructureOverlayVisibility),
                typeof(Visibility),
                typeof(HexEditor),
                new PropertyMetadata(Visibility.Collapsed, OnStructureOverlayVisibilityChanged));

        /// <summary>
        /// Visibility of the Structure Overlay panel
        /// </summary>
        public Visibility StructureOverlayVisibility
        {
            get => (Visibility)GetValue(StructureOverlayVisibilityProperty);
            set => SetValue(StructureOverlayVisibilityProperty, value);
        }

        private static void OnStructureOverlayVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var visible = (Visibility)e.NewValue == Visibility.Visible;
                if (visible && editor._structureOverlayPanel != null && editor._viewModel?.Provider != null)
                {
                    // Update file bytes when panel becomes visible
                    editor.UpdateStructureOverlayFileBytes();
                }
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize Structure Overlay panel
        /// </summary>
        private void InitializeStructureOverlay()
        {
            _activeOverlays = new List<(long, int, Color)>();
            _structureOverlayPanel = this.FindName("StructureOverlayPanel") as IStructureOverlayPanel;

            if (_structureOverlayPanel != null)
            {
                // Hook into events
                _structureOverlayPanel.OnOverlayAdded += StructureOverlayPanel_OnOverlayAdded;
                _structureOverlayPanel.OnAllOverlaysCleared += StructureOverlayPanel_OnAllOverlaysCleared;
                _structureOverlayPanel.OnFieldSelectedForHighlight += StructureOverlayPanel_OnFieldSelected;
                _structureOverlayPanel.OnStructureSelectedForHighlight += StructureOverlayPanel_OnStructureSelected;
            }
        }

        #endregion

        #region Event Handlers

        private void StructureOverlayPanel_OnOverlayAdded(object sender, OverlayStructure overlay)
        {
            if (!overlay.IsVisible)
                return;

            // Add field overlays to custom background blocks
            foreach (var field in overlay.Fields)
            {
                AddCustomBackgroundBlock(new Core.CustomBackgroundBlock
                {
                    StartOffset = field.Offset,
                    Length = field.Length,
                    Color = new System.Windows.Media.SolidColorBrush(field.Color)
                });
                _activeOverlays.Add((field.Offset, field.Length, field.Color));
            }

            RefreshView(true);
        }

        private void StructureOverlayPanel_OnAllOverlaysCleared(object sender, EventArgs e)
        {
            // Remove all overlay backgrounds
            CustomBackgroundService.ClearAll();

            _activeOverlays.Clear();
            RefreshView(true);
        }

        private void StructureOverlayPanel_OnFieldSelected(object sender, OverlayField field)
        {
            if (field == null)
                return;

            // Navigate to field and select it
            SetPosition(field.Offset, 1);
            SelectionStart = field.Offset;
            SelectionStop = field.Offset + field.Length - 1;

            RefreshView(true);
        }

        private void StructureOverlayPanel_OnStructureSelected(object sender, OverlayStructure structure)
        {
            if (structure == null)
                return;

            // Navigate to structure start
            SetPosition(structure.StartOffset, 1);

            // Optionally select the entire structure
            SelectionStart = structure.StartOffset;
            SelectionStop = structure.StartOffset + structure.TotalLength - 1;

            RefreshView(true);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Update the file bytes in Structure Overlay panel
        /// </summary>
        public void UpdateStructureOverlayFileBytes()
        {
            if (_structureOverlayPanel == null || _viewModel?.Provider == null)
                return;

            try
            {
                // Read all file bytes (or a reasonable chunk for large files)
                var maxBytes = Math.Min(_viewModel.Provider.Length, 1024 * 1024); // Max 1MB for overlay
                var bytes = _viewModel.Provider.GetBytes(0, (int)maxBytes);

                _structureOverlayPanel.UpdateFileBytes(bytes);
            }
            catch (Exception)
            {
                // Silently ignore errors
            }
        }

        /// <summary>
        /// Get reference to the Structure Overlay panel
        /// </summary>
        public IStructureOverlayPanel GetStructureOverlayPanel()
        {
            return _structureOverlayPanel;
        }

        /// <summary>
        /// Add overlay from current format detection
        /// </summary>
        public void AddOverlayFromCurrentFormat()
        {
            if (_structureOverlayPanel == null || _detectedFormat == null || _viewModel?.Provider == null)
                return;

            UpdateStructureOverlayFileBytes();

            // Convert FormatDefinition to JObject for the panel
            var jobj = Newtonsoft.Json.Linq.JObject.FromObject(_detectedFormat);
            _structureOverlayPanel.AddOverlayFromFormat(jobj);
        }

        #endregion
    }
}
