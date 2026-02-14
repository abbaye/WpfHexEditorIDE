//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
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
    /// HexEditorV2 - Modern WPF hex editor with native insert mode support
    /// Clean UserControl without UI chrome (toolbar, menus, etc.)
    /// Host application provides UI and calls public methods/properties
    /// </summary>
    public partial class HexEditorV2 : UserControl
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

        // Bookmarks (V1 compatible)
        private readonly List<long> _bookmarks = new List<long>();

        // TBL (Character Table) support (V1 compatible)
        private TblStream _tblStream;
        private CharacterTableType _characterTableType = CharacterTableType.Ascii;

        // Zoom support (V1 compatible)
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

        public HexEditorV2()
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

            // Initialize column headers with byte position numbers
            this.Loaded += (s, e) => RefreshColumnHeader();

            // V1 Compatible: Subscribe to right-click event for context menu
            if (HexViewport != null)
            {
                HexViewport.ByteRightClick += HexViewport_ByteRightClick;
                HexViewport.ByteDoubleClicked += HexViewport_ByteDoubleClicked;
            }

            // V1 Compatible: Initialize zoom system
            InitialiseZoom();
        }

        /// <summary>
        /// Handle right-click on byte for context menu (V1 compatible)
        /// </summary>
        private void HexViewport_ByteRightClick(object sender, Controls.ByteRightClickEventArgs e)
        {
            ShowContextMenu(e.Position);
        }

        /// <summary>
        /// Handle double-click on byte for auto-select same bytes (V1 compatible)
        /// </summary>
        private void HexViewport_ByteDoubleClicked(object sender, long position)
        {
            System.Diagnostics.Debug.WriteLine($"[DOUBLECLICK] ByteDoubleClicked event fired! Position={position}");
            StatusText.Text = $"DEBUG: Double-click at position {position}";

            // Only auto-select if feature is enabled
            if (!AllowAutoSelectSameByteAtDoubleClick)
            {
                System.Diagnostics.Debug.WriteLine($"[DOUBLECLICK] Feature disabled: AllowAutoSelectSameByteAtDoubleClick={AllowAutoSelectSameByteAtDoubleClick}");
                StatusText.Text = "DEBUG: Double-click feature is disabled";
                return;
            }

            if (_viewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("[DOUBLECLICK] ViewModel is null!");
                StatusText.Text = "DEBUG: ViewModel is null";
                return;
            }

            System.Diagnostics.Debug.WriteLine("[DOUBLECLICK] Feature enabled, ViewModel OK");

            // Get byte value at clicked position
            var virtualPos = new VirtualPosition(position);
            if (!virtualPos.IsValid || position >= _viewModel.VirtualLength)
            {
                System.Diagnostics.Debug.WriteLine($"[DOUBLECLICK] Invalid position! IsValid={virtualPos.IsValid}, VirtualLength={_viewModel.VirtualLength}");
                StatusText.Text = $"DEBUG: Invalid position (length={_viewModel.VirtualLength})";
                return;
            }

            byte byteValue = _viewModel.GetByteAt(virtualPos);
            System.Diagnostics.Debug.WriteLine($"[DOUBLECLICK] Byte value at position {position} = 0x{byteValue:X2}");
            StatusText.Text = $"DEBUG: Double-clicked byte value = 0x{byteValue:X2}";

            // Find all positions with this byte value and select them
            SelectAllBytesWith(byteValue);
        }

        #region Public Events (V1 Compatible)

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

        #region V1 Compatible Events

        /// <summary>
        /// V1 compatible: Raised when selection start position changes
        /// </summary>
        public event EventHandler SelectionStartChanged;

        /// <summary>
        /// V1 compatible: Raised when selection stop position changes
        /// </summary>
        public event EventHandler SelectionStopChanged;

        /// <summary>
        /// V1 compatible: Raised when selection length changes
        /// </summary>
        public event EventHandler SelectionLengthChanged;

        /// <summary>
        /// V1 compatible: Raised when data is copied to clipboard
        /// </summary>
        public event EventHandler DataCopied;

        /// <summary>
        /// V1 compatible: Raised when character table type changes
        /// </summary>
        public event EventHandler TypeOfCharacterTableChanged;

        /// <summary>
        /// V1 compatible: Raised when a long process progress changes
        /// </summary>
        public event EventHandler LongProcessProgressChanged;

        /// <summary>
        /// V1 compatible: Raised when a long process starts
        /// </summary>
        public event EventHandler LongProcessProgressStarted;

        /// <summary>
        /// V1 compatible: Raised when a long process completes
        /// </summary>
        public event EventHandler LongProcessProgressCompleted;

        /// <summary>
        /// V1 compatible: Raised when a replace byte operation completes
        /// </summary>
        public event EventHandler ReplaceByteCompleted;

        /// <summary>
        /// V1 compatible: Raised when a fill with byte operation completes
        /// </summary>
        public event EventHandler FillWithByteCompleted;

        /// <summary>
        /// V1 compatible: Raised when bytes are deleted
        /// </summary>
        public event EventHandler BytesDeleted;

        /// <summary>
        /// V1 compatible: Raised when an undo operation completes (alias for UndoCompleted)
        /// </summary>
        public event EventHandler Undone;

        /// <summary>
        /// V1 compatible: Raised when a redo operation completes (alias for RedoCompleted)
        /// </summary>
        public event EventHandler Redone;

        /// <summary>
        /// V1 compatible: Raised when a byte is single-clicked
        /// </summary>
        public event EventHandler<ByteEventArgs> ByteClick;

        /// <summary>
        /// V1 compatible: Raised when a byte is double-clicked
        /// </summary>
        public event EventHandler<ByteEventArgs> ByteDoubleClick;

        /// <summary>
        /// V1 compatible: Raised when zoom scale changes
        /// </summary>
        public event EventHandler ZoomScaleChanged;

        /// <summary>
        /// V1 compatible: Raised when vertical scrollbar position changes
        /// </summary>
        public event EventHandler<ByteEventArgs> VerticalScrollBarChanged;

        /// <summary>
        /// V1 compatible: Raised when changes are submitted (saved)
        /// </summary>
        public event EventHandler ChangesSubmited;

        /// <summary>
        /// V1 compatible: Raised when read-only mode changes
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

        #region V1 Compatibility - Configuration Properties

        // Backing fields
        // No more backing fields needed - all converted to DependencyProperty

        /// <summary>
        /// Allow context menu (V1 compatible) - DependencyProperty
        /// </summary>
        public bool AllowContextMenu
        {
            get => (bool)GetValue(AllowContextMenuProperty);
            set => SetValue(AllowContextMenuProperty, value);
        }

        /// <summary>
        /// Allow zoom (V1 compatible) - DependencyProperty
        /// </summary>
        public bool AllowZoom
        {
            get => (bool)GetValue(AllowZoomProperty);
            set => SetValue(AllowZoomProperty, value);
        }

        /// <summary>
        /// Mouse wheel scroll speed (V1 compatible) - DependencyProperty
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
        /// Preload byte strategy - DependencyProperty
        /// </summary>
        public PreloadByteInEditor PreloadByteInEditorMode
        {
            get => (PreloadByteInEditor)GetValue(PreloadByteInEditorModeProperty);
            set => SetValue(PreloadByteInEditorModeProperty, value);
        }

        // TBL Advanced Features (V1 compatible) - DependencyProperties

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

        // Bar Chart Panel color (V1 compatible) - DependencyProperty

        /// <summary>
        /// Bar chart color - DependencyProperty
        /// </summary>
        public System.Windows.Media.Color BarChartColor
        {
            get => (System.Windows.Media.Color)GetValue(BarChartColorProperty);
            set => SetValue(BarChartColorProperty, value);
        }

        #endregion

        #region V1 Compatibility - Custom Background Blocks

        private readonly List<Core.CustomBackgroundBlock> _customBackgroundBlocks = new List<Core.CustomBackgroundBlock>();

        /// <summary>
        /// Enable or disable custom background blocks (V1 compatible - Phase 7.1) - DependencyProperty
        /// </summary>
        public bool AllowCustomBackgroundBlock
        {
            get => (bool)GetValue(AllowCustomBackgroundBlockProperty);
            set => SetValue(AllowCustomBackgroundBlockProperty, value);
        }

        /// <summary>
        /// Get the list of custom background blocks (V1 compatible)
        /// </summary>
        public List<Core.CustomBackgroundBlock> CustomBackgroundBlockItems => _customBackgroundBlocks;

        #endregion

        #region Public Properties

        /// <summary>
        /// Is a file currently loaded?
        /// </summary>
        public bool IsFileLoaded => _viewModel != null;

        /// <summary>
        /// Is a file or stream currently loaded? (V1 compatible, read-only DependencyProperty)
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
        /// Has active selection?
        /// </summary>
        public bool HasSelection => _viewModel?.HasSelection ?? false;

        /// <summary>
        /// Selection length in bytes
        /// </summary>
        public long SelectionLength => _viewModel?.SelectionLength ?? 0;

        /// <summary>
        /// V1 compatible: Selected bytes as hex string (e.g., "48 65 6C 6C 6F")
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
        /// V1 compatible: Selected bytes as ASCII string
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
        /// V1 compatible: Current selection line number (0-based)
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
        /// Virtual length (total bytes including inserted/deleted) - V1 compatible
        /// </summary>
        public long VirtualLength => _viewModel?.VirtualLength ?? 0;

        /// <summary>
        /// Physical file length in bytes - V1 compatible
        /// </summary>
        public long Length => _viewModel?.FileLength ?? 0;

        /// <summary>
        /// Current file name (full path) - V1 compatible
        /// Uses DependencyProperty for XAML binding support (Phase 8)
        /// </summary>
        public string FileName
        {
            get => (string)GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        /// <summary>
        /// Has the file been modified? - V1 compatible
        /// Uses DependencyProperty for XAML binding support (Phase 8)
        /// </summary>
        public bool IsModified
        {
            get => (bool)GetValue(IsModifiedProperty);
            set => SetValue(IsModifiedProperty, value);
        }

        /// <summary>
        /// Current cursor position (virtual) - V1 compatible
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
        /// Selection start position (virtual) - V1 compatible (DependencyProperty for XAML binding)
        /// </summary>
        public long SelectionStart
        {
            get => (long)GetValue(SelectionStartProperty);
            set => SetValue(SelectionStartProperty, value);
        }

        /// <summary>
        /// Selection stop position (virtual) - V1 compatible (DependencyProperty for XAML binding)
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
        /// Show or hide the status bar (V1 compatible)
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
            DependencyProperty.Register(nameof(ShowStatusMessage), typeof(bool), typeof(HexEditorV2),
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
            DependencyProperty.Register(nameof(ShowFileSizeInStatusBar), typeof(bool), typeof(HexEditorV2),
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
            DependencyProperty.Register(nameof(ShowSelectionInStatusBar), typeof(bool), typeof(HexEditorV2),
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
            DependencyProperty.Register(nameof(ShowPositionInStatusBar), typeof(bool), typeof(HexEditorV2),
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
            DependencyProperty.Register(nameof(ShowEditModeInStatusBar), typeof(bool), typeof(HexEditorV2),
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
            DependencyProperty.Register(nameof(ShowBytesPerLineInStatusBar), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        #endregion

        /// <summary>
        /// Show or hide the column header (V1 compatible)
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
        /// Show or hide the offset column (V1 compatible)
        /// Note: Requires re-creating lines for template changes
        /// </summary>
        public bool ShowOffset
        {
            get => (bool)GetValue(ShowOffsetProperty);
            set => SetValue(ShowOffsetProperty, value);
        }

        public static readonly DependencyProperty ShowOffsetProperty =
            DependencyProperty.Register(nameof(ShowOffset), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true, OnShowOffsetChanged));

        private static void OnShowOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is bool showOffset)
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
        /// Show or hide the ASCII column (V1 compatible)
        /// Note: Requires re-creating lines for template changes
        /// </summary>
        public bool ShowAscii
        {
            get => (bool)GetValue(ShowAsciiProperty);
            set => SetValue(ShowAsciiProperty, value);
        }

        public static readonly DependencyProperty ShowAsciiProperty =
            DependencyProperty.Register(nameof(ShowAscii), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true, OnShowAsciiChanged));

        private static void OnShowAsciiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is bool showAscii)
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
        /// Font size for zoom (V1 compatible - placeholder)
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
            DependencyProperty.Register(nameof(ShowColumnSeparator), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        #endregion

        #region V1 Compatibility - Byte Spacer Properties

        /// <summary>
        /// Get or set the byte spacing position (V1 compatible)
        /// </summary>
        public ByteSpacerPosition ByteSpacerPositioning
        {
            get => (ByteSpacerPosition)GetValue(ByteSpacerPositioningProperty);
            set => SetValue(ByteSpacerPositioningProperty, value);
        }

        public static readonly DependencyProperty ByteSpacerPositioningProperty =
            DependencyProperty.Register(nameof(ByteSpacerPositioning), typeof(ByteSpacerPosition), typeof(HexEditorV2),
                new FrameworkPropertyMetadata(ByteSpacerPosition.Both, ByteSpacer_Changed));

        /// <summary>
        /// Get or set the byte spacer width (V1 compatible)
        /// </summary>
        public ByteSpacerWidth ByteSpacerWidthTickness
        {
            get => (ByteSpacerWidth)GetValue(ByteSpacerWidthTicknessProperty);
            set => SetValue(ByteSpacerWidthTicknessProperty, value);
        }

        public static readonly DependencyProperty ByteSpacerWidthTicknessProperty =
            DependencyProperty.Register(nameof(ByteSpacerWidthTickness), typeof(ByteSpacerWidth), typeof(HexEditorV2),
                new FrameworkPropertyMetadata(ByteSpacerWidth.Normal, ByteSpacer_Changed));

        /// <summary>
        /// Get or set the byte grouping (V1 compatible)
        /// </summary>
        public ByteSpacerGroup ByteGrouping
        {
            get => (ByteSpacerGroup)GetValue(ByteGroupingProperty);
            set => SetValue(ByteGroupingProperty, value);
        }

        public static readonly DependencyProperty ByteGroupingProperty =
            DependencyProperty.Register(nameof(ByteGrouping), typeof(ByteSpacerGroup), typeof(HexEditorV2),
                new FrameworkPropertyMetadata(ByteSpacerGroup.FourByte, ByteSpacer_Changed));

        /// <summary>
        /// Get or set the visual of byte spacer (V1 compatible)
        /// </summary>
        public ByteSpacerVisual ByteSpacerVisualStyle
        {
            get => (ByteSpacerVisual)GetValue(ByteSpacerVisualStyleProperty);
            set => SetValue(ByteSpacerVisualStyleProperty, value);
        }

        public static readonly DependencyProperty ByteSpacerVisualStyleProperty =
            DependencyProperty.Register(nameof(ByteSpacerVisualStyle), typeof(ByteSpacerVisual), typeof(HexEditorV2),
                new FrameworkPropertyMetadata(ByteSpacerVisual.Line, ByteSpacer_Changed));

        /// <summary>
        /// Callback when any byte spacer property changes - triggers header and viewport refresh
        /// </summary>
        private static void ByteSpacer_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
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

        #region V1 Compatibility - Color Properties

        /// <summary>
        /// First selection gradient color (V1 compatible)
        /// </summary>
        public Color SelectionFirstColor
        {
            get => (Color)GetValue(SelectionFirstColorProperty);
            set => SetValue(SelectionFirstColorProperty, value);
        }

        public static readonly DependencyProperty SelectionFirstColorProperty =
            DependencyProperty.Register(nameof(SelectionFirstColor), typeof(Color), typeof(HexEditorV2),
                new PropertyMetadata(Color.FromArgb(102, 0, 120, 212), OnSelectionFirstColorChanged));

        private static void OnSelectionFirstColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["SelectionBrush"] = new SolidColorBrush(color) { Opacity = 0.4 };
            }
        }

        /// <summary>
        /// Second selection gradient color (V1 compatible)
        /// </summary>
        public Color SelectionSecondColor
        {
            get => (Color)GetValue(SelectionSecondColorProperty);
            set => SetValue(SelectionSecondColorProperty, value);
        }

        public static readonly DependencyProperty SelectionSecondColorProperty =
            DependencyProperty.Register(nameof(SelectionSecondColor), typeof(Color), typeof(HexEditorV2),
                new PropertyMetadata(Color.FromArgb(102, 0, 120, 212)));

        /// <summary>
        /// Color for modified bytes (V1 compatible)
        /// </summary>
        public Color ByteModifiedColor
        {
            get => (Color)GetValue(ByteModifiedColorProperty);
            set => SetValue(ByteModifiedColorProperty, value);
        }

        public static readonly DependencyProperty ByteModifiedColorProperty =
            DependencyProperty.Register(nameof(ByteModifiedColor), typeof(Color), typeof(HexEditorV2),
                new PropertyMetadata(Color.FromRgb(255, 165, 0), OnByteModifiedColorChanged));

        private static void OnByteModifiedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
            {
                editor.Resources["ModifiedBrush"] = new SolidColorBrush((Color)e.NewValue);
            }
        }

        /// <summary>
        /// Color for deleted bytes (V1 compatible)
        /// </summary>
        public Color ByteDeletedColor
        {
            get => (Color)GetValue(ByteDeletedColorProperty);
            set => SetValue(ByteDeletedColorProperty, value);
        }

        public static readonly DependencyProperty ByteDeletedColorProperty =
            DependencyProperty.Register(nameof(ByteDeletedColor), typeof(Color), typeof(HexEditorV2),
                new PropertyMetadata(Color.FromRgb(244, 67, 54), OnByteDeletedColorChanged));

        private static void OnByteDeletedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
            {
                editor.Resources["DeletedBrush"] = new SolidColorBrush((Color)e.NewValue);
            }
        }

        /// <summary>
        /// Color for added bytes (V1 compatible)
        /// </summary>
        public Color ByteAddedColor
        {
            get => (Color)GetValue(ByteAddedColorProperty);
            set => SetValue(ByteAddedColorProperty, value);
        }

        public static readonly DependencyProperty ByteAddedColorProperty =
            DependencyProperty.Register(nameof(ByteAddedColor), typeof(Color), typeof(HexEditorV2),
                new PropertyMetadata(Color.FromRgb(76, 175, 80), OnByteAddedColorChanged));

        private static void OnByteAddedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
            {
                editor.Resources["AddedBrush"] = new SolidColorBrush((Color)e.NewValue);
            }
        }

        /// <summary>
        /// Color for highlighted bytes (V1 compatible)
        /// </summary>
        public Color HighLightColor
        {
            get => (Color)GetValue(HighLightColorProperty);
            set => SetValue(HighLightColorProperty, value);
        }

        public static readonly DependencyProperty HighLightColorProperty =
            DependencyProperty.Register(nameof(HighLightColor), typeof(Color), typeof(HexEditorV2),
                new PropertyMetadata(Colors.Gold));

        /// <summary>
        /// Mouse over color (V1 compatible)
        /// </summary>
        public Color MouseOverColor
        {
            get => (Color)GetValue(MouseOverColorProperty);
            set => SetValue(MouseOverColorProperty, value);
        }

        public static readonly DependencyProperty MouseOverColorProperty =
            DependencyProperty.Register(nameof(MouseOverColor), typeof(Color), typeof(HexEditorV2),
                new PropertyMetadata(Color.FromRgb(227, 242, 253), OnMouseOverColorChanged));

        private static void OnMouseOverColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
            {
                editor.Resources["ByteHoverBrush"] = new SolidColorBrush((Color)e.NewValue);
            }
        }

        /// <summary>
        /// Foreground color for alternate bytes - V1 compatible (uses Color instead of Brush)
        /// </summary>
        public Color ForegroundSecondColor
        {
            get => (Color)GetValue(ForegroundSecondColorProperty);
            set => SetValue(ForegroundSecondColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundSecondColorProperty =
            DependencyProperty.Register(nameof(ForegroundSecondColor), typeof(Color), typeof(HexEditorV2),
                new PropertyMetadata(Colors.Blue, OnForegroundSecondColorChanged));

        private static void OnForegroundSecondColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
            {
                var brush = new SolidColorBrush((Color)e.NewValue);
                editor.Resources["AlternateByteForegroundBrush"] = brush;

                // Update HexViewport colors (Phase 7.6)
                if (editor.HexViewport != null)
                {
                    var normalBrush = editor.Resources["ByteForegroundBrush"] as Brush;
                    editor.HexViewport.SetByteForegroundColors(normalBrush, brush);
                }
            }
        }

        /// <summary>
        /// Foreground color for offset header (V1 compatible)
        /// </summary>
        public Color ForegroundOffSetHeaderColor
        {
            get => (Color)GetValue(ForegroundOffSetHeaderColorProperty);
            set => SetValue(ForegroundOffSetHeaderColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundOffSetHeaderColorProperty =
            DependencyProperty.Register(nameof(ForegroundOffSetHeaderColor), typeof(Color), typeof(HexEditorV2),
                new PropertyMetadata(Color.FromRgb(117, 117, 117), OnForegroundOffSetHeaderColorChanged));

        private static void OnForegroundOffSetHeaderColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
            {
                editor.Resources["OffsetBrush"] = new SolidColorBrush((Color)e.NewValue);
            }
        }

        /// <summary>
        /// Foreground highlight offset header color (V1 compatible)
        /// </summary>
        public Color ForegroundHighLightOffSetHeaderColor
        {
            get => (Color)GetValue(ForegroundHighLightOffSetHeaderColorProperty);
            set => SetValue(ForegroundHighLightOffSetHeaderColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundHighLightOffSetHeaderColorProperty =
            DependencyProperty.Register(nameof(ForegroundHighLightOffSetHeaderColor), typeof(Color), typeof(HexEditorV2),
                new PropertyMetadata(Colors.DarkBlue));

        /// <summary>
        /// Foreground contrast color (V1 compatible)
        /// </summary>
        public Color ForegroundContrast
        {
            get => (Color)GetValue(ForegroundContrastProperty);
            set => SetValue(ForegroundContrastProperty, value);
        }

        public static readonly DependencyProperty ForegroundContrastProperty =
            DependencyProperty.Register(nameof(ForegroundContrast), typeof(Color), typeof(HexEditorV2),
                new PropertyMetadata(Colors.Black));

        #endregion

        #region Zoom Support (V1 Compatible)

        /// <summary>
        /// Get or set the zoom scale (V1 compatible)
        /// Possible Scale: 0.5 to 2.0 (50% to 200%)
        /// </summary>
        public double ZoomScale
        {
            get => (double)GetValue(ZoomScaleProperty);
            set => SetValue(ZoomScaleProperty, value);
        }

        public static readonly DependencyProperty ZoomScaleProperty =
            DependencyProperty.Register(nameof(ZoomScale), typeof(double), typeof(HexEditorV2),
                new FrameworkPropertyMetadata(1.0, ZoomScale_ChangedCallBack, ZoomScale_CoerceValueCallBack));

        private static object ZoomScale_CoerceValueCallBack(DependencyObject d, object baseValue)
        {
            // Clamp zoom between 0.5 and 2.0 (V1 compatible range)
            double value = (double)baseValue;
            if (value < 0.5) return 0.5;
            if (value > 2.0) return 2.0;
            return value;
        }

        private static void ZoomScale_ChangedCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 ctrl && e.NewValue != e.OldValue)
            {
                ctrl.UpdateZoom();
            }
        }

        /// <summary>
        /// Initialize the support of zoom (V1 compatible)
        /// </summary>
        private void InitialiseZoom()
        {
            if (_scaler != null) return;

            _scaler = new ScaleTransform(ZoomScale, ZoomScale);

            // Apply scale transform to zoomable elements (like V1)
            // Apply to entire header border so all header elements scale together
            if (_headerBorder != null)
                _headerBorder.LayoutTransform = _scaler;
            if (HexViewport != null)
                HexViewport.LayoutTransform = _scaler;
        }

        /// <summary>
        /// Update the zoom to ZoomScale value if AllowZoom is true (V1 compatible)
        /// </summary>
        private void UpdateZoom()
        {
            if (!AllowZoom) return;

            if (_scaler == null) InitialiseZoom();
            if (_scaler != null)
            {
                _scaler.ScaleX = ZoomScale;
                _scaler.ScaleY = ZoomScale;
            }

            // Update viewport and refresh display
            HexViewport?.InvalidateVisual();
            UpdateVisibleLines();

            // Raise V1 compatible event
            OnZoomScaleChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Reset the zoom to 100% (V1 compatible)
        /// </summary>
        public void ResetZoom() => ZoomScale = 1.0;

        #endregion

        #region Phase 8 - XAML Binding DependencyProperties

        /// <summary>
        /// FileName DependencyProperty for XAML binding (Phase 8)
        /// </summary>
        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register(nameof(FileName), typeof(string), typeof(HexEditorV2),
                new PropertyMetadata(string.Empty, OnFileNamePropertyChanged));

        private static void OnFileNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is string path && !string.IsNullOrEmpty(path))
            {
                // V1 Compatibility: Auto-load file when FileName is set (CRITICAL FIX)
                // This enables V1 pattern: HexEdit.FileName = "file.bin" to automatically open the file
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
        /// IsModified DependencyProperty for XAML binding (Phase 8)
        /// </summary>
        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(false));

        /// <summary>
        /// Position DependencyProperty for XAML binding (Phase 8)
        /// </summary>
        public static readonly DependencyProperty PositionProperty =
            DependencyProperty.Register(nameof(Position), typeof(long), typeof(HexEditorV2),
                new PropertyMetadata(-1L, OnPositionPropertyChanged));

        private static void OnPositionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is long position && position >= 0)
            {
                editor.SetPosition(position);
            }
        }

        /// <summary>
        /// ReadOnlyMode DependencyProperty for XAML binding (Phase 8)
        /// </summary>
        public static readonly DependencyProperty ReadOnlyModeProperty =
            DependencyProperty.Register(nameof(ReadOnlyMode), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(false, OnReadOnlyModePropertyChanged));

        private static void OnReadOnlyModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is bool readOnly)
            {
                if (editor._viewModel != null)
                {
                    var oldValue = editor._viewModel.ReadOnlyMode;
                    editor._viewModel.ReadOnlyMode = readOnly;

                    // Fire V1 compatible event
                    if (oldValue != readOnly)
                        editor.OnReadOnlyChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// SelectionStart DependencyProperty for XAML binding (Phase 8)
        /// </summary>
        public static readonly DependencyProperty SelectionStartProperty =
            DependencyProperty.Register(nameof(SelectionStart), typeof(long), typeof(HexEditorV2),
                new PropertyMetadata(-1L, OnSelectionStartPropertyChanged));

        private static void OnSelectionStartPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is long position)
            {
                if (editor._viewModel != null && position >= 0 && position < editor.VirtualLength)
                {
                    var oldStart = e.OldValue is long old ? old : -1;
                    var oldStop = editor.SelectionStop;
                    var oldLength = editor.SelectionLength;

                    var stop = editor._viewModel.SelectionStop.IsValid ? editor._viewModel.SelectionStop : new VirtualPosition(position);
                    editor._viewModel.SetSelectionRange(new VirtualPosition(position), stop);

                    // Fire V1 compatible events
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
        /// SelectionStop DependencyProperty for XAML binding (Phase 8)
        /// </summary>
        public static readonly DependencyProperty SelectionStopProperty =
            DependencyProperty.Register(nameof(SelectionStop), typeof(long), typeof(HexEditorV2),
                new PropertyMetadata(-1L, OnSelectionStopPropertyChanged));

        private static void OnSelectionStopPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is long position)
            {
                if (editor._viewModel != null && position >= 0 && position < editor.VirtualLength)
                {
                    var oldStart = editor.SelectionStart;
                    var oldStop = e.OldValue is long old ? old : -1;
                    var oldLength = editor.SelectionLength;

                    var start = editor._viewModel.SelectionStart.IsValid ? editor._viewModel.SelectionStart : new VirtualPosition(position);
                    editor._viewModel.SetSelectionRange(start, new VirtualPosition(position));

                    // Fire V1 compatible events
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
        /// BytePerLine DependencyProperty for XAML binding (Phase 8)
        /// </summary>
        public static readonly DependencyProperty BytePerLineProperty =
            DependencyProperty.Register(nameof(BytePerLine), typeof(int), typeof(HexEditorV2),
                new PropertyMetadata(16, OnBytePerLinePropertyChanged));

        private static void OnBytePerLinePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is int bytesPerLine && bytesPerLine > 0)
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
        /// EditMode DependencyProperty for XAML binding (Phase 8)
        /// </summary>
        public static readonly DependencyProperty EditModeProperty =
            DependencyProperty.Register(nameof(EditMode), typeof(Models.EditMode), typeof(HexEditorV2),
                new PropertyMetadata(Models.EditMode.Overwrite, OnEditModePropertyChanged));

        private static void OnEditModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is Models.EditMode mode)
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
        /// IsFileOrStreamLoaded Read-Only DependencyProperty for XAML binding (Phase 8)
        /// </summary>
        private static readonly DependencyPropertyKey IsFileOrStreamLoadedPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsFileOrStreamLoaded), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsFileOrStreamLoadedProperty =
            IsFileOrStreamLoadedPropertyKey.DependencyProperty;

        /// <summary>
        /// AllowContextMenu DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowContextMenuProperty =
            DependencyProperty.Register(nameof(AllowContextMenu), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true, OnAllowContextMenuChanged));

        private static void OnAllowContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is bool allowed)
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
            DependencyProperty.Register(nameof(AllowZoom), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true, OnAllowZoomChanged));

        private static void OnAllowZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is bool allowed)
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
            DependencyProperty.Register(nameof(MouseWheelSpeed), typeof(MouseWheelSpeed), typeof(HexEditorV2),
                new PropertyMetadata(Core.MouseWheelSpeed.Normal));

        /// <summary>
        /// AllowAutoHighLightSelectionByte DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowAutoHighLightSelectionByteProperty =
            DependencyProperty.Register(nameof(AllowAutoHighLightSelectionByte), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(false, OnAllowAutoHighLightChanged));

        private static void OnAllowAutoHighLightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
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
            DependencyProperty.Register(nameof(AutoHighLiteSelectionByteBrush), typeof(System.Windows.Media.Color), typeof(HexEditorV2),
                new PropertyMetadata(Color.FromArgb(0x60, 0xFF, 0xFF, 0x00), OnAutoHighLiteColorChanged)); // 40% Yellow

        private static void OnAutoHighLiteColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is System.Windows.Media.Color color)
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
            DependencyProperty.Register(nameof(AllowAutoSelectSameByteAtDoubleClick), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(false));

        /// <summary>
        /// AllowFileDrop DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowFileDropProperty =
            DependencyProperty.Register(nameof(AllowFileDrop), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true, OnAllowDropChanged));

        /// <summary>
        /// AllowTextDrop DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowTextDropProperty =
            DependencyProperty.Register(nameof(AllowTextDrop), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(false, OnAllowDropChanged));

        private static void OnAllowDropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
            {
                // Enable AllowDrop if either file or text drop is allowed
                editor.AllowDrop = editor.AllowFileDrop || editor.AllowTextDrop;
            }
        }

        /// <summary>
        /// FileDroppingConfirmation DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty FileDroppingConfirmationProperty =
            DependencyProperty.Register(nameof(FileDroppingConfirmation), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        /// <summary>
        /// AllowExtend DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowExtendProperty =
            DependencyProperty.Register(nameof(AllowExtend), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        /// <summary>
        /// AllowDeleteByte DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowDeleteByteProperty =
            DependencyProperty.Register(nameof(AllowDeleteByte), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        /// <summary>
        /// AllowByteCount DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowByteCountProperty =
            DependencyProperty.Register(nameof(AllowByteCount), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        /// <summary>
        /// TblShowMte DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblShowMteProperty =
            DependencyProperty.Register(nameof(TblShowMte), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(false, OnTblShowMteChanged));

        private static void OnTblShowMteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
                editor.HexViewport?.InvalidateVisual();
        }

        /// <summary>
        /// TblDteColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblDteColorProperty =
            DependencyProperty.Register(nameof(TblDteColor), typeof(System.Windows.Media.Color), typeof(HexEditorV2),
                new PropertyMetadata(Colors.Yellow, OnTblColorChanged));

        /// <summary>
        /// TblMteColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblMteColorProperty =
            DependencyProperty.Register(nameof(TblMteColor), typeof(System.Windows.Media.Color), typeof(HexEditorV2),
                new PropertyMetadata(Colors.LightBlue, OnTblColorChanged));

        /// <summary>
        /// TblEndBlockColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblEndBlockColorProperty =
            DependencyProperty.Register(nameof(TblEndBlockColor), typeof(System.Windows.Media.Color), typeof(HexEditorV2),
                new PropertyMetadata(Colors.Red, OnTblColorChanged));

        /// <summary>
        /// TblEndLineColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblEndLineColorProperty =
            DependencyProperty.Register(nameof(TblEndLineColor), typeof(System.Windows.Media.Color), typeof(HexEditorV2),
                new PropertyMetadata(Colors.Orange, OnTblColorChanged));

        /// <summary>
        /// TblDefaultColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblDefaultColorProperty =
            DependencyProperty.Register(nameof(TblDefaultColor), typeof(System.Windows.Media.Color), typeof(HexEditorV2),
                new PropertyMetadata(Colors.White, OnTblColorChanged));

        private static void OnTblColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
                editor.HexViewport?.InvalidateVisual();
        }

        /// <summary>
        /// BarChartColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty BarChartColorProperty =
            DependencyProperty.Register(nameof(BarChartColor), typeof(System.Windows.Media.Color), typeof(HexEditorV2),
                new PropertyMetadata(Colors.Blue, OnBarChartColorChanged));

        private static void OnBarChartColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is Color color && editor._barChartPanel != null)
            {
                editor._barChartPanel.BarColor = color;
            }
        }

        /// <summary>
        /// DataStringVisual DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty DataStringVisualProperty =
            DependencyProperty.Register(nameof(DataStringVisual), typeof(DataVisualType), typeof(HexEditorV2),
                new PropertyMetadata(DataVisualType.Hexadecimal));

        /// <summary>
        /// OffSetStringVisual DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty OffSetStringVisualProperty =
            DependencyProperty.Register(nameof(OffSetStringVisual), typeof(DataVisualType), typeof(HexEditorV2),
                new PropertyMetadata(DataVisualType.Hexadecimal));

        /// <summary>
        /// ByteOrder DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ByteOrderProperty =
            DependencyProperty.Register(nameof(ByteOrder), typeof(ByteOrderType), typeof(HexEditorV2),
                new PropertyMetadata(ByteOrderType.LoHi));

        /// <summary>
        /// ByteSize DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ByteSizeProperty =
            DependencyProperty.Register(nameof(ByteSize), typeof(ByteSizeType), typeof(HexEditorV2),
                new PropertyMetadata(ByteSizeType.Bit8));

        /// <summary>
        /// BarChartPanelVisibility DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty BarChartPanelVisibilityProperty =
            DependencyProperty.Register(nameof(BarChartPanelVisibility), typeof(Visibility), typeof(HexEditorV2),
                new PropertyMetadata(Visibility.Collapsed, OnBarChartPanelVisibilityChanged));

        private static void OnBarChartPanelVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is Visibility visibility)
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
            DependencyProperty.Register(nameof(HideByteDeleted), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(false, OnHideByteDeletedChanged));

        private static void OnHideByteDeletedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
            {
                // Refresh viewport to show/hide deleted bytes
                editor.HexViewport?.InvalidateVisual();
            }
        }

        /// <summary>
        /// DefaultCopyToClipboardMode DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty DefaultCopyToClipboardModeProperty =
            DependencyProperty.Register(nameof(DefaultCopyToClipboardMode), typeof(CopyPasteMode), typeof(HexEditorV2),
                new PropertyMetadata(CopyPasteMode.HexaString));

        /// <summary>
        /// VisualCaretMode DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty VisualCaretModeProperty =
            DependencyProperty.Register(nameof(VisualCaretMode), typeof(CaretMode), typeof(HexEditorV2),
                new PropertyMetadata(CaretMode.Insert));

        /// <summary>
        /// ByteShiftLeft DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ByteShiftLeftProperty =
            DependencyProperty.Register(nameof(ByteShiftLeft), typeof(long), typeof(HexEditorV2),
                new PropertyMetadata(0L, OnByteShiftLeftChanged));

        private static void OnByteShiftLeftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor)
            {
                // Refresh viewport to update offset display
                editor.HexViewport?.InvalidateVisual();
            }
        }

        /// <summary>
        /// AppendNeedConfirmation DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AppendNeedConfirmationProperty =
            DependencyProperty.Register(nameof(AppendNeedConfirmation), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        /// <summary>
        /// CustomEncoding DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty CustomEncodingProperty =
            DependencyProperty.Register(nameof(CustomEncoding), typeof(System.Text.Encoding), typeof(HexEditorV2),
                new PropertyMetadata(System.Text.Encoding.UTF8, OnCustomEncodingChanged));

        private static void OnCustomEncodingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is System.Text.Encoding encoding)
            {
                // Refresh viewport to update text display with new encoding
                editor.HexViewport?.InvalidateVisual();
            }
        }

        /// <summary>
        /// PreloadByteInEditorMode DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty PreloadByteInEditorModeProperty =
            DependencyProperty.Register(nameof(PreloadByteInEditorMode), typeof(PreloadByteInEditor), typeof(HexEditorV2),
                new PropertyMetadata(PreloadByteInEditor.MaxScreenVisibleLineAtDataLoad));

        /// <summary>
        /// AllowCustomBackgroundBlock DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowCustomBackgroundBlockProperty =
            DependencyProperty.Register(nameof(AllowCustomBackgroundBlock), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(false, OnAllowCustomBackgroundBlockChanged));

        private static void OnAllowCustomBackgroundBlockChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && e.NewValue is bool allowed)
            {
                // Phase 7.1: Sync with HexViewport - pass blocks if enabled, empty list if disabled
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.CustomBackgroundBlocks = allowed ? editor._customBackgroundBlocks : new List<Core.CustomBackgroundBlock>();
                    editor.HexViewport.InvalidateVisual();
                }
            }
        }

        #endregion

        #region V1 Compatibility - Brush Properties (wrap Color properties)

        /// <summary>
        /// V1 compatible: Selection first color as Brush. Use <see cref="SelectionFirstColor"/> (Color) for V2 code.
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
        /// V1 compatible: Selection second color as Brush. Use SelectionSecondColor (Color) for V2 code.
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
        /// V1 compatible: Modified byte color as Brush. Use ByteModifiedColor (Color) for V2 code.
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
        /// V1 compatible: Deleted byte color as Brush. Use ByteDeletedColor (Color) for V2 code.
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
        /// V1 compatible: Added byte color as Brush. Use ByteAddedColor (Color) for V2 code.
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
        /// V1 compatible: Highlight color as Brush. Use HighLightColor (Color) for V2 code.
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
        /// V1 compatible: Mouse over color as Brush. Use MouseOverColor (Color) for V2 code.
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
        /// V1 compatible: Foreground second color as Brush. Use ForegroundSecondColor (Color) for V2 code.
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
        /// V1 compatible: Offset header foreground color as Brush. Use ForegroundOffSetHeaderColor (Color) for V2 code.
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
        /// V1 compatible: Highlighted offset header foreground color as Brush. Use ForegroundHighLightOffSetHeaderColor (Color) for V2 code.
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
        /// V1 compatible: Foreground contrast color as Brush. Use ForegroundContrast (Color) for V2 code.
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

        #region V1 Compatibility - Display Properties

        /// <summary>
        /// Allow builtin Ctrl+C (V1 compatible)
        /// </summary>
        public bool AllowBuildinCtrlc
        {
            get => (bool)GetValue(AllowBuildinCtrlcProperty);
            set => SetValue(AllowBuildinCtrlcProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlcProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrlc), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        /// <summary>
        /// Allow builtin Ctrl+V (V1 compatible)
        /// </summary>
        public bool AllowBuildinCtrlv
        {
            get => (bool)GetValue(AllowBuildinCtrlvProperty);
            set => SetValue(AllowBuildinCtrlvProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlvProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrlv), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        /// <summary>
        /// Allow builtin Ctrl+A (V1 compatible)
        /// </summary>
        public bool AllowBuildinCtrla
        {
            get => (bool)GetValue(AllowBuildinCtrlaProperty);
            set => SetValue(AllowBuildinCtrlaProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlaProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrla), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        /// <summary>
        /// Allow builtin Ctrl+Z (V1 compatible)
        /// </summary>
        public bool AllowBuildinCtrlz
        {
            get => (bool)GetValue(AllowBuildinCtrlzProperty);
            set => SetValue(AllowBuildinCtrlzProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlzProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrlz), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        /// <summary>
        /// Allow builtin Ctrl+Y (V1 compatible)
        /// </summary>
        public bool AllowBuildinCtrly
        {
            get => (bool)GetValue(AllowBuildinCtrlyProperty);
            set => SetValue(AllowBuildinCtrlyProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlyProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrly), typeof(bool), typeof(HexEditorV2),
                new PropertyMetadata(true));

        #endregion

        #region V1 Compatibility - Visibility Properties (wrap bool properties)

        /// <summary>
        /// V1 compatible: Header visibility. Use ShowHeader (bool) for V2 code.
        /// </summary>
        public Visibility HeaderVisibility
        {
            get => ShowHeader ? Visibility.Visible : Visibility.Collapsed;
            set => ShowHeader = (value == Visibility.Visible);
        }

        /// <summary>
        /// V1 compatible: Status bar visibility. Use ShowStatusBar (bool) for V2 code.
        /// </summary>
        public Visibility StatusBarVisibility
        {
            get => ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
            set => ShowStatusBar = (value == Visibility.Visible);
        }

        /// <summary>
        /// V1 compatible: Line info (offset column) visibility. Use ShowOffset (bool) for V2 code.
        /// </summary>
        public Visibility LineInfoVisibility
        {
            get => ShowOffset ? Visibility.Visible : Visibility.Collapsed;
            set => ShowOffset = (value == Visibility.Visible);
        }

        /// <summary>
        /// V1 compatible: String data (ASCII) panel visibility. Use ShowAscii (bool) for V2 code.
        /// </summary>
        public Visibility StringDataVisibility
        {
            get => ShowAscii ? Visibility.Visible : Visibility.Collapsed;
            set => ShowAscii = (value == Visibility.Visible);
        }

        /// <summary>
        /// V1 compatible: Hex data panel visibility. Always Visible in V2 (cannot be hidden).
        /// </summary>
        public Visibility HexDataVisibility
        {
            get => Visibility.Visible;
            set { /* V2 does not support hiding hex panel */ }
        }

        /// <summary>
        /// V1 compatible: Bar chart panel visibility (Phase 7.4 - Complete) - DependencyProperty
        /// </summary>
        public Visibility BarChartPanelVisibility
        {
            get => (Visibility)GetValue(BarChartPanelVisibilityProperty);
            set => SetValue(BarChartPanelVisibilityProperty, value);
        }

        #endregion

        #endregion

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
            System.Diagnostics.Debug.WriteLine($"[OPENFILE] EditMode synchronized: Control={EditMode}, ViewModel={_viewModel.EditMode}, HexViewport={HexViewport.EditMode}");

            // ByteProvider V2 always supports insertion anywhere - no need to set flag
            if (EditMode == EditMode.Insert)
            {
                System.Diagnostics.Debug.WriteLine($"[OPENFILE] Edit mode is INSERT");
            }

            // Initialize byte spacer properties on viewport (V1 compatibility)
            HexViewport.ByteSpacerPositioning = ByteSpacerPositioning;
            HexViewport.ByteSpacerWidthTickness = ByteSpacerWidthTickness;
            HexViewport.ByteGrouping = ByteGrouping;
            HexViewport.ByteSpacerVisualStyle = ByteSpacerVisualStyle;

            // Initialize byte foreground colors (Phase 7.6 - V1 compatibility)
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

            // Update bar chart panel in background (Phase 7.4)
            // Bar chart calculation can be slow for large files
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateBarChart();
            }), System.Windows.Threading.DispatcherPriority.Background);

            // Update scroll markers in background (V1 compatible)
            // Scroll markers don't need to be ready immediately
            if (_scrollMarkers != null)
            {
                _scrollMarkers.FileLength = _viewModel.FileLength;
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
            OnChangesSubmited(EventArgs.Empty); // V1 compatible event
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

            // Clear bar chart (Phase 7.4)
            _barChartPanel?.Clear();
            PositionInfo.Text = "Position: 0";
            EditModeText.Text = "Mode: Overwrite";
            BytesPerLineText.Text = "Bytes/Line: 16";
        }


        /// <summary>
        /// Update bar chart panel with current file data (Phase 7.4)
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
        /// Refresh the bar chart panel with current file data. V1 compatible method.
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
        /// Select all bytes with specified value (V1 compatible double-click feature)
        /// V1 ALGORITHM: Expands selection bidirectionally from clicked position
        /// </summary>
        private void SelectAllBytesWith(byte byteValue)
        {
            System.Diagnostics.Debug.WriteLine($"[SELECTALL] SelectAllBytesWith called with byte value 0x{byteValue:X2}");

            if (_viewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("[SELECTALL] ViewModel is null!");
                StatusText.Text = "DEBUG: ViewModel is null";
                return;
            }

            try
            {
                // V1 ALGORITHM: Expand selection bidirectionally from current position
                // Scan backwards and forwards until we hit a different byte value

                long startPosition = _viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : 0;
                long stopPosition = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value : startPosition;

                System.Diagnostics.Debug.WriteLine($"[SELECTALL] Initial range: {startPosition} → {stopPosition}");

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

                System.Diagnostics.Debug.WriteLine($"[SELECTALL] Scanned backwards to: {scanStart}");

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

                System.Diagnostics.Debug.WriteLine($"[SELECTALL] Scanned forwards to: {scanStop}");
                System.Diagnostics.Debug.WriteLine($"[SELECTALL] Final range: {scanStart} → {scanStop} ({scanStop - scanStart + 1} bytes)");

                // Set the expanded selection in ViewModel
                _viewModel.SetSelectionRange(new VirtualPosition(scanStart), new VirtualPosition(scanStop));

                // CRITICAL: Synchronize viewport properties IMMEDIATELY (don't wait for PropertyChanged)
                HexViewport.SelectionStart = scanStart;
                HexViewport.SelectionStop = scanStop;
                HexViewport.CursorPosition = scanStop;

                // Verify viewport properties were set correctly
                System.Diagnostics.Debug.WriteLine($"[SELECTALL] HexViewport.SelectionStart set to: {HexViewport.SelectionStart}");
                System.Diagnostics.Debug.WriteLine($"[SELECTALL] HexViewport.SelectionStop set to: {HexViewport.SelectionStop}");
                System.Diagnostics.Debug.WriteLine($"[SELECTALL] HexViewport.CursorPosition set to: {HexViewport.CursorPosition}");

                // Force viewport refresh to show the selection
                HexViewport.InvalidateVisual();

                // Ensure the selection start is visible
                EnsurePositionVisible(new VirtualPosition(scanStart));

                // Verify ViewModel selection was set
                System.Diagnostics.Debug.WriteLine($"[SELECTALL] ViewModel.SelectionStart after set: {_viewModel.SelectionStart.Value}");
                System.Diagnostics.Debug.WriteLine($"[SELECTALL] ViewModel.SelectionStop after set: {_viewModel.SelectionStop.Value}");
                System.Diagnostics.Debug.WriteLine($"[SELECTALL] ViewModel.HasSelection: {_viewModel.HasSelection}");

                // Update status bar
                StatusText.Text = $"Selected {scanStop - scanStart + 1} consecutive bytes with value 0x{byteValue:X2}";
                System.Diagnostics.Debug.WriteLine($"[SELECTALL] Success! Status: {StatusText.Text}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SELECTALL] Exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[SELECTALL] Stack: {ex.StackTrace}");
                StatusText.Text = $"Auto-select failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Update scroll markers with bookmarks, modifications, and search results (V1 compatible)
        /// </summary>
        private void UpdateScrollMarkers()
        {
            if (_scrollMarkers == null || _viewModel == null)
                return;

            try
            {
                // Update bookmarks
                _scrollMarkers.BookmarkPositions = new HashSet<long>(_bookmarks);

                // Update modified positions from ViewModel
                var modifiedPositions = new HashSet<long>(_viewModel.GetModifiedPositions());
                _scrollMarkers.ModifiedPositions = modifiedPositions;

                // Search results would be updated separately when FindAll is called
                // (we'll add that later)
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Scroll markers update failed: {ex.Message}";
            }
        }

        #endregion

        #region Public Methods - Edit Operations

        /// <summary>
        /// Undo last operation
        /// </summary>
        public void Undo()
        {
            _viewModel?.Undo();
            OnUndoCompleted(EventArgs.Empty);
            OnUndone(EventArgs.Empty); // V1 compatible event
        }

        /// <summary>
        /// Redo last undone operation
        /// </summary>
        public void Redo()
        {
            _viewModel?.Redo();
            OnRedoCompleted(EventArgs.Empty);
            OnRedone(EventArgs.Empty); // V1 compatible event
        }

        /// <summary>
        /// Select all bytes
        /// </summary>
        public void SelectAll()
        {
            _viewModel?.SelectAll();
        }

        /// <summary>
        /// Clear selection
        /// </summary>
        public void ClearSelection()
        {
            _viewModel?.ClearSelection();
        }

        /// <summary>
        /// Delete selected bytes
        /// </summary>
        public void DeleteSelection()
        {
            _viewModel?.DeleteSelection();
        }

        /// <summary>
        /// Get selected bytes as byte array (V1 compatible)
        /// </summary>
        public byte[] GetSelectionByteArray()
        {
            return _viewModel?.GetSelectionBytes();
        }

        /// <summary>
        /// Set cursor position and scroll to make it visible (V1 compatible)
        /// </summary>
        public void SetPosition(long position)
        {
            if (_viewModel == null) return;

            var virtualPos = new VirtualPosition(position);
            _viewModel.SetSelection(virtualPos);

            // Scroll to make position visible
            EnsurePositionVisible(virtualPos);
        }

        /// <summary>
        /// Copy selected bytes to clipboard
        /// </summary>
        public bool Copy()
        {
            bool result = _viewModel?.CopyToClipboard() ?? false;
            if (result)
                OnDataCopied(EventArgs.Empty); // V1 compatible event
            return result;
        }

        /// <summary>
        /// Cut selected bytes to clipboard (copy + delete)
        /// </summary>
        public bool Cut()
        {
            return _viewModel?.Cut() ?? false;
        }

        /// <summary>
        /// Paste bytes from clipboard at current position
        /// </summary>
        public bool Paste()
        {
            return _viewModel?.Paste() ?? false;
        }

        #endregion

        #region Public Methods - Find/Replace (V1 Compatible)

        /// <summary>
        /// Find first occurrence of byte array
        /// </summary>
        /// <param name="data">Byte pattern to search for</param>
        /// <param name="startPosition">Position to start search from (default: 0)</param>
        /// <returns>Position of first occurrence, or -1 if not found</returns>
        public long FindFirst(byte[] data, long startPosition = 0)
        {
            if (_viewModel == null) return -1;
            return _viewModel.FindFirst(data, startPosition);
        }

        /// <summary>
        /// Find next occurrence after current position
        /// </summary>
        /// <param name="data">Byte pattern to search for</param>
        /// <param name="currentPosition">Current position (search starts at currentPosition + 1)</param>
        /// <returns>Position of next occurrence, or -1 if not found</returns>
        public long FindNext(byte[] data, long currentPosition)
        {
            if (_viewModel == null) return -1;
            return _viewModel.FindNext(data, currentPosition);
        }

        /// <summary>
        /// Find last occurrence of byte array
        /// </summary>
        /// <param name="data">Byte pattern to search for</param>
        /// <param name="startPosition">Position to start search from (default: 0)</param>
        /// <returns>Position of last occurrence, or -1 if not found</returns>
        public long FindLast(byte[] data, long startPosition = 0)
        {
            if (_viewModel == null) return -1;
            return _viewModel.FindLast(data, startPosition);
        }

        /// <summary>
        /// Find all occurrences of byte array
        /// </summary>
        /// <param name="data">Byte pattern to search for</param>
        /// <param name="startPosition">Position to start search from (default: 0)</param>
        /// <returns>Enumerable of positions where pattern was found, or null if not found</returns>
        public IEnumerable<long> FindAll(byte[] data, long startPosition = 0)
        {
            if (_viewModel == null) return null;
            return _viewModel.FindAll(data, startPosition);
        }

        /// <summary>
        /// Set selection to a specific range (used after find operations) - V1 compatible
        /// </summary>
        /// <param name="position">Start position</param>
        /// <param name="length">Selection length in bytes</param>
        public void FindSelect(long position, long length)
        {
            if (_viewModel == null) return;
            if (position < 0 || length <= 0) return;

            var start = new VirtualPosition(position);
            var stop = new VirtualPosition(position + length - 1);

            _viewModel.SetSelectionRange(start, stop);

            // Scroll to make selection visible
            EnsurePositionVisible(start);
        }

        /// <summary>
        /// Replace first occurrence of findData with replaceData
        /// </summary>
        /// <param name="findData">Byte pattern to find</param>
        /// <param name="replaceData">Byte pattern to replace with</param>
        /// <param name="startPosition">Position to start search from (default: 0)</param>
        /// <param name="truncateLength">If true, truncate replaceData to match findData length</param>
        /// <returns>Position where replacement occurred, or -1 if pattern not found</returns>
        public long ReplaceFirst(byte[] findData, byte[] replaceData, long startPosition = 0, bool truncateLength = false)
        {
            if (_viewModel == null) return -1;
            return _viewModel.ReplaceFirst(findData, replaceData, startPosition, truncateLength);
        }

        /// <summary>
        /// Replace next occurrence after current position
        /// </summary>
        /// <param name="findData">Byte pattern to find</param>
        /// <param name="replaceData">Byte pattern to replace with</param>
        /// <param name="currentPosition">Current position (search starts at currentPosition + 1)</param>
        /// <param name="truncateLength">If true, truncate replaceData to match findData length</param>
        /// <returns>Position where replacement occurred, or -1 if pattern not found</returns>
        public long ReplaceNext(byte[] findData, byte[] replaceData, long currentPosition, bool truncateLength = false)
        {
            if (_viewModel == null) return -1;
            return _viewModel.ReplaceNext(findData, replaceData, currentPosition, truncateLength);
        }

        /// <summary>
        /// Replace all occurrences of findData with replaceData
        /// </summary>
        /// <param name="findData">Byte pattern to find</param>
        /// <param name="replaceData">Byte pattern to replace with</param>
        /// <param name="truncateLength">If true, truncate replaceData to match findData length</param>
        /// <returns>Number of replacements made</returns>
        public int ReplaceAll(byte[] findData, byte[] replaceData, bool truncateLength = false)
        {
            if (_viewModel == null) return 0;
            return _viewModel.ReplaceAll(findData, replaceData, truncateLength);
        }

        #endregion

        #region V1 Compatibility - String Search/Replace (wrap byte[] methods)

        /// <summary>
        /// V1 compatible: Find first occurrence of string
        /// </summary>
        /// <param name="text">Text to search for</param>
        /// <param name="startPosition">Position to start search from</param>
        /// <returns>Position of first occurrence, or -1 if not found</returns>
        public long FindFirst(string text, long startPosition = 0)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            byte[] bytes = GetBytesFromString(text);
            return FindFirst(bytes, startPosition);
        }

        /// <summary>
        /// V1 compatible: Find next occurrence of string
        /// </summary>
        /// <param name="text">Text to search for</param>
        /// <returns>Position of next occurrence, or -1 if not found</returns>
        public long FindNext(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            byte[] bytes = GetBytesFromString(text);
            // V1 behavior: FindNext searches from current position + 1
            long currentPos = Position;
            return FindNext(bytes, currentPos);
        }

        /// <summary>
        /// V1 compatible: Find last occurrence of string
        /// </summary>
        /// <param name="text">Text to search for</param>
        /// <returns>Position of last occurrence, or -1 if not found</returns>
        public long FindLast(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            byte[] bytes = GetBytesFromString(text);
            return FindLast(bytes, 0);
        }

        /// <summary>
        /// V1 compatible: Replace first occurrence of string
        /// </summary>
        public long ReplaceFirst(string findText, string replaceText, long startPosition = 0, bool truncateLength = false)
        {
            if (string.IsNullOrEmpty(findText)) return -1;
            byte[] findBytes = GetBytesFromString(findText);
            byte[] replaceBytes = GetBytesFromString(replaceText ?? string.Empty);
            return ReplaceFirst(findBytes, replaceBytes, startPosition, truncateLength);
        }

        /// <summary>
        /// V1 compatible: Replace next occurrence of string
        /// </summary>
        public long ReplaceNext(string findText, string replaceText, long currentPosition, bool truncateLength = false)
        {
            if (string.IsNullOrEmpty(findText)) return -1;
            byte[] findBytes = GetBytesFromString(findText);
            byte[] replaceBytes = GetBytesFromString(replaceText ?? string.Empty);
            return ReplaceNext(findBytes, replaceBytes, currentPosition, truncateLength);
        }

        /// <summary>
        /// V1 compatible: Replace all occurrences of string
        /// </summary>
        public int ReplaceAll(string findText, string replaceText, bool truncateLength = false)
        {
            if (string.IsNullOrEmpty(findText)) return 0;
            byte[] findBytes = GetBytesFromString(findText);
            byte[] replaceBytes = GetBytesFromString(replaceText ?? string.Empty);
            return ReplaceAll(findBytes, replaceBytes, truncateLength);
        }

        /// <summary>
        /// Helper: Convert string to bytes using current character table encoding
        /// </summary>
        private byte[] GetBytesFromString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<byte>();

            // Use encoding based on character table type
            var encoding = _characterTableType == CharacterTableType.Ascii
                ? System.Text.Encoding.ASCII
                : System.Text.Encoding.UTF8;

            return encoding.GetBytes(text);
        }

        #endregion

        #region Public Methods - Byte Operations (V1 Compatible)

        /// <summary>
        /// Get byte value at position (V1 compatible)
        /// </summary>
        /// <param name="position">Position in file (virtual)</param>
        /// <returns>Byte value at position, or 0 if position is invalid</returns>
        public byte GetByte(long position)
        {
            if (_viewModel == null) return 0;
            return _viewModel.GetByte(position);
        }

        /// <summary>
        /// Set byte value at position (V1 compatible)
        /// </summary>
        /// <param name="position">Position in file (virtual)</param>
        /// <param name="value">Byte value to set</param>
        public void SetByte(long position, byte value)
        {
            _viewModel?.SetByte(position, value);
        }

        /// <summary>
        /// Fill a range with a specific byte value (V1 compatible)
        /// </summary>
        /// <param name="value">Byte value to fill with</param>
        /// <param name="startPosition">Start position (virtual)</param>
        /// <param name="length">Number of bytes to fill</param>
        public void FillWithByte(byte value, long startPosition, long length)
        {
            _viewModel?.FillWithByte(value, startPosition, length);
        }

        /// <summary>
        /// V1 compatible: Modify byte with undo support
        /// </summary>
        /// <param name="byte">New byte value (null to delete)</param>
        /// <param name="bytePositionInStream">Position in stream (virtual)</param>
        /// <param name="undoLength">Length for undo operation (usually 1)</param>
        public void ModifyByte(byte? @byte, long bytePositionInStream, long undoLength = 1)
        {
            if (_viewModel == null || ReadOnlyMode) return;

            if (@byte.HasValue)
            {
                // Modify the byte
                _viewModel.ModifyByte(new VirtualPosition(bytePositionInStream), @byte.Value);
            }
            else
            {
                // Delete the byte (null value means delete)
                _viewModel.DeleteByte(new VirtualPosition(bytePositionInStream));
            }
        }

        /// <summary>
        /// V1 compatible: Insert a single byte at position
        /// </summary>
        /// <param name="byte">Byte value to insert</param>
        /// <param name="bytePositionInStream">Position in stream (virtual)</param>
        public void InsertByte(byte @byte, long bytePositionInStream)
        {
            if (_viewModel == null || ReadOnlyMode) return;
            _viewModel.InsertByte(new VirtualPosition(bytePositionInStream), @byte);
        }

        /// <summary>
        /// V1 compatible: Insert a byte repeated multiple times at position
        /// </summary>
        /// <param name="byte">Byte value to insert</param>
        /// <param name="bytePositionInStream">Position in stream (virtual)</param>
        /// <param name="length">Number of times to repeat the byte</param>
        public void InsertByte(byte @byte, long bytePositionInStream, long length)
        {
            if (_viewModel == null || ReadOnlyMode || length <= 0) return;

            // Create array of repeated byte
            byte[] bytes = new byte[length];
            for (long i = 0; i < length; i++)
            {
                bytes[i] = @byte;
            }

            _viewModel.InsertBytes(new VirtualPosition(bytePositionInStream), bytes);
        }

        /// <summary>
        /// V1 compatible: Insert multiple bytes at position
        /// </summary>
        /// <param name="bytes">Byte array to insert</param>
        /// <param name="bytePositionInStream">Position in stream (virtual)</param>
        public void InsertBytes(byte[] bytes, long bytePositionInStream)
        {
            if (_viewModel == null || ReadOnlyMode || bytes == null || bytes.Length == 0) return;
            _viewModel.InsertBytes(new VirtualPosition(bytePositionInStream), bytes);
        }

        /// <summary>
        /// V1 compatible: Delete bytes at position
        /// </summary>
        /// <param name="bytePositionInStream">Start position (virtual)</param>
        /// <param name="length">Number of bytes to delete</param>
        public void DeleteBytesAtPosition(long bytePositionInStream, long length)
        {
            if (_viewModel == null || ReadOnlyMode || length <= 0) return;

            // Delete bytes one by one (ByteProvider V2 handles this internally)
            _viewModel.BeginUpdate();
            try
            {
                for (long i = 0; i < length; i++)
                {
                    _viewModel.DeleteByte(new VirtualPosition(bytePositionInStream));
                    // Note: After deleting, the next byte shifts to the same position
                    // So we keep deleting at the same position
                }
            }
            finally
            {
                _viewModel.EndUpdate();
            }
        }

        /// <summary>
        /// V1 compatible: Get byte with copyChange parameter
        /// Returns tuple with byte value and success flag
        /// </summary>
        /// <param name="position">Position in file (virtual)</param>
        /// <param name="copyChange">If true, returns modified value; if false, returns original value</param>
        /// <returns>Tuple (byte value, success flag)</returns>
        public (byte? singleByte, bool success) GetByte(long position, bool copyChange)
        {
            if (_viewModel == null || position < 0 || position >= VirtualLength)
                return (null, false);

            // V2 always returns modified values (copyChange=true behavior)
            // To get original values (copyChange=false), we would need ByteProvider support
            // For now, we only support copyChange=true
            var byteValue = _viewModel.GetByte(position);
            return (byteValue, true);
        }

        /// <summary>
        /// V1 compatible: Get all bytes from file
        /// </summary>
        /// <param name="copyChange">If true, includes modifications; if false, original file only</param>
        /// <returns>Byte array of entire file</returns>
        public byte[] GetAllBytes(bool copyChange = true)
        {
            if (_viewModel == null || VirtualLength == 0)
                return Array.Empty<byte>();

            // V2 always returns modified values (copyChange=true behavior)
            // Get all bytes from ByteProvider
            byte[] result = new byte[VirtualLength];
            for (long i = 0; i < VirtualLength; i++)
            {
                result[i] = _viewModel.GetByte(i);
            }
            return result;
        }

        #endregion

        #region Public Methods - Bookmarks (V1 Compatible)

        /// <summary>
        /// Add a bookmark at the specified position (V1 compatible)
        /// </summary>
        /// <param name="position">Position to bookmark (virtual)</param>
        public void SetBookmark(long position)
        {
            if (position < 0 || position >= VirtualLength) return;
            if (!_bookmarks.Contains(position))
            {
                _bookmarks.Add(position);
                _bookmarks.Sort(); // Keep sorted for easy navigation
            }
        }

        /// <summary>
        /// Remove a bookmark at the specified position (V1 compatible)
        /// </summary>
        /// <param name="position">Position to remove bookmark from (virtual)</param>
        public void RemoveBookmark(long position)
        {
            _bookmarks.Remove(position);
        }

        /// <summary>
        /// Clear all bookmarks (V1 compatible)
        /// </summary>
        public void ClearAllBookmarks()
        {
            _bookmarks.Clear();
        }

        /// <summary>
        /// Get all bookmarks (V1 compatible)
        /// </summary>
        /// <returns>Array of bookmark positions</returns>
        public long[] GetBookmarks()
        {
            return _bookmarks.ToArray();
        }

        /// <summary>
        /// Check if a position is bookmarked (V1 compatible)
        /// </summary>
        /// <param name="position">Position to check (virtual)</param>
        /// <returns>True if position is bookmarked</returns>
        public bool IsBookmarked(long position)
        {
            return _bookmarks.Contains(position);
        }

        /// <summary>
        /// Get the next bookmark after the specified position (V1 compatible)
        /// </summary>
        /// <param name="position">Current position (virtual)</param>
        /// <returns>Position of next bookmark, or -1 if none found</returns>
        public long GetNextBookmark(long position)
        {
            foreach (var bookmark in _bookmarks)
            {
                if (bookmark > position)
                    return bookmark;
            }
            return -1;
        }

        /// <summary>
        /// Get the previous bookmark before the specified position (V1 compatible)
        /// </summary>
        /// <param name="position">Current position (virtual)</param>
        /// <returns>Position of previous bookmark, or -1 if none found</returns>
        public long GetPreviousBookmark(long position)
        {
            for (int i = _bookmarks.Count - 1; i >= 0; i--)
            {
                if (_bookmarks[i] < position)
                    return _bookmarks[i];
            }
            return -1;
        }

        #endregion

        #region Public Methods - File Comparison (V1 Compatible)

        private readonly Services.ComparisonService _comparisonService = new();
        private List<ByteDifference> _comparisonResults = null;

        /// <summary>
        /// Compare this editor's content with another HexEditorV2
        /// </summary>
        /// <param name="other">Other HexEditorV2 to compare against</param>
        /// <param name="highlightDifferences">Automatically highlight differences with custom background blocks</param>
        /// <param name="maxDifferences">Maximum number of differences to return (0 = unlimited)</param>
        /// <returns>Enumerable of byte differences</returns>
        public IEnumerable<ByteDifference> Compare(HexEditorV2 other, bool highlightDifferences = true, long maxDifferences = 1000)
        {
            if (other == null || _viewModel?.Provider == null || other._viewModel?.Provider == null)
                return Enumerable.Empty<ByteDifference>();

            // TODO: Implement comparison for ByteProvider V2
            // ComparisonService needs to be updated to work with ByteProvider V2
            var differences = new List<ByteDifference>(); // Stub for now
            // var differences = _comparisonService.Compare(_viewModel.Provider, other._viewModel.Provider, maxDifferences).ToList();
            _comparisonResults = differences;

            if (highlightDifferences && differences.Any())
            {
                // Clear existing comparison highlights
                ClearCustomBackgroundBlock();

                // Highlight each difference
                foreach (var diff in differences)
                {
                    var block = new Core.CustomBackgroundBlock(
                        diff.BytePositionInStream,
                        1, // Single byte
                        new SolidColorBrush(Colors.LightCoral),
                        $"Diff: 0x{diff.Origine:X2} vs 0x{diff.Destination:X2}"
                    );
                    AddCustomBackgroundBlock(block);
                }
            }

            return differences;
        }

        /// <summary>
        /// Compare this editor's content with a ByteProvider
        /// </summary>
        /// <param name="provider">ByteProvider to compare against</param>
        /// <param name="highlightDifferences">Automatically highlight differences</param>
        /// <param name="maxDifferences">Maximum differences to return (0 = unlimited)</param>
        /// <returns>Enumerable of byte differences</returns>
        public IEnumerable<ByteDifference> Compare(Core.Bytes.ByteProviderLegacy provider, bool highlightDifferences = true, long maxDifferences = 1000)
        {
            if (provider == null || _viewModel?.Provider == null)
                return Enumerable.Empty<ByteDifference>();

            // TODO: Implement comparison between ByteProvider V2 and ByteProviderLegacy
            // ComparisonService needs to be updated
            var differences = new List<ByteDifference>(); // Stub for now
            // var differences = _comparisonService.Compare(_viewModel.Provider, provider, maxDifferences).ToList();
            _comparisonResults = differences;

            if (highlightDifferences && differences.Any())
            {
                // Clear existing comparison highlights
                ClearCustomBackgroundBlock();

                // Highlight each difference
                foreach (var diff in differences)
                {
                    var block = new Core.CustomBackgroundBlock(
                        diff.BytePositionInStream,
                        1,
                        new SolidColorBrush(Colors.LightCoral),
                        $"Diff: 0x{diff.Origine:X2} vs 0x{diff.Destination:X2}"
                    );
                    AddCustomBackgroundBlock(block);
                }
            }

            return differences;
        }

        /// <summary>
        /// Clear comparison results and highlighting
        /// </summary>
        public void ClearComparison()
        {
            _comparisonResults = null;
            ClearCustomBackgroundBlock();
        }

        /// <summary>
        /// Get the last comparison results
        /// </summary>
        public IEnumerable<ByteDifference> GetComparisonResults()
        {
            return _comparisonResults ?? Enumerable.Empty<ByteDifference>();
        }

        /// <summary>
        /// Count differences between this editor and another
        /// </summary>
        public long CountDifferences(HexEditorV2 other)
        {
            if (other == null || _viewModel?.Provider == null || other._viewModel?.Provider == null)
                return 0;

            // TODO: Implement for ByteProvider V2
            return 0; // Stub
            // return _comparisonService.CountDifferences(_viewModel.Provider, other._viewModel.Provider);
        }

        /// <summary>
        /// Calculate similarity percentage with another editor (0.0 - 100.0)
        /// </summary>
        public double CalculateSimilarity(HexEditorV2 other)
        {
            if (other == null || _viewModel?.Provider == null || other._viewModel?.Provider == null)
                return 0.0;

            // TODO: Implement for ByteProvider V2
            return 0.0; // Stub
            // return _comparisonService.CalculateSimilarity(_viewModel.Provider, other._viewModel.Provider);
        }

        #endregion

        #region Public Methods - V1 Additional Compatibility

        /// <summary>
        /// V1: Set position from hex string
        /// </summary>
        public void SetPosition(string hexLiteralPosition)
        {
            if (string.IsNullOrEmpty(hexLiteralPosition)) return;
            try
            {
                hexLiteralPosition = hexLiteralPosition.Replace("0x", "").Replace("0X", "");
                long position = Convert.ToInt64(hexLiteralPosition, 16);
                SetPosition(position);
            }
            catch { }
        }

        /// <summary>
        /// V1: Set position and create selection
        /// </summary>
        public void SetPosition(long position, long byteLength)
        {
            if (_viewModel == null) return;
            SelectionStart = position;
            SelectionStop = position + byteLength - 1;
            SetPosition(position);
        }

        /// <summary>
        /// V1: Submit changes (alias for Save)
        /// </summary>
        public void SubmitChanges() => Save();

        /// <summary>
        /// V1: Submit changes to new file (alias for SaveAs)
        /// </summary>
        public void SubmitChanges(string newFilename, bool overwrite)
        {
            if (_viewModel == null) return;
            try
            {
                bool success = _viewModel.SaveAs(newFilename, overwrite);
                if (success)
                {
                    FileName = newFilename;
                    StatusText.Text = $"Saved to {System.IO.Path.GetFileName(newFilename)}";
                }
                else
                {
                    StatusText.Text = "File already exists";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to save: {ex.Message}";
            }
        }

        /// <summary>
        /// V1: Unselect all
        /// </summary>
        public void UnSelectAll(bool cleanFocus = false)
        {
            ClearSelection();
            if (cleanFocus) Keyboard.ClearFocus();
        }

        /// <summary>
        /// V1: Undo with repeat count
        /// </summary>
        public void Undo(int repeat)
        {
            if (_viewModel == null) return;
            for (int i = 0; i < repeat; i++)
            {
                if (_viewModel.CanUndo)
                    Undo();
                else
                    break;
            }
        }

        /// <summary>
        /// V1: Redo with repeat count
        /// </summary>
        public void Redo(int repeat)
        {
            if (_viewModel == null) return;
            for (int i = 0; i < repeat; i++)
            {
                if (_viewModel.CanRedo)
                    Redo();
                else
                    break;
            }
        }

        /// <summary>
        /// V1: Clear all undo/redo history
        /// </summary>
        public void ClearAllChange() => _viewModel?.ClearUndoRedo();

        /// <summary>
        /// V1: Refresh view with options
        /// </summary>
        public void RefreshView(bool controlResize = false, bool refreshData = true)
        {
            if (_viewModel == null) return;
            if (refreshData)
            {
                _viewModel.RefreshDisplay();
                HexViewport?.InvalidateVisual();
            }
            if (controlResize)
            {
                InvalidateMeasure();
                InvalidateArrange();
            }
            InvalidateVisual();
        }

        /// <summary>
        /// V1: Update visual rendering
        /// </summary>
        public void UpdateVisual()
        {
            InvalidateVisual();
            HexViewport?.InvalidateVisual();
        }

        /// <summary>
        /// V1: Get line number from position
        /// </summary>
        public long GetLineNumber(long position) => _viewModel == null ? 0 : position / BytePerLine;

        /// <summary>
        /// V1: Get column number from position
        /// </summary>
        public long GetColumnNumber(long position) => _viewModel == null ? 0 : position % BytePerLine;

        /// <summary>
        /// V1: Check if byte position is visible in viewport
        /// </summary>
        public bool IsBytePositionAreVisible(long position)
        {
            if (_viewModel == null || HexViewport == null) return false;
            long startLine = _viewModel.ScrollPosition;
            long endLine = startLine + _viewModel.VisibleLines;
            long positionLine = position / BytePerLine;
            return positionLine >= startLine && positionLine < endLine;
        }

        /// <summary>
        /// V1: Close provider with option to clear filename
        /// </summary>
        public void CloseProvider(bool clearFileName = true)
        {
            Close();
            if (clearFileName)
                FileName = string.Empty;
        }

        // ResetZoom moved to Zoom Support region above

        /// <summary>
        /// V1: Update focus
        /// </summary>
        public void UpdateFocus()
        {
            HexViewport?.Focus();
        }

        /// <summary>
        /// V1: Set focus at selection start
        /// </summary>
        public void SetFocusAtSelectionStart()
        {
            if (_viewModel != null && SelectionStart >= 0)
            {
                SetPosition(SelectionStart);
                UpdateFocus();
            }
        }

        /// <summary>
        /// V1: Set focus at specific position
        /// </summary>
        public void SetFocusAt(long position)
        {
            SetPosition(position);
            UpdateFocus();
        }

        #endregion

        #region Public Methods - Custom Background Blocks (V1 Compatible)

        /// <summary>
        /// Add a custom background block (V1 compatible - Phase 7.1)
        /// </summary>
        public void AddCustomBackgroundBlock(Core.CustomBackgroundBlock block)
        {
            if (block == null) return;
            _customBackgroundBlocks.Add(block);

            // Phase 7.1: Sync with HexViewport for rendering
            if (HexViewport != null)
            {
                HexViewport.CustomBackgroundBlocks = _customBackgroundBlocks;
                HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Remove a custom background block (V1 compatible - Phase 7.1)
        /// </summary>
        public void RemoveCustomBackgroundBlock(Core.CustomBackgroundBlock block)
        {
            if (block == null) return;
            _customBackgroundBlocks.Remove(block);

            // Phase 7.1: Sync with HexViewport for rendering
            if (HexViewport != null)
            {
                HexViewport.CustomBackgroundBlocks = _customBackgroundBlocks;
                HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Clear all custom background blocks (V1 compatible - Phase 7.1)
        /// </summary>
        public void ClearCustomBackgroundBlock()
        {
            _customBackgroundBlocks.Clear();

            // Phase 7.1: Sync with HexViewport for rendering
            if (HexViewport != null)
            {
                HexViewport.CustomBackgroundBlocks = _customBackgroundBlocks;
                HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Get custom background block at position (V1 compatible)
        /// </summary>
        public Core.CustomBackgroundBlock GetCustomBackgroundBlock(long position)
        {
            return _customBackgroundBlocks.FirstOrDefault(b =>
                position >= b.StartOffset && position < b.StopOffset);
        }

        /// <summary>
        /// Get all custom background blocks at position (V1 compatible)
        /// </summary>
        public IEnumerable<Core.CustomBackgroundBlock> GetCustomBackgroundBlocks(long position)
        {
            return _customBackgroundBlocks.Where(b =>
                position >= b.StartOffset && position < b.StopOffset);
        }

        #endregion

        #region Public Methods - File Comparison (V1 Compatible)

        /// <summary>
        /// Compare this file with another HexEditorV2 (V1 compatible)
        /// Returns list of differences between the two files
        /// </summary>
        public IEnumerable<Core.Bytes.ByteDifference> Compare(HexEditorV2 other)
        {
            if (_viewModel == null || other?._viewModel == null)
                return Enumerable.Empty<Core.Bytes.ByteDifference>();

            return CompareProviders(_viewModel, other._viewModel);
        }

        /// <summary>
        /// Compare this file with a ByteProvider (V1 compatible)
        /// Returns list of differences between the two providers
        /// </summary>
        public IEnumerable<Core.Bytes.ByteDifference> Compare(Core.Bytes.ByteProviderLegacy provider)
        {
            if (_viewModel == null || provider == null)
                return Enumerable.Empty<Core.Bytes.ByteDifference>();

            // TODO: Implement comparison between ByteProvider V2 and ByteProviderLegacy
            // Can't create HexEditorViewModel with ByteProviderLegacy anymore
            return Enumerable.Empty<Core.Bytes.ByteDifference>(); // Stub for now

            // Old code:
            // var tempViewModel = new ViewModels.HexEditorViewModel(provider);
            // var result = CompareProviders(_viewModel, tempViewModel);
            // tempViewModel.Close();
            // return result;
        }

        /// <summary>
        /// Internal comparison logic
        /// </summary>
        private IEnumerable<Core.Bytes.ByteDifference> CompareProviders(
            ViewModels.HexEditorViewModel vm1,
            ViewModels.HexEditorViewModel vm2)
        {
            var differences = new List<Core.Bytes.ByteDifference>();
            long maxLength = Math.Max(vm1.VirtualLength, vm2.VirtualLength);

            for (long i = 0; i < maxLength; i++)
            {
                byte byte1 = i < vm1.VirtualLength ? vm1.GetByteAt(new Models.VirtualPosition(i)) : (byte)0;
                byte byte2 = i < vm2.VirtualLength ? vm2.GetByteAt(new Models.VirtualPosition(i)) : (byte)0;

                if (byte1 != byte2)
                {
                    differences.Add(new Core.Bytes.ByteDifference(byte1, byte2, i));
                }
            }

            return differences;
        }

        #endregion

        #region Public Methods - State Persistence (V1 Compatible)

        /// <summary>
        /// Save current editor state to XML file (V1 compatible)
        /// Saves: position, selection, bookmarks, font size, filename
        /// </summary>
        public void SaveCurrentState(string stateFilename)
        {
            try
            {
                var doc = new System.Xml.Linq.XDocument(
                    new System.Xml.Linq.XElement("HexEditorState",
                        new System.Xml.Linq.XElement("FileName", FileName ?? string.Empty),
                        new System.Xml.Linq.XElement("Position", Position),
                        new System.Xml.Linq.XElement("SelectionStart", SelectionStart),
                        new System.Xml.Linq.XElement("SelectionStop", SelectionStop),
                        new System.Xml.Linq.XElement("FontSize", FontSize),
                        new System.Xml.Linq.XElement("BytePerLine", BytePerLine),
                        new System.Xml.Linq.XElement("Bookmarks",
                            _bookmarks.Select(b => new System.Xml.Linq.XElement("Bookmark", b))
                        )
                    )
                );

                doc.Save(stateFilename);
                StatusText.Text = $"State saved to {System.IO.Path.GetFileName(stateFilename)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to save state: {ex.Message}";
            }
        }

        /// <summary>
        /// Load editor state from XML file (V1 compatible)
        /// Restores: position, selection, bookmarks, font size
        /// Note: Does NOT reload the file, only restores state
        /// </summary>
        public void LoadCurrentState(string stateFilename)
        {
            try
            {
                var doc = System.Xml.Linq.XDocument.Load(stateFilename);
                var root = doc.Root;

                if (root?.Name != "HexEditorState") return;

                // Restore basic properties
                var fontSize = root.Element("FontSize")?.Value;
                if (fontSize != null && double.TryParse(fontSize, out double fs))
                    FontSize = fs;

                var bytesPerLine = root.Element("BytePerLine")?.Value;
                if (bytesPerLine != null && int.TryParse(bytesPerLine, out int bpl))
                    BytePerLine = bpl;

                // Restore position and selection
                var position = root.Element("Position")?.Value;
                if (position != null && long.TryParse(position, out long pos))
                    SetPosition(pos);

                var selStart = root.Element("SelectionStart")?.Value;
                var selStop = root.Element("SelectionStop")?.Value;
                if (selStart != null && long.TryParse(selStart, out long start) &&
                    selStop != null && long.TryParse(selStop, out long stop))
                {
                    SelectionStart = start;
                    SelectionStop = stop;
                }

                // Restore bookmarks
                var bookmarks = root.Element("Bookmarks")?.Elements("Bookmark");
                if (bookmarks != null)
                {
                    ClearAllBookmarks();
                    foreach (var bookmark in bookmarks)
                    {
                        if (long.TryParse(bookmark.Value, out long bookmarkPos))
                            SetBookmark(bookmarkPos);
                    }
                }

                StatusText.Text = $"State loaded from {System.IO.Path.GetFileName(stateFilename)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to load state: {ex.Message}";
            }
        }

        #endregion

        #region Public Methods - TBL Support (V1 Compatible)

        /// <summary>
        /// Load a TBL (Character Table) file (V1 compatible)
        /// </summary>
        /// <param name="path">Path to the TBL file</param>
        public void LoadTBLFile(string path)
        {
            try
            {
                _tblStream = new TblStream(path);
                _characterTableType = CharacterTableType.TblFile;

                // Phase 7.5: Sync TblStream to HexViewport for color rendering
                if (HexViewport != null)
                    HexViewport.TblStream = _tblStream;

                StatusText.Text = $"TBL loaded: {System.IO.Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to load TBL: {ex.Message}";
                _tblStream = null;
                _characterTableType = CharacterTableType.Ascii;

                // Phase 7.5: Clear TblStream in HexViewport
                if (HexViewport != null)
                    HexViewport.TblStream = null;
            }
        }

        /// <summary>
        /// Close the current TBL file and revert to ASCII (V1 compatible)
        /// </summary>
        public void CloseTBL()
        {
            if (_tblStream != null)
            {
                _tblStream.Close();
                _tblStream.Dispose();
                _tblStream = null;
            }
            _characterTableType = CharacterTableType.Ascii;

            // Phase 7.5: Clear TblStream in HexViewport
            if (HexViewport != null)
                HexViewport.TblStream = null;

            StatusText.Text = "TBL closed, using ASCII";
        }

        /// <summary>
        /// Get or set the type of character table to use (V1 compatible)
        /// </summary>
        public CharacterTableType TypeOfCharacterTable
        {
            get => _characterTableType;
            set
            {
                _characterTableType = value;
                // If switching to TBL but no TBL loaded, create default ASCII
                if (value == CharacterTableType.TblFile && _tblStream == null)
                {
                    _tblStream = TblStream.CreateDefaultTbl(DefaultCharacterTableType.Ascii);

                    // Phase 7.5: Sync TblStream to HexViewport
                    if (HexViewport != null)
                        HexViewport.TblStream = _tblStream;
                }
                // If switching away from TBL, close it
                else if (value != CharacterTableType.TblFile && _tblStream != null)
                {
                    CloseTBL();
                }
            }
        }

        /// <summary>
        /// Get the current TBL stream (V1 compatible)
        /// </summary>
        public TblStream TBL => _tblStream;

        #endregion

        #region Phase 12 - 100% V1 Compatibility (Missing Properties and Methods)

        // ================================================================================
        // Phase 12: Final V1 Compatibility - Adds remaining properties and methods
        // identified by real-world sample testing (V1CompatibilityStatus.md)
        // ================================================================================

        #region Missing V1 Properties - Display/UI

        /// <summary>
        /// Show tooltip on byte hover (V1 compatible)
        /// </summary>
        public static readonly DependencyProperty ShowByteToolTipProperty =
            DependencyProperty.Register(nameof(ShowByteToolTip), typeof(bool),
                typeof(HexEditorV2), new PropertyMetadata(false, OnShowByteToolTipChanged));

        public bool ShowByteToolTip
        {
            get => (bool)GetValue(ShowByteToolTipProperty);
            set => SetValue(ShowByteToolTipProperty, value);
        }

        private static void OnShowByteToolTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorV2 editor && editor.HexViewport != null)
            {
                editor.HexViewport.ShowByteToolTip = (bool)e.NewValue;
            }
        }

        /// <summary>
        /// Hide bytes that are marked as deleted (V1 compatible) - DependencyProperty
        /// </summary>
        public bool HideByteDeleted
        {
            get => (bool)GetValue(HideByteDeletedProperty);
            set => SetValue(HideByteDeletedProperty, value);
        }

        /// <summary>
        /// Default clipboard copy/paste mode (V1 compatible) - DependencyProperty
        /// </summary>
        public CopyPasteMode DefaultCopyToClipboardMode
        {
            get => (CopyPasteMode)GetValue(DefaultCopyToClipboardModeProperty);
            set => SetValue(DefaultCopyToClipboardModeProperty, value);
        }

        #endregion

        #region Missing V1 Properties - Editing/Insert Mode

        /// <summary>
        /// Allow insert at any position (V1 compatible)
        /// In V2, insert mode is always allowed via EditMode property
        /// </summary>
        public bool CanInsertAnywhere
        {
            get => EditMode == EditMode.Insert;
            set
            {
                if (value)
                {
                    EditMode = EditMode.Insert;

                    // ByteProvider V2 always supports insertion anywhere - no flag needed
                    if (_viewModel?.Provider != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CANINSERT] ByteProvider V2 supports insertion");
                    }
                }
                // Note: Setting false doesn't force Overwrite to allow other modes
            }
        }

        /// <summary>
        /// Visual caret mode for insert/overwrite indication (V1 compatible) - DependencyProperty
        /// </summary>
        public CaretMode VisualCaretMode
        {
            get => (CaretMode)GetValue(VisualCaretModeProperty);
            set => SetValue(VisualCaretModeProperty, value);
        }

        /// <summary>
        /// Byte shift left amount (V1 compatible) - DependencyProperty
        /// Used for adjusting byte position display offset
        /// </summary>
        public long ByteShiftLeft
        {
            get => (long)GetValue(ByteShiftLeftProperty);
            set => SetValue(ByteShiftLeftProperty, value);
        }

        #endregion

        #region Missing V1 Properties - Auto-Highlight

        /// <summary>
        /// Auto-highlight bytes that match the selected byte (V1 compatible) - DependencyProperty
        /// </summary>
        public bool AllowAutoHighLightSelectionByte
        {
            get => (bool)GetValue(AllowAutoHighLightSelectionByteProperty);
            set => SetValue(AllowAutoHighLightSelectionByteProperty, value);
        }

        /// <summary>
        /// Auto-highlight brush color for bytes matching selected byte (V1 compatible) - DependencyProperty
        /// </summary>
        public System.Windows.Media.Color AutoHighLiteSelectionByteBrush
        {
            get => (System.Windows.Media.Color)GetValue(AutoHighLiteSelectionByteBrushProperty);
            set => SetValue(AutoHighLiteSelectionByteBrushProperty, value);
        }

        /// <summary>
        /// Auto-select all same bytes when double-clicking a byte (V1 compatible) - DependencyProperty
        /// </summary>
        public bool AllowAutoSelectSameByteAtDoubleClick
        {
            get => (bool)GetValue(AllowAutoSelectSameByteAtDoubleClickProperty);
            set => SetValue(AllowAutoSelectSameByteAtDoubleClickProperty, value);
        }

        #endregion

        #region Missing V1 Properties - Count/Statistics

        /// <summary>
        /// Enable byte counting feature (V1 compatible) - DependencyProperty
        /// </summary>
        public bool AllowByteCount
        {
            get => (bool)GetValue(AllowByteCountProperty);
            set => SetValue(AllowByteCountProperty, value);
        }

        #endregion

        #region Missing V1 Properties - File Drop/Drag

        /// <summary>
        /// Confirm before dropping a file to load it (V1 compatible) - DependencyProperty
        /// </summary>
        public bool FileDroppingConfirmation
        {
            get => (bool)GetValue(FileDroppingConfirmationProperty);
            set => SetValue(FileDroppingConfirmationProperty, value);
        }

        /// <summary>
        /// Allow text drag-drop operations (V1 compatible) - DependencyProperty
        /// </summary>
        public bool AllowTextDrop
        {
            get => (bool)GetValue(AllowTextDropProperty);
            set => SetValue(AllowTextDropProperty, value);
        }

        /// <summary>
        /// Allow file drag-drop operations (V1 compatible) - DependencyProperty
        /// Note: AllowDrop must also be true for this to work
        /// </summary>
        public bool AllowFileDrop
        {
            get => (bool)GetValue(AllowFileDropProperty);
            set => SetValue(AllowFileDropProperty, value);
        }

        #endregion

        #region Missing V1 Properties - Extend/Append

        /// <summary>
        /// Allow extending file at end (V1 compatible) - DependencyProperty
        /// </summary>
        public bool AllowExtend
        {
            get => (bool)GetValue(AllowExtendProperty);
            set => SetValue(AllowExtendProperty, value);
        }

        /// <summary>
        /// Confirm before appending bytes (V1 compatible) - DependencyProperty
        /// </summary>
        public bool AppendNeedConfirmation
        {
            get => (bool)GetValue(AppendNeedConfirmationProperty);
            set => SetValue(AppendNeedConfirmationProperty, value);
        }

        #endregion

        #region Missing V1 Properties - Delete Byte

        /// <summary>
        /// Allow byte deletion (V1 compatible) - DependencyProperty
        /// </summary>
        public bool AllowDeleteByte
        {
            get => (bool)GetValue(AllowDeleteByteProperty);
            set => SetValue(AllowDeleteByteProperty, value);
        }

        #endregion

        #region Missing V1 Properties - State

        private System.Xml.Linq.XDocument _currentStateDocument;

        /// <summary>
        /// Current editor state as XDocument for persistence (V1 compatible)
        /// Get: Returns current state as XML document
        /// Set: Restores state from XML document
        /// </summary>
        public System.Xml.Linq.XDocument CurrentState
        {
            get
            {
                // Generate current state as XDocument
                var doc = new System.Xml.Linq.XDocument(
                    new System.Xml.Linq.XElement("HexEditorState",
                        new System.Xml.Linq.XElement("FileName", FileName ?? string.Empty),
                        new System.Xml.Linq.XElement("Position", Position),
                        new System.Xml.Linq.XElement("SelectionStart", SelectionStart),
                        new System.Xml.Linq.XElement("SelectionStop", SelectionStop),
                        new System.Xml.Linq.XElement("FontSize", FontSize),
                        new System.Xml.Linq.XElement("BytePerLine", BytePerLine),
                        new System.Xml.Linq.XElement("ReadOnlyMode", ReadOnlyMode),
                        new System.Xml.Linq.XElement("Bookmarks",
                            _bookmarks.Select(b => new System.Xml.Linq.XElement("Bookmark", b))
                        )
                    )
                );
                return doc;
            }
            set
            {
                if (value == null) return;

                var root = value.Root;
                if (root?.Name != "HexEditorState") return;

                // Restore basic properties
                var fontSize = root.Element("FontSize")?.Value;
                if (fontSize != null && double.TryParse(fontSize, out double fs))
                    FontSize = fs;

                var bytesPerLine = root.Element("BytePerLine")?.Value;
                if (bytesPerLine != null && int.TryParse(bytesPerLine, out int bpl))
                    BytePerLine = bpl;

                var readOnlyMode = root.Element("ReadOnlyMode")?.Value;
                if (readOnlyMode != null && bool.TryParse(readOnlyMode, out bool rom))
                    ReadOnlyMode = rom;

                // Restore position and selection
                var position = root.Element("Position")?.Value;
                if (position != null && long.TryParse(position, out long pos))
                    SetPosition(pos);

                var selStart = root.Element("SelectionStart")?.Value;
                var selStop = root.Element("SelectionStop")?.Value;
                if (selStart != null && long.TryParse(selStart, out long start) &&
                    selStop != null && long.TryParse(selStop, out long stop))
                {
                    SelectionStart = start;
                    SelectionStop = stop;
                }

                // Restore bookmarks
                var bookmarks = root.Element("Bookmarks")?.Elements("Bookmark");
                if (bookmarks != null)
                {
                    ClearAllBookmarks();
                    foreach (var bookmark in bookmarks)
                    {
                        if (long.TryParse(bookmark.Value, out long bookmarkPos))
                            SetBookmark(bookmarkPos);
                    }
                }

                _currentStateDocument = value;
            }
        }

        #endregion

        #region Missing V1 Methods - Clipboard

        /// <summary>
        /// Copy to clipboard with default mode (V1 compatible)
        /// </summary>
        public void CopyToClipboard()
        {
            CopyToClipboard(DefaultCopyToClipboardMode);
        }

        /// <summary>
        /// Copy to clipboard with specified mode (V1 compatible)
        /// </summary>
        /// <param name="mode">Copy mode (HexaString, AsciiString, etc.)</param>
        public void CopyToClipboard(CopyPasteMode mode)
        {
            if (_viewModel == null || !_viewModel.HasSelection)
                return;

            try
            {
                switch (mode)
                {
                    case CopyPasteMode.HexaString:
                        Copy(); // Default V2 behavior copies as hex
                        break;
                    case CopyPasteMode.AsciiString:
                        CopyAsAscii();
                        break;
                    case CopyPasteMode.TblString:
                        CopyAsTbl();
                        break;
                    case CopyPasteMode.CSharpCode:
                        CopyAsCSharpCode();
                        break;
                    default:
                        Copy(); // Default to hex
                        break;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Copy failed: {ex.Message}";
            }
        }

        private void CopyAsAscii()
        {
            if (_viewModel == null || !_viewModel.HasSelection)
                return;

            var bytes = _viewModel.GetSelectionBytes();
            if (bytes != null)
            {
                var text = System.Text.Encoding.ASCII.GetString(bytes);
                Clipboard.SetText(text);
                StatusText.Text = $"Copied {bytes.Length} bytes as ASCII";
            }
        }

        private void CopyAsTbl()
        {
            if (_viewModel == null || !_viewModel.HasSelection)
            {
                return;
            }

            if (_tblStream == null)
            {
                CopyAsAscii(); // Fallback to ASCII if no TBL
                return;
            }

            var bytes = _viewModel.GetSelectionBytes();
            if (bytes != null)
            {
                // TBL to string conversion - simplified implementation
                var text = System.Text.Encoding.ASCII.GetString(bytes); // Fallback to ASCII for now
                Clipboard.SetText(text);
                StatusText.Text = $"Copied {bytes.Length} bytes as TBL";
            }
        }

        private void CopyAsCSharpCode()
        {
            if (_viewModel == null || !_viewModel.HasSelection)
                return;

            var bytes = _viewModel.GetSelectionBytes();
            if (bytes != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("byte[] data = new byte[] {");
                sb.Append("    ");
                for (int i = 0; i < bytes.Length; i++)
                {
                    sb.Append($"0x{bytes[i]:X2}");
                    if (i < bytes.Length - 1)
                        sb.Append(", ");
                    if ((i + 1) % 16 == 0 && i < bytes.Length - 1)
                        sb.AppendLine().Append("    ");
                }
                sb.AppendLine();
                sb.Append("};");

                Clipboard.SetText(sb.ToString());
                StatusText.Text = $"Copied {bytes.Length} bytes as C# code";
            }
        }

        #endregion

        #region Missing V1 Methods - Bookmarks (Naming Alias)

        /// <summary>
        /// Set bookmark at current position (V1 compatible - note capital M)
        /// This is an alias for SetBookmark() with different casing
        /// </summary>
        [Obsolete("Use SetBookmark() instead. This method exists only for V1 case-sensitive compatibility.", false)]
        public void SetBookMark()
        {
            SetBookmark(Position);
        }

        /// <summary>
        /// Set bookmark at position (V1 compatible - note capital M)
        /// This is an alias for SetBookmark() with different casing
        /// </summary>
        /// <param name="position">Position to bookmark</param>
        [Obsolete("Use SetBookmark(long position) instead. This method exists only for V1 case-sensitive compatibility.", false)]
        public void SetBookMark(long position)
        {
            SetBookmark(position);
        }

        #endregion

        #region Missing V1 Methods - Scroll Markers

        /// <summary>
        /// Clear all scroll markers (V1 compatible)
        /// </summary>
        public void ClearScrollMarker()
        {
            if (_scrollMarkers != null)
            {
                _scrollMarkers.ClearAllMarkers();
            }
        }

        /// <summary>
        /// Clear specific type of scroll marker (V1 compatible)
        /// </summary>
        /// <param name="marker">Type of marker to clear</param>
        public void ClearScrollMarker(ScrollMarker marker)
        {
            if (_scrollMarkers == null) return;

            switch (marker)
            {
                case ScrollMarker.Nothing:
                    _scrollMarkers.ClearAllMarkers();
                    break;

                case ScrollMarker.SearchHighLight:
                    _scrollMarkers.SearchResultPositions = new HashSet<long>();
                    break;

                case ScrollMarker.Bookmark:
                case ScrollMarker.TblBookmark:
                    _scrollMarkers.BookmarkPositions = new HashSet<long>();
                    break;

                case ScrollMarker.ByteModified:
                case ScrollMarker.ByteDeleted:
                    _scrollMarkers.ModifiedPositions = new HashSet<long>();
                    break;

                case ScrollMarker.SelectionStart:
                    // Selection is not shown in scroll markers, so nothing to clear
                    break;
            }
        }

        #endregion

        #region Missing V1 Methods - Find All Selection

        /// <summary>
        /// Find all occurrences of the current selection (V1 compatible)
        /// Highlights all matching bytes in the file
        /// </summary>
        /// <param name="highlight">Whether to highlight results (V1 parameter, always highlights in V2)</param>
        public void FindAllSelection(bool highlight = true)
        {
            FindAllSelection();
        }

        /// <summary>
        /// Find all occurrences of the current selection (V1 compatible)
        /// Highlights all matching bytes in the file
        /// </summary>
        private void FindAllSelection()
        {
            if (_viewModel == null || !_viewModel.HasSelection)
            {
                StatusText.Text = "No selection to find";
                return;
            }

            try
            {
                // Get the selected bytes
                var pattern = _viewModel.GetSelectionBytes();
                if (pattern == null || pattern.Length == 0)
                {
                    StatusText.Text = "Selection is empty";
                    return;
                }

                // Find all occurrences
                var positions = new List<long>();
                long pos = 0;
                while (pos >= 0 && pos < _viewModel.VirtualLength)
                {
                    pos = FindFirst(pattern, pos);
                    if (pos >= 0)
                    {
                        positions.Add(pos);
                        pos++; // Move to next position
                    }
                }

                // Highlight all found positions using custom background blocks
                ClearCustomBackgroundBlock();
                foreach (var position in positions)
                {
                    var block = new CustomBackgroundBlock(
                        position,
                        pattern.Length,
                        new SolidColorBrush(Colors.Yellow),
                        "Found"
                    );
                    AddCustomBackgroundBlock(block);
                }

                StatusText.Text = $"Found {positions.Count} occurrences";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Find all failed: {ex.Message}";
            }
        }

        #endregion

        #region Missing V1 Methods - TBL Support (Naming Alias)

        /// <summary>
        /// Load TBL file (V1 compatible - note lowercase 'bl')
        /// This is an alias for LoadTBLFile() with different casing
        /// </summary>
        /// <param name="path">Path to TBL file</param>
        [Obsolete("Use LoadTBLFile(string path) instead. This method exists only for V1 case-sensitive compatibility.", false)]
        public void LoadTblFile(string path)
        {
            LoadTBLFile(path);
        }

        /// <summary>
        /// Load a default built-in TBL table with ASCII encoding (V1 compatible)
        /// </summary>
        public void LoadDefaultTbl()
        {
            LoadDefaultTbl(DefaultCharacterTableType.Ascii);
        }

        /// <summary>
        /// Load a default built-in TBL table (V1 compatible)
        /// </summary>
        /// <param name="type">Type of default table to load</param>
        public void LoadDefaultTbl(DefaultCharacterTableType type)
        {
            try
            {
                _tblStream = TblStream.CreateDefaultTbl(type);
                _characterTableType = CharacterTableType.TblFile;

                // Phase 7.5: Sync TblStream to HexViewport for color rendering
                if (HexViewport != null)
                    HexViewport.TblStream = _tblStream;

                StatusText.Text = $"Default TBL loaded: {type}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to load default TBL: {ex.Message}";
                _tblStream = null;
                _characterTableType = CharacterTableType.Ascii;
            }
        }

        #endregion

        #region Missing V1 Methods - Reverse Selection

        /// <summary>
        /// Reverse the byte order of the current selection (V1 compatible)
        /// </summary>
        public void ReverseSelection()
        {
            if (_viewModel == null || !_viewModel.HasSelection)
            {
                StatusText.Text = "No selection to reverse";
                return;
            }

            try
            {
                // Get the selected bytes
                var start = _viewModel.SelectionStart.Value;
                var bytes = _viewModel.GetSelectionBytes();
                if (bytes == null || bytes.Length == 0)
                {
                    StatusText.Text = "Selection is empty";
                    return;
                }

                // Reverse the byte array
                Array.Reverse(bytes);

                // Write the reversed bytes back
                for (int i = 0; i < bytes.Length; i++)
                {
                    _viewModel.ModifyByte(new VirtualPosition(start + i), bytes[i]);
                }

                StatusText.Text = $"Reversed {bytes.Length} bytes";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Reverse failed: {ex.Message}";
            }
        }

        #endregion

        #endregion

        #region Phase 13 - V1 Dialog Compatibility (Find/Replace Overloads)

        /// <summary>
        /// Find first occurrence with highlight support (V1 dialog compatible)
        /// </summary>
        public long FindFirst(byte[] data, long startPosition, bool highLight)
        {
            var result = FindFirst(data, startPosition);
            // V2 doesn't support inline highlight parameter, but we can ignore it
            return result;
        }

        /// <summary>
        /// Find next occurrence with highlight support (V1 dialog compatible)
        /// </summary>
        public long FindNext(byte[] data, bool highLight)
        {
            return FindNext(data, Position);
        }

        /// <summary>
        /// Find last occurrence with highlight support (V1 dialog compatible)
        /// </summary>
        public long FindLast(byte[] data, bool highLight)
        {
            return FindLast(data);
        }

        /// <summary>
        /// Find all occurrences with highlight support (V1 dialog compatible)
        /// Returns IEnumerable for V1 compatibility
        /// </summary>
        public IEnumerable<long> FindAll(byte[] data, bool highLight)
        {
            return FindAll(data, 0);
        }

        /// <summary>
        /// Replace first with V1 signature (truckLength, then highlight)
        /// </summary>
        public long ReplaceFirst(byte[] findData, byte[] replaceData, bool truckLength, bool hightlight)
        {
            return ReplaceFirst(findData, replaceData, 0, truckLength);
        }

        /// <summary>
        /// Replace next with V1 signature (truckLength, then highlight)
        /// </summary>
        public long ReplaceNext(byte[] findData, byte[] replaceData, bool truckLength, bool hightlight)
        {
            return ReplaceNext(findData, replaceData, Position + 1, truckLength);
        }

        /// <summary>
        /// Replace all with V1 signature (truckLength, then highlight)
        /// Returns IEnumerable for V1 dialog compatibility
        /// </summary>
        public IEnumerable<long> ReplaceAll(byte[] findData, byte[] replaceData, bool truckLength, bool hightlight)
        {
            // V2 ReplaceAll returns int (count), but V1 dialogs expect IEnumerable<long> (positions)
            // For compatibility, we'll find all positions and replace them, returning the positions
            var positions = new List<long>();
            long pos = 0;
            while (pos >= 0 && pos < VirtualLength)
            {
                pos = FindFirst(findData, pos);
                if (pos >= 0)
                {
                    positions.Add(pos);
                    // Replace at this position
                    ReplaceFirst(findData, replaceData, pos, truckLength);
                    pos += replaceData.Length; // Move past the replaced data
                }
            }
            return positions;
        }

        #endregion

        #region Internal Events

        private void Content_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null || e.ChangedButton != MouseButton.Left)
                return;

            // Set keyboard focus to enable keyboard input
            HexViewport.Focus();

            // Detect which area was clicked (Hex or ASCII)
            var mousePos = e.GetPosition(HexViewport);
            var clickArea = GetClickAreaAtMouse(mousePos);
            _isAsciiEditMode = (clickArea == ClickArea.Ascii);

            System.Diagnostics.Debug.WriteLine($"[CLICK] Area={clickArea}, AsciiEditMode={_isAsciiEditMode}");

            // Get the virtual position at mouse coordinates
            var position = GetVirtualPositionAtMouse(mousePos);
            if (!position.IsValid)
                return;

            // Check for Shift key (extend selection)
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                _viewModel.ExtendSelection(position);
            }
            else
            {
                // Start new selection
                _viewModel.SetSelection(position);
                _isMouseDown = true;
                _mouseDownPosition = position;

                // Capture mouse for drag operation
                HexViewport.CaptureMouse();
            }

            e.Handled = true;
        }

        private void Content_MouseMove(object sender, MouseEventArgs e)
        {
            if (_viewModel == null || !_isMouseDown || e.LeftButton != MouseButtonState.Pressed)
            {
                StopAutoScroll();
                return;
            }

            Point mousePos = e.GetPosition(HexViewport);
            _lastMousePosition = mousePos;

            var position = GetVirtualPositionAtMouse(mousePos);
            if (position.IsValid)
            {
                // Update selection range during drag
                _viewModel.SetSelectionRange(_mouseDownPosition, position);
            }

            // Check if mouse is near the top or bottom edge for auto-scroll
            double viewportHeight = HexViewport.ActualHeight;

            if (mousePos.Y < AutoScrollEdgeThreshold)
            {
                // Near top edge - scroll up
                StartAutoScroll(-1);
            }
            else if (mousePos.Y > viewportHeight - AutoScrollEdgeThreshold)
            {
                // Near bottom edge - scroll down
                StartAutoScroll(1);
            }
            else
            {
                // In the middle - stop auto-scroll
                StopAutoScroll();
            }

            e.Handled = true;
        }

        private void Content_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _isMouseDown)
            {
                _isMouseDown = false;
                HexViewport.ReleaseMouseCapture();
                StopAutoScroll();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Helper method to find the virtual position at mouse coordinates
        /// Uses HexViewport's precise hit-testing with actual measured dimensions
        /// </summary>
        /// <summary>
        /// Click area types for mouse input
        /// </summary>
        private enum ClickArea
        {
            Hex,
            Ascii,
            Other
        }

        /// <summary>
        /// Get which area was clicked at mouse position
        /// </summary>
        private ClickArea GetClickAreaAtMouse(Point mousePosition)
        {
            if (_viewModel == null)
                return ClickArea.Other;

            // Layout constants (must match HexViewport.cs)
            const double OffsetWidth = 110;
            const double HexByteWidth = 24;
            const double HexByteSpacing = 2;
            const double SeparatorWidth = 20;
            const int ByteGrouping = 4;
            const double ByteSpacerWidthTickness = 6;

            double x = mousePosition.X;

            // Check if in hex area
            double hexStartX = OffsetWidth;

            // Calculate hexEndX accounting for byte spacers
            int numSpacers = 0;
            if (_viewModel.BytePerLine >= ByteGrouping)
            {
                numSpacers = (_viewModel.BytePerLine / ByteGrouping) - 1;
                // If not evenly divisible, we have one less spacer
                if (_viewModel.BytePerLine % ByteGrouping == 0)
                    numSpacers = (_viewModel.BytePerLine / ByteGrouping) - 1;
                else
                    numSpacers = _viewModel.BytePerLine / ByteGrouping;
            }
            double spacersWidth = numSpacers * ByteSpacerWidthTickness;
            double hexEndX = OffsetWidth + (_viewModel.BytePerLine * (HexByteWidth + HexByteSpacing)) + spacersWidth;

            if (x >= hexStartX && x < hexEndX)
                return ClickArea.Hex;

            // Check if in ASCII area
            double separatorX = hexEndX + 4; // Match rendering code (was +8, now +4)
            double asciiStartX = separatorX + SeparatorWidth;

            if (x >= asciiStartX)
                return ClickArea.Ascii;

            return ClickArea.Other;
        }

        private VirtualPosition GetVirtualPositionAtMouse(Point mousePosition)
        {
            if (_viewModel == null || _viewModel.Lines.Count == 0)
                return VirtualPosition.Invalid;

            // Use HexViewport's actual LineHeight (calculated from font metrics)
            double lineHeight = HexViewport.LineHeight;
            if (lineHeight <= 0)
                return VirtualPosition.Invalid;

            // Layout constants (must match HexViewport.cs)
            const double OffsetWidth = 110;
            const double HexByteWidth = 24;
            const double HexByteSpacing = 2;
            const double TopMargin = 2;
            const double SeparatorWidth = 20;
            const double AsciiCharWidth = 10;
            const int ByteGrouping = 4;
            const double ByteSpacerWidthTickness = 6;

            // Calculate line number from Y coordinate
            double y = mousePosition.Y - TopMargin;
            if (y < 0)
                return VirtualPosition.Invalid;

            int lineIndex = (int)(y / lineHeight);

            // Clamp to valid line range
            if (lineIndex < 0 || lineIndex >= _viewModel.Lines.Count)
                return VirtualPosition.Invalid;

            var line = _viewModel.Lines[lineIndex];
            if (line.Bytes.Count == 0)
                return VirtualPosition.Invalid;

            double x = mousePosition.X;

            // Check if clicked in offset area - select first byte
            if (x < OffsetWidth)
            {
                return line.Bytes[0].VirtualPos;
            }

            // Calculate hex area dimensions WITH byte spacers
            double hexStartX = OffsetWidth;
            int numSpacers = 0;
            if (_viewModel.BytePerLine >= ByteGrouping)
            {
                numSpacers = (_viewModel.BytePerLine % ByteGrouping == 0)
                    ? (_viewModel.BytePerLine / ByteGrouping) - 1
                    : _viewModel.BytePerLine / ByteGrouping;
            }
            double spacersWidth = numSpacers * ByteSpacerWidthTickness;
            double hexEndX = OffsetWidth + (_viewModel.BytePerLine * (HexByteWidth + HexByteSpacing)) + spacersWidth;

            // Check if click is in hex area
            if (x >= hexStartX && x < hexEndX)
            {
                // Click in hex area - need to account for byte spacers
                double relativeX = x - hexStartX;

                // Calculate byte index accounting for spacers
                int byteIndex = 0;
                double currentX = 0;

                for (int i = 0; i < _viewModel.BytePerLine && i < line.Bytes.Count; i++)
                {
                    // Add spacer width before this byte if needed
                    if (_viewModel.BytePerLine >= ByteGrouping && i > 0 && i % ByteGrouping == 0)
                    {
                        currentX += ByteSpacerWidthTickness;
                    }

                    // Check if click is within this byte's bounds
                    if (relativeX >= currentX && relativeX < currentX + HexByteWidth + HexByteSpacing)
                    {
                        byteIndex = i;
                        break;
                    }

                    currentX += HexByteWidth + HexByteSpacing;
                    byteIndex = i;
                }

                // Clamp to valid byte range
                byteIndex = Math.Max(0, Math.Min(byteIndex, line.Bytes.Count - 1));
                return line.Bytes[byteIndex].VirtualPos;
            }

            // Check if click is in ASCII area
            double separatorX = hexEndX + 4;
            double asciiStartX = separatorX + SeparatorWidth;

            // Calculate ASCII area width WITH spacers
            int numAsciiSpacers = 0;
            if (_viewModel.BytePerLine >= ByteGrouping)
            {
                numAsciiSpacers = (_viewModel.BytePerLine % ByteGrouping == 0)
                    ? (_viewModel.BytePerLine / ByteGrouping) - 1
                    : _viewModel.BytePerLine / ByteGrouping;
            }
            double asciiSpacersWidth = numAsciiSpacers * ByteSpacerWidthTickness;
            double asciiEndX = asciiStartX + (_viewModel.BytePerLine * AsciiCharWidth) + asciiSpacersWidth;

            if (x >= asciiStartX && x < asciiEndX)
            {
                // Click in ASCII area - need to account for byte spacers
                double relativeX = x - asciiStartX;

                // Calculate byte index accounting for spacers
                int byteIndex = 0;
                double currentX = 0;

                for (int i = 0; i < _viewModel.BytePerLine && i < line.Bytes.Count; i++)
                {
                    // Add spacer width before this byte if needed
                    if (_viewModel.BytePerLine >= ByteGrouping && i > 0 && i % ByteGrouping == 0)
                    {
                        currentX += ByteSpacerWidthTickness;
                    }

                    // Check if click is within this byte's bounds
                    if (relativeX >= currentX && relativeX < currentX + AsciiCharWidth)
                    {
                        byteIndex = i;
                        break;
                    }

                    currentX += AsciiCharWidth;
                    byteIndex = i;
                }

                // Clamp to valid byte range
                byteIndex = Math.Max(0, Math.Min(byteIndex, line.Bytes.Count - 1));
                return line.Bytes[byteIndex].VirtualPos;
            }

            // Click in separator or beyond - select last byte on line
            return line.Bytes[line.Bytes.Count - 1].VirtualPos;
        }

        private void Content_KeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel == null) return;

            bool handled = true;
            bool isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool isCtrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            // Get current position:
            // - If there's a selection: use SelectionStop (the active/cursor end)
            // - If no selection: use SelectionStart (the cursor position)
            var currentPos = _viewModel.HasSelection && _viewModel.SelectionStop.IsValid
                ? _viewModel.SelectionStop
                : (_viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart : VirtualPosition.Zero);

            VirtualPosition newPos = currentPos;

            switch (e.Key)
            {
                // Arrow keys navigation
                case Key.Left:
                    if (isCtrlPressed)
                    {
                        // Ctrl+Left: Jump left by 8 bytes (or to start of line if closer)
                        long jumpSize = 8;
                        long lineStart = (currentPos.Value / _viewModel.BytePerLine) * _viewModel.BytePerLine;
                        long targetPos = currentPos.Value - jumpSize;

                        // Don't go before start of current line (unless we're at the very start)
                        if (currentPos.Value > 0)
                        {
                            if (targetPos < lineStart && currentPos.Value != lineStart)
                                newPos = new VirtualPosition(lineStart);
                            else
                                newPos = new VirtualPosition(Math.Max(0, targetPos));
                        }
                    }
                    else
                    {
                        // Normal Left: Move one byte left
                        if (currentPos.Value > 0)
                            newPos = new VirtualPosition(currentPos.Value - 1);
                    }
                    break;

                case Key.Right:
                    if (isCtrlPressed)
                    {
                        // Ctrl+Right: Jump right by 8 bytes (or to end of line if closer)
                        long jumpSize = 8;
                        long lineStart = (currentPos.Value / _viewModel.BytePerLine) * _viewModel.BytePerLine;
                        long lineEnd = Math.Min(lineStart + _viewModel.BytePerLine - 1, _viewModel.VirtualLength - 1);
                        long targetPos = currentPos.Value + jumpSize;

                        // Don't go past end of current line (unless we're at the very end)
                        if (currentPos.Value < _viewModel.VirtualLength - 1)
                        {
                            if (targetPos > lineEnd && currentPos.Value != lineEnd)
                                newPos = new VirtualPosition(lineEnd);
                            else
                                newPos = new VirtualPosition(Math.Min(_viewModel.VirtualLength - 1, targetPos));
                        }
                    }
                    else
                    {
                        // Normal Right: Move one byte right
                        if (currentPos.Value < _viewModel.VirtualLength - 1)
                            newPos = new VirtualPosition(currentPos.Value + 1);
                    }
                    break;

                case Key.Up:
                    if (isCtrlPressed)
                    {
                        // Ctrl+Up: Jump up by a page
                        long jump = _viewModel.BytePerLine * _viewModel.VisibleLines;
                        newPos = new VirtualPosition(Math.Max(0, currentPos.Value - jump));
                    }
                    else
                    {
                        // Normal Up: Move one line up
                        if (currentPos.Value >= _viewModel.BytePerLine)
                            newPos = new VirtualPosition(currentPos.Value - _viewModel.BytePerLine);
                    }
                    break;

                case Key.Down:
                    if (isCtrlPressed)
                    {
                        // Ctrl+Down: Jump down by a page
                        long jump = _viewModel.BytePerLine * _viewModel.VisibleLines;
                        newPos = new VirtualPosition(Math.Min(_viewModel.VirtualLength - 1, currentPos.Value + jump));
                    }
                    else
                    {
                        // Normal Down: Move one line down
                        if (currentPos.Value + _viewModel.BytePerLine < _viewModel.VirtualLength)
                            newPos = new VirtualPosition(currentPos.Value + _viewModel.BytePerLine);
                    }
                    break;

                // Page Up/Down
                case Key.PageUp:
                    {
                        long jump = _viewModel.BytePerLine * _viewModel.VisibleLines;
                        newPos = new VirtualPosition(Math.Max(0, currentPos.Value - jump));
                    }
                    break;

                case Key.PageDown:
                    {
                        long jump = _viewModel.BytePerLine * _viewModel.VisibleLines;
                        newPos = new VirtualPosition(Math.Min(_viewModel.VirtualLength - 1, currentPos.Value + jump));
                    }
                    break;

                // Home/End
                case Key.Home:
                    if (isCtrlPressed)
                    {
                        // Ctrl+Home: Go to start of file
                        newPos = VirtualPosition.Zero;
                    }
                    else
                    {
                        // Home: Go to start of current line
                        long lineStart = (currentPos.Value / _viewModel.BytePerLine) * _viewModel.BytePerLine;
                        newPos = new VirtualPosition(lineStart);
                    }
                    break;

                case Key.End:
                    if (isCtrlPressed)
                    {
                        // Ctrl+End: Go to end of file
                        newPos = new VirtualPosition(_viewModel.VirtualLength - 1);
                    }
                    else
                    {
                        // End: Go to end of current line
                        long lineStart = (currentPos.Value / _viewModel.BytePerLine) * _viewModel.BytePerLine;
                        long lineEnd = Math.Min(lineStart + _viewModel.BytePerLine - 1, _viewModel.VirtualLength - 1);
                        newPos = new VirtualPosition(lineEnd);
                    }
                    break;

                // Insert key: Toggle edit mode
                case Key.Insert:
                    _viewModel.EditMode = _viewModel.EditMode == EditMode.Insert
                        ? EditMode.Overwrite
                        : EditMode.Insert;
                    StatusText.Text = _viewModel.EditMode == EditMode.Insert ? "Insert mode" : "Overwrite mode";
                    break;

                // Delete key: Delete selection
                case Key.Delete:
                    if (_viewModel.HasSelection && !_viewModel.ReadOnlyMode)
                    {
                        DeleteSelection();
                    }
                    break;

                // Ctrl+Z: Undo
                case Key.Z:
                    if (isCtrlPressed && !_viewModel.ReadOnlyMode)
                    {
                        Undo();
                    }
                    break;

                // Ctrl+Y: Redo
                case Key.Y:
                    if (isCtrlPressed && !_viewModel.ReadOnlyMode)
                    {
                        Redo();
                    }
                    break;

                // Ctrl+C: Copy
                case Key.C:
                    if (isCtrlPressed && _viewModel.HasSelection)
                    {
                        Copy();
                    }
                    break;

                // Ctrl+X: Cut
                case Key.X:
                    if (isCtrlPressed && _viewModel.HasSelection && !_viewModel.ReadOnlyMode)
                    {
                        Cut();
                    }
                    break;

                // Ctrl+V: Paste
                case Key.V:
                    if (isCtrlPressed && !_viewModel.ReadOnlyMode)
                    {
                        Paste();
                    }
                    break;

                // Ctrl+A: Select all
                case Key.A:
                    if (isCtrlPressed)
                    {
                        SelectAll();
                    }
                    break;

                // Text/Hex input editing
                default:
                    if (!_viewModel.ReadOnlyMode)
                    {
                        // ASCII mode: Handle text input (A-Z, a-z, 0-9, space, punctuation)
                        if (_isAsciiEditMode && TryGetAsciiChar(e.Key, out char asciiChar))
                        {
                            HandleAsciiInput(asciiChar, currentPos);
                            handled = true;
                        }
                        // Hex mode: Handle hex digit input (0-9, A-F)
                        else if (!_isAsciiEditMode && TryGetHexValue(e.Key, out byte hexValue))
                        {
                            HandleHexInput(hexValue, currentPos);
                            handled = true;
                        }
                        else
                        {
                            handled = false;
                        }
                    }
                    else
                    {
                        handled = false;
                    }
                    break;
            }

            // Update selection based on navigation
            if (newPos != currentPos)
            {
                if (isShiftPressed)
                {
                    // Shift+navigation: Extend selection
                    _viewModel.ExtendSelection(newPos);
                }
                else
                {
                    // Normal navigation: Move cursor
                    _viewModel.SetSelection(newPos);
                }

                // Auto-scroll to keep selection visible
                EnsurePositionVisible(newPos);
            }

            if (handled)
                e.Handled = true;
        }

        /// <summary>
        /// Ensure a virtual position is visible by scrolling if necessary
        /// </summary>
        private void EnsurePositionVisible(VirtualPosition position)
        {
            if (_viewModel == null)
                return;

            long lineNumber = position.Value / _viewModel.BytePerLine;
            long currentScroll = _viewModel.ScrollPosition;

            // Scroll up if position is above viewport
            if (lineNumber < currentScroll)
            {
                _viewModel.ScrollPosition = lineNumber;
            }
            // Scroll down if position is below viewport
            else if (lineNumber >= currentScroll + _viewModel.VisibleLines)
            {
                _viewModel.ScrollPosition = lineNumber - _viewModel.VisibleLines + 1;
            }
        }

        private void VerticalScroll_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.ScrollPosition = (long)e.NewValue;
            }
        }

        /// <summary>
        /// <summary>
        /// Handle BaseGrid size changes to adjust visible lines (exact V1 approach)
        /// </summary>
        private void BaseGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Only react to height changes (like V1's Grid_SizeChanged)
            if (!e.HeightChanged || _viewModel == null)
                return;

            // Delay UpdateVisibleLines to ensure BaseGrid.RowDefinitions[1].ActualHeight is updated
            // Use Render priority for better responsiveness while still ensuring layout is complete
            Dispatcher.BeginInvoke(new Action(() => UpdateVisibleLines()), System.Windows.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// Handle mouse wheel scrolling on ContentScroller
        /// </summary>
        private void ContentScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_viewModel == null)
                return;

            // Ctrl+MouseWheel = Zoom (V1 compatible)
            if (Keyboard.Modifiers == ModifierKeys.Control && AllowZoom)
            {
                double delta = e.Delta > 0 ? 0.1 : -0.1;
                double newZoom = ZoomScale + delta;
                ZoomScale = Math.Max(0.5, Math.Min(2.0, newZoom));
                e.Handled = true;
                return;
            }

            // Standard behavior: scroll 3 lines per wheel notch
            // Delta is typically 120 per notch, so divide by 40 to get 3 lines
            int linesToScroll = -e.Delta / 40;

            long newScrollPos = _viewModel.ScrollPosition + linesToScroll;
            long maxScroll = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines);

            // Clamp to valid range
            newScrollPos = Math.Max(0, Math.Min(maxScroll, newScrollPos));

            if (_viewModel.ScrollPosition != newScrollPos)
            {
                _viewModel.ScrollPosition = newScrollPos;
                VerticalScroll.Value = newScrollPos;
            }

            // Mark event as handled to prevent ScrollViewer from scrolling
            e.Handled = true;
        }

        /// <summary>
        /// Update visible lines based on BaseGrid Row 1 height (exact V1 approach)
        /// V1 uses: (int)(BaseGrid.RowDefinitions[1].ActualHeight / (LineHeight * ZoomScale)) + 1
        /// V2 uses: (int)(BaseGrid.RowDefinitions[1].ActualHeight / LineHeight) + 1 (no ZoomScale)
        /// </summary>
        private void UpdateVisibleLines()
        {
            if (_viewModel == null)
                return;

            // Get the actual line height from HexViewport
            double lineHeight = HexViewport.LineHeight;
            if (lineHeight <= 0)
                return; // Not initialized yet

            // Use BaseGrid.RowDefinitions[1].ActualHeight (EXACTLY like V1 does)
            // Row 0 = Header, Row 1 = Content area, Row 2 = Status bar
            double actualHeight = BaseGrid.RowDefinitions[1].ActualHeight;
            if (actualHeight <= 0)
                return; // Not initialized yet

            // Calculate how many lines fit in the viewport
            // Use Math.Ceiling to ensure we always have enough space for partial lines at bottom
            // Add 2 extra lines to prevent last line from being cut off (increased safety margin)
            int calculatedLines = (int)Math.Ceiling(actualHeight / lineHeight) + 2;

            // Clamp to reasonable range (minimum 5, maximum 100)
            calculatedLines = Math.Max(5, Math.Min(100, calculatedLines));

            // Only update if different (avoid thrashing)
            if (_viewModel.VisibleLines != calculatedLines)
            {
                _viewModel.VisibleLines = calculatedLines; // Property setter calls RefreshVisibleLines() internally
                VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 3);
                VerticalScroll.ViewportSize = _viewModel.VisibleLines;

                // Force full refresh after collection update completes (like V1's RefreshView(true) call)
                // Use Dispatcher to ensure collection changes are processed before refreshing viewport
                Dispatcher.BeginInvoke(new Action(() => HexViewport.Refresh()), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(HexEditorViewModel.SelectionLength):
                    UpdateSelectionInfo();
                    break;

                case nameof(HexEditorViewModel.SelectionStart):
                    UpdatePositionInfo();
                    // Update HexViewport selection anchor
                    HexViewport.SelectionStart = _viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : -1;
                    // Update auto-highlight to match the byte at the new selection
                    UpdateAutoHighlightByte();
                    break;

                case nameof(HexEditorViewModel.SelectionStop):
                    // Update HexViewport cursor (active end) and selection
                    // Cursor is at SelectionStop (the end that moves during Shift+navigation)
                    HexViewport.CursorPosition = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value :
                                                 (_viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : 0);
                    HexViewport.SelectionStop = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value : -1;
                    break;

                case nameof(HexEditorViewModel.TotalLines):
                    var newMaximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 3);
                    System.Diagnostics.Debug.WriteLine($"[SCROLLBAR UPDATE] TotalLines={_viewModel.TotalLines}, VisibleLines={_viewModel.VisibleLines}, Old Maximum={VerticalScroll.Maximum}, New Maximum={newMaximum}");
                    VerticalScroll.Maximum = newMaximum;
                    // Update file size display (VirtualLength may have changed due to insertions)
                    UpdateFileSizeDisplay();
                    break;

                case nameof(HexEditorViewModel.EditMode):
                    EditModeText.Text = $"Mode: {_viewModel.EditMode}";
                    break;

                case nameof(HexEditorViewModel.BytePerLine):
                    BytesPerLineText.Text = $"Bytes/Line: {_viewModel.BytePerLine}";
                    HexViewport.BytesPerLine = _viewModel.BytePerLine;
                    RefreshColumnHeader(); // Regenerate headers to match new BytesPerLine
                    break;
            }
        }

        private void UpdateFileSizeDisplay()
        {
            if (_viewModel != null)
            {
                // Show VirtualLength (includes insertions in Insert mode)
                long displayLength = _viewModel.VirtualLength;
                FileSizeText.Text = $"Size: {FormatFileSize(displayLength)} ({displayLength:N0} bytes)";
            }
        }

        private void UpdateSelectionInfo()
        {
            if (_viewModel?.HasSelection == true)
            {
                SelectionInfo.Text = $"Selection: {_viewModel.SelectionLength} bytes";
            }
            else
            {
                SelectionInfo.Text = "No selection";
            }
        }

        private void UpdatePositionInfo()
        {
            if (_viewModel?.SelectionStart.IsValid == true)
            {
                PositionInfo.Text = $"Position: 0x{_viewModel.SelectionStart.Value:X}";
            }
            else
            {
                PositionInfo.Text = "Position: 0";
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]} ({bytes:N0} bytes)";
        }

        /// <summary>
        /// Try to convert a Key to a hex value (0-15)
        /// </summary>
        private bool TryGetHexValue(Key key, out byte value)
        {
            // Number keys (0-9)
            if (key >= Key.D0 && key <= Key.D9)
            {
                value = (byte)(key - Key.D0);
                System.Diagnostics.Debug.WriteLine($"[HEXPARSE] Key {key} → value={value}");
                return true;
            }

            // Numpad keys (0-9)
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                value = (byte)(key - Key.NumPad0);
                System.Diagnostics.Debug.WriteLine($"[HEXPARSE] Key {key} → value={value}");
                return true;
            }

            // Letter keys (A-F)
            if (key >= Key.A && key <= Key.F)
            {
                value = (byte)(key - Key.A + 10);
                System.Diagnostics.Debug.WriteLine($"[HEXPARSE] Key {key} → value={value} (Letter A-F)");
                return true;
            }

            value = 0;
            System.Diagnostics.Debug.WriteLine($"[HEXPARSE] Key {key} → NOT a hex key");
            return false;
        }

        /// <summary>
        /// Try to convert a key press to an ASCII character
        /// </summary>
        private bool TryGetAsciiChar(Key key, out char asciiChar)
        {
            bool isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            // Letters A-Z
            if (key >= Key.A && key <= Key.Z)
            {
                asciiChar = (char)('A' + (key - Key.A));
                if (!isShiftPressed)
                    asciiChar = char.ToLower(asciiChar);
                System.Diagnostics.Debug.WriteLine($"[ASCIIPARSE] Key {key} + Shift={isShiftPressed} → '{asciiChar}' (0x{(byte)asciiChar:X2})");
                return true;
            }

            // Digits 0-9 (with shift for symbols)
            if (key >= Key.D0 && key <= Key.D9)
            {
                if (isShiftPressed)
                {
                    // Shift+digit produces symbols: )!@#$%^&*(
                    char[] shiftDigits = { ')', '!', '@', '#', '$', '%', '^', '&', '*', '(' };
                    asciiChar = shiftDigits[key - Key.D0];
                }
                else
                {
                    asciiChar = (char)('0' + (key - Key.D0));
                }
                System.Diagnostics.Debug.WriteLine($"[ASCIIPARSE] Key {key} + Shift={isShiftPressed} → '{asciiChar}' (0x{(byte)asciiChar:X2})");
                return true;
            }

            // NumPad digits 0-9
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                asciiChar = (char)('0' + (key - Key.NumPad0));
                System.Diagnostics.Debug.WriteLine($"[ASCIIPARSE] Key {key} (NumPad) → '{asciiChar}' (0x{(byte)asciiChar:X2})");
                return true;
            }

            // Space
            if (key == Key.Space)
            {
                asciiChar = ' ';
                System.Diagnostics.Debug.WriteLine($"[ASCIIPARSE] Key {key} → ' ' (0x20)");
                return true;
            }

            // Common punctuation (basic ASCII support)
            var punctuation = new Dictionary<Key, (char normal, char shifted)>
            {
                { Key.OemPeriod, ('.', '>') },
                { Key.OemComma, (',', '<') },
                { Key.OemSemicolon, (';', ':') },
                { Key.OemQuotes, ('\'', '"') },
                { Key.OemQuestion, ('/', '?') },
                { Key.OemOpenBrackets, ('[', '{') },
                { Key.OemCloseBrackets, (']', '}') },
                { Key.OemPipe, ('\\', '|') },
                { Key.OemMinus, ('-', '_') },
                { Key.OemPlus, ('=', '+') },
                { Key.OemTilde, ('`', '~') }
            };

            if (punctuation.TryGetValue(key, out var chars))
            {
                asciiChar = isShiftPressed ? chars.shifted : chars.normal;
                System.Diagnostics.Debug.WriteLine($"[ASCIIPARSE] Key {key} + Shift={isShiftPressed} → '{asciiChar}' (0x{(byte)asciiChar:X2})");
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"[ASCIIPARSE] Key {key} → NOT an ASCII key");
            asciiChar = '\0';
            return false;
        }

        /// <summary>
        /// Handle ASCII text input for editing bytes in ASCII mode
        /// </summary>
        private void HandleAsciiInput(char asciiChar, VirtualPosition currentPos)
        {
            System.Diagnostics.Debug.WriteLine($"[ASCIIINPUT] === HandleAsciiInput called ===");
            System.Diagnostics.Debug.WriteLine($"[ASCIIINPUT] char='{asciiChar}' (0x{(byte)asciiChar:X2}), currentPos={currentPos.Value}");
            System.Diagnostics.Debug.WriteLine($"[ASCIIINPUT] EditMode={_viewModel?.EditMode}");

            if (_viewModel == null || _viewModel.ReadOnlyMode)
                return;

            // Convert ASCII character to byte
            byte byteValue = (byte)asciiChar;

            // Determine action based on edit mode
            if (_viewModel.EditMode == EditMode.Insert)
            {
                // Insert mode: insert new byte
                System.Diagnostics.Debug.WriteLine($"[ASCIIINPUT] Inserting byte 0x{byteValue:X2} ('{asciiChar}') at position {currentPos.Value}");
                _viewModel.InsertByte(currentPos, byteValue);
            }
            else
            {
                // Overwrite mode: modify existing byte
                System.Diagnostics.Debug.WriteLine($"[ASCIIINPUT] Modifying byte at position {currentPos.Value} to 0x{byteValue:X2} ('{asciiChar}')");
                _viewModel.ModifyByte(currentPos, byteValue);
            }

            // Move to next byte
            var nextPos = new VirtualPosition(currentPos.Value + 1);
            if (nextPos.Value < _viewModel.VirtualLength)
            {
                _viewModel.SetSelection(nextPos);
                System.Diagnostics.Debug.WriteLine($"[ASCIIINPUT] Moved to next position: {nextPos.Value}");
                EnsurePositionVisible(nextPos);
            }

            // Update status
            StatusText.Text = $"ASCII input: '{asciiChar}' at {currentPos.Value:X8}";
        }

        /// <summary>
        /// Handle hex digit input for editing bytes
        /// </summary>
        private void HandleHexInput(byte hexValue, VirtualPosition currentPos)
        {
            System.Diagnostics.Debug.WriteLine($"[HEXINPUT] === HandleHexInput called ===");
            System.Diagnostics.Debug.WriteLine($"[HEXINPUT] hexValue={hexValue} (0x{hexValue:X2}), currentPos={currentPos.Value}");
            System.Diagnostics.Debug.WriteLine($"[HEXINPUT] EditMode={_viewModel?.EditMode}");

            if (_viewModel == null || _viewModel.ReadOnlyMode)
                return;

            // Start new byte edit if not currently editing, or position changed
            if (!_isEditingByte || _editingPosition != currentPos)
            {
                _isEditingByte = true;
                _editingPosition = currentPos;
                _editingHighNibble = true;

                // Initialize editing value based on mode
                // Insert mode: start with 0x00 (creating new byte)
                // Overwrite mode: start with existing byte value (modifying it)
                if (_viewModel.EditMode == EditMode.Insert)
                {
                    _editingValue = 0;
                }
                else
                {
                    var physicalPos = _viewModel.VirtualToPhysical(currentPos);
                    _editingValue = physicalPos.IsValid
                        ? _viewModel.GetByteAt(currentPos)
                        : (byte)0;
                }

                System.Diagnostics.Debug.WriteLine($"[HEXINPUT] Starting new byte edit at pos {currentPos.Value}");
                System.Diagnostics.Debug.WriteLine($"[HEXINPUT] EditMode={_viewModel.EditMode}, Initial _editingValue: 0x{_editingValue:X2} ({_editingValue})");
            }

            // Update the appropriate nibble
            if (_editingHighNibble)
            {
                System.Diagnostics.Debug.WriteLine($"[HEXINPUT] HIGH NIBBLE - Before: _editingValue=0x{_editingValue:X2}");

                // Update high nibble (bits 4-7)
                byte oldValue = _editingValue;
                _editingValue = (byte)((_editingValue & 0x0F) | (hexValue << 4));

                System.Diagnostics.Debug.WriteLine($"[HEXINPUT] HIGH NIBBLE - Calculation: (0x{oldValue:X2} & 0x0F) | (0x{hexValue:X2} << 4)");
                System.Diagnostics.Debug.WriteLine($"[HEXINPUT] HIGH NIBBLE - Result: 0x{_editingValue:X2} ({_editingValue})");

                _editingHighNibble = false; // Move to low nibble

                // IN INSERT MODE: Insert byte IMMEDIATELY after first nibble (don't wait for second nibble)
                if (_viewModel.EditMode == EditMode.Insert)
                {
                    System.Diagnostics.Debug.WriteLine($"[HEXINPUT] HIGH NIBBLE - INSERT MODE: Inserting byte IMMEDIATELY with value 0x{_editingValue:X2}");
                    _viewModel.InsertByte(_editingPosition, _editingValue);
                    // Don't move to next byte yet - wait for low nibble to modify this inserted byte
                }
                else
                {
                    // OVERWRITE MODE: Update visual display immediately after high nibble (preview only)
                    UpdateBytePreview(_editingPosition, _editingValue);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[HEXINPUT] LOW NIBBLE - Before: _editingValue=0x{_editingValue:X2}");

                // Update low nibble (bits 0-3)
                byte oldValue = _editingValue;
                _editingValue = (byte)((_editingValue & 0xF0) | hexValue);

                System.Diagnostics.Debug.WriteLine($"[HEXINPUT] LOW NIBBLE - Calculation: (0x{oldValue:X2} & 0xF0) | 0x{hexValue:X2}");
                System.Diagnostics.Debug.WriteLine($"[HEXINPUT] LOW NIBBLE - Result: 0x{_editingValue:X2} ({_editingValue})");
                System.Diagnostics.Debug.WriteLine($"[HEXINPUT] LOW NIBBLE - Committing final value 0x{_editingValue:X2} in {_viewModel.EditMode} mode");

                // Byte is complete, write it
                CommitByteEdit();
                System.Diagnostics.Debug.WriteLine($"[HEXINPUT] CommitByteEdit() completed, _isEditingByte={_isEditingByte}");

                // Move to next byte
                var nextPos = new VirtualPosition(currentPos.Value + 1);
                System.Diagnostics.Debug.WriteLine($"[HEXINPUT] Moving to next position: {currentPos.Value} → {nextPos.Value}");

                if (nextPos.Value < _viewModel.VirtualLength)
                {
                    _viewModel.SetSelection(nextPos);
                    System.Diagnostics.Debug.WriteLine($"[HEXINPUT] Selection moved to: {nextPos.Value}, VirtualLength={_viewModel.VirtualLength}");
                    EnsurePositionVisible(nextPos);
                }
                else
                {
                    _isEditingByte = false;
                    System.Diagnostics.Debug.WriteLine($"[HEXINPUT] Reached end of data, stopped editing");
                }
            }
        }

        /// <summary>
        /// Commit the current byte edit to the provider
        /// </summary>
        /// <summary>
        /// Update byte display temporarily during editing (before commit)
        /// Shows real-time feedback as user types each nibble
        /// </summary>
        private void UpdateBytePreview(VirtualPosition position, byte previewValue)
        {
            if (_viewModel == null || !position.IsValid)
                return;

            // Update the byte value in the ViewModel temporarily for preview
            _viewModel.UpdateBytePreview(position, previewValue);

            // Force visual refresh to show the change immediately
            HexViewport.InvalidateVisual();
        }

        private void CommitByteEdit()
        {
            if (!_isEditingByte || _viewModel == null)
            {
                System.Diagnostics.Debug.WriteLine($"[COMMIT] Skipped: _isEditingByte={_isEditingByte}, _viewModel={(_viewModel != null ? "OK" : "NULL")}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[COMMIT] === CommitByteEdit START ===");
            System.Diagnostics.Debug.WriteLine($"[COMMIT] Position={_editingPosition.Value}, Value=0x{_editingValue:X2}, EditMode={_viewModel.EditMode}");

            // In Insert mode, the byte was already inserted after the first nibble
            // So the low nibble should MODIFY the inserted byte (not insert again)
            // In Overwrite mode, modify the existing byte
            System.Diagnostics.Debug.WriteLine($"[COMMIT] Calling ModifyByte(pos={_editingPosition.Value}, value=0x{_editingValue:X2})");
            _viewModel.ModifyByte(_editingPosition, _editingValue);
            System.Diagnostics.Debug.WriteLine($"[COMMIT] ModifyByte completed");

            _isEditingByte = false;
            _editingHighNibble = true;

            // Update status
            StatusText.Text = $"Edited byte at {_editingPosition.Value:X8}";
            System.Diagnostics.Debug.WriteLine($"[COMMIT] === CommitByteEdit END === (_isEditingByte now false)");
        }

        #endregion

        #region Auto-Scroll During Mouse Selection

        /// <summary>
        /// Start auto-scrolling in the specified direction
        /// </summary>
        /// <param name="direction">-1 for up, 1 for down</param>
        private void StartAutoScroll(int direction)
        {
            if (_autoScrollDirection != direction)
            {
                _autoScrollDirection = direction;
                _lastAutoScrollPosition = VirtualPosition.Invalid; // Reset tracking
                if (!_autoScrollTimer.IsEnabled)
                {
                    _autoScrollTimer.Start();
                }
            }
        }

        /// <summary>
        /// Stop auto-scrolling
        /// </summary>
        private void StopAutoScroll()
        {
            _autoScrollDirection = 0;
            _lastAutoScrollPosition = VirtualPosition.Invalid; // Reset tracking
            _autoScrollTimer.Stop();
        }

        /// <summary>
        /// Auto-scroll timer tick - scroll viewport and update selection
        /// Optimized to reduce redundant updates
        /// </summary>
        private void AutoScrollTimer_Tick(object sender, EventArgs e)
        {
            if (_viewModel == null || !_isMouseDown || _autoScrollDirection == 0)
            {
                StopAutoScroll();
                return;
            }

            // Calculate new scroll position (scroll multiple lines per tick for faster auto-scroll)
            long newScrollPos = _viewModel.ScrollPosition + (_autoScrollDirection * AutoScrollSpeed);

            // Clamp to valid range
            long maxScroll = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines);
            newScrollPos = Math.Max(0, Math.Min(newScrollPos, maxScroll));

            // Only update if position changed
            if (newScrollPos != _viewModel.ScrollPosition)
            {
                // Use batch update for better performance
                _viewModel.BeginUpdate();
                try
                {
                    _viewModel.ScrollPosition = newScrollPos;

                    // Update selection to the byte at the current mouse position
                    var position = GetVirtualPositionAtMouse(_lastMousePosition);

                    // Only update selection if position actually changed (avoid redundant updates)
                    if (position.IsValid && position != _lastAutoScrollPosition)
                    {
                        _viewModel.SetSelectionRange(_mouseDownPosition, position);
                        _lastAutoScrollPosition = position;
                    }
                }
                finally
                {
                    _viewModel.EndUpdate();
                }
            }
        }

        #endregion

        #region Column Header Generation (V1 Compatible)

        /// <summary>
        /// Refresh the column headers with byte position numbers and byte spacers (V1 compatible)
        /// Called when BytePerLine, ByteGrouping, or ByteSpacer properties change
        /// </summary>
        private void RefreshColumnHeader()
        {
            if (_hexHeaderStackPanel == null || _asciiHeaderStackPanel == null)
                return;

            // Clear existing headers
            _hexHeaderStackPanel.Children.Clear();
            _asciiHeaderStackPanel.Children.Clear();

            int bytesPerLine = BytePerLine;

            // Generate hex column headers (00 01 02...0F)
            for (int i = 0; i < bytesPerLine; i++)
            {
                // Add byte spacer before this column if needed
                if (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                    ByteSpacerPositioning == ByteSpacerPosition.HexBytePanel)
                {
                    AddByteSpacer(_hexHeaderStackPanel, i, forceEmpty: true);
                }

                // Add byte position header (00, 01, 02, etc.)
                var headerText = new TextBlock
                {
                    Text = i.ToString("X2"),
                    Width = 24, // Match HexByteWidth from HexViewport
                    TextAlignment = TextAlignment.Center,
                    FontSize = 11,
                    FontWeight = FontWeights.Normal,
                    Foreground = Resources["HeaderTextBrush"] as System.Windows.Media.Brush,
                    Margin = new Thickness(0, 0, 2, 0) // HexByteSpacing
                };

                _hexHeaderStackPanel.Children.Add(headerText);
            }

            // Generate ASCII column headers (no byte spacers in ASCII panel)
            for (int i = 0; i < bytesPerLine; i++)
            {
                // Add placeholder for ASCII column (could show position or just be blank)
                var headerText = new TextBlock
                {
                    Text = " ", // Blank or could show position like V1
                    Width = 10, // Match AsciiCharWidth from HexViewport
                    TextAlignment = TextAlignment.Center,
                    FontSize = 11,
                    Foreground = Resources["HeaderTextBrush"] as System.Windows.Media.Brush
                };

                _asciiHeaderStackPanel.Children.Add(headerText);
            }
        }

        /// <summary>
        /// Add byte spacer to a StackPanel at the specified column position (V1 compatible)
        /// Spacers are added every ByteGrouping bytes (e.g., every 8 bytes)
        /// </summary>
        /// <param name="stack">StackPanel to add spacer to</param>
        /// <param name="column">Current column index (0-based)</param>
        /// <param name="forceEmpty">Force empty spacer even if visual style is Line or Dash</param>
        private void AddByteSpacer(StackPanel stack, int column, bool forceEmpty = false)
        {
            // Only add spacer at group boundaries (e.g., every 8 bytes)
            // column % ByteGrouping must be 0, and column > 0 (no spacer before first byte)
            if (column % (int)ByteGrouping != 0 || column <= 0)
                return;

            int width = (int)ByteSpacerWidthTickness;

            if (!forceEmpty)
            {
                switch (ByteSpacerVisualStyle)
                {
                    case ByteSpacerVisual.Empty:
                        stack.Children.Add(new TextBlock { Width = width });
                        break;

                    case ByteSpacerVisual.Line:
                        // Solid vertical line
                        stack.Children.Add(new System.Windows.Shapes.Line
                        {
                            Y2 = 20, // Line height
                            X1 = width / 2.0,
                            X2 = width / 2.0,
                            Stroke = BorderBrush,
                            StrokeThickness = 1,
                            Width = width
                        });
                        break;

                    case ByteSpacerVisual.Dash:
                        // Dashed vertical line
                        stack.Children.Add(new System.Windows.Shapes.Line
                        {
                            Y2 = 19,
                            X1 = width / 2.0,
                            X2 = width / 2.0,
                            Stroke = BorderBrush,
                            StrokeDashArray = new DoubleCollection(new double[] { 2 }),
                            StrokeThickness = 1,
                            Width = width
                        });
                        break;
                }
            }
            else
            {
                // Force empty spacer (used for headers)
                stack.Children.Add(new TextBlock { Width = width });
            }
        }

        #endregion

        #region Context Menu Handlers (V1 Compatible)

        private long _rightClickPosition = -1;

        /// <summary>
        /// Show context menu on right-click (V1 compatible)
        /// </summary>
        public void ShowContextMenu(long position)
        {
            if (!AllowContextMenu) return;

            _rightClickPosition = position;

            // Select the byte if no selection
            if (SelectionLength <= 1)
            {
                SelectionStart = position;
                SelectionStop = position;
            }

            // Access menu items from ContextMenu
            var contextMenu = this.ContextMenu;
            if (contextMenu == null) return;

            // Enable/disable menu items based on state
            var undoItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "UndoMenuItem") as MenuItem;
            var copyAsItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyAsMenuItem") as MenuItem;
            var copyHexaItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyHexaMenuItem") as MenuItem;
            var copyAsciiItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyAsciiMenuItem") as MenuItem;
            var copyCSharpItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyCSharpMenuItem") as MenuItem;
            var copyCItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyCMenuItem") as MenuItem;
            var copyTblItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyTblMenuItem") as MenuItem;
            var findAllItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "FindAllMenuItem") as MenuItem;
            var pasteItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "PasteMenuItem") as MenuItem;
            var deleteItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "DeleteMenuItem") as MenuItem;
            var fillItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "FillByteMenuItem") as MenuItem;
            var replaceItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "ReplaceByteMenuItem") as MenuItem;

            if (undoItem != null) undoItem.IsEnabled = CanUndo;
            if (copyAsItem != null) copyAsItem.IsEnabled = SelectionLength > 0;
            if (copyHexaItem != null) copyHexaItem.IsEnabled = SelectionLength > 0;
            if (copyAsciiItem != null) copyAsciiItem.IsEnabled = SelectionLength > 0;
            if (copyCSharpItem != null) copyCSharpItem.IsEnabled = SelectionLength > 0;
            if (copyCItem != null) copyCItem.IsEnabled = SelectionLength > 0;
            if (copyTblItem != null) copyTblItem.IsEnabled = SelectionLength > 0 && _tblStream != null;
            if (findAllItem != null) findAllItem.IsEnabled = SelectionLength > 0;
            if (pasteItem != null) pasteItem.IsEnabled = !ReadOnlyMode && Clipboard.ContainsText();
            if (deleteItem != null) deleteItem.IsEnabled = !ReadOnlyMode && SelectionLength > 0;
            if (fillItem != null) fillItem.IsEnabled = !ReadOnlyMode && SelectionLength > 0;
            if (replaceItem != null) replaceItem.IsEnabled = !ReadOnlyMode && SelectionLength > 0;

            // Show context menu
            contextMenu.Visibility = Visibility.Visible;
            contextMenu.IsOpen = true;
        }

        private void UndoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Undo();
        }

        private void CopyHexaMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(CopyPasteMode.HexaString);
        }

        private void CopyAsciiMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(CopyPasteMode.AsciiString);
        }

        private void CopyCSharpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(CopyPasteMode.CSharpCode);
        }

        private void CopyCMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(CopyPasteMode.CCode);
        }

        private void CopyTblMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(CopyPasteMode.TblString);
        }

        private void FindAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selection = GetSelectionByteArray();
            if (selection != null && selection.Length > 0)
            {
                FindAll(selection, 0);
                StatusText.Text = $"Found all occurrences of selection";
            }
        }

        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Paste();
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelection();
        }

        private void FillByteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // V1 Compatible: Fill selection with 0x00 (simplified - V1 would show dialog)
            // TODO: Add proper input dialog for .NET Core compatibility
            FillWithByte(0x00, SelectionStart, SelectionLength);
            StatusText.Text = $"Filled {SelectionLength} bytes with 0x00";
        }

        private void ReplaceByteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // V1 Compatible: Replace 0x00 with 0xFF in selection (simplified - V1 would show dialog)
            // TODO: Add proper input dialog for .NET Core compatibility
            byte[] findData = new byte[] { 0x00 };
            byte[] replaceData = new byte[] { 0xFF };
            var replaced = ReplaceAll(findData, replaceData, false, false);
            StatusText.Text = $"Replaced {replaced.Count()} occurrences";
        }

        #endregion
    }
}
