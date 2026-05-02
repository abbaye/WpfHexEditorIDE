//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
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
using WpfHexEditor.HexEditor.Controls;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Models;
using WpfHexEditor.HexEditor.ViewModels;
using WpfHexEditor.Core.Services;
using WpfHexEditor.Core.Properties;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor - Modern WPF hex editor with native insert mode support (V2 Architecture)
    /// Clean UserControl without UI chrome (toolbar, menus, etc.)
    /// Host application provides UI and calls public methods/properties
    ///
    /// NOTE: This is the modern V2 implementation - 99% faster with critical bug fixes.
    /// Legacy V1 control (HexEditorLegacy) was removed in v2.6+ (Feb 2026).
    /// </summary>
    public partial class HexEditor : UserControl, IDocumentEditor
    {
        private HexEditorViewModel _viewModel;
        private bool _isMouseDown = false;
        private VirtualPosition _mouseDownPosition = VirtualPosition.Invalid;
        private bool _isOffsetLineDrag = false;
        private VirtualPosition _offsetDragAnchorStart = VirtualPosition.Invalid;
        private VirtualPosition _offsetDragAnchorEnd = VirtualPosition.Invalid;
        private Border _headerBorder;
        private System.Windows.Controls.Primitives.StatusBar _statusBar;
        private StackPanel _hexHeaderStackPanel;
        private StackPanel _asciiHeaderStackPanel;
        private Controls.ScrollMarkerPanel _scrollMarkers;
        private System.Windows.Media.TranslateTransform _headerScrollTransform;

        // Bookmarks
        private readonly List<long> _bookmarks = new List<long>();
        private readonly BookmarkService _bookmarkService = new(); // V2 bookmark service

        // Long-running operations
        private readonly LongRunningOperationService _longRunningService = new();

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
        private string _editingBuffer = "";     // Accumulated input buffer ("2" â†’ "25" â†’ "255")
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

            // Seed scrollbar brushes from the DP defaults so DynamicResource in the template
            // has a value even when no App theme is loaded (standalone / designer mode).
            // ApplyThemeFromResources() will overwrite these with the active theme colors later.
            Resources["ScrollBarBrush"]          = new System.Windows.Media.SolidColorBrush(ScrollBarBackgroundColor);
            Resources["ScrollBarThumbBrush"]     = new System.Windows.Media.SolidColorBrush(ScrollBarThumbColor);
            Resources["ScrollBarThumbHoverBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA8, 0xA8, 0xA8));
            Resources["ScrollBarThumbDragBrush"]  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC8, 0xC8, 0xC8));

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
            // Use BaseGrid.RowDefinitions[2].ActualHeight â€” Row 2 is content (BreadcrumbBar added as Row 0)
            BaseGrid.SizeChanged += BaseGrid_SizeChanged;

            // Handle mouse wheel scrolling (use PreviewMouseWheel on ScrollViewer to intercept before it scrolls)
            ContentScroller.PreviewMouseWheel += ContentScroller_PreviewMouseWheel;

            // Sync header horizontal position with content horizontal scroll
            ContentScroller.ScrollChanged += ContentScroller_ScrollChanged;

            // Initialize breadcrumb bar (lazy via EnsureBreadcrumbBar) and column highlight overlay
            EnsureBreadcrumbBar();
            InitializeColumnHighlight();

            // Find XAML elements for display options
            _headerBorder = this.FindName("HeaderBorder") as Border;
            _statusBar = this.FindName("StatusBar") as System.Windows.Controls.Primitives.StatusBar;
            _hexHeaderStackPanel = this.FindName("HexHeaderStackPanel") as StackPanel;
            _asciiHeaderStackPanel = this.FindName("AsciiHeaderStackPanel") as StackPanel;
            _scrollMarkers = this.FindName("ScrollMarkers") as Controls.ScrollMarkerPanel;
            _headerScrollTransform = this.FindName("HeaderScrollTransform") as System.Windows.Media.TranslateTransform;

            // Subscribe to scroll marker click event for navigation
            // Disabled: clicking on markers complicates navigation
            //if (_scrollMarkers != null)
            //{
            //    _scrollMarkers.MarkerClicked += ScrollMarkers_MarkerClicked;
            //}

            // Initialize column headers with byte position numbers
            // Apply theme colors from application resources (if HexEditor_* keys are defined)
            this.Loaded += (s, e) =>
            {
                RefreshColumnHeader();
                ApplyThemeFromResources();

                // Explicitly apply the App-level implicit ScrollBar style to VerticalScroll.
                // WPF implicit style lookup walks the logical tree, but UserControl elements
                // declared in XAML sometimes resolve before the control is fully integrated
                // into the App's logical tree. Assigning explicitly here guarantees the
                // themed style (from Docking/ContentControls.xaml) is applied after Loaded.
                if (TryFindResource(typeof(System.Windows.Controls.Primitives.ScrollBar)) is Style sbStyle)
                    VerticalScroll.Style = sbStyle;

                // Wire window-level mouse handlers so auto-scroll continues when the mouse
                // leaves the HexViewport boundary (e.g. into docking tabs or the title bar).
                // Mouse.AddPreviewMouseMoveHandler uses tunneling — it fires even when another
                // element has mouse capture, which is more reliable than relying solely on
                // HexViewport.CaptureMouse() across docking/WindowChrome containers.
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    Mouse.AddPreviewMouseMoveHandler(window, OnWindowPreviewMouseMove);
                    Mouse.AddPreviewMouseUpHandler(window, OnWindowPreviewMouseUp);
                }
            };

            this.Unloaded += (s, e) =>
            {
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    Mouse.RemovePreviewMouseMoveHandler(window, OnWindowPreviewMouseMove);
                    Mouse.RemovePreviewMouseUpHandler(window, OnWindowPreviewMouseUp);
                }
            };

            // Subscribe to right-click event for context menu
            if (HexViewport != null)
            {
                HexViewport.ByteRightClick += HexViewport_ByteRightClick;
                HexViewport.ByteDoubleClicked += HexViewport_ByteDoubleClicked;

                // CRITICAL: Sync TBL colors from DP defaults to HexViewport on initialization
                // Without this, the DP default values never propagate because OnTblColorChanged
                // only fires when values CHANGE, not during initialization
                HexViewport.TblDteColor = TblDteColor;
                HexViewport.TblMteColor = TblMteColor;
                HexViewport.TblAsciiColor = TblAsciiColor;
                HexViewport.TblJaponaisColor = TblJaponaisColor;
                HexViewport.TblEndBlockColor = TblEndBlockColor;
                HexViewport.TblEndLineColor = TblEndLineColor;
                HexViewport.TblDefaultColor = TblDefaultColor;
                HexViewport.Tbl3ByteColor = Tbl3ByteColor;
                HexViewport.Tbl4PlusByteColor = Tbl4PlusByteColor;
                HexViewport.ByteDragSelection += HexViewport_ByteDragSelection;
                HexViewport.OffsetLineClicked += HexViewport_OffsetLineSelection;
                HexViewport.OffsetLineDragSelection += HexViewport_OffsetLineSelection;
                HexViewport.KeyboardNavigation += HexViewport_KeyboardNavigation;
                HexViewport.RefreshTimeUpdated += HexViewport_RefreshTimeUpdated;
                HexViewport.PanVerticalScrollRequested += OnHexViewportPanScroll;

                // Initialize selection brushes
                HexViewport.SelectionActiveBrush = SelectionActiveBrush;
                HexViewport.SelectionInactiveBrush = SelectionInactiveBrush;

                // Initialize mouse hover brush
                HexViewport.MouseHoverBrush = new SolidColorBrush(MouseOverColor);

                // Initialize data visual format settings
                HexViewport.DataStringVisual = DataStringVisual;
                HexViewport.OffSetStringVisual = OffSetStringVisual;

                // CRITICAL: Sync tooltip DP defaults to HexViewport on initialization
                // Without this, DP default values never propagate because the callback
                // only fires when values CHANGE, not for the initial default value
                HexViewport.ByteToolTipDisplayMode = ByteToolTipDisplayMode;
                HexViewport.ByteToolTipDetailLevel = ByteToolTipDetailLevel;

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
                StatusText.Text = WpfHexEditor.Core.Properties.Resources.StatusText_SearchMarkersCleared;
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

            // Notify plugins â€” ViewModel-level assignment bypasses DP callbacks.
            OnSelectionStartChanged(EventArgs.Empty);
            OnSelectionStopChanged(EventArgs.Empty);

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

            // Notify plugins subscribed via HexEditorServiceImpl (SelectionStartChanged / SelectionStopChanged).
            // These events are not fired via the DP path when selection is set directly on the ViewModel.
            OnSelectionStartChanged(EventArgs.Empty);
            OnSelectionStopChanged(EventArgs.Empty);

            // Update UI
            UpdateSelectionInfo();
        }

        /// <summary>
        /// Handle click/drag on offset column to select entire line(s).
        /// Also activates auto-scroll support so dragging beyond viewport edges scrolls.
        /// </summary>
        private void HexViewport_OffsetLineSelection(object sender, Controls.OffsetLineSelectionEventArgs e)
        {
            if (_viewModel == null)
                return;

            var start = new VirtualPosition(e.StartPosition);
            var end = new VirtualPosition(e.EndPosition);

            // Activate HexEditor-level mouse-down state so Content_MouseMove triggers auto-scroll
            _isMouseDown = true;
            _isOffsetLineDrag = true;
            _offsetDragAnchorStart = start;
            _offsetDragAnchorEnd = end;
            _mouseDownPosition = start;

            _viewModel.SetSelectionRange(start, end);

            // Notify plugins â€” ViewModel-level change bypasses DP callbacks.
            OnSelectionStartChanged(EventArgs.Empty);
            OnSelectionStopChanged(EventArgs.Empty);

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

            // Notify plugins (DataInspector, etc.) â€” ViewModel-level changes bypass the DP callbacks.
            OnSelectionStartChanged(EventArgs.Empty);
            OnSelectionStopChanged(EventArgs.Empty);

            // Scroll to ensure new position is visible.
            // Always sync VerticalScroll.Value alongside the ViewModel: the ScrollBar has
            // no binding and setting .Value programmatically does NOT fire VerticalScroll_Scroll
            // (Scroll event is user-interaction only), so there is no feedback loop.
            long targetLine = newPos / _viewModel.BytePerLine;
            if (targetLine < _viewModel.ScrollPosition)
            {
                _viewModel.ScrollPosition = targetLine;
                VerticalScroll.Value = targetLine;
            }
            // VisibleLines has +2 safety margin (see HexEditor.Events.cs line 710)
            // Scroll when cursor reaches actual visible area boundary (VisibleLines - 2)
            else if (targetLine >= _viewModel.ScrollPosition + _viewModel.VisibleLines - 2)
            {
                _viewModel.ScrollPosition = targetLine - _viewModel.VisibleLines + 3;
                VerticalScroll.Value = _viewModel.ScrollPosition;
            }

            // Update UI
            UpdateSelectionInfo();
            UpdatePositionInfo();
        }

        /// <summary>
        /// Handle refresh time update from HexViewport
        /// </summary>
        private void HexViewport_RefreshTimeUpdated(object sender, long refreshTimeMs)
        {
            // Update StatusBarItem for IDE integration (via IStatusBarContributor)
            if (_sbRefreshTime != null)
            {
                _sbRefreshTime.Value = $"{refreshTimeMs} ms";
            }

            // Update internal StatusBar TextBlock (for standalone use)
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
        protected virtual void OnSelectionChanged(HexSelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, e);
            RaiseDocumentEditorSelectionChanged();
            UpdateBreadcrumb();
        }

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
        /// Raised when the user requests to compare the current file with another file.
        /// <c>null</c> FilePath means no file is open. Subscribe in the application shell
        /// to delegate to <c>CompareFileLaunchService</c>.
        /// </summary>
        public event EventHandler<CompareFileRequestedEventArgs>? CompareFileRequested;

        /// <summary>
        /// Raised when the user requests to use the current byte selection as the LEFT side
        /// of a comparison (writes selection to a temp file first).
        /// </summary>
        public event EventHandler<CompareFileRequestedEventArgs>? CompareSelectionRequested;

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

        // Configuration Properties â†’ HexEditor.ConfigProperties.cs


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
        /// Can undo? Delegates to the shared <see cref="UndoEngine"/> when co-editing,
        /// or to the local <c>ByteProvider</c> stack in standalone mode.
        /// </summary>
        public bool CanUndo => _sharedUndoEngine?.CanUndo ?? (_viewModel?.CanUndo ?? false);

        /// <summary>
        /// Can redo? Delegates to the shared <see cref="UndoEngine"/> when co-editing,
        /// or to the local <c>ByteProvider</c> stack in standalone mode.
        /// </summary>
        public bool CanRedo => _sharedUndoEngine?.CanRedo ?? (_viewModel?.CanRedo ?? false);

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
                new PropertyMetadata(true, (d, e) =>
                {
                    if (d is HexEditor h && h._sbRefreshTime != null)
                        h._sbRefreshTime.IsVisible = (bool)e.NewValue;
                }));

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
                        if (child is TextBlock tb && tb.Text == WpfHexEditor.Core.Properties.Resources.OffsetString)
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
        /// Current first-line scroll offset (line number of the topmost visible line).
        /// Synchronized with <see cref="VerticalScroll"/>.Value on every scroll event.
        /// </summary>
        public long ScrollPosition => _viewModel?.ScrollPosition ?? 0;

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

                editor.RaiseHexStatusChanged();
            }
        }

        #endregion

        // Color Properties â†’ HexEditor.ColorProperties.cs

        // Dependency Properties â†’ HexEditor.DependencyProperties.cs

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

        #endregion

        #region Long-Running Operations Event Handlers

        /// <summary>
        /// Handle operation started event
        /// </summary>
        private void LongRunningService_OperationStarted(object sender, OperationProgressEventArgs e)
        {
            // Relay to IDocumentEditor subscribers (host app progress bar, etc.)
            RaiseDocEditorOperationStarted(e);

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

                    // Show built-in progress overlay only when not bypassed by the host
                    if (ShowProgressOverlay)
                    {
                        ProgressOverlay.Visibility = Visibility.Visible;
                        ProgressOverlay.ViewModel.OperationTitle = e.OperationTitle;
                        ProgressOverlay.ViewModel.StatusMessage = e.StatusMessage;
                        ProgressOverlay.ViewModel.ProgressPercentage = e.ProgressPercentage;
                        ProgressOverlay.ViewModel.IsIndeterminate = e.IsIndeterminate;
                        ProgressOverlay.ViewModel.CanCancel = e.CanCancel;
                    }

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
        private void LongRunningService_OperationProgress(object sender, OperationProgressEventArgs e)
        {
            // Relay to IDocumentEditor subscribers
            RaiseDocEditorOperationProgress(e);

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
                    // Update built-in overlay only when not bypassed by the host
                    if (ShowProgressOverlay)
                    {
                        ProgressOverlay.ViewModel.ProgressPercentage = e.ProgressPercentage;
                        ProgressOverlay.ViewModel.StatusMessage = e.StatusMessage;
                    }
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
        private void LongRunningService_OperationCompleted(object sender, OperationCompletedEventArgs e)
        {
            // Relay to IDocumentEditor subscribers
            RaiseDocEditorOperationCompleted(e);

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

                    // Hide built-in progress overlay only when not bypassed by the host
                    if (ShowProgressOverlay && ProgressOverlay != null)
                        ProgressOverlay.Visibility = Visibility.Collapsed;

                    // Restore cursor
                    Mouse.OverrideCursor = null;

                    // Update status bar
                    if (!e.Success && StatusText != null)
                    {
                        if (e.WasCancelled)
                            StatusText.Text = WpfHexEditor.Core.Properties.Resources.StatusText_OperationCancelled;
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
