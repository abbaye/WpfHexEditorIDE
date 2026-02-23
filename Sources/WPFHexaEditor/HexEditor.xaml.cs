//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexaEditor.Controls;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.CharacterTable;
using WpfHexaEditor.Events;
using WpfHexaEditor.Models;
using WpfHexaEditor.ViewModels;

namespace WpfHexaEditor
{
    /// <summary>
    /// HexEditor - Modern WPF hex editor with native insert mode support (V2 Architecture)
    /// Clean UserControl without UI chrome (toolbar, menus, etc.)
    /// Host application provides UI and calls public methods/properties
    ///
    /// NOTE: This is the modern V2 implementation - 99% faster with critical bug fixes.
    /// Legacy V1 control (HexEditorLegacy) was removed in v2.6+ (Feb 2026).
    /// </summary>
    public partial class HexEditor : UserControl
    {
        private HexEditorViewModel _viewModel;
        private bool _isMouseDown = false;
        private VirtualPosition _mouseDownPosition = VirtualPosition.Invalid;
        private Border _headerBorder;
        private System.Windows.Controls.Primitives.StatusBar _statusBar;
        private StackPanel _hexHeaderStackPanel;
        private StackPanel _asciiHeaderStackPanel;
        private Controls.BarChartPanel _barChartPanel;
        private Controls.ScrollMarkerPanel _scrollMarkers;

        // Bookmarks
        private readonly List<long> _bookmarks = new List<long>();
        private readonly Services.BookmarkService _bookmarkService = new(); // V2 bookmark service

        // Long-running operations
        private readonly Services.LongRunningOperationService _longRunningService = new();

        // File opening re-entrancy guard (prevents infinite recursion in OnFileNamePropertyChanged)
        private bool _isOpeningFile = false;

        // CRITICAL: Closing flag to prevent async operations from accessing disposed resources
        private volatile bool _isClosing = false;

        // Highlights  - stores ranges of highlighted bytes
        private readonly List<(long start, long length)> _highlights = new List<(long, long)>();

        // TBL (Character Table) support 
        private TblStream _tblStream;
        private CharacterTableType _characterTableType = CharacterTableType.Ascii;

        // Zoom support 
        private ScaleTransform _scaler;

        // Byte editing state (format-aware for Hex/Decimal/Binary)
        private bool _isEditingByte = false;
        private VirtualPosition _editingPosition = VirtualPosition.Invalid;
        private byte _editingValue = 0;
        private int _editingCharIndex = 0;      // Current character index (0-based) - replaces _editingHighNibble
        private int _editingMaxChars = 2;       // Max chars for current format (Hex=2, Decimal=3, Binary=8)
        private string _editingBuffer = "";     // Accumulated input buffer ("2" → "25" → "255")
        private bool _isAsciiEditMode = false;  // true = editing in ASCII area, false = editing in Hex/Decimal/Binary area

        // Auto-scroll during mouse drag selection
        private DispatcherTimer _autoScrollTimer;
        private int _autoScrollDirection = 0; // -1 = up, 1 = down, 0 = no auto-scroll
        private Point _lastMousePosition;
        private VirtualPosition _lastAutoScrollPosition = VirtualPosition.Invalid; // Track last position to avoid redundant updates
        private const double AutoScrollEdgeThreshold = 40.0; // Pixels from edge to trigger auto-scroll
        private const int AutoScrollInterval = 40; // Milliseconds between auto-scroll ticks (faster for better UX)
        private const int AutoScrollSpeed = 2; // Lines to scroll per tick

        public HexEditor()
        {
            InitializeComponent();

            // Initialize custom background blocks system
            InitializeCustomBackgroundBlocks();

            // Initialize format detection system
            InitializeFormatDetection();

            // Initialize parsed fields panel (Issue #111)
            InitializeParsedFieldsPanel();

            // Initialize auto-scroll timer
            _autoScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(AutoScrollInterval)
            };
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;

            // Auto-adjust visible lines when BaseGrid is resized (V1-style approach)
            // Use BaseGrid.RowDefinitions[1].ActualHeight like V1 does
            BaseGrid.SizeChanged += BaseGrid_SizeChanged;

            // Handle mouse wheel scrolling (use PreviewMouseWheel on ScrollViewer to intercept before it scrolls)
            ContentScroller.PreviewMouseWheel += ContentScroller_PreviewMouseWheel;

            // Find XAML elements for display options
            _headerBorder = this.FindName("HeaderBorder") as Border;
            _statusBar = this.FindName("StatusBar") as System.Windows.Controls.Primitives.StatusBar;
            _hexHeaderStackPanel = this.FindName("HexHeaderStackPanel") as StackPanel;
            _asciiHeaderStackPanel = this.FindName("AsciiHeaderStackPanel") as StackPanel;
            _barChartPanel = this.FindName("BarChartPanel") as Controls.BarChartPanel;
            _scrollMarkers = this.FindName("ScrollMarkers") as Controls.ScrollMarkerPanel;

            // Subscribe to scroll marker click event for navigation
            // Disabled: clicking on markers complicates navigation
            //if (_scrollMarkers != null)
            //{
            //    _scrollMarkers.MarkerClicked += ScrollMarkers_MarkerClicked;
            //}

            // Initialize column headers with byte position numbers
            this.Loaded += (s, e) => RefreshColumnHeader();

            // Subscribe to right-click event for context menu
            if (HexViewport != null)
            {
                HexViewport.ByteRightClick += HexViewport_ByteRightClick;
                HexViewport.ByteDoubleClicked += HexViewport_ByteDoubleClicked;
                HexViewport.ByteDragSelection += HexViewport_ByteDragSelection;
                HexViewport.KeyboardNavigation += HexViewport_KeyboardNavigation;
                HexViewport.RefreshTimeUpdated += HexViewport_RefreshTimeUpdated;

                // Initialize selection brushes
                HexViewport.SelectionActiveBrush = SelectionActiveBrush;
                HexViewport.SelectionInactiveBrush = SelectionInactiveBrush;

                // Initialize mouse hover brush
                HexViewport.MouseHoverBrush = new SolidColorBrush(MouseOverColor);

                // Initialize data visual format settings
                HexViewport.DataStringVisual = DataStringVisual;
                HexViewport.OffSetStringVisual = OffSetStringVisual;

                // Initialize ActualOffsetWidth based on current format
                ActualOffsetWidth = new GridLength(HexViewport.ActualOffsetWidth);

                // Bug 4: Subscribe to FontSize/FontFamily changes to update dynamic CellWidth cache
                var fontSizeDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                    Control.FontSizeProperty, typeof(HexEditor));
                fontSizeDescriptor?.AddValueChanged(this, OnFontPropertyChanged);

