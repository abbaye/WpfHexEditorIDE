// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.StreamOperations.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class providing V1-compatible stream and memory data opening methods
//     for the HexEditor. Exposes OpenStream, OpenMemory, and related overloads
//     that delegate to the V2 file/data loading infrastructure.
//
// Architecture Notes:
//     Compatibility bridge for Stream-based and byte[]-based consumers.
//     All operations ultimately route through HexViewport data pipeline.
//
// ==========================================================

using System;
using System.IO;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Stream Operations
    /// Contains methods for opening streams and memory data (V1 compatibility)
    /// </summary>
    public partial class HexEditor
    {
        #region Public Methods - Stream Operations (V1 Compatibility)

        /// <summary>
        /// Open a stream for editing (V1 compatibility)
        /// Replaces the V1 setter: editor.Stream = myStream
        /// </summary>
        /// <param name="stream">Stream to open for editing</param>
        /// <param name="readOnly">If true, opens in read-only mode</param>
        /// <exception cref="ArgumentNullException">Thrown when stream is null</exception>
        /// <remarks>
        /// This method provides V1 compatibility for the Stream property setter.
        /// In V1: editor.Stream = myStream;
        /// In V2: editor.OpenStream(myStream);
        ///
        /// The stream must support reading. For write operations, it must also support seeking.
        /// </remarks>
        public void OpenStream(Stream stream, bool readOnly = false)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead)
                throw new ArgumentException("Stream must support reading", nameof(stream));

            try
            {
                // CRITICAL: Set flag to prevent infinite recursion
                _isOpeningFile = true;

                // Close previous file/stream properly to reset all state
                if (_viewModel != null)
                {
                    Close();
                }

                // Create ByteProvider with stream
                var provider = new Core.Bytes.ByteProvider();
                provider.OpenStream(stream, readOnly);

                // Create ViewModel with provider
                _viewModel = new HexEditorViewModel(provider);
                HexViewport.LinesSource = _viewModel.Lines;

                // Synchronize ViewModel with control's BytePerLine
                _viewModel.BytePerLine = BytePerLine;
                HexViewport.BytesPerLine = BytePerLine;

                // Synchronize ViewModel with control's EditMode
                _viewModel.EditMode = EditMode;
                HexViewport.EditMode = EditMode;

                // CRITICAL FIX: Synchronize ByteSize and ByteOrder from DependencyProperties
                _viewModel.ByteSize = ByteSize;
                _viewModel.ByteOrder = ByteOrder;

                // Synchronize ByteShiftLeft (V1 Legacy feature)
                _viewModel.ByteShiftLeft = ByteShiftLeft;

                // Initialize byte spacer properties on viewport
                HexViewport.ByteSpacerPositioning = ByteSpacerPositioning;
                HexViewport.ByteSpacerWidthTickness = ByteSpacerWidthTickness;
                HexViewport.ByteGrouping = ByteGrouping;
                HexViewport.ByteSpacerVisualStyle = ByteSpacerVisualStyle;

                // Initialize byte foreground colors
                var normalBrush = Resources["ByteForegroundBrush"] as System.Windows.Media.Brush;
                var alternateBrush = Resources["AlternateByteForegroundBrush"] as System.Windows.Media.Brush;
                HexViewport.SetByteForegroundColors(normalBrush, alternateBrush);

                // Store file info (no filename for stream)
                FileName = null;
                IsModified = false;
                IsFileOrStreamLoaded = true;  // FIX: Update read-only DP for settings panel

                // Subscribe to property changes
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;

                // Calculate initial visible lines
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateVisibleLines();
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                // Update scrollbar with initial values
                VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 3);
                VerticalScroll.ViewportSize = _viewModel.VisibleLines;

                // Raise FileOpened event (also used for streams)
                OnFileOpened(EventArgs.Empty);

                // Update status bar
                StatusText.Text = readOnly ? "Stream loaded (read-only)" : "Stream loaded";
                UpdateFileSizeDisplay();
                BytesPerLineText.Text = $"Bytes/Line: {_viewModel.BytePerLine}";
                EditModeText.Text = $"Mode: {_viewModel.EditMode}";
                RaiseHexStatusChanged();
            }
            finally
            {
                _isOpeningFile = false;
            }
        }

        /// <summary>
        /// Open byte array in memory for editing (V1 compatibility)
        /// Useful for editing in-memory data without file I/O
        /// </summary>
        /// <param name="data">Byte array to edit</param>
        /// <param name="readOnly">If true, opens in read-only mode</param>
        /// <exception cref="ArgumentNullException">Thrown when data is null</exception>
        /// <remarks>
        /// This method provides V1 compatibility and simplified in-memory editing.
        /// Common use cases:
        /// - Unit testing without file I/O
        /// - Editing data before writing to file
        /// - Processing binary data from network/database
        ///
        /// Example:
        /// var data = new byte[] { 0x00, 0x11, 0x22, 0x33 };
        /// editor.OpenMemory(data);
        /// // Edit the data
        /// var modified = editor.GetAllBytes();
        /// </remarks>
        public void OpenMemory(byte[] data, bool readOnly = false)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                // CRITICAL: Set flag to prevent infinite recursion
                _isOpeningFile = true;

                // Close previous file/stream properly to reset all state
                if (_viewModel != null)
                {
                    Close();
                }

                // Create ByteProvider with memory data
                var provider = new Core.Bytes.ByteProvider();
                provider.OpenMemory(data, readOnly);

                // Create ViewModel with provider
                _viewModel = new HexEditorViewModel(provider);
                HexViewport.LinesSource = _viewModel.Lines;

                // Synchronize ViewModel with control's BytePerLine
                _viewModel.BytePerLine = BytePerLine;
                HexViewport.BytesPerLine = BytePerLine;

                // Synchronize ViewModel with control's EditMode
                _viewModel.EditMode = EditMode;
                HexViewport.EditMode = EditMode;

                // CRITICAL FIX: Synchronize ByteSize and ByteOrder from DependencyProperties
                _viewModel.ByteSize = ByteSize;
                _viewModel.ByteOrder = ByteOrder;

                // Synchronize ByteShiftLeft (V1 Legacy feature)
                _viewModel.ByteShiftLeft = ByteShiftLeft;

                // Initialize byte spacer properties on viewport
                HexViewport.ByteSpacerPositioning = ByteSpacerPositioning;
                HexViewport.ByteSpacerWidthTickness = ByteSpacerWidthTickness;
                HexViewport.ByteGrouping = ByteGrouping;
                HexViewport.ByteSpacerVisualStyle = ByteSpacerVisualStyle;

                // Initialize byte foreground colors
                var normalBrush = Resources["ByteForegroundBrush"] as System.Windows.Media.Brush;
                var alternateBrush = Resources["AlternateByteForegroundBrush"] as System.Windows.Media.Brush;
                HexViewport.SetByteForegroundColors(normalBrush, alternateBrush);

                // Store file info (no filename for memory)
                FileName = null;
                IsModified = false;
                IsFileOrStreamLoaded = true;  // FIX: Update read-only DP for settings panel

                // Subscribe to property changes
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;

                // Calculate initial visible lines
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateVisibleLines();
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                // Update scrollbar with initial values
                VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 3);
                VerticalScroll.ViewportSize = _viewModel.VisibleLines;

                // Raise FileOpened event (also used for memory)
                OnFileOpened(EventArgs.Empty);

                // Update status bar
                var sizeKB = data.Length / 1024.0;
                StatusText.Text = readOnly
                    ? $"Memory loaded: {sizeKB:F2} KB (read-only)"
                    : $"Memory loaded: {sizeKB:F2} KB";
                UpdateFileSizeDisplay();
                BytesPerLineText.Text = $"Bytes/Line: {_viewModel.BytePerLine}";
                EditModeText.Text = $"Mode: {_viewModel.EditMode}";
                RaiseHexStatusChanged();
            }
            finally
            {
                _isOpeningFile = false;
            }
        }

        /// <summary>
        /// Opens the editor on a new empty in-memory buffer.
        /// The document is considered unsaved; the first Save (Ctrl+S) triggers a SaveFileDialog.
        /// </summary>
        /// <param name="displayName">
        /// Logical name shown in the title bar and used as the default file name in
        /// the save dialog (e.g. "New1.bin").
        /// </param>
        public void OpenNew(string displayName = "New1.bin")
        {
            // Reuse OpenMemory with an empty buffer
            OpenMemory([], readOnly: false);

            // Mark as new unsaved file — flags declared in HexEditor.DocumentEditor.cs
            _isNewUnsavedFile   = true;
            _newFileDisplayName = displayName;

            // Override the status-bar text set by OpenMemory
            StatusText.Text = $"New: {displayName}";
            RaiseHexStatusChanged();

            // Notify consumers (title bar, dirty indicators)
            _docEditorModifiedChanged?.Invoke(this, EventArgs.Empty);
            RaiseDocumentEditorTitleChanged();
        }

        #endregion
    }
}
