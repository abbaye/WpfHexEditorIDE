//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
    /// NOTE: This is V2 (formerly HexEditor) - 99% faster with critical bug fixes.
    /// For legacy V1 control, use HexEditorLegacy (deprecated, will be removed in v3.0).
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

        // Highlights  - stores ranges of highlighted bytes
        private readonly List<(long start, long length)> _highlights = new List<(long, long)>();

        // TBL (Character Table) support 
        private TblStream _tblStream;
        private CharacterTableType _characterTableType = CharacterTableType.Ascii;

        // Zoom support 
        private ScaleTransform _scaler;

        // Hex editing state
        private bool _isEditingByte = false;
        private VirtualPosition _editingPosition = VirtualPosition.Invalid;
        private byte _editingValue = 0;
        private bool _editingHighNibble = true; // true = high nibble, false = low nibble
        private bool _isAsciiEditMode = false; // true = editing in ASCII area, false = editing in Hex area

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
            if (_scrollMarkers != null)
            {
                _scrollMarkers.MarkerClicked += ScrollMarkers_MarkerClicked;
            }

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
            }

            // Initialize zoom system
            InitialiseZoom();
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
        /// </summary>
        private void HexViewport_ByteRightClick(object sender, Controls.ByteRightClickEventArgs e)
        {
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

            // Update selection range in ViewModel
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

            switch (e.Key)
            {
                case System.Windows.Input.Key.Left:
                    newPos = Math.Max(0, currentPos - 1);
                    break;

                case System.Windows.Input.Key.Right:
                    newPos = Math.Min(_viewModel.VirtualLength - 1, currentPos + 1);
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
        /// Allow context menu  - DependencyProperty
        /// </summary>
        public bool AllowContextMenu
        {
            get => (bool)GetValue(AllowContextMenuProperty);
            set => SetValue(AllowContextMenuProperty, value);
        }

        /// <summary>
        /// Allow zoom  - DependencyProperty
        /// </summary>
        public bool AllowZoom
        {
            get => (bool)GetValue(AllowZoomProperty);
            set => SetValue(AllowZoomProperty, value);
        }

        /// <summary>
        /// Mouse wheel scroll speed  - DependencyProperty
        /// </summary>
        public MouseWheelSpeed MouseWheelSpeed
        {
            get => (MouseWheelSpeed)GetValue(MouseWheelSpeedProperty);
            set => SetValue(MouseWheelSpeedProperty, value);
        }

        /// <summary>
        /// Data string display format (Hex/Decimal/Octal/Binary) - DependencyProperty
        /// </summary>
        public DataVisualType DataStringVisual
        {
            get => (DataVisualType)GetValue(DataStringVisualProperty);
            set => SetValue(DataStringVisualProperty, value);
        }

        /// <summary>
        /// Offset string display format (Hex/Decimal/Octal/Binary) - DependencyProperty
        /// </summary>
        public DataVisualType OffSetStringVisual
        {
            get => (DataVisualType)GetValue(OffSetStringVisualProperty);
            set => SetValue(OffSetStringVisualProperty, value);
        }

        /// <summary>
        /// Byte order (Lo-Hi / Hi-Lo) - DependencyProperty
        /// </summary>
        public ByteOrderType ByteOrder
        {
            get => (ByteOrderType)GetValue(ByteOrderProperty);
            set => SetValue(ByteOrderProperty, value);
        }

        /// <summary>
        /// Byte size display (8/16/32-bit) - DependencyProperty
        /// </summary>
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
        public PreloadByteInEditor PreloadByteInEditorMode
        {
            get => (PreloadByteInEditor)GetValue(PreloadByteInEditorModeProperty);
            set => SetValue(PreloadByteInEditorModeProperty, value);
        }

        // TBL Advanced Features  - DependencyProperties

        /// <summary>
        /// Show MTE (Multi-Title Encoding) in TBL - DependencyProperty
        /// </summary>
        public bool TblShowMte
        {
            get => (bool)GetValue(TblShowMteProperty);
            set => SetValue(TblShowMteProperty, value);
        }

        /// <summary>
        /// DTE (Dual-Tile Encoding) color - DependencyProperty
        /// </summary>
        public System.Windows.Media.Color TblDteColor
        {
            get => (System.Windows.Media.Color)GetValue(TblDteColorProperty);
            set => SetValue(TblDteColorProperty, value);
        }

        /// <summary>
        /// MTE (Multi-Title Encoding) color - DependencyProperty
        /// </summary>
        public System.Windows.Media.Color TblMteColor
        {
            get => (System.Windows.Media.Color)GetValue(TblMteColorProperty);
            set => SetValue(TblMteColorProperty, value);
        }

        /// <summary>
        /// End block color for TBL - DependencyProperty
        /// </summary>
        public System.Windows.Media.Color TblEndBlockColor
        {
            get => (System.Windows.Media.Color)GetValue(TblEndBlockColorProperty);
            set => SetValue(TblEndBlockColorProperty, value);
        }

        /// <summary>
        /// End line color for TBL - DependencyProperty
        /// </summary>
        public System.Windows.Media.Color TblEndLineColor
        {
            get => (System.Windows.Media.Color)GetValue(TblEndLineColorProperty);
            set => SetValue(TblEndLineColorProperty, value);
        }

        /// <summary>
        /// Default color for TBL - DependencyProperty
        /// </summary>
        public System.Windows.Media.Color TblDefaultColor
        {
            get => (System.Windows.Media.Color)GetValue(TblDefaultColorProperty);
            set => SetValue(TblDefaultColorProperty, value);
        }

        // Bar Chart Panel color  - DependencyProperty

        /// <summary>
        /// Bar chart color - DependencyProperty
        /// </summary>
        public System.Windows.Media.Color BarChartColor
        {
            get => (System.Windows.Media.Color)GetValue(BarChartColorProperty);
            set => SetValue(BarChartColorProperty, value);
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
        public bool IsFileOrStreamLoaded
        {
            get => (bool)GetValue(IsFileOrStreamLoadedProperty);
            private set => SetValue(IsFileOrStreamLoadedPropertyKey, value);
        }

        /// <summary>
        /// Current edit mode - DependencyProperty for XAML binding
        /// </summary>
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
        public bool IsOnLongProcess
        {
            get => _isOnLongProcess;
            set => _isOnLongProcess = value;
        }

        private double _longProcessProgress = 0;
        /// <summary>
        /// Progress of current long process (0.0 to 1.0)
        /// </summary>
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
        public string FileName
        {
            get => (string)GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        /// <summary>
        /// Has the file been modified?
        /// Uses DependencyProperty for XAML binding support
        /// </summary>
        public bool IsModified
        {
            get => (bool)GetValue(IsModifiedProperty);
            set => SetValue(IsModifiedProperty, value);
        }

        /// <summary>
        /// Current cursor position (virtual)
        /// </summary>
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
        public long SelectionStart
        {
            get => (long)GetValue(SelectionStartProperty);
            set => SetValue(SelectionStartProperty, value);
        }

        /// <summary>
        /// Selection stop position (virtual) (DependencyProperty for XAML binding)
        /// </summary>
        public long SelectionStop
        {
            get => (long)GetValue(SelectionStopProperty);
            set => SetValue(SelectionStopProperty, value);
        }

        /// <summary>
        /// Read-only mode (DependencyProperty for XAML binding)
        /// </summary>
        public bool ReadOnlyMode
        {
            get => (bool)GetValue(ReadOnlyModeProperty);
            set => SetValue(ReadOnlyModeProperty, value);
        }

        /// <summary>
        /// Show or hide the status bar 
        /// </summary>
        public bool ShowStatusBar
        {
            get => StatusBar.Visibility == Visibility.Visible;
            set => StatusBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Status Bar Item Visibility Properties

        /// <summary>
        /// Show or hide status message in status bar
        /// </summary>
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
        /// Number of bytes per line (8, 16, 32, etc.) - DependencyProperty for XAML binding
        /// </summary>
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

        /// <summary>
        /// Show column separator between hex and ASCII
        /// </summary>
        public bool ShowColumnSeparator
        {
            get => (bool)GetValue(ShowColumnSeparatorProperty);
            set => SetValue(ShowColumnSeparatorProperty, value);
        }

        public static readonly DependencyProperty ShowColumnSeparatorProperty =
            DependencyProperty.Register(nameof(ShowColumnSeparator), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        #endregion

        #region Byte Spacer Properties

        /// <summary>
        /// Get or set the byte spacing position 
        /// </summary>
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
            }
        }

        /// <summary>
        /// Second selection gradient color 
        /// </summary>
        public Color SelectionSecondColor
        {
            get => (Color)GetValue(SelectionSecondColorProperty);
            set => SetValue(SelectionSecondColorProperty, value);
        }

        public static readonly DependencyProperty SelectionSecondColorProperty =
            DependencyProperty.Register(nameof(SelectionSecondColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromArgb(102, 0, 120, 212)));

        /// <summary>
        /// Color for modified bytes 
        /// </summary>
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
                editor.Resources["ModifiedBrush"] = new SolidColorBrush((Color)e.NewValue);
            }
        }

        /// <summary>
        /// Color for deleted bytes 
        /// </summary>
        public Color ByteDeletedColor
        {
            get => (Color)GetValue(ByteDeletedColorProperty);
            set => SetValue(ByteDeletedColorProperty, value);
        }

        public static readonly DependencyProperty ByteDeletedColorProperty =
            DependencyProperty.Register(nameof(ByteDeletedColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(244, 67, 54), OnByteDeletedColorChanged));

        private static void OnByteDeletedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                editor.Resources["DeletedBrush"] = new SolidColorBrush((Color)e.NewValue);
            }
        }

        /// <summary>
        /// Color for added bytes 
        /// </summary>
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
                editor.Resources["AddedBrush"] = new SolidColorBrush((Color)e.NewValue);
            }
        }

        /// <summary>
        /// Color for highlighted bytes 
        /// </summary>
        public Color HighLightColor
        {
            get => (Color)GetValue(HighLightColorProperty);
            set => SetValue(HighLightColorProperty, value);
        }

        public static readonly DependencyProperty HighLightColorProperty =
            DependencyProperty.Register(nameof(HighLightColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Gold));

        /// <summary>
        /// Mouse over color 
        /// </summary>
        public Color MouseOverColor
        {
            get => (Color)GetValue(MouseOverColorProperty);
            set => SetValue(MouseOverColorProperty, value);
        }

        public static readonly DependencyProperty MouseOverColorProperty =
            DependencyProperty.Register(nameof(MouseOverColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(227, 242, 253), OnMouseOverColorChanged));

        private static void OnMouseOverColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                editor.Resources["ByteHoverBrush"] = new SolidColorBrush((Color)e.NewValue);
            }
        }

        /// <summary>
        /// Foreground color for alternate bytes (uses Color instead of Brush)
        /// </summary>
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
                var brush = new SolidColorBrush((Color)e.NewValue);
                editor.Resources["AlternateByteForegroundBrush"] = brush;

                // Update HexViewport colors
                if (editor.HexViewport != null)
                {
                    var normalBrush = editor.Resources["ByteForegroundBrush"] as Brush;
                    editor.HexViewport.SetByteForegroundColors(normalBrush, brush);
                }
            }
        }

        /// <summary>
        /// Foreground color for offset header 
        /// </summary>
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
                editor.Resources["OffsetBrush"] = new SolidColorBrush((Color)e.NewValue);
            }
        }

        /// <summary>
        /// Foreground highlight offset header color 
        /// </summary>
        public Color ForegroundHighLightOffSetHeaderColor
        {
            get => (Color)GetValue(ForegroundHighLightOffSetHeaderColorProperty);
            set => SetValue(ForegroundHighLightOffSetHeaderColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundHighLightOffSetHeaderColorProperty =
            DependencyProperty.Register(nameof(ForegroundHighLightOffSetHeaderColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.DarkBlue));

        /// <summary>
        /// Foreground contrast color 
        /// </summary>
        public Color ForegroundContrast
        {
            get => (Color)GetValue(ForegroundContrastProperty);
            set => SetValue(ForegroundContrastProperty, value);
        }

        public static readonly DependencyProperty ForegroundContrastProperty =
            DependencyProperty.Register(nameof(ForegroundContrast), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Black));

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
        /// MouseWheelSpeed DependencyProperty for XAML binding
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
        /// TblShowMte DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblShowMteProperty =
            DependencyProperty.Register(nameof(TblShowMte), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnTblShowMteChanged));

        private static void OnTblShowMteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
                editor.HexViewport?.InvalidateVisual();
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
        /// TblDefaultColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblDefaultColorProperty =
            DependencyProperty.Register(nameof(TblDefaultColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.White, OnTblColorChanged));

        private static void OnTblColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
                editor.HexViewport?.InvalidateVisual();
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
        /// DataStringVisual DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty DataStringVisualProperty =
            DependencyProperty.Register(nameof(DataStringVisual), typeof(DataVisualType), typeof(HexEditor),
                new PropertyMetadata(DataVisualType.Hexadecimal));

        /// <summary>
        /// OffSetStringVisual DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty OffSetStringVisualProperty =
            DependencyProperty.Register(nameof(OffSetStringVisual), typeof(DataVisualType), typeof(HexEditor),
                new PropertyMetadata(DataVisualType.Hexadecimal));

        /// <summary>
        /// ByteOrder DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ByteOrderProperty =
            DependencyProperty.Register(nameof(ByteOrder), typeof(ByteOrderType), typeof(HexEditor),
                new PropertyMetadata(ByteOrderType.LoHi));

        /// <summary>
        /// ByteSize DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ByteSizeProperty =
            DependencyProperty.Register(nameof(ByteSize), typeof(ByteSizeType), typeof(HexEditor),
                new PropertyMetadata(ByteSizeType.Bit8));

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

        /// <summary>
        /// HideByteDeleted DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty HideByteDeletedProperty =
            DependencyProperty.Register(nameof(HideByteDeleted), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnHideByteDeletedChanged));

        private static void OnHideByteDeletedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                // Refresh viewport to show/hide deleted bytes
                editor.HexViewport?.InvalidateVisual();
            }
        }

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
            if (d is HexEditor editor)
            {
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
                    editor.HexViewport.CustomBackgroundBlocks = allowed ? editor._customBackgroundBlocks : new List<Core.CustomBackgroundBlock>();
                    editor.HexViewport.InvalidateVisual();
                }
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
        /// Deleted byte color as Brush. Use ByteDeletedColor (Color) for V2 code.
        /// </summary>
        public Brush ByteDeletedColorBrush
        {
            get => new SolidColorBrush(ByteDeletedColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ByteDeletedColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Added byte color as Brush. Use ByteAddedColor (Color) for V2 code.
        /// </summary>
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

        #region Keyboard Shortcuts Properties

        /// <summary>
        /// Allow builtin Ctrl+C 
        /// </summary>
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
        public Visibility HeaderVisibility
        {
            get => ShowHeader ? Visibility.Visible : Visibility.Collapsed;
            set => ShowHeader = (value == Visibility.Visible);
        }

        /// <summary>
        /// Status bar visibility. Use ShowStatusBar (bool) for V2 code.
        /// </summary>
        public Visibility StatusBarVisibility
        {
            get => ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
            set => ShowStatusBar = (value == Visibility.Visible);
        }

        /// <summary>
        /// Line info (offset column) visibility. Use ShowOffset (bool) for V2 code.
        /// </summary>
        public Visibility LineInfoVisibility
        {
            get => ShowOffset ? Visibility.Visible : Visibility.Collapsed;
            set => ShowOffset = (value == Visibility.Visible);
        }

        /// <summary>
        /// String data (ASCII) panel visibility. Use ShowAscii (bool) for V2 code.
        /// </summary>
        public Visibility StringDataVisibility
        {
            get => ShowAscii ? Visibility.Visible : Visibility.Collapsed;
            set => ShowAscii = (value == Visibility.Visible);
        }

        /// <summary>
        /// Hex data panel visibility. Always Visible in V2 (cannot be hidden).
        /// </summary>
        public Visibility HexDataVisibility
        {
            get => Visibility.Visible;
            set { /* V2 does not support hiding hex panel */ }
        }

        /// <summary>
        /// Bar chart panel visibility
        /// </summary>
        public Visibility BarChartPanelVisibility
        {
            get => (Visibility)GetValue(BarChartPanelVisibilityProperty);
            set => SetValue(BarChartPanelVisibilityProperty, value);
        }

        #endregion

        #endregion

    }
}
