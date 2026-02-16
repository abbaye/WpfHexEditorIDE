//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfHexaEditor.Models;
using WpfHexaEditor.ViewModels;

namespace WpfHexaEditor
{
    /// <summary>
    /// HexEditor partial class - File Operations
    /// Contains methods for opening, saving, and closing files
    /// </summary>
    public partial class HexEditor
    {
        #region Public Methods - File Operations

        /// <summary>
        /// Open a file for editing
        /// </summary>
        public void OpenFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            _viewModel = HexEditorViewModel.OpenFile(filePath);
            HexViewport.LinesSource = _viewModel.Lines;

            // Synchronize ViewModel with control's BytePerLine (which may have been set in XAML before file opened)
            _viewModel.BytePerLine = BytePerLine;
            HexViewport.BytesPerLine = BytePerLine;

            // Synchronize ViewModel with control's EditMode (which may have been set before file opened, e.g., from settings)
            _viewModel.EditMode = EditMode;
            HexViewport.EditMode = EditMode; // Also sync to HexViewport for caret display

            // ByteProvider V2 always supports insertion anywhere - no need to set flag
            if (EditMode == EditMode.Insert)
            {
            }

            // Initialize byte spacer properties on viewport (V1 compatibility)
            HexViewport.ByteSpacerPositioning = ByteSpacerPositioning;
            HexViewport.ByteSpacerWidthTickness = ByteSpacerWidthTickness;
            HexViewport.ByteGrouping = ByteGrouping;
            HexViewport.ByteSpacerVisualStyle = ByteSpacerVisualStyle;

            // Initialize byte foreground colors
            var normalBrush = Resources["ByteForegroundBrush"] as Brush;
            var alternateBrush = Resources["AlternateByteForegroundBrush"] as Brush;
            HexViewport.SetByteForegroundColors(normalBrush, alternateBrush);

            // Store file info
            FileName = filePath;
            IsModified = false;

            // Subscribe to property changes
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Calculate initial visible lines AFTER the control is fully loaded AND layout is complete
            // Use ApplicationIdle priority to ensure BaseGrid.RowDefinitions[1].ActualHeight is set
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateVisibleLines();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            // Update scrollbar with initial values
            VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 3);
            VerticalScroll.ViewportSize = _viewModel.VisibleLines;

            // Raise FileOpened event
            OnFileOpened(EventArgs.Empty);

            // Update status bar
            StatusText.Text = $"Loaded: {System.IO.Path.GetFileName(filePath)}";
            UpdateFileSizeDisplay();
            BytesPerLineText.Text = $"Bytes/Line: {_viewModel.BytePerLine}";
            EditModeText.Text = $"Mode: {_viewModel.EditMode}";

            // STARTUP OPTIMIZATION: Defer expensive operations to background (low priority)
            // These operations can be done after the control is loaded and visible