                var fontFamilyDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                    Control.FontFamilyProperty, typeof(HexEditor));
                fontFamilyDescriptor?.AddValueChanged(this, OnFontPropertyChanged);
            }

            // Subscribe to PreviewKeyDown for Escape key (clear search markers)
            this.PreviewKeyDown += HexEditor_PreviewKeyDown;

            // Subscribe to long-running operation events
            _longRunningService.OperationStarted += LongRunningService_OperationStarted;
            _longRunningService.OperationProgress += LongRunningService_OperationProgress;
            _longRunningService.OperationCompleted += LongRunningService_OperationCompleted;

            // Wire up cancel button to service
            if (ProgressOverlay != null)
            {
                ProgressOverlay.ViewModel.CancelRequested += (s, e) => _longRunningService.CancelCurrentOperation();
            }

            // Initialize zoom system
            InitialiseZoom();
        }

        /// <summary>
        /// Bug 4: Handle FontSize/FontFamily changes to update HexViewport dynamic CellWidth cache
        /// Called when user changes font settings in Settings panel or programmatically
        /// </summary>
        private void OnFontPropertyChanged(object sender, EventArgs e)
        {
            if (HexViewport == null)
                return;

            // Get current font settings (from UserControl.FontFamily and UserControl.FontSize)
            string fontFamily = FontFamily.Source;
            double fontSize = FontSize;

            // Update HexViewport font and invalidate CellWidth cache
            HexViewport.UpdateFont(fontFamily, fontSize);

            // Update ActualOffsetWidth since it depends on font settings
            ActualOffsetWidth = new GridLength(HexViewport.ActualOffsetWidth);

            // Refresh column headers with new widths
            RefreshColumnHeader();
        }

        /// <summary>
        /// Handle Escape key to clear search markers (Find All results)
        /// </summary>
        private void HexEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // Use centralized method to clear all search state
                ClearSearchState();
                StatusText.Text = "Search markers cleared.";
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle scroll marker click to navigate to that position
        /// </summary>
        private void ScrollMarkers_MarkerClicked(object sender, long position)
        {
            if (_viewModel == null || _viewModel.VirtualLength == 0)
                return;

            // Navigate to the clicked position
            _viewModel.SelectionStart = new VirtualPosition(position);
            _viewModel.SelectionStop = new VirtualPosition(position);

            // Calculate the line number for this position
            long lineNumber = position / _viewModel.BytePerLine;

            // Center the position on screen by scrolling to a few lines before
            long scrollToLine = Math.Max(0, lineNumber - (_viewModel.VisibleLines / 2));
            long maxScroll = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines);
            scrollToLine = Math.Min(scrollToLine, maxScroll);

            // Update scroll position
            _viewModel.ScrollPosition = scrollToLine;
            VerticalScroll.Value = scrollToLine;

            // Update UI
            UpdateSelectionInfo();
            UpdatePositionInfo();
        }

        /// <summary>
        /// Handle right-click on byte for context menu
        /// Text-editor-like behavior: if clicking outside selection, create new selection at click position
        /// </summary>
        private void HexViewport_ByteRightClick(object sender, Controls.ByteRightClickEventArgs e)
        {
            if (_viewModel == null)
            {
                ShowContextMenu(e.Position);
                return;
            }

            // Check if clicked position is within current selection
            bool isInSelection = false;
            if (_viewModel.HasSelection && _viewModel.SelectionStart.IsValid && _viewModel.SelectionStop.IsValid)
            {
                long start = Math.Min(_viewModel.SelectionStart.Value, _viewModel.SelectionStop.Value);
                long stop = Math.Max(_viewModel.SelectionStart.Value, _viewModel.SelectionStop.Value);
                isInSelection = (e.Position >= start && e.Position <= stop);
            }

            // If clicked outside selection, create new selection at click position (text-editor behavior)
            if (!isInSelection)
            {
                _viewModel.SetSelection(new VirtualPosition(e.Position));
            }

            ShowContextMenu(e.Position);
        }

        /// <summary>
        /// Handle double-click on byte for auto-select same bytes
        /// </summary>
        private void HexViewport_ByteDoubleClicked(object sender, long position)
        {
            // Only auto-select if feature is enabled
            if (!AllowAutoSelectSameByteAtDoubleClick)
                return;

            if (_viewModel == null)
                return;

            // Get byte value at clicked position
            var virtualPos = new VirtualPosition(position);
            if (!virtualPos.IsValid || position >= _viewModel.VirtualLength)
                return;

            byte byteValue = _viewModel.GetByteAt(virtualPos);

            // Find all positions with this byte value and select them
            SelectAllBytesWith(byteValue);

            // Update status with meaningful info
            int count = CountBytesWith(byteValue);
            StatusText.Text = $"Selected {count} byte(s) with value 0x{byteValue:X2}";
        }

        /// <summary>
        /// Count occurrences of a specific byte value in the file
        /// </summary>
        private int CountBytesWith(byte value)
        {
            if (_viewModel == null)
                return 0;

            int count = 0;
            for (long i = 0; i < _viewModel.VirtualLength; i++)
            {
                if (_viewModel.GetByteAt(new VirtualPosition(i)) == value)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Handle mouse drag selection
        /// </summary>
        private void HexViewport_ByteDragSelection(object sender, Controls.ByteDragSelectionEventArgs e)
        {
            if (_viewModel == null)
                return;

            // Use positions directly from hit testing - don't snap!
            // The hit testing should already return the correct ByteData position
            _viewModel.SetSelectionRange(
                new VirtualPosition(e.StartPosition),
                new VirtualPosition(e.EndPosition));

            // Update UI
            UpdateSelectionInfo();
        }

        /// <summary>
        /// Handle keyboard navigation (Arrow keys, Page Up/Down, Home/End)
        /// </summary>
        private void HexViewport_KeyboardNavigation(object sender, Controls.KeyboardNavigationEventArgs e)
        {
            if (_viewModel == null)
                return;

            var currentPos = _viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : 0;
            long newPos = currentPos;

            // Calculate stride based on ByteSize for multi-byte navigation
            int stride = _viewModel.ByteSize switch
            {
                Core.ByteSizeType.Bit8 => 1,
                Core.ByteSizeType.Bit16 => 2,
                Core.ByteSizeType.Bit32 => 4,
                _ => 1
            };

            switch (e.Key)
            {
                case System.Windows.Input.Key.Left:
                    // Move by stride (1 byte in Bit8, 2 in Bit16, 4 in Bit32)
                    newPos = Math.Max(0, currentPos - stride);
                    break;

                case System.Windows.Input.Key.Right:
                    // Move by stride
                    newPos = Math.Min(_viewModel.VirtualLength - 1, currentPos + stride);
                    break;

                case System.Windows.Input.Key.Up:
                    newPos = Math.Max(0, currentPos - _viewModel.BytePerLine);
                    break;

                case System.Windows.Input.Key.Down:
                    newPos = Math.Min(_viewModel.VirtualLength - 1, currentPos + _viewModel.BytePerLine);
                    break;

                case System.Windows.Input.Key.PageUp:
                    newPos = Math.Max(0, currentPos - (_viewModel.BytePerLine * _viewModel.VisibleLines));
                    break;

                case System.Windows.Input.Key.PageDown:
                    newPos = Math.Min(_viewModel.VirtualLength - 1, currentPos + (_viewModel.BytePerLine * _viewModel.VisibleLines));
                    break;

                case System.Windows.Input.Key.Home:
                    if (e.IsControlPressed)
                        newPos = 0; // Ctrl+Home: Go to start of file
                    else
                        newPos = (currentPos / _viewModel.BytePerLine) * _viewModel.BytePerLine; // Home: Go to start of line
                    break;

                case System.Windows.Input.Key.End:
                    if (e.IsControlPressed)
                        newPos = _viewModel.VirtualLength - 1; // Ctrl+End: Go to end of file
                    else
                    {
                        // End: Go to end of line
                        long lineStart = (currentPos / _viewModel.BytePerLine) * _viewModel.BytePerLine;
                        newPos = Math.Min(_viewModel.VirtualLength - 1, lineStart + _viewModel.BytePerLine - 1);
                    }
                    break;
            }

            // Direction-aware snapping for multi-byte modes
            if (stride > 1)
            {
                bool movingForward = (e.Key == System.Windows.Input.Key.Right ||
                                      e.Key == System.Windows.Input.Key.Down ||
                                      e.Key == System.Windows.Input.Key.PageDown);

                if (movingForward)
                {
                    // Moving forward: Snap UP to next boundary (or stay if already aligned)
                    // Formula: ceiling division = (n + stride - 1) / stride * stride
                    newPos = ((newPos + stride - 1) / stride) * stride;
                }
                else
                {
                    // Moving backward: Snap DOWN to previous boundary (or stay if already aligned)
                    newPos = (newPos / stride) * stride;
                }
            }

            // Update selection based on Shift key
            if (e.IsShiftPressed)
            {
                // Shift pressed: extend selection
                if (!_viewModel.SelectionStart.IsValid)
                    _viewModel.SelectionStart = new VirtualPosition(currentPos);

                _viewModel.SelectionStop = new VirtualPosition(newPos);
            }
            else
            {
                // No Shift: move cursor
                _viewModel.SelectionStart = new VirtualPosition(newPos);
                _viewModel.SelectionStop = new VirtualPosition(newPos);
            }

            // Scroll to ensure new position is visible
            long targetLine = newPos / _viewModel.BytePerLine;
            if (targetLine < _viewModel.ScrollPosition)
                _viewModel.ScrollPosition = targetLine;
            // VisibleLines has +2 safety margin (see HexEditor.Events.cs line 710)
            // Scroll when cursor reaches actual visible area boundary (VisibleLines - 2)
            else if (targetLine >= _viewModel.ScrollPosition + _viewModel.VisibleLines - 2)
                _viewModel.ScrollPosition = targetLine - _viewModel.VisibleLines + 3;

            // Update UI
            UpdateSelectionInfo();
            UpdatePositionInfo();
        }

        /// <summary>
        /// Handle refresh time update from HexViewport
        /// </summary>
        private void HexViewport_RefreshTimeUpdated(object sender, long refreshTimeMs)
        {
            if (RefreshTimeText != null)
            {
                RefreshTimeText.Text = $"Refresh: {refreshTimeMs} ms";
            }
        }

        #region Public Events

        /// <summary>
        /// Raised when a byte is modified, added, or deleted
        /// </summary>
        public event EventHandler<ByteModifiedEventArgs> ByteModified;

        /// <summary>
        /// Raised when the selection changes
        /// </summary>
        public event EventHandler<HexSelectionChangedEventArgs> SelectionChanged;

        /// <summary>
        /// Raised when the cursor position changes
        /// </summary>
        public event EventHandler<PositionChangedEventArgs> PositionChanged;

        /// <summary>
        /// Raised when a file is opened
        /// </summary>
        public event EventHandler FileOpened;

        /// <summary>
        /// Raised when a file is closed
        /// </summary>
        public event EventHandler FileClosed;

        /// <summary>
        /// Raised when an async operation state changes (starts or completes).
        /// Use IsOperationActive property to check current state.
        /// Parent applications can use this to disable UI elements during long-running operations.
        /// </summary>
        public event EventHandler<OperationStateChangedEventArgs> OperationStateChanged;

        /// <summary>
        /// Raised when an undo operation completes
        /// </summary>
        public event EventHandler UndoCompleted;

        /// <summary>
        /// Raised when a redo operation completes
        /// </summary>
        public event EventHandler RedoCompleted;

        /// <summary>
        /// Event helper: Raise ByteModified event
        /// </summary>
        protected virtual void OnByteModified(ByteModifiedEventArgs e) => ByteModified?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise SelectionChanged event
        /// </summary>
        protected virtual void OnSelectionChanged(HexSelectionChangedEventArgs e) => SelectionChanged?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise PositionChanged event
        /// </summary>
        protected virtual void OnPositionChanged(PositionChangedEventArgs e) => PositionChanged?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise FileOpened event
        /// </summary>
        protected virtual void OnFileOpened(EventArgs e) => FileOpened?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise FileClosed event
        /// </summary>
        protected virtual void OnFileClosed(EventArgs e) => FileClosed?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise OperationStateChanged event
        /// </summary>
        protected virtual void OnOperationStateChanged(OperationStateChangedEventArgs e) => OperationStateChanged?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise UndoCompleted event
        /// </summary>
        protected virtual void OnUndoCompleted(EventArgs e) => UndoCompleted?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise RedoCompleted event
        /// </summary>
        protected virtual void OnRedoCompleted(EventArgs e) => RedoCompleted?.Invoke(this, e);

        #endregion

        #region Events

        /// <summary>
        /// Raised when selection start position changes
        /// </summary>
        public event EventHandler SelectionStartChanged;

        /// <summary>
        /// Raised when selection stop position changes
        /// </summary>
        public event EventHandler SelectionStopChanged;

        /// <summary>
        /// Raised when selection length changes
        /// </summary>
        public event EventHandler SelectionLengthChanged;

        /// <summary>
        /// Raised when data is copied to clipboard
        /// </summary>
        public event EventHandler DataCopied;

        /// <summary>
        /// Raised when character table type changes
        /// </summary>
        public event EventHandler TypeOfCharacterTableChanged;

        /// <summary>
        /// Raised when a long process progress changes
        /// </summary>
        public event EventHandler LongProcessProgressChanged;

        /// <summary>
        /// Raised when a long process starts
        /// </summary>
        public event EventHandler LongProcessProgressStarted;

        /// <summary>
        /// Raised when a long process completes
        /// </summary>
        public event EventHandler LongProcessProgressCompleted;

        /// <summary>
        /// Raised when a replace byte operation completes
        /// </summary>
        public event EventHandler ReplaceByteCompleted;

        /// <summary>
        /// Raised when a fill with byte operation completes
        /// </summary>
        public event EventHandler FillWithByteCompleted;

        /// <summary>
        /// Raised when bytes are deleted
        /// </summary>
        public event EventHandler BytesDeleted;

        /// <summary>
        /// Raised when an undo operation completes (alias for UndoCompleted)
        /// </summary>
        public event EventHandler Undone;

        /// <summary>
        /// Raised when a redo operation completes (alias for RedoCompleted)
        /// </summary>
        public event EventHandler Redone;

        /// <summary>
        /// Raised when a byte is single-clicked
        /// </summary>
        public event EventHandler<ByteEventArgs> ByteClick;

        /// <summary>
        /// Raised when a byte is double-clicked
        /// </summary>
        public event EventHandler<ByteEventArgs> ByteDoubleClick;

        /// <summary>
        /// Raised when zoom scale changes
        /// </summary>
        public event EventHandler ZoomScaleChanged;

        /// <summary>
        /// Raised when vertical scrollbar position changes
        /// </summary>
        public event EventHandler<ByteEventArgs> VerticalScrollBarChanged;

        /// <summary>
        /// Raised when changes are submitted (saved)
        /// </summary>
        public event EventHandler ChangesSubmited;

        /// <summary>
        /// Raised when read-only mode changes
        /// </summary>
        public event EventHandler ReadOnlyChanged;

        /// <summary>
        /// Event helper: Raise SelectionStartChanged
        /// </summary>
        protected virtual void OnSelectionStartChanged(EventArgs e) => SelectionStartChanged?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise SelectionStopChanged
        /// </summary>
        protected virtual void OnSelectionStopChanged(EventArgs e) => SelectionStopChanged?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise SelectionLengthChanged
        /// </summary>
        protected virtual void OnSelectionLengthChanged(EventArgs e) => SelectionLengthChanged?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise DataCopied
        /// </summary>
        protected virtual void OnDataCopied(EventArgs e) => DataCopied?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise TypeOfCharacterTableChanged
        /// </summary>
        protected virtual void OnTypeOfCharacterTableChanged(EventArgs e) => TypeOfCharacterTableChanged?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise LongProcessProgressChanged
        /// </summary>
        protected virtual void OnLongProcessProgressChanged(EventArgs e) => LongProcessProgressChanged?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise LongProcessProgressStarted
        /// </summary>
        protected virtual void OnLongProcessProgressStarted(EventArgs e) => LongProcessProgressStarted?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise LongProcessProgressCompleted
        /// </summary>
        protected virtual void OnLongProcessProgressCompleted(EventArgs e) => LongProcessProgressCompleted?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise ReplaceByteCompleted
        /// </summary>
        protected virtual void OnReplaceByteCompleted(EventArgs e) => ReplaceByteCompleted?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise FillWithByteCompleted
        /// </summary>
        protected virtual void OnFillWithByteCompleted(EventArgs e) => FillWithByteCompleted?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise BytesDeleted
        /// </summary>
        protected virtual void OnBytesDeleted(EventArgs e) => BytesDeleted?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise Undone
        /// </summary>
        protected virtual void OnUndone(EventArgs e) => Undone?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise Redone
        /// </summary>
        protected virtual void OnRedone(EventArgs e) => Redone?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise ByteClick
        /// </summary>
        protected virtual void OnByteClick(ByteEventArgs e) => ByteClick?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise ByteDoubleClick
        /// </summary>
        protected virtual void OnByteDoubleClick(ByteEventArgs e) => ByteDoubleClick?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise ZoomScaleChanged
        /// </summary>
        protected virtual void OnZoomScaleChanged(EventArgs e) => ZoomScaleChanged?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise VerticalScrollBarChanged
        /// </summary>
        protected virtual void OnVerticalScrollBarChanged(ByteEventArgs e) => VerticalScrollBarChanged?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise ChangesSubmited
        /// </summary>
        protected virtual void OnChangesSubmited(EventArgs e) => ChangesSubmited?.Invoke(this, e);

        /// <summary>
        /// Event helper: Raise ReadOnlyChanged
        /// </summary>
        protected virtual void OnReadOnlyChanged(EventArgs e) => ReadOnlyChanged?.Invoke(this, e);

        #endregion

        #region Configuration Properties

        /// <summary>
        /// Allow context menu - DependencyProperty
        /// </summary>
        [Category("Behavior")]
        public bool AllowContextMenu
        {
            get => (bool)GetValue(AllowContextMenuProperty);
            set => SetValue(AllowContextMenuProperty, value);
        }

        /// <summary>
        /// Allow zoom - DependencyProperty
        /// </summary>
        [Category("Behavior")]
        public bool AllowZoom
        {
            get => (bool)GetValue(AllowZoomProperty);
            set => SetValue(AllowZoomProperty, value);
        }

        /// <summary>
        /// Gets or sets the mouse wheel scroll speed for vertical scrolling.
        /// Category: Keyboard &amp; mouse
        /// Default: Normal
        /// </summary>
        [Category("Keyboard")]
        public MouseWheelSpeed MouseWheelSpeed
        {
            get => (MouseWheelSpeed)GetValue(MouseWheelSpeedProperty);
            set => SetValue(MouseWheelSpeedProperty, value);
        }

        /// <summary>
        /// Data string display format (Hex/Decimal/Octal/Binary) - DependencyProperty
        /// </summary>
        [Category("Display")]
        public DataVisualType DataStringVisual
        {
            get => (DataVisualType)GetValue(DataStringVisualProperty);
            set => SetValue(DataStringVisualProperty, value);
        }

        /// <summary>
        /// Offset string display format (Hex/Decimal/Octal/Binary) - DependencyProperty
        /// </summary>
        [Category("Display")]
        public DataVisualType OffSetStringVisual
        {
            get => (DataVisualType)GetValue(OffSetStringVisualProperty);
            set => SetValue(OffSetStringVisualProperty, value);
        }

        /// <summary>
        /// Actual offset column width (dynamically calculated based on OffSetStringVisual format)
        /// This property is auto-calculated and should not be displayed in settings panel
        /// </summary>
        [Category("Display")]
        [Browsable(false)]  // Hide from auto-generated settings panel (GridLength not supported)
        public GridLength ActualOffsetWidth
        {
            get => (GridLength)GetValue(ActualOffsetWidthProperty);
            set => SetValue(ActualOffsetWidthProperty, value);
        }

        /// <summary>
        /// Byte order (Lo-Hi / Hi-Lo) - DependencyProperty
        /// </summary>
        [Category("Visual")]
        public ByteOrderType ByteOrder
        {
            get => (ByteOrderType)GetValue(ByteOrderProperty);
            set => SetValue(ByteOrderProperty, value);
        }

        /// <summary>
        /// Byte size display (8/16/32-bit) - DependencyProperty
        /// </summary>
        [Category("Visual")]
        public ByteSizeType ByteSize
        {
            get => (ByteSizeType)GetValue(ByteSizeProperty);
            set => SetValue(ByteSizeProperty, value);
        }

        /// <summary>
        /// Custom text encoding - DependencyProperty
        /// </summary>
        public System.Text.Encoding CustomEncoding
        {
            get => (System.Text.Encoding)GetValue(CustomEncodingProperty);
            set => SetValue(CustomEncodingProperty, value ?? System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Gets the underlying stream used by the ByteProvider
        /// Read-only property for V1 compatibility
        /// </summary>
        public Stream Stream => _viewModel?.Provider?.Stream;

        /// <summary>
        /// Preload byte strategy - DependencyProperty
        /// </summary>
        [Category("Data")]
        public PreloadByteInEditor PreloadByteInEditorMode
        {
            get => (PreloadByteInEditor)GetValue(PreloadByteInEditorModeProperty);
            set => SetValue(PreloadByteInEditorModeProperty, value);
        }

        // TBL Advanced Features  - DependencyProperties

        /// <summary>
        /// Show ASCII characters in TBL - DependencyProperty
        /// </summary>
        [Category("TBL")]
        public bool ShowTblAscii
        {
            get => (bool)GetValue(ShowTblAsciiProperty);
            set => SetValue(ShowTblAsciiProperty, value);
        }

        /// <summary>
        /// Show DTE (Dual-Title Encoding) in TBL - DependencyProperty
        /// </summary>
        [Category("TBL")]
        public bool ShowTblDte
        {
            get => (bool)GetValue(ShowTblDteProperty);
            set => SetValue(ShowTblDteProperty, value);
        }

        /// <summary>
        /// Show MTE (Multi-Title Encoding) in TBL - DependencyProperty (renamed for consistency)
        /// </summary>
        [Category("TBL")]
        public bool ShowTblMte
        {
            get => (bool)GetValue(ShowTblMteProperty);
            set => SetValue(ShowTblMteProperty, value);
        }

        /// <summary>
        /// Show Japanese characters in TBL - DependencyProperty
        /// </summary>
        [Category("TBL")]
        public bool ShowTblJaponais
        {
            get => (bool)GetValue(ShowTblJaponaisProperty);
            set => SetValue(ShowTblJaponaisProperty, value);
        }

        /// <summary>
        /// Show End Block markers in TBL - DependencyProperty
        /// </summary>
        [Category("TBL")]
        public bool ShowTblEndBlock
        {
            get => (bool)GetValue(ShowTblEndBlockProperty);
            set => SetValue(ShowTblEndBlockProperty, value);
        }

        /// <summary>
        /// Show End Line markers in TBL - DependencyProperty
        /// </summary>
        [Category("TBL")]
        public bool ShowTblEndLine
        {
            get => (bool)GetValue(ShowTblEndLineProperty);
            set => SetValue(ShowTblEndLineProperty, value);
        }

        /// <summary>
        /// DTE (Dual-Tile Encoding) color - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblDteColor
        {
            get => (System.Windows.Media.Color)GetValue(TblDteColorProperty);
            set => SetValue(TblDteColorProperty, value);
        }

        /// <summary>
        /// MTE (Multi-Title Encoding) color - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblMteColor
        {
            get => (System.Windows.Media.Color)GetValue(TblMteColorProperty);
            set => SetValue(TblMteColorProperty, value);
        }

        /// <summary>
        /// End block color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblEndBlockColor
        {
            get => (System.Windows.Media.Color)GetValue(TblEndBlockColorProperty);
            set => SetValue(TblEndBlockColorProperty, value);
        }

        /// <summary>
        /// End line color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblEndLineColor
        {
            get => (System.Windows.Media.Color)GetValue(TblEndLineColorProperty);
            set => SetValue(TblEndLineColorProperty, value);
        }

        /// <summary>
        /// ASCII color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblAsciiColor
        {
            get => (System.Windows.Media.Color)GetValue(TblAsciiColorProperty);
            set => SetValue(TblAsciiColorProperty, value);
        }

        /// <summary>
        /// Japanese characters color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblJaponaisColor
        {
            get => (System.Windows.Media.Color)GetValue(TblJaponaisColorProperty);
            set => SetValue(TblJaponaisColorProperty, value);
        }

        /// <summary>
        /// 3-byte sequences color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color Tbl3ByteColor
        {
            get => (System.Windows.Media.Color)GetValue(Tbl3ByteColorProperty);
            set => SetValue(Tbl3ByteColorProperty, value);
        }

        /// <summary>
        /// 4+ byte sequences color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color Tbl4PlusByteColor
        {
            get => (System.Windows.Media.Color)GetValue(Tbl4PlusByteColorProperty);
            set => SetValue(Tbl4PlusByteColorProperty, value);
        }

        /// <summary>
        /// Default color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblDefaultColor
        {
            get => (System.Windows.Media.Color)GetValue(TblDefaultColorProperty);
            set => SetValue(TblDefaultColorProperty, value);
        }

        // Bar Chart Panel color  - DependencyProperty

        /// <summary>
        /// Bar chart color - DependencyProperty
        /// </summary>
        [Category("Colors.Charts")]
        [DisplayName("Bar Color")]
        public System.Windows.Media.Color BarChartColor
        {
            get => (System.Windows.Media.Color)GetValue(BarChartColorProperty);
            set => SetValue(BarChartColorProperty, value);
        }

        /// <summary>
        /// Inline bar chart color (for ASCII panel bar visualization) - DependencyProperty
        /// </summary>
        [Category("Colors.Charts")]
        [DisplayName("Inline Bar Color")]
        public System.Windows.Media.Color InlineBarChartColor
        {
            get => (System.Windows.Media.Color)GetValue(InlineBarChartColorProperty);
            set => SetValue(InlineBarChartColorProperty, value);
        }

        /// <summary>
        /// Selection brush for the active panel (Hex or ASCII) - DependencyProperty
        /// </summary>
        [Category("Colors.Selection")]
        public Brush SelectionActiveBrush
        {
            get => (Brush)GetValue(SelectionActiveBrushProperty);
            set => SetValue(SelectionActiveBrushProperty, value);
        }

        /// <summary>
        /// Selection brush for the inactive panel (Hex or ASCII) - DependencyProperty
        /// </summary>
        [Category("Colors.Selection")]
        public Brush SelectionInactiveBrush
        {
            get => (Brush)GetValue(SelectionInactiveBrushProperty);
            set => SetValue(SelectionInactiveBrushProperty, value);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Is a file currently loaded?
        /// </summary>
        public bool IsFileLoaded => _viewModel != null;

        /// <summary>
        /// Is a file or stream currently loaded?
        /// </summary>
        [Category("Data")]
        public bool IsFileOrStreamLoaded
        {
            get => (bool)GetValue(IsFileOrStreamLoadedProperty);
            private set => SetValue(IsFileOrStreamLoadedPropertyKey, value);
        }

        /// <summary>
        /// Indicates whether a long-running async operation is currently active.
        /// When true, commands and menu items should be disabled to prevent concurrent operations.
        /// This property is automatically set by the LongRunningOperationService.
        /// </summary>
        [Category("Data")]
        public bool IsOperationActive
        {
            get => (bool)GetValue(IsOperationActiveProperty);
            private set => SetValue(IsOperationActivePropertyKey, value);
        }

        /// <summary>
        /// Current edit mode - DependencyProperty for XAML binding
        /// </summary>
        [Category("Data")]
        public EditMode EditMode
        {
            get => (EditMode)GetValue(EditModeProperty);
            set => SetValue(EditModeProperty, value);
        }

        /// <summary>
        /// Can undo?
        /// </summary>
        public bool CanUndo => _viewModel?.CanUndo ?? false;

        /// <summary>
        /// Can redo?
        /// </summary>
        public bool CanRedo => _viewModel?.CanRedo ?? false;

        /// <summary>
        /// Number of undo operations available
        /// </summary>
        public long UndoCount => _viewModel?.Provider?.UndoCount ?? 0;

        /// <summary>
        /// Number of redo operations available
        /// </summary>
        public long RedoCount => _viewModel?.Provider?.RedoCount ?? 0;

        private bool _isOnLongProcess = false;
        /// <summary>
        /// Is a long process currently running? Set to false to cancel.
        /// </summary>
        [Category("Data")]
        public bool IsOnLongProcess
        {
            get => _isOnLongProcess;
            set => _isOnLongProcess = value;
        }

        private double _longProcessProgress = 0;
        /// <summary>
        /// Progress of current long process (0.0 to 1.0)
        /// </summary>
        [Category("Data")]
        public double LongProcessProgress
        {
            get => _longProcessProgress;
            set
            {
                if (Math.Abs(_longProcessProgress - value) > 0.001) // Avoid too many events
                {
                    _longProcessProgress = value;
                    OnLongProcessProgressChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Can copy selection to clipboard?
        /// </summary>
        public bool CanCopy => HasSelection && !ReadOnlyMode;

        /// <summary>
        /// Can delete selection?
        /// </summary>
        public bool CanDelete => HasSelection && !ReadOnlyMode;

        /// <summary>
        /// Is the file locked (read-only)?
        /// </summary>
        [Category("Data")]
        public bool IsLockedFile
        {
            get
            {
                if (_viewModel?.Provider == null)
                    return false;

                // Check if file is read-only
                return _viewModel.Provider.IsReadOnly;
            }
        }

        /// <summary>
        /// Is selection start position visible in viewport?
        /// </summary>
        [Category("Data")]
        public bool SelectionStartIsVisible
        {
            get
            {
                if (_viewModel == null || !_viewModel.HasSelection)
                    return false;

                var selectionLine = SelectionLine;
                var scrollPos = _viewModel.ScrollPosition;
                var visibleLines = _viewModel.VisibleLines;

                return selectionLine >= scrollPos && selectionLine < scrollPos + visibleLines;
            }
        }

        /// <summary>
        /// Is caret visible?
        /// Always true in V2 when there's a selection
        /// </summary>
        public bool IsCaretVisible => HasSelection;

        /// <summary>
        /// Has active selection?
        /// </summary>
        public bool HasSelection => _viewModel?.HasSelection ?? false;

        /// <summary>
        /// Selection length in bytes
        /// </summary>
        public long SelectionLength => _viewModel?.SelectionLength ?? 0;

        /// <summary>
        /// Selected bytes as hex string (e.g., "48 65 6C 6C 6F")
        /// </summary>
        [Category("Data")]
        public string SelectionHex
        {
            get
            {
                if (_viewModel == null || !_viewModel.HasSelection)
                    return string.Empty;

                var bytes = _viewModel.GetSelectionBytes();
                if (bytes == null || bytes.Length == 0)
                    return string.Empty;

                return BitConverter.ToString(bytes).Replace("-", " ");
            }
        }

        /// <summary>
        /// Selected bytes as ASCII string
        /// </summary>
        [Category("Data")]
        public string SelectionString
        {
            get
            {
                if (_viewModel == null || !_viewModel.HasSelection)
                    return string.Empty;

                var bytes = _viewModel.GetSelectionBytes();
                if (bytes == null || bytes.Length == 0)
                    return string.Empty;

                var chars = new char[bytes.Length];
                for (int i = 0; i < bytes.Length; i++)
                {
                    chars[i] = ByteConverters.ByteToChar(bytes[i]);
                }
                return new string(chars);
            }
        }

        /// <summary>
        /// Current selection line number (0-based)
        /// </summary>
        [Category("Data")]
        public long SelectionLine
        {
            get
            {
                if (_viewModel == null)
                    return 0;

                var position = _viewModel.SelectionStart;
                if (!position.IsValid)
                    return 0;

                return position.Value / (_viewModel.BytePerLine > 0 ? _viewModel.BytePerLine : 16);
            }
        }

        /// <summary>
        /// Virtual length (total bytes including inserted/deleted)
        /// </summary>
        public long VirtualLength => _viewModel?.VirtualLength ?? 0;

        /// <summary>
        /// Physical file length in bytes
        /// </summary>
        public long Length => _viewModel?.FileLength ?? 0;

        /// <summary>
        /// Current file name (full path)
        /// Uses DependencyProperty for XAML binding support
        /// </summary>
        [Category("Data")]
        public string FileName
        {
            get => (string)GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        /// <summary>
        /// Has the file been modified?
        /// Uses DependencyProperty for XAML binding support
        /// Read-only - reflects ByteProvider state
        /// </summary>
        [Category("Data")]
        [ReadOnly(true)]
        public bool IsModified
        {
            get => (bool)GetValue(IsModifiedProperty);
            set => SetValue(IsModifiedProperty, value);
        }

        /// <summary>
        /// Current cursor position (virtual)
        /// </summary>
        [Category("Data")]
        public long Position
        {
            get => _viewModel?.SelectionStart.Value ?? 0;
            set
            {
                if (_viewModel != null && value >= 0 && value < VirtualLength)
                {
                    var oldPosition = Position;
                    _viewModel.SetSelection(new VirtualPosition(value));

                    if (oldPosition != value)
                        OnPositionChanged(new PositionChangedEventArgs(value));
                }
            }
        }

        /// <summary>
        /// Selection start position (virtual) (DependencyProperty for XAML binding)
        /// </summary>
        [Category("Data")]
        public long SelectionStart
        {
            get => (long)GetValue(SelectionStartProperty);
            set => SetValue(SelectionStartProperty, value);
        }

        /// <summary>
        /// Selection stop position (virtual) (DependencyProperty for XAML binding)
        /// </summary>
        [Category("Data")]
        public long SelectionStop
        {
            get => (long)GetValue(SelectionStopProperty);
            set => SetValue(SelectionStopProperty, value);
        }

        /// <summary>
        /// Read-only mode (DependencyProperty for XAML binding)
        /// </summary>
        [Category("Data")]
        public bool ReadOnlyMode
        {
            get => (bool)GetValue(ReadOnlyModeProperty);
            set => SetValue(ReadOnlyModeProperty, value);
        }

        /// <summary>
        /// Show or hide the status bar
        /// </summary>
        [Category("StatusBar")]
        public bool ShowStatusBar
        {
            get => StatusBar.Visibility == Visibility.Visible;
            set => StatusBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Status Bar Item Visibility Properties

        /// <summary>
        /// Show or hide status message in status bar
        /// </summary>
        [Category("StatusBar")]
        public bool ShowStatusMessage
        {
            get => (bool)GetValue(ShowStatusMessageProperty);
            set => SetValue(ShowStatusMessageProperty, value);
        }

        public static readonly DependencyProperty ShowStatusMessageProperty =
            DependencyProperty.Register(nameof(ShowStatusMessage), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Show or hide file size in status bar
        /// </summary>
        [Category("StatusBar")]
        public bool ShowFileSizeInStatusBar
        {
            get => (bool)GetValue(ShowFileSizeInStatusBarProperty);
            set => SetValue(ShowFileSizeInStatusBarProperty, value);
        }

        public static readonly DependencyProperty ShowFileSizeInStatusBarProperty =
            DependencyProperty.Register(nameof(ShowFileSizeInStatusBar), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Show or hide selection info in status bar
        /// </summary>
        [Category("StatusBar")]
        public bool ShowSelectionInStatusBar
        {
            get => (bool)GetValue(ShowSelectionInStatusBarProperty);
            set => SetValue(ShowSelectionInStatusBarProperty, value);
        }

        public static readonly DependencyProperty ShowSelectionInStatusBarProperty =
            DependencyProperty.Register(nameof(ShowSelectionInStatusBar), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Show or hide position info in status bar
        /// </summary>
        [Category("StatusBar")]
        public bool ShowPositionInStatusBar
        {
            get => (bool)GetValue(ShowPositionInStatusBarProperty);
            set => SetValue(ShowPositionInStatusBarProperty, value);
        }

        public static readonly DependencyProperty ShowPositionInStatusBarProperty =
            DependencyProperty.Register(nameof(ShowPositionInStatusBar), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Show or hide edit mode in status bar
        /// </summary>
        [Category("StatusBar")]
        public bool ShowEditModeInStatusBar
        {
            get => (bool)GetValue(ShowEditModeInStatusBarProperty);
            set => SetValue(ShowEditModeInStatusBarProperty, value);
        }

        public static readonly DependencyProperty ShowEditModeInStatusBarProperty =
            DependencyProperty.Register(nameof(ShowEditModeInStatusBar), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Show or hide bytes per line in status bar
        /// </summary>
        [Category("StatusBar")]
        public bool ShowBytesPerLineInStatusBar
        {
            get => (bool)GetValue(ShowBytesPerLineInStatusBarProperty);
            set => SetValue(ShowBytesPerLineInStatusBarProperty, value);
        }

        public static readonly DependencyProperty ShowBytesPerLineInStatusBarProperty =
            DependencyProperty.Register(nameof(ShowBytesPerLineInStatusBar), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Show or hide refresh time in status bar
        /// </summary>
        [Category("StatusBar")]
        public bool ShowRefreshTimeInStatusBar
        {
            get => (bool)GetValue(ShowRefreshTimeInStatusBarProperty);
            set => SetValue(ShowRefreshTimeInStatusBarProperty, value);
        }

        public static readonly DependencyProperty ShowRefreshTimeInStatusBarProperty =
            DependencyProperty.Register(nameof(ShowRefreshTimeInStatusBar), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        #endregion

        /// <summary>
        /// Show or hide the column header
        /// </summary>
        [Category("Display")]
        public bool ShowHeader
        {
            get => _headerBorder?.Visibility == Visibility.Visible;
            set
            {
                if (_headerBorder != null)
                    _headerBorder.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Show or hide the offset column
        /// Note: Requires re-creating lines for template changes
        /// </summary>
        [Category("Display")]
        public bool ShowOffset
        {
            get => (bool)GetValue(ShowOffsetProperty);
            set => SetValue(ShowOffsetProperty, value);
        }

        public static readonly DependencyProperty ShowOffsetProperty =
            DependencyProperty.Register(nameof(ShowOffset), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnShowOffsetChanged));

        private static void OnShowOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool showOffset)
            {
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.ShowOffset = showOffset;
                    editor.HexViewport.InvalidateVisual();
                }

                // Also update column header visibility
                if (editor._headerBorder != null && editor._headerBorder.Child is Grid headerGrid)
                {
                    // Find the offset TextBlock (Grid.Column="0", Text="Offset")
                    foreach (UIElement child in headerGrid.Children)
                    {
                        if (child is TextBlock tb && tb.Text == "Offset")
                        {
                            tb.Visibility = showOffset ? Visibility.Visible : Visibility.Collapsed;

                            // Also adjust the column width to 0 when hidden
                            if (headerGrid.ColumnDefinitions.Count > 0)
                            {
                                headerGrid.ColumnDefinitions[0].Width = showOffset
                                    ? new GridLength(110)
                                    : new GridLength(0);
                            }
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Show or hide the ASCII column
        /// Note: Requires re-creating lines for template changes
        /// </summary>
        [Category("Display")]
        public bool ShowAscii
        {
            get => (bool)GetValue(ShowAsciiProperty);
            set => SetValue(ShowAsciiProperty, value);
        }

        public static readonly DependencyProperty ShowAsciiProperty =
            DependencyProperty.Register(nameof(ShowAscii), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnShowAsciiChanged));

        private static void OnShowAsciiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool showAscii)
            {
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.ShowAscii = showAscii;
                    editor.HexViewport.InvalidateVisual();
                }

                // Also update ASCII column header visibility
                if (editor._asciiHeaderStackPanel != null)
                {
                    editor._asciiHeaderStackPanel.Visibility = showAscii ? Visibility.Visible : Visibility.Collapsed;
                }

                // Hide separator and ASCII column in header grid
                if (editor._headerBorder != null && editor._headerBorder.Child is Grid headerGrid)
                {
                    // Separator is Grid.Column="2", ASCII header is Grid.Column="3"
                    if (headerGrid.ColumnDefinitions.Count > 3)
                    {
                        headerGrid.ColumnDefinitions[2].Width = showAscii ? new GridLength(20) : new GridLength(0);
                        headerGrid.ColumnDefinitions[3].Width = showAscii ? new GridLength(180) : new GridLength(0);
                    }

                    // Also hide the separator Border
                    foreach (UIElement child in headerGrid.Children)
                    {
                        if (child is Border border && Grid.GetColumn(border) == 2)
                        {
                            border.Visibility = showAscii ? Visibility.Visible : Visibility.Collapsed;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Show inline bar chart in ASCII panel (replaces ASCII chars with vertical bars representing byte values)
        /// V1 Legacy feature compatibility
        /// </summary>
        [Category("BarChart")]
        [DisplayName("Show Inline Bar Chart")]
        public bool ShowInlineBarChart
        {
            get => (bool)GetValue(ShowInlineBarChartProperty);
            set => SetValue(ShowInlineBarChartProperty, value);
        }

        public static readonly DependencyProperty ShowInlineBarChartProperty =
            DependencyProperty.Register(nameof(ShowInlineBarChart), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnShowInlineBarChartChanged));

        private static void OnShowInlineBarChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool showInlineBarChart)
            {
                if (editor.HexViewport != null)
                {
                    // TODO: ShowInlineBarChart property not yet implemented in HexViewport
                    // editor.HexViewport.ShowInlineBarChart = showInlineBarChart;
                    editor.HexViewport.InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Number of bytes per line (8, 16, 32, etc.) - DependencyProperty for XAML binding
        /// </summary>
        [Category("Visual")]
        public int BytePerLine
        {
            get => (int)GetValue(BytePerLineProperty);
            set => SetValue(BytePerLineProperty, value);
        }

        /// <summary>
        /// Font size for zoom (placeholder)
        /// </summary>
        public new double FontSize
        {
            get => base.FontSize;
            set => base.FontSize = value;
        }

        /// <summary>
        /// Number of visible lines in the viewport
        /// Increasing this value shows more bytes at once but may impact performance
        /// </summary>
        [Category("Data")]
        public int VisibleLines
        {
            get => _viewModel?.VisibleLines ?? 20;
            set
            {
                if (_viewModel != null && value > 0)
                {
                    _viewModel.VisibleLines = value;
                    VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 3);
                    VerticalScroll.ViewportSize = _viewModel.VisibleLines;
                }
            }
        }

        #region Visual Customization Properties (V2 Native)

        /// <summary>
        /// Foreground color for normal bytes
        /// </summary>
        public System.Windows.Media.Brush ByteForeground
        {
            get => (System.Windows.Media.Brush)Resources["ByteForegroundBrush"];
            set => Resources["ByteForegroundBrush"] = value;
        }

        /// <summary>
        /// Foreground color for alternate bytes (every other byte)
        /// </summary>
        public System.Windows.Media.Brush AlternateByteForeground
        {
            get => (System.Windows.Media.Brush)Resources["AlternateByteForegroundBrush"];
            set => Resources["AlternateByteForegroundBrush"] = value;
        }

        #endregion

        #region Byte Spacer Properties

        /// <summary>
        /// Get or set the byte spacing position
        /// </summary>
        [Category("Visual")]
        public ByteSpacerPosition ByteSpacerPositioning
        {
            get => (ByteSpacerPosition)GetValue(ByteSpacerPositioningProperty);
            set => SetValue(ByteSpacerPositioningProperty, value);
        }

        public static readonly DependencyProperty ByteSpacerPositioningProperty =
            DependencyProperty.Register(nameof(ByteSpacerPositioning), typeof(ByteSpacerPosition), typeof(HexEditor),
                new FrameworkPropertyMetadata(ByteSpacerPosition.HexBytePanel, ByteSpacer_Changed));

        /// <summary>
        /// Get or set the byte spacer width
        /// </summary>
        [Category("Visual")]
        public ByteSpacerWidth ByteSpacerWidthTickness
        {
            get => (ByteSpacerWidth)GetValue(ByteSpacerWidthTicknessProperty);
            set => SetValue(ByteSpacerWidthTicknessProperty, value);
        }

        public static readonly DependencyProperty ByteSpacerWidthTicknessProperty =
            DependencyProperty.Register(nameof(ByteSpacerWidthTickness), typeof(ByteSpacerWidth), typeof(HexEditor),
                new FrameworkPropertyMetadata(ByteSpacerWidth.Normal, ByteSpacer_Changed));

        /// <summary>
        /// Get or set the byte grouping
        /// </summary>
        [Category("Visual")]
        public ByteSpacerGroup ByteGrouping
        {
            get => (ByteSpacerGroup)GetValue(ByteGroupingProperty);
            set => SetValue(ByteGroupingProperty, value);
        }

        public static readonly DependencyProperty ByteGroupingProperty =
            DependencyProperty.Register(nameof(ByteGrouping), typeof(ByteSpacerGroup), typeof(HexEditor),
                new FrameworkPropertyMetadata(ByteSpacerGroup.FourByte, ByteSpacer_Changed));

        /// <summary>
        /// Get or set the visual of byte spacer
        /// </summary>
        [Category("Visual")]
        public ByteSpacerVisual ByteSpacerVisualStyle
        {
            get => (ByteSpacerVisual)GetValue(ByteSpacerVisualStyleProperty);
            set => SetValue(ByteSpacerVisualStyleProperty, value);
        }

        public static readonly DependencyProperty ByteSpacerVisualStyleProperty =
            DependencyProperty.Register(nameof(ByteSpacerVisualStyle), typeof(ByteSpacerVisual), typeof(HexEditor),
                new FrameworkPropertyMetadata(ByteSpacerVisual.Line, ByteSpacer_Changed));

        /// <summary>
        /// Callback when any byte spacer property changes - triggers header and viewport refresh
        /// </summary>
        private static void ByteSpacer_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                // Sync byte spacer properties to HexViewport
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.ByteSpacerPositioning = editor.ByteSpacerPositioning;
                    editor.HexViewport.ByteSpacerWidthTickness = editor.ByteSpacerWidthTickness;
                    editor.HexViewport.ByteGrouping = editor.ByteGrouping;
                    editor.HexViewport.ByteSpacerVisualStyle = editor.ByteSpacerVisualStyle;
                }

                // Refresh column header to show new spacers
                editor.RefreshColumnHeader();

                // Refresh viewport rendering to show new spacers
                editor.HexViewport?.InvalidateVisual();
            }
        }

        #endregion

        #region Color Properties

        /// <summary>
        /// First selection gradient color
        /// </summary>
        [Category("Colors.Selection")]
        public Color SelectionFirstColor
        {
            get => (Color)GetValue(SelectionFirstColorProperty);
            set => SetValue(SelectionFirstColorProperty, value);
        }

        public static readonly DependencyProperty SelectionFirstColorProperty =
            DependencyProperty.Register(nameof(SelectionFirstColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromArgb(102, 0, 120, 212), OnSelectionFirstColorChanged));

        private static void OnSelectionFirstColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["SelectionBrush"] = new SolidColorBrush(color) { Opacity = 0.4 };

                // Update HexViewport color
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.SelectionColor = color;

                    // CRITICAL: Also update SelectionActiveBrush which is used in rendering
                    editor.SelectionActiveBrush = new SolidColorBrush(color);
                    editor.HexViewport.SelectionActiveBrush = editor.SelectionActiveBrush;
                }
            }
        }

        /// <summary>
        /// Second selection gradient color
        /// </summary>
        [Category("Colors.Selection")]
        public Color SelectionSecondColor
        {
            get => (Color)GetValue(SelectionSecondColorProperty);
            set => SetValue(SelectionSecondColorProperty, value);
        }

        public static readonly DependencyProperty SelectionSecondColorProperty =
            DependencyProperty.Register(nameof(SelectionSecondColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromArgb(102, 0, 120, 212), OnSelectionSecondColorChanged));

        private static void OnSelectionSecondColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                var color = (Color)e.NewValue;

                // SelectionSecondColor updates the inactive selection brush
                editor.SelectionInactiveBrush = new SolidColorBrush(color);
                editor.HexViewport.SelectionInactiveBrush = editor.SelectionInactiveBrush;
                editor.HexViewport.SelectionColor = color;
            }
        }

        /// <summary>
        /// Color for modified bytes
        /// </summary>
        [Category("Colors.ByteStates")]
        public Color ByteModifiedColor
        {
            get => (Color)GetValue(ByteModifiedColorProperty);
            set => SetValue(ByteModifiedColorProperty, value);
        }

        public static readonly DependencyProperty ByteModifiedColorProperty =
            DependencyProperty.Register(nameof(ByteModifiedColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(255, 165, 0), OnByteModifiedColorChanged));

        private static void OnByteModifiedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["ModifiedBrush"] = new SolidColorBrush(color);

                // Update HexViewport color
                if (editor.HexViewport != null)
                    editor.HexViewport.ModifiedByteColor = color;
            }
        }

        /// <summary>
        /// Color for added bytes
        /// </summary>
        [Category("Colors.ByteStates")]
        public Color ByteAddedColor
        {
            get => (Color)GetValue(ByteAddedColorProperty);
            set => SetValue(ByteAddedColorProperty, value);
        }

        public static readonly DependencyProperty ByteAddedColorProperty =
            DependencyProperty.Register(nameof(ByteAddedColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(76, 175, 80), OnByteAddedColorChanged));

        private static void OnByteAddedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["AddedBrush"] = new SolidColorBrush(color);

                // Update HexViewport color
                if (editor.HexViewport != null)
                    editor.HexViewport.AddedByteColor = color;
            }
        }

        /// <summary>
        /// Color for highlighted bytes
        /// </summary>
        [Category("Colors.ByteStates")]
        public Color HighLightColor
        {
            get => (Color)GetValue(HighLightColorProperty);
            set => SetValue(HighLightColorProperty, value);
        }

        public static readonly DependencyProperty HighLightColorProperty =
            DependencyProperty.Register(nameof(HighLightColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Gold, OnHighLightColorChanged));

        private static void OnHighLightColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                var color = (Color)e.NewValue;
                editor.HexViewport.HighlightColor = color;
            }
        }

        /// <summary>
        /// Mouse over color
        /// </summary>
        [Category("Colors.ByteStates")]
        public Color MouseOverColor
        {
            get => (Color)GetValue(MouseOverColorProperty);
            set => SetValue(MouseOverColorProperty, value);
        }

        public static readonly DependencyProperty MouseOverColorProperty =
            DependencyProperty.Register(nameof(MouseOverColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0, 102, 204), OnMouseOverColorChanged)); // Deep Blue - HDR visible

        private static void OnMouseOverColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Color color)
            {
                editor.Resources["ByteHoverBrush"] = new SolidColorBrush(color);

                // Update HexViewport hover brush for V2
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.MouseHoverBrush = new SolidColorBrush(color);
                    editor.HexViewport.InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Foreground color for normal bytes (even columns: 00, 02, 04...)
        /// </summary>
        [Category("Colors.Foreground")]
        public Color ForegroundFirstColor
        {
            get => (Color)GetValue(ForegroundFirstColorProperty);
            set => SetValue(ForegroundFirstColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundFirstColorProperty =
            DependencyProperty.Register(nameof(ForegroundFirstColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Black, OnForegroundFirstColorChanged));

        private static void OnForegroundFirstColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["ByteForegroundBrush"] = new SolidColorBrush(color);

                // Update HexViewport color
                if (editor.HexViewport != null)
                    editor.HexViewport.NormalByteColor = color;
            }
        }

        /// <summary>
        /// Foreground color for alternate bytes (odd columns: 01, 03, 05...)
        /// </summary>
        [Category("Colors.Foreground")]
        public Color ForegroundSecondColor
        {
            get => (Color)GetValue(ForegroundSecondColorProperty);
            set => SetValue(ForegroundSecondColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundSecondColorProperty =
            DependencyProperty.Register(nameof(ForegroundSecondColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Blue, OnForegroundSecondColorChanged));

        private static void OnForegroundSecondColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["AlternateByteForegroundBrush"] = new SolidColorBrush(color);

                // Update HexViewport color
                if (editor.HexViewport != null)
                    editor.HexViewport.AlternateByteColor = color;
            }
        }

        /// <summary>
        /// Foreground color for offset header
        /// </summary>
        [Category("Colors.Foreground")]
        public Color ForegroundOffSetHeaderColor
        {
            get => (Color)GetValue(ForegroundOffSetHeaderColorProperty);
            set => SetValue(ForegroundOffSetHeaderColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundOffSetHeaderColorProperty =
            DependencyProperty.Register(nameof(ForegroundOffSetHeaderColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(117, 117, 117), OnForegroundOffSetHeaderColorChanged));

        private static void OnForegroundOffSetHeaderColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["OffsetBrush"] = new SolidColorBrush(color);

                // Update HexViewport offset foreground color
                if (editor.HexViewport != null)
                    editor.HexViewport.OffsetForegroundColor = color;
            }
        }

        /// <summary>
        /// Foreground highlight offset header color
        /// </summary>
        [Category("Colors.Foreground")]
        public Color ForegroundHighLightOffSetHeaderColor
        {
            get => (Color)GetValue(ForegroundHighLightOffSetHeaderColorProperty);
            set => SetValue(ForegroundHighLightOffSetHeaderColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundHighLightOffSetHeaderColorProperty =
            DependencyProperty.Register(nameof(ForegroundHighLightOffSetHeaderColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.DarkBlue, OnForegroundHighLightOffSetHeaderColorChanged));

        private static void OnForegroundHighLightOffSetHeaderColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                // This color is used for highlighted offset headers
                // Currently not directly used in HexViewport rendering, but keep for future use
                editor.HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Foreground contrast color
        /// </summary>
        [Category("Colors.Foreground")]
        public Color ForegroundContrast
        {
            get => (Color)GetValue(ForegroundContrastProperty);
            set => SetValue(ForegroundContrastProperty, value);
        }

        public static readonly DependencyProperty ForegroundContrastProperty =
            DependencyProperty.Register(nameof(ForegroundContrast), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Black, OnForegroundContrastChanged));

        private static void OnForegroundContrastChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                // This color is used for high contrast foreground
                // Currently not directly used in HexViewport rendering, but keep for future use
                editor.HexViewport.InvalidateVisual();
            }
        }

        #endregion


        #region Dependency Properties

        /// <summary>
        /// FileName DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register(nameof(FileName), typeof(string), typeof(HexEditor),
                new PropertyMetadata(string.Empty, OnFileNamePropertyChanged));

        private static void OnFileNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is string path && !string.IsNullOrEmpty(path))
            {
                // CRITICAL: Prevent infinite recursion
                // OpenFile sets FileName, which triggers this callback, which would call OpenFile again
                if (editor._isOpeningFile)
                    return;

                // Auto-load file when FileName is set
                // This enables the pattern: HexEdit.FileName = "file.bin" to automatically open the file
                try
                {
                    // Only open if different from current file and file exists
                    if (path != e.OldValue?.ToString() && System.IO.File.Exists(path))
                    {
                        editor.OpenFile(path);
                    }
                }
                catch (Exception ex)
                {
                    editor.StatusText.Text = $"Failed to open file: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// IsModified DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false));

        /// <summary>
        /// Position DependencyProperty for XAML binding 
        /// </summary>
        public static readonly DependencyProperty PositionProperty =
            DependencyProperty.Register(nameof(Position), typeof(long), typeof(HexEditor),
                new PropertyMetadata(-1L, OnPositionPropertyChanged));

        private static void OnPositionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is long position && position >= 0)
            {
                // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                var currentVmStart = editor._viewModel?.SelectionStart.IsValid == true ? editor._viewModel.SelectionStart.Value : -1L;
                if (currentVmStart == position)
                    return; // Already synced, avoid recursion

                editor.SetPosition(position);
            }
        }

        /// <summary>
        /// ReadOnlyMode DependencyProperty for XAML binding 
        /// </summary>
        public static readonly DependencyProperty ReadOnlyModeProperty =
            DependencyProperty.Register(nameof(ReadOnlyMode), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnReadOnlyModePropertyChanged));

        private static void OnReadOnlyModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool readOnly)
            {
                if (editor._viewModel != null)
                {
                    var oldValue = editor._viewModel.ReadOnlyMode;
                    editor._viewModel.ReadOnlyMode = readOnly;

                    // Fire event
                    if (oldValue != readOnly)
                        editor.OnReadOnlyChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// SelectionStart DependencyProperty for XAML binding 
        /// </summary>
        public static readonly DependencyProperty SelectionStartProperty =
            DependencyProperty.Register(nameof(SelectionStart), typeof(long), typeof(HexEditor),
                new PropertyMetadata(-1L, OnSelectionStartPropertyChanged));

        private static void OnSelectionStartPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is long position)
            {
                if (editor._viewModel != null && position >= 0 && position < editor.VirtualLength)
                {
                    // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                    var currentVmStart = editor._viewModel.SelectionStart.IsValid ? editor._viewModel.SelectionStart.Value : -1L;
                    if (currentVmStart == position)
                        return; // Already synced, avoid recursion

                    var oldStart = e.OldValue is long old ? old : -1;
                    var oldStop = editor.SelectionStop;
                    var oldLength = editor.SelectionLength;

                    var stop = editor._viewModel.SelectionStop.IsValid ? editor._viewModel.SelectionStop : new VirtualPosition(position);
                    editor._viewModel.SetSelectionRange(new VirtualPosition(position), stop);

                    // Fire events
                    if (oldStart != position || oldStop != editor.SelectionStop)
                    {
                        editor.OnSelectionChanged(new HexSelectionChangedEventArgs(position, editor.SelectionStop, editor.SelectionLength));

                        if (oldStart != position)
                            editor.OnSelectionStartChanged(EventArgs.Empty);
                        if (oldLength != editor.SelectionLength)
                            editor.OnSelectionLengthChanged(EventArgs.Empty);
                    }

                    // Update auto-highlight byte value
                    editor.UpdateAutoHighlightByte();
                }
            }
        }

        /// <summary>
        /// SelectionStop DependencyProperty for XAML binding 
        /// </summary>
        public static readonly DependencyProperty SelectionStopProperty =
            DependencyProperty.Register(nameof(SelectionStop), typeof(long), typeof(HexEditor),
                new PropertyMetadata(-1L, OnSelectionStopPropertyChanged));

        private static void OnSelectionStopPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is long position)
            {
                if (editor._viewModel != null && position >= 0 && position < editor.VirtualLength)
                {
                    // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                    var currentVmStop = editor._viewModel.SelectionStop.IsValid ? editor._viewModel.SelectionStop.Value : -1L;
                    if (currentVmStop == position)
                        return; // Already synced, avoid recursion

                    var oldStart = editor.SelectionStart;
                    var oldStop = e.OldValue is long old ? old : -1;
                    var oldLength = editor.SelectionLength;

                    var start = editor._viewModel.SelectionStart.IsValid ? editor._viewModel.SelectionStart : new VirtualPosition(position);
                    editor._viewModel.SetSelectionRange(start, new VirtualPosition(position));

                    // Fire events
                    if (oldStop != position || oldStart != editor.SelectionStart)
                    {
                        editor.OnSelectionChanged(new HexSelectionChangedEventArgs(editor.SelectionStart, position, editor.SelectionLength));

                        if (oldStop != position)
                            editor.OnSelectionStopChanged(EventArgs.Empty);
                        if (oldLength != editor.SelectionLength)
                            editor.OnSelectionLengthChanged(EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// BytePerLine DependencyProperty for XAML binding 
        /// </summary>
        public static readonly DependencyProperty BytePerLineProperty =
            DependencyProperty.Register(nameof(BytePerLine), typeof(int), typeof(HexEditor),
                new PropertyMetadata(16, OnBytePerLinePropertyChanged));

        private static void OnBytePerLinePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is int bytesPerLine && bytesPerLine > 0)
            {
                // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                if (editor._viewModel != null && editor._viewModel.BytePerLine == bytesPerLine)
                    return; // Already synced, avoid recursion

                // ALWAYS update viewport and headers (even during initialization when ViewModel doesn't exist yet)
                editor.HexViewport.BytesPerLine = bytesPerLine;
                editor.RefreshColumnHeader();
                editor.BytesPerLineText.Text = $"Bytes/Line: {bytesPerLine}";

                // Update ViewModel if it exists (may not exist during initial XAML loading)
                if (editor._viewModel != null)
                {
                    editor._viewModel.BytePerLine = bytesPerLine;

                    // Update scrollbar to reflect new total lines (with +3 margin to access last lines)
                    editor.VerticalScroll.Maximum = Math.Max(0, editor._viewModel.TotalLines - editor._viewModel.VisibleLines + 3);
                }
            }
        }

        /// <summary>
        /// EditMode DependencyProperty for XAML binding 
        /// </summary>
        public static readonly DependencyProperty EditModeProperty =
            DependencyProperty.Register(nameof(EditMode), typeof(Models.EditMode), typeof(HexEditor),
                new PropertyMetadata(Models.EditMode.Overwrite, OnEditModePropertyChanged));

        private static void OnEditModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Models.EditMode mode)
            {
                // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                if (editor._viewModel != null && editor._viewModel.EditMode == mode)
                    return; // Already synced, avoid recursion

                if (editor._viewModel != null)
                {
                    editor._viewModel.EditMode = mode;
                }

                // Sync to HexViewport for caret display
                editor.HexViewport.EditMode = mode;
                editor.HexViewport.InvalidateVisual(); // Refresh to show/hide caret

                // Update status bar
                editor.EditModeText.Text = mode == Models.EditMode.Insert ? "Mode: Insert" : "Mode: Overwrite";
            }
        }

        /// <summary>
        /// IsFileOrStreamLoaded Read-Only DependencyProperty for XAML binding
        /// </summary>
        private static readonly DependencyPropertyKey IsFileOrStreamLoadedPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsFileOrStreamLoaded), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsFileOrStreamLoadedProperty =
            IsFileOrStreamLoadedPropertyKey.DependencyProperty;

        /// <summary>
        /// IsOperationActive Read-Only DependencyProperty for XAML binding
        /// </summary>
        private static readonly DependencyPropertyKey IsOperationActivePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(IsOperationActive),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(false, OnIsOperationActiveChanged));

        public static readonly DependencyProperty IsOperationActiveProperty =
            IsOperationActivePropertyKey.DependencyProperty;

        private static void OnIsOperationActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool isActive)
            {
                try
                {
                    // CRITICAL: Check if control is still loaded (prevents crashes during app shutdown)
                    if (!editor.IsLoaded)
                        return;

                    // Notify CommandManager to re-evaluate all ICommand bindings
                    CommandManager.InvalidateRequerySuggested();

                    // Raise event for parent applications
                    editor.OnOperationStateChanged(new OperationStateChangedEventArgs(isActive));

                    // Update menu items enabled state
                    editor.UpdateMenuItemsEnabled(!isActive);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OnIsOperationActiveChanged error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// AllowContextMenu DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowContextMenuProperty =
            DependencyProperty.Register(nameof(AllowContextMenu), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnAllowContextMenuChanged));

        private static void OnAllowContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool allowed)
            {
                // Enable or disable context menu
                if (editor.ByteContextMenu != null)
                    editor.ContextMenu = allowed ? editor.ByteContextMenu : null;
            }
        }

        /// <summary>
        /// AllowZoom DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowZoomProperty =
            DependencyProperty.Register(nameof(AllowZoom), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnAllowZoomChanged));

        private static void OnAllowZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool allowed)
            {
                // Initialize zoom if enabled, or reset to 1.0 if disabled
                if (allowed)
                    editor.InitialiseZoom();
                else if (editor._scaler != null)
                {
                    editor._scaler.ScaleX = 1.0;
                    editor._scaler.ScaleY = 1.0;
                }
            }
        }

        /// <summary>
        /// Identifies the <see cref="MouseWheelSpeed"/> dependency property.
        /// Controls the mouse wheel scroll speed for vertical scrolling through the hex data.
        /// </summary>
        public static readonly DependencyProperty MouseWheelSpeedProperty =
            DependencyProperty.Register(nameof(MouseWheelSpeed), typeof(MouseWheelSpeed), typeof(HexEditor),
                new PropertyMetadata(Core.MouseWheelSpeed.Normal));

        /// <summary>
        /// AllowAutoHighLightSelectionByte DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowAutoHighLightSelectionByteProperty =
            DependencyProperty.Register(nameof(AllowAutoHighLightSelectionByte), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnAllowAutoHighLightChanged));

        private static void OnAllowAutoHighLightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                // Update auto-highlight byte value when feature is toggled
                editor.UpdateAutoHighlightByte();

                // Refresh viewport to show/hide auto-highlighting
                editor.HexViewport?.InvalidateVisual();
            }
        }

        /// <summary>
        /// AutoHighLiteSelectionByteBrush DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AutoHighLiteSelectionByteBrushProperty =
            DependencyProperty.Register(nameof(AutoHighLiteSelectionByteBrush), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromArgb(0x60, 0xFF, 0xFF, 0x00), OnAutoHighLiteColorChanged)); // 40% Yellow

        private static void OnAutoHighLiteColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is System.Windows.Media.Color color)
            {
                // Update HexViewport highlight brush
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.AutoHighLiteBrush = new SolidColorBrush(color);
                    editor.HexViewport.InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// AllowAutoSelectSameByteAtDoubleClick DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowAutoSelectSameByteAtDoubleClickProperty =
            DependencyProperty.Register(nameof(AllowAutoSelectSameByteAtDoubleClick), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false));

        /// <summary>
        /// AllowMarkerClickNavigation DependencyProperty for XAML binding
        /// Enables/disables navigation when clicking on scroll markers (default: true)
        /// </summary>
        public static readonly DependencyProperty AllowMarkerClickNavigationProperty =
            DependencyProperty.Register(nameof(AllowMarkerClickNavigation), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnAllowMarkerClickNavigationChanged));

        private static void OnAllowMarkerClickNavigationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor._scrollMarkers != null)
            {
                editor._scrollMarkers.AllowMarkerClickNavigation = (bool)e.NewValue;
            }
        }

        /// <summary>
        /// AllowFileDrop DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowFileDropProperty =
            DependencyProperty.Register(nameof(AllowFileDrop), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnAllowDropChanged));

        /// <summary>
        /// AllowTextDrop DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowTextDropProperty =
            DependencyProperty.Register(nameof(AllowTextDrop), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnAllowDropChanged));

        private static void OnAllowDropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                // Enable AllowDrop if either file or text drop is allowed
                editor.AllowDrop = editor.AllowFileDrop || editor.AllowTextDrop;
            }
        }

        /// <summary>
        /// FileDroppingConfirmation DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty FileDroppingConfirmationProperty =
            DependencyProperty.Register(nameof(FileDroppingConfirmation), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// AllowExtend DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowExtendProperty =
            DependencyProperty.Register(nameof(AllowExtend), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// AllowDeleteByte DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowDeleteByteProperty =
            DependencyProperty.Register(nameof(AllowDeleteByte), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// AllowByteCount DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowByteCountProperty =
            DependencyProperty.Register(nameof(AllowByteCount), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// ShowTblAscii DependencyProperty - Show ASCII characters in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblAsciiProperty =
            DependencyProperty.Register(nameof(ShowTblAscii), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// ShowTblDte DependencyProperty - Show DTE (Dual-Title Encoding) in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblDteProperty =
            DependencyProperty.Register(nameof(ShowTblDte), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// ShowTblMte DependencyProperty - Show MTE (Multi-Title Encoding) in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblMteProperty =
            DependencyProperty.Register(nameof(ShowTblMte), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// ShowTblJaponais DependencyProperty - Show Japanese characters in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblJaponaisProperty =
            DependencyProperty.Register(nameof(ShowTblJaponais), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// ShowTblEndBlock DependencyProperty - Show End Block markers in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblEndBlockProperty =
            DependencyProperty.Register(nameof(ShowTblEndBlock), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// ShowTblEndLine DependencyProperty - Show End Line markers in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblEndLineProperty =
            DependencyProperty.Register(nameof(ShowTblEndLine), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// Callback when any TBL type visibility changes - sync to HexViewport and refresh
        /// </summary>
        private static void OnTblTypeVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                // Sync all TBL type visibility flags to HexViewport
                editor.HexViewport.ShowTblAscii = editor.ShowTblAscii;
                editor.HexViewport.ShowTblDte = editor.ShowTblDte;
                editor.HexViewport.ShowTblMte = editor.ShowTblMte;
                editor.HexViewport.ShowTblJaponais = editor.ShowTblJaponais;
                editor.HexViewport.ShowTblEndBlock = editor.ShowTblEndBlock;
                editor.HexViewport.ShowTblEndLine = editor.ShowTblEndLine;

                editor.HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// TblDteColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblDteColorProperty =
            DependencyProperty.Register(nameof(TblDteColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Yellow, OnTblColorChanged));

        /// <summary>
        /// TblMteColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblMteColorProperty =
            DependencyProperty.Register(nameof(TblMteColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.LightBlue, OnTblColorChanged));

        /// <summary>
        /// TblEndBlockColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblEndBlockColorProperty =
            DependencyProperty.Register(nameof(TblEndBlockColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Red, OnTblColorChanged));

        /// <summary>
        /// TblEndLineColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblEndLineColorProperty =
            DependencyProperty.Register(nameof(TblEndLineColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Orange, OnTblColorChanged));

        /// <summary>
        /// TblAsciiColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblAsciiColorProperty =
            DependencyProperty.Register(nameof(TblAsciiColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.LightGreen, OnTblColorChanged));

        /// <summary>
        /// TblJaponaisColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblJaponaisColorProperty =
            DependencyProperty.Register(nameof(TblJaponaisColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Pink, OnTblColorChanged));

        /// <summary>
        /// Tbl3ByteColor DependencyProperty for XAML binding (3-byte sequences)
        /// </summary>
        public static readonly DependencyProperty Tbl3ByteColorProperty =
            DependencyProperty.Register(nameof(Tbl3ByteColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Cyan, OnTblColorChanged));

        /// <summary>
        /// Tbl4PlusByteColor DependencyProperty for XAML binding (4+ byte sequences)
        /// </summary>
        public static readonly DependencyProperty Tbl4PlusByteColorProperty =
            DependencyProperty.Register(nameof(Tbl4PlusByteColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Magenta, OnTblColorChanged));

        /// <summary>
        /// TblDefaultColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblDefaultColorProperty =
            DependencyProperty.Register(nameof(TblDefaultColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.White, OnTblColorChanged));

        private static void OnTblColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                // Sync all TBL colors to HexViewport for rendering
                editor.HexViewport.TblDteColor = editor.TblDteColor;
                editor.HexViewport.TblMteColor = editor.TblMteColor;
                editor.HexViewport.TblAsciiColor = editor.TblAsciiColor;
                editor.HexViewport.TblJaponaisColor = editor.TblJaponaisColor;
                editor.HexViewport.TblEndBlockColor = editor.TblEndBlockColor;
                editor.HexViewport.TblEndLineColor = editor.TblEndLineColor;
                editor.HexViewport.TblDefaultColor = editor.TblDefaultColor;
                editor.HexViewport.Tbl3ByteColor = editor.Tbl3ByteColor;         // NEW: 3-byte color
                editor.HexViewport.Tbl4PlusByteColor = editor.Tbl4PlusByteColor; // NEW: 4+ byte color

                editor.HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// BarChartColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty BarChartColorProperty =
            DependencyProperty.Register(nameof(BarChartColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Blue, OnBarChartColorChanged));

        private static void OnBarChartColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Color color && editor._barChartPanel != null)
            {
                editor._barChartPanel.BarColor = color;
            }
        }

        /// <summary>
        /// InlineBarChartColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty InlineBarChartColorProperty =
            DependencyProperty.Register(nameof(InlineBarChartColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0x00, 0x78, 0xD4), OnInlineBarChartColorChanged));

        private static void OnInlineBarChartColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Color color && editor.HexViewport != null)
            {
                // TODO: InlineBarChartColor property not yet implemented in HexViewport
                // editor.HexViewport.InlineBarChartColor = color;
                editor.HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// SelectionActiveBrush DependencyProperty for XAML binding
        /// Brush used for selection in the active panel (Hex or ASCII)
        /// </summary>
        public static readonly DependencyProperty SelectionActiveBrushProperty =
            DependencyProperty.Register(nameof(SelectionActiveBrush), typeof(Brush), typeof(HexEditor),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0x78, 0xD4)), OnSelectionBrushChanged));

        /// <summary>
        /// SelectionInactiveBrush DependencyProperty for XAML binding
        /// Brush used for selection in the inactive panel (Hex or ASCII)
        /// </summary>
        public static readonly DependencyProperty SelectionInactiveBrushProperty =
            DependencyProperty.Register(nameof(SelectionInactiveBrush), typeof(Brush), typeof(HexEditor),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x78, 0xD4)), OnSelectionBrushChanged));

        private static void OnSelectionBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Brush brush && editor.HexViewport != null)
            {
                // Update HexViewport brushes
                if (e.Property == SelectionActiveBrushProperty)
                {
                    editor.HexViewport.SelectionActiveBrush = brush;
                }
                else if (e.Property == SelectionInactiveBrushProperty)
                {
                    editor.HexViewport.SelectionInactiveBrush = brush;
                }
                editor.HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// DataStringVisual DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty DataStringVisualProperty =
            DependencyProperty.Register(nameof(DataStringVisual), typeof(DataVisualType), typeof(HexEditor),
                new PropertyMetadata(DataVisualType.Hexadecimal, OnDataStringVisualChanged));

        private static void OnDataStringVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is DataVisualType visualType)
            {
                // Cancel any in-progress byte edit (format-specific editing state no longer valid)
                if (editor._isEditingByte)
                {
                    editor._isEditingByte = false;
                    editor._editingPosition = VirtualPosition.Invalid;
                    editor._editingValue = 0;
                    editor._editingCharIndex = 0;
                    editor._editingMaxChars = 2;
                    editor._editingBuffer = "";
                }

                // Update HexViewport's DataStringVisual property
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.DataStringVisual = visualType;
                    editor.HexViewport.InvalidateVisual();

                    // Refresh column headers to match the new format
                    editor.RefreshColumnHeader();
                }
            }
        }

        /// <summary>
        /// OffSetStringVisual DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty OffSetStringVisualProperty =
            DependencyProperty.Register(nameof(OffSetStringVisual), typeof(DataVisualType), typeof(HexEditor),
                new PropertyMetadata(DataVisualType.Hexadecimal, OnOffSetStringVisualChanged));

        private static void OnOffSetStringVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is DataVisualType visualType)
            {
                // Update HexViewport's OffSetStringVisual property
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.OffSetStringVisual = visualType;
                    editor.HexViewport.InvalidateVisual();

                    // Update ActualOffsetWidth to reflect the new format
                    editor.ActualOffsetWidth = new GridLength(editor.HexViewport.ActualOffsetWidth);
                }
            }
        }

        /// <summary>
        /// ActualOffsetWidth DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ActualOffsetWidthProperty =
            DependencyProperty.Register(nameof(ActualOffsetWidth), typeof(GridLength), typeof(HexEditor),
                new PropertyMetadata(new GridLength(110.0))); // Default value matches hexadecimal format

        /// <summary>
        /// ByteOrder DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ByteOrderProperty =
            DependencyProperty.Register(nameof(ByteOrder), typeof(ByteOrderType), typeof(HexEditor),
                new PropertyMetadata(ByteOrderType.LoHi, OnByteOrderChanged));

        private static void OnByteOrderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is ByteOrderType byteOrder)
            {
                // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                if (editor._viewModel != null && editor._viewModel.ByteOrder == byteOrder)
                    return; // Already synced, avoid recursion

                if (editor._viewModel != null)
                {
                    editor._viewModel.ByteOrder = byteOrder;
                    // ByteOrder change triggers automatic RefreshVisibleLines() in ViewModel
                    // No need to call RefreshColumnHeader() - headers don't depend on ByteOrder
                }
            }
        }

        /// <summary>
        /// ByteSize DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ByteSizeProperty =
            DependencyProperty.Register(nameof(ByteSize), typeof(ByteSizeType), typeof(HexEditor),
                new PropertyMetadata(ByteSizeType.Bit8, OnByteSizeChanged));

        private static void OnByteSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is ByteSizeType byteSize)
            {
                // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                if (editor._viewModel != null && editor._viewModel.ByteSize == byteSize)
                {
                    return; // Already synced, avoid recursion
                }

                if (editor._viewModel != null)
                {
                    editor._viewModel.ByteSize = byteSize;
                    // ByteSize change triggers automatic ClearLineCache() + RefreshVisibleLines() in ViewModel
                    editor.RefreshColumnHeader(); // Update headers to reflect new stride
                    editor.HexViewport.InvalidateVisual(); // Force viewport redraw
                }
            }
        }

        /// <summary>
        /// BarChartPanelVisibility DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty BarChartPanelVisibilityProperty =
            DependencyProperty.Register(nameof(BarChartPanelVisibility), typeof(Visibility), typeof(HexEditor),
                new PropertyMetadata(Visibility.Collapsed, OnBarChartPanelVisibilityChanged));

        private static void OnBarChartPanelVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Visibility visibility)
            {
                if (editor.BarChartBorder != null)
                    editor.BarChartBorder.Visibility = visibility;

                // Update bar chart data when becoming visible
                if (visibility == Visibility.Visible)
                    editor.UpdateBarChart();
            }
        }

        #region Bar Chart Properties

        /// <summary>
        /// Bar chart panel height in pixels
        /// </summary>
        [Category("BarChart")]
        [DisplayName("Panel Height")]
        [Description("Height of the bar chart panel in pixels")]
        public int BarChartPanelHeight
        {
            get => (int)GetValue(BarChartPanelHeightProperty);
            set => SetValue(BarChartPanelHeightProperty, value);
        }

        public static readonly DependencyProperty BarChartPanelHeightProperty =
            DependencyProperty.Register(nameof(BarChartPanelHeight), typeof(int), typeof(HexEditor),
                new PropertyMetadata(200, OnBarChartPanelHeightChanged));

        private static void OnBarChartPanelHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is int height)
            {
                if (editor._barChartPanel != null)
                    editor._barChartPanel.Height = height;
            }
        }

        /// <summary>
        /// Bar chart background color
        /// </summary>
        [Category("Colors.Charts")]
        [DisplayName("Background Color")]
        public Color BarChartBackgroundColor
        {
            get => (Color)GetValue(BarChartBackgroundColorProperty);
            set => SetValue(BarChartBackgroundColorProperty, value);
        }

        public static readonly DependencyProperty BarChartBackgroundColorProperty =
            DependencyProperty.Register(nameof(BarChartBackgroundColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.White, OnBarChartBackgroundColorChanged));

        private static void OnBarChartBackgroundColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Color color && editor._barChartPanel != null)
            {
                editor._barChartPanel.BackgroundColor = color;
                editor._barChartPanel.InvalidateVisual();
            }
        }

        /// <summary>
        /// Bar chart text color for labels and statistics
        /// </summary>
        [Category("Colors.Charts")]
        [DisplayName("Text Color")]
        public Color BarChartTextColor
        {
            get => (Color)GetValue(BarChartTextColorProperty);
            set => SetValue(BarChartTextColorProperty, value);
        }

        public static readonly DependencyProperty BarChartTextColorProperty =
            DependencyProperty.Register(nameof(BarChartTextColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0x42, 0x42, 0x42), OnBarChartTextColorChanged));

        private static void OnBarChartTextColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Color color && editor._barChartPanel != null)
            {
                editor._barChartPanel.TextColor = color;
                editor._barChartPanel.InvalidateVisual();
            }
        }

        /// <summary>
        /// Show bar chart axis labels (00-FF)
        /// </summary>
        [Category("BarChart")]
        [DisplayName("Show Axis Labels")]
        public bool BarChartShowAxisLabels
        {
            get => (bool)GetValue(BarChartShowAxisLabelsProperty);
            set => SetValue(BarChartShowAxisLabelsProperty, value);
        }

        public static readonly DependencyProperty BarChartShowAxisLabelsProperty =
            DependencyProperty.Register(nameof(BarChartShowAxisLabels), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnBarChartShowAxisLabelsChanged));

        private static void OnBarChartShowAxisLabelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool show && editor._barChartPanel != null)
            {
                editor._barChartPanel.ShowAxisLabels = show;
                editor._barChartPanel.InvalidateVisual();
            }
        }

        /// <summary>
        /// Show bar chart grid lines
        /// </summary>
        [Category("BarChart")]
        [DisplayName("Show Grid Lines")]
        public bool BarChartShowGridLines
        {
            get => (bool)GetValue(BarChartShowGridLinesProperty);
            set => SetValue(BarChartShowGridLinesProperty, value);
        }

        public static readonly DependencyProperty BarChartShowGridLinesProperty =
            DependencyProperty.Register(nameof(BarChartShowGridLines), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnBarChartShowGridLinesChanged));

        private static void OnBarChartShowGridLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool show && editor._barChartPanel != null)
            {
                editor._barChartPanel.ShowGridLines = show;
                editor._barChartPanel.InvalidateVisual();
            }
        }

        /// <summary>
        /// Show bar chart statistics overlay
        /// </summary>
        [Category("BarChart")]
        [DisplayName("Show Statistics")]
        public bool BarChartShowStatistics
        {
            get => (bool)GetValue(BarChartShowStatisticsProperty);
            set => SetValue(BarChartShowStatisticsProperty, value);
        }

        public static readonly DependencyProperty BarChartShowStatisticsProperty =
            DependencyProperty.Register(nameof(BarChartShowStatistics), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnBarChartShowStatisticsChanged));

        private static void OnBarChartShowStatisticsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool show && editor._barChartPanel != null)
            {
                editor._barChartPanel.ShowStatistics = show;
                editor._barChartPanel.InvalidateVisual();
            }
        }

        #endregion

        /// <summary>
        /// DefaultCopyToClipboardMode DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty DefaultCopyToClipboardModeProperty =
            DependencyProperty.Register(nameof(DefaultCopyToClipboardMode), typeof(CopyPasteMode), typeof(HexEditor),
                new PropertyMetadata(CopyPasteMode.Auto));

        /// <summary>
        /// VisualCaretMode DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty VisualCaretModeProperty =
            DependencyProperty.Register(nameof(VisualCaretMode), typeof(CaretMode), typeof(HexEditor),
                new PropertyMetadata(CaretMode.Insert));

        /// <summary>
        /// ByteShiftLeft DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ByteShiftLeftProperty =
            DependencyProperty.Register(nameof(ByteShiftLeft), typeof(long), typeof(HexEditor),
                new PropertyMetadata(0L, OnByteShiftLeftChanged));

        private static void OnByteShiftLeftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is long byteShiftLeft)
            {
                // Prevent infinite loop - only update ViewModel if value actually changed
                if (editor._viewModel != null && editor._viewModel.ByteShiftLeft == byteShiftLeft)
                {
                    return; // Already synced, avoid recursion
                }

                if (editor._viewModel != null)
                {
                    editor._viewModel.ByteShiftLeft = byteShiftLeft;
                    // ViewModel triggers RefreshVisibleLines() which updates viewport
                }

                // Refresh viewport to update offset display
                editor.HexViewport?.InvalidateVisual();
            }
        }

        /// <summary>
        /// AppendNeedConfirmation DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AppendNeedConfirmationProperty =
            DependencyProperty.Register(nameof(AppendNeedConfirmation), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// CustomEncoding DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty CustomEncodingProperty =
            DependencyProperty.Register(nameof(CustomEncoding), typeof(System.Text.Encoding), typeof(HexEditor),
                new PropertyMetadata(System.Text.Encoding.UTF8, OnCustomEncodingChanged));

        private static void OnCustomEncodingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is System.Text.Encoding encoding)
            {
                // Refresh viewport to update text display with new encoding
                editor.HexViewport?.InvalidateVisual();
            }
        }

        /// <summary>
        /// PreloadByteInEditorMode DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty PreloadByteInEditorModeProperty =
            DependencyProperty.Register(nameof(PreloadByteInEditorMode), typeof(PreloadByteInEditor), typeof(HexEditor),
                new PropertyMetadata(PreloadByteInEditor.MaxScreenVisibleLineAtDataLoad));

        /// <summary>
        /// AllowCustomBackgroundBlock DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowCustomBackgroundBlockProperty =
            DependencyProperty.Register(nameof(AllowCustomBackgroundBlock), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnAllowCustomBackgroundBlockChanged));

        private static void OnAllowCustomBackgroundBlockChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool allowed)
            {
                // Sync with HexViewport - pass blocks if enabled, empty list if disabled
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.CustomBackgroundBlocks = allowed
                        ? editor._customBackgroundService.GetAllBlocks().ToList()
                        : new List<Core.CustomBackgroundBlock>();
                    editor.HexViewport.InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// ProgressRefreshRate DependencyProperty for configuring progress bar update frequency
        /// </summary>
        public static readonly DependencyProperty ProgressRefreshRateProperty =
            DependencyProperty.Register(nameof(ProgressRefreshRate), typeof(Models.ProgressRefreshRate), typeof(HexEditor),
                new PropertyMetadata(Models.ProgressRefreshRate.Fast, OnProgressRefreshRateChanged));

        /// <summary>
        /// Progress bar refresh rate for long-running operations (Open, Save, Find, Replace)
        /// </summary>
        public Models.ProgressRefreshRate ProgressRefreshRate
        {
            get => (Models.ProgressRefreshRate)GetValue(ProgressRefreshRateProperty);
            set => SetValue(ProgressRefreshRateProperty, value);
        }

        private static void OnProgressRefreshRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Models.ProgressRefreshRate rate)
            {
                // Update the long-running operation service with new refresh interval
                editor._longRunningService.MinProgressIntervalMs = (int)rate;
            }
        }

        #endregion

        #region Brush Properties (Color Wrappers)

        /// <summary>
        /// Selection first color as Brush. Use <see cref="SelectionFirstColor"/> (Color) for V2 code.
        /// </summary>
        /// <remarks>
        /// This property is provided for V1 compatibility. New code should use the Color-based property.
        /// </remarks>
        [Category("Colors.Legacy")]
        [Obsolete("Use SelectionFirstColor (Color property) instead. This Brush wrapper is for V1 compatibility only.", false)]
        public Brush SelectionFirstColorBrush
        {
            get => new SolidColorBrush(SelectionFirstColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    SelectionFirstColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Selection second color as Brush. Use SelectionSecondColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush SelectionSecondColorBrush
        {
            get => new SolidColorBrush(SelectionSecondColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    SelectionSecondColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Modified byte color as Brush. Use ByteModifiedColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ByteModifiedColorBrush
        {
            get => new SolidColorBrush(ByteModifiedColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ByteModifiedColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Added byte color as Brush. Use ByteAddedColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ByteAddedColorBrush
        {
            get => new SolidColorBrush(ByteAddedColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ByteAddedColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Highlight color as Brush. Use HighLightColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush HighLightColorBrush
        {
            get => new SolidColorBrush(HighLightColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    HighLightColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Mouse over color as Brush. Use MouseOverColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush MouseOverColorBrush
        {
            get => new SolidColorBrush(MouseOverColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    MouseOverColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Foreground second color as Brush. Use ForegroundSecondColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ForegroundSecondColorBrush
        {
            get => new SolidColorBrush(ForegroundSecondColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ForegroundSecondColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Offset header foreground color as Brush. Use ForegroundOffSetHeaderColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ForegroundOffSetHeaderColorBrush
        {
            get => new SolidColorBrush(ForegroundOffSetHeaderColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ForegroundOffSetHeaderColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Highlighted offset header foreground color as Brush. Use ForegroundHighLightOffSetHeaderColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ForegroundHighLightOffSetHeaderColorBrush
        {
            get => new SolidColorBrush(ForegroundHighLightOffSetHeaderColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ForegroundHighLightOffSetHeaderColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Foreground contrast color as Brush. Use ForegroundContrast (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ForegroundContrastBrush
        {
            get => new SolidColorBrush(ForegroundContrast);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ForegroundContrast = solidBrush.Color;
            }
        }

        #endregion

        #region Scroll Markers Properties

        /// <summary>
        /// Show bookmark markers on the scrollbar
        /// </summary>
        [Category("ScrollMarkers")]
        public bool ShowBookmarkMarkers
        {
            get => (bool)GetValue(ShowBookmarkMarkersProperty);
            set => SetValue(ShowBookmarkMarkersProperty, value);
        }

        public static readonly DependencyProperty ShowBookmarkMarkersProperty =
            DependencyProperty.Register(nameof(ShowBookmarkMarkers), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnScrollMarkerVisibilityChanged));

        /// <summary>
        /// Show modified byte markers on the scrollbar
        /// </summary>
        [Category("ScrollMarkers")]
        public bool ShowModifiedMarkers
        {
            get => (bool)GetValue(ShowModifiedMarkersProperty);
            set => SetValue(ShowModifiedMarkersProperty, value);
        }

        public static readonly DependencyProperty ShowModifiedMarkersProperty =
            DependencyProperty.Register(nameof(ShowModifiedMarkers), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnScrollMarkerVisibilityChanged));

        /// <summary>
        /// Show inserted byte markers on the scrollbar
        /// </summary>
        [Category("ScrollMarkers")]
        public bool ShowInsertedMarkers
        {
            get => (bool)GetValue(ShowInsertedMarkersProperty);
            set => SetValue(ShowInsertedMarkersProperty, value);
        }

        public static readonly DependencyProperty ShowInsertedMarkersProperty =
            DependencyProperty.Register(nameof(ShowInsertedMarkers), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnScrollMarkerVisibilityChanged));

        /// <summary>
        /// Show deleted byte markers on the scrollbar
        /// </summary>
        [Category("ScrollMarkers")]
        public bool ShowDeletedMarkers
        {
            get => (bool)GetValue(ShowDeletedMarkersProperty);
            set => SetValue(ShowDeletedMarkersProperty, value);
        }

        public static readonly DependencyProperty ShowDeletedMarkersProperty =
            DependencyProperty.Register(nameof(ShowDeletedMarkers), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnScrollMarkerVisibilityChanged));

        /// <summary>
        /// Show search result markers on the scrollbar
        /// </summary>
        [Category("ScrollMarkers")]
        public bool ShowSearchResultMarkers
        {
            get => (bool)GetValue(ShowSearchResultMarkersProperty);
            set => SetValue(ShowSearchResultMarkersProperty, value);
        }

        public static readonly DependencyProperty ShowSearchResultMarkersProperty =
            DependencyProperty.Register(nameof(ShowSearchResultMarkers), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnScrollMarkerVisibilityChanged));

        /// <summary>
        /// Callback when any scroll marker visibility changes
        /// </summary>
        private static void OnScrollMarkerVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                // Force update of scroll markers panel
                editor.UpdateScrollMarkersVisibility();
            }
        }

        #endregion

        #region Keyboard Shortcuts Properties

        /// <summary>
        /// Allow builtin Ctrl+C
        /// </summary>
        [Category("Keyboard")]
        public bool AllowBuildinCtrlc
        {
            get => (bool)GetValue(AllowBuildinCtrlcProperty);
            set => SetValue(AllowBuildinCtrlcProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlcProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrlc), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Allow builtin Ctrl+V
        /// </summary>
        [Category("Keyboard")]
        public bool AllowBuildinCtrlv
        {
            get => (bool)GetValue(AllowBuildinCtrlvProperty);
            set => SetValue(AllowBuildinCtrlvProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlvProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrlv), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Allow builtin Ctrl+A
        /// </summary>
        [Category("Keyboard")]
        public bool AllowBuildinCtrla
        {
            get => (bool)GetValue(AllowBuildinCtrlaProperty);
            set => SetValue(AllowBuildinCtrlaProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlaProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrla), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Allow builtin Ctrl+Z
        /// </summary>
        [Category("Keyboard")]
        public bool AllowBuildinCtrlz
        {
            get => (bool)GetValue(AllowBuildinCtrlzProperty);
            set => SetValue(AllowBuildinCtrlzProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlzProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrlz), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Allow builtin Ctrl+Y
        /// </summary>
        [Category("Keyboard")]
        public bool AllowBuildinCtrly
        {
            get => (bool)GetValue(AllowBuildinCtrlyProperty);
            set => SetValue(AllowBuildinCtrlyProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlyProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrly), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        #endregion

        #region Visibility Properties

        /// <summary>
        /// Header visibility. Use ShowHeader (bool) for V2 code.
        /// </summary>
        [Category("Display")]
        public Visibility HeaderVisibility
        {
            get => ShowHeader ? Visibility.Visible : Visibility.Collapsed;
            set => ShowHeader = (value == Visibility.Visible);
        }

        /// <summary>
        /// Status bar visibility. Use ShowStatusBar (bool) for V2 code.
        /// </summary>
        [Category("Display")]
        public Visibility StatusBarVisibility
        {
            get => ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
            set => ShowStatusBar = (value == Visibility.Visible);
        }

        /// <summary>
        /// Line info (offset column) visibility. Use ShowOffset (bool) for V2 code.
        /// </summary>
        [Category("Display")]
        public Visibility LineInfoVisibility
        {
            get => ShowOffset ? Visibility.Visible : Visibility.Collapsed;
            set => ShowOffset = (value == Visibility.Visible);
        }

        /// <summary>
        /// String data (ASCII) panel visibility. Use ShowAscii (bool) for V2 code.
        /// </summary>
        [Category("Display")]
        public Visibility StringDataVisibility
        {
            get => ShowAscii ? Visibility.Visible : Visibility.Collapsed;
            set => ShowAscii = (value == Visibility.Visible);
        }

        /// <summary>
        /// Hex data panel visibility. Always Visible in V2 (cannot be hidden).
        /// </summary>
        [Category("Display")]
        public Visibility HexDataVisibility
        {
            get => Visibility.Visible;
            set { /* V2 does not support hiding hex panel */ }
        }

        /// <summary>
        /// Bar chart panel visibility
        /// </summary>
        [Category("BarChart")]
        [DisplayName("Panel Visibility")]
        public Visibility BarChartPanelVisibility
        {
            get => (Visibility)GetValue(BarChartPanelVisibilityProperty);
            set => SetValue(BarChartPanelVisibilityProperty, value);
        }

        #endregion

        #region Long-Running Operations Event Handlers

        /// <summary>
        /// Handle operation started event
        /// </summary>
        private void LongRunningService_OperationStarted(object sender, Events.OperationProgressEventArgs e)
        {
            // Check if dispatcher is valid (control might be disposed)
            if (Dispatcher == null || !Dispatcher.CheckAccess() && Dispatcher.HasShutdownStarted)
                return;

            // Use BeginInvoke for non-blocking UI updates
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // CRITICAL: Check if control is still loaded (prevents crashes during app shutdown)
                if (!IsLoaded || ProgressOverlay == null)
                    return;

                try
                {
                    // Set operation active flag (disables commands and menu items)
                    IsOperationActive = true;

                    // Show progress overlay
                    ProgressOverlay.Visibility = Visibility.Visible;
                    ProgressOverlay.ViewModel.OperationTitle = e.OperationTitle;
                    ProgressOverlay.ViewModel.StatusMessage = e.StatusMessage;
                    ProgressOverlay.ViewModel.ProgressPercentage = e.ProgressPercentage;
                    ProgressOverlay.ViewModel.IsIndeterminate = e.IsIndeterminate;
                    ProgressOverlay.ViewModel.CanCancel = e.CanCancel;

                    // Change cursor to Wait
                    Mouse.OverrideCursor = Cursors.Wait;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OperationStarted error: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// Handle operation progress event
        /// </summary>
        private void LongRunningService_OperationProgress(object sender, Events.OperationProgressEventArgs e)
        {
            // Check if dispatcher is valid (control might be disposed)
            if (Dispatcher == null || !Dispatcher.CheckAccess() && Dispatcher.HasShutdownStarted)
                return;

            // Use BeginInvoke for non-blocking UI updates
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // CRITICAL: Check if control is still loaded (prevents crashes during app shutdown)
                if (!IsLoaded || ProgressOverlay == null)
                    return;

                try
                {
                    // Update progress
                    ProgressOverlay.ViewModel.ProgressPercentage = e.ProgressPercentage;
                    ProgressOverlay.ViewModel.StatusMessage = e.StatusMessage;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OperationProgress error: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// Handle operation completed event
        /// </summary>
        private void LongRunningService_OperationCompleted(object sender, Events.OperationCompletedEventArgs e)
        {
            // Check if dispatcher is valid (control might be disposed)
            if (Dispatcher == null || !Dispatcher.CheckAccess() && Dispatcher.HasShutdownStarted)
                return;

            // Use BeginInvoke for non-blocking UI updates
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // CRITICAL: Check if control is still loaded (prevents crashes during app shutdown)
                if (!IsLoaded)
                    return;

                try
                {
                    // Clear operation active flag (re-enables commands and menu items)
                    IsOperationActive = false;

                    // Hide progress overlay
                    if (ProgressOverlay != null)
                        ProgressOverlay.Visibility = Visibility.Collapsed;

                    // Restore cursor
                    Mouse.OverrideCursor = null;

                    // Update status bar
                    if (!e.Success && StatusText != null)
                    {
                        if (e.WasCancelled)
                            StatusText.Text = "Operation cancelled";
                        else
                            StatusText.Text = $"Operation failed: {e.ErrorMessage}";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OperationCompleted error: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// Enable or disable menu items based on operation state.
        /// Read-only operations (Copy, SelectAll) remain enabled.
        /// </summary>
        /// <param name="enabled">True to enable menu items, false to disable</param>
        private void UpdateMenuItemsEnabled(bool enabled)
        {
            // CRITICAL: Check if control is still loaded (prevents crashes during app shutdown)
            if (!IsLoaded || ByteContextMenu == null)
            {
                System.Diagnostics.Debug.WriteLine("UpdateMenuItemsEnabled: Control not loaded or ByteContextMenu is NULL");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"UpdateMenuItemsEnabled: enabled={enabled}, Items count={ByteContextMenu.Items.Count}");

                foreach (var item in ByteContextMenu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        UpdateMenuItemEnabledRecursive(menuItem, enabled);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateMenuItemsEnabled error: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively update menu item and its children
        /// </summary>
        private void UpdateMenuItemEnabledRecursive(MenuItem menuItem, bool enabled)
        {
            if (menuItem == null)
                return;

            try
            {
                // Keep read-only operations always enabled
                if (ShouldAlwaysBeEnabled(menuItem))
                {
                    System.Diagnostics.Debug.WriteLine($"  MenuItem '{menuItem.Name}' ALWAYS enabled (read-only)");
                    menuItem.IsEnabled = true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  MenuItem '{menuItem.Name}' set to {enabled}");
                    menuItem.IsEnabled = enabled;
                }

                // Recursively update submenu items
                foreach (var subItem in menuItem.Items)
                {
                    if (subItem is MenuItem subMenuItem)
                    {
                        UpdateMenuItemEnabledRecursive(subMenuItem, enabled);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateMenuItemEnabledRecursive error for '{menuItem?.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Determines if a menu item should always be enabled (read-only operations).
        /// </summary>
        /// <param name="menuItem">The menu item to check</param>
        /// <returns>True if the item should stay enabled during operations</returns>
        private bool ShouldAlwaysBeEnabled(MenuItem menuItem)
        {
            if (menuItem == null)
                return false;

            // Debug: Check if our field references are null
            if (CopyMenuItem == null)
                System.Diagnostics.Debug.WriteLine("WARNING: CopyMenuItem field is NULL!");
            if (SelectAllMenuItem == null)
                System.Diagnostics.Debug.WriteLine("WARNING: SelectAllMenuItem field is NULL!");

            // Copy commands are read-only and safe
            bool isReadOnly = menuItem == CopyMenuItem ||
                   menuItem == CopyAsMenuItem ||
                   menuItem == CopyHexaMenuItem ||
                   menuItem == CopyAsciiMenuItem ||
                   menuItem == CopyCSharpMenuItem ||
                   menuItem == CopyCMenuItem ||
                   menuItem == CopyTblMenuItem ||
                   menuItem == CopyFormattedViewMenuItem ||
                   menuItem == SelectAllMenuItem;

            System.Diagnostics.Debug.WriteLine($"    ShouldAlwaysBeEnabled('{menuItem.Name ?? menuItem.Header?.ToString()}') = {isReadOnly}");
            return isReadOnly;
        }

        #endregion

        #endregion

    }
}