            // Update bar chart panel in background
            // Bar chart calculation can be slow for large files
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateBarChart();
            }), System.Windows.Threading.DispatcherPriority.Background);

            // Update scroll markers in background
            // Scroll markers don't need to be ready immediately
            if (_scrollMarkers != null)
            {
                // Use VirtualLength for correct marker positioning (includes insertions)
                _scrollMarkers.FileLength = _viewModel.VirtualLength;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateScrollMarkers();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// Save current file
        /// </summary>
        public void Save()
        {
            if (_viewModel == null)
                throw new InvalidOperationException("No file loaded");

            _viewModel.Save();
            StatusText.Text = "File saved";
            OnChangesSubmited(EventArgs.Empty);
        }

        /// <summary>
        /// Close current file
        /// </summary>
        public void Close()
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.Close();
                _viewModel = null;
            }

            // Reset file info
            FileName = string.Empty;
            IsModified = false;

            // Raise FileClosed event
            OnFileClosed(EventArgs.Empty);

            HexViewport.LinesSource = null;
            StatusText.Text = "Ready";
            FileSizeText.Text = "Size: -";
            SelectionInfo.Text = "No selection";

            // Clear bar chart
            _barChartPanel?.Clear();
            PositionInfo.Text = "Position: 0";
            EditModeText.Text = "Mode: Overwrite";
            BytesPerLineText.Text = "Bytes/Line: 16";
        }


        /// <summary>
        /// Update bar chart panel with current file data
        /// </summary>
        private void UpdateBarChart()
        {
            if (_barChartPanel == null || _viewModel == null)
                return;

            // Only update if bar chart is visible
            if (BarChartPanelVisibility != Visibility.Visible)
                return;

            try
            {
                // Set bar color
                _barChartPanel.BarColor = BarChartColor;

                // Use efficient ViewModel-based update for large files
                _barChartPanel.UpdateDataFromViewModel(_viewModel);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Bar chart update failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Refresh the bar chart panel with current file data. .
        /// </summary>
        public void RefreshBarChart()
        {
            UpdateBarChart();
        }

        /// <summary>
        /// Update auto-highlight byte value when selection changes
        /// </summary>
        private void UpdateAutoHighlightByte()
        {
            if (HexViewport == null || _viewModel == null)
                return;

            // Only update if auto-highlight is enabled
            if (!AllowAutoHighLightSelectionByte)
            {
                HexViewport.AutoHighlightByteValue = null;
                HexViewport.InvalidateVisual();
                return;
            }

            // Get byte value at current selection position or first byte
            try
            {
                VirtualPosition positionToUse;

                // Use selection start if valid, otherwise use position 0
                if (_viewModel.SelectionStart.IsValid && _viewModel.SelectionStart.Value < _viewModel.VirtualLength)
                {
                    positionToUse = _viewModel.SelectionStart;
                }
                else if (_viewModel.VirtualLength > 0)
                {
                    positionToUse = new VirtualPosition(0);
                }
                else
                {
                    HexViewport.AutoHighlightByteValue = null;
                    HexViewport.InvalidateVisual();
                    return;
                }

                byte byteValue = _viewModel.GetByteAt(positionToUse);
                HexViewport.AutoHighlightByteValue = byteValue;
                HexViewport.InvalidateVisual();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Auto-highlight error: {ex.Message}";
                HexViewport.AutoHighlightByteValue = null;
                HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Select all bytes with specified value
        /// V1 ALGORITHM: Expands selection bidirectionally from clicked position
        /// </summary>
        private void SelectAllBytesWith(byte byteValue)
        {

            if (_viewModel == null)
            {
                StatusText.Text = "DEBUG: ViewModel is null";
                return;
            }

            try
            {
                // V1 ALGORITHM: Expand selection bidirectionally from current position
                // Scan backwards and forwards until we hit a different byte value

                long startPosition = _viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : 0;
                long stopPosition = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value : startPosition;


                // Scan BACKWARDS from startPosition while byte matches
                long scanStart = startPosition;
                while (scanStart > 0)
                {
                    var prevPos = new VirtualPosition(scanStart - 1);
                    if (!prevPos.IsValid || (scanStart - 1) < 0)
                        break;

                    byte prevByte = _viewModel.GetByteAt(prevPos);
                    if (prevByte != byteValue)
                        break;

                    scanStart--;
                }


                // Scan FORWARDS from stopPosition while byte matches
                long scanStop = stopPosition;
                while (scanStop < _viewModel.VirtualLength - 1)
                {
                    var nextPos = new VirtualPosition(scanStop + 1);
                    if (!nextPos.IsValid || (scanStop + 1) >= _viewModel.VirtualLength)
                        break;

                    byte nextByte = _viewModel.GetByteAt(nextPos);
                    if (nextByte != byteValue)
                        break;

                    scanStop++;
                }


                // Set the expanded selection in ViewModel
                _viewModel.SetSelectionRange(new VirtualPosition(scanStart), new VirtualPosition(scanStop));

                // CRITICAL: Synchronize viewport properties IMMEDIATELY (don't wait for PropertyChanged)
                HexViewport.SelectionStart = scanStart;
                HexViewport.SelectionStop = scanStop;
                HexViewport.CursorPosition = scanStop;

                // Verify viewport properties were set correctly

                // Force viewport refresh to show the selection
                HexViewport.InvalidateVisual();

                // Ensure the selection start is visible
                EnsurePositionVisible(new VirtualPosition(scanStart));

                // Verify ViewModel selection was set

                // Update status bar
                StatusText.Text = $"Selected {scanStop - scanStart + 1} consecutive bytes with value 0x{byteValue:X2}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Auto-select failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Update scroll markers with bookmarks, modifications, and search results
        /// </summary>
        private void UpdateScrollMarkers()
        {
            if (_scrollMarkers == null || _viewModel == null || _viewModel.Provider == null)
                return;

            try
            {
                // Update bookmarks
                _scrollMarkers.BookmarkPositions = new HashSet<long>(_bookmarks);

                // Get modifications by type from Provider
                var modifiedDict = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Modified);
                var insertedDict = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Added);
                var deletedDict = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Deleted);

                // Update scroll markers with separate positions by type
                _scrollMarkers.ModifiedPositions = modifiedDict != null ? new HashSet<long>(modifiedDict.Keys) : new HashSet<long>();
                _scrollMarkers.InsertedPositions = insertedDict != null ? new HashSet<long>(insertedDict.Keys) : new HashSet<long>();
                _scrollMarkers.DeletedPositions = deletedDict != null ? new HashSet<long>(deletedDict.Keys) : new HashSet<long>();

                // Search results would be updated separately when FindAll is called
                // (we'll add that later)
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Scroll markers update failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Update scroll markers selection bar to show current selection
        /// </summary>
        private void UpdateScrollMarkersSelection()
        {
            if (_scrollMarkers == null || _viewModel == null)
                return;

            try
            {
                long start = _viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : -1;
                long stop = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value : -1;

                if (start >= 0 && stop >= 0)
                {
                    _scrollMarkers.SetSelection(start, stop);
                }
                else
                {
                    _scrollMarkers.ClearSelection();
                }
            }
            catch (Exception _)
            {
            }
        }

        #endregion
    }
}
