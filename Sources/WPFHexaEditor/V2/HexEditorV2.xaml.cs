//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.CharacterTable;
using WpfHexaEditor.V2.Events;
using WpfHexaEditor.V2.Models;
using WpfHexaEditor.V2.ViewModels;

namespace WpfHexaEditor.V2
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

        // Bookmarks (V1 compatible)
        private readonly List<long> _bookmarks = new List<long>();

        // TBL (Character Table) support (V1 compatible)
        private TblStream _tblStream;
        private CharacterTableType _characterTableType = CharacterTableType.Ascii;

        // Hex editing state
        private bool _isEditingByte = false;
        private VirtualPosition _editingPosition = VirtualPosition.Invalid;
        private byte _editingValue = 0;
        private bool _editingHighNibble = true; // true = high nibble, false = low nibble

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

            // Initialize column headers with byte position numbers
            this.Loaded += (s, e) => RefreshColumnHeader();
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

        #region Public Properties

        /// <summary>
        /// Is a file currently loaded?
        /// </summary>
        public bool IsFileLoaded => _viewModel != null;

        /// <summary>
        /// Current edit mode
        /// </summary>
        public EditMode EditMode
        {
            get => _viewModel?.EditMode ?? EditMode.Overwrite;
            set
            {
                if (_viewModel != null)
                {
                    _viewModel.EditMode = value;
                    StatusText.Text = value == EditMode.Insert ? "Insert mode" : "Overwrite mode";
                }
            }
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
        /// Virtual length (total bytes including inserted/deleted) - V1 compatible
        /// </summary>
        public long VirtualLength => _viewModel?.VirtualLength ?? 0;

        /// <summary>
        /// Physical file length in bytes - V1 compatible
        /// </summary>
        public long Length => _viewModel?.FileLength ?? 0;

        /// <summary>
        /// Current file name (full path) - V1 compatible
        /// </summary>
        public string FileName { get; private set; } = string.Empty;

        /// <summary>
        /// Has the file been modified? - V1 compatible
        /// </summary>
        public bool IsModified { get; private set; } = false;

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
        /// Selection start position (virtual) - V1 compatible
        /// </summary>
        public long SelectionStart
        {
            get => _viewModel?.SelectionStart.Value ?? -1;
            set
            {
                if (_viewModel != null && value >= 0 && value < VirtualLength)
                {
                    var oldStart = SelectionStart;
                    var oldStop = SelectionStop;
                    var stop = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop : new VirtualPosition(value);
                    _viewModel.SetSelectionRange(new VirtualPosition(value), stop);

                    if (oldStart != value || oldStop != SelectionStop)
                        OnSelectionChanged(new HexSelectionChangedEventArgs(value, SelectionStop, SelectionLength));
                }
            }
        }

        /// <summary>
        /// Selection stop position (virtual) - V1 compatible
        /// </summary>
        public long SelectionStop
        {
            get => _viewModel?.SelectionStop.Value ?? -1;
            set
            {
                if (_viewModel != null && value >= 0 && value < VirtualLength)
                {
                    var oldStart = SelectionStart;
                    var oldStop = SelectionStop;
                    var start = _viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart : new VirtualPosition(value);
                    _viewModel.SetSelectionRange(start, new VirtualPosition(value));

                    if (oldStop != value || oldStart != SelectionStart)
                        OnSelectionChanged(new HexSelectionChangedEventArgs(SelectionStart, value, SelectionLength));
                }
            }
        }

        /// <summary>
        /// Read-only mode
        /// </summary>
        public bool ReadOnlyMode
        {
            get => _viewModel?.ReadOnlyMode ?? false;
            set
            {
                if (_viewModel != null)
                    _viewModel.ReadOnlyMode = value;
            }
        }

        /// <summary>
        /// Show or hide the status bar (V1 compatible)
        /// </summary>
        public bool ShowStatusBar
        {
            get => StatusBar.Visibility == Visibility.Visible;
            set => StatusBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

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
            // Changing offset visibility would require XAML template modifications
            // For V1 compatibility, we'll keep it simple and always show offset
            // Advanced implementations could dynamically modify the DataTemplate
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
            // Changing ASCII visibility would require XAML template modifications
            // For V1 compatibility, we'll keep it simple and always show ASCII
            // Advanced implementations could dynamically modify the DataTemplate
        }

        /// <summary>
        /// Number of bytes per line (8, 16, 32, etc.)
        /// </summary>
        public int BytePerLine
        {
            get => _viewModel?.BytePerLine ?? 16;
            set
            {
                if (_viewModel != null && value > 0)
                {
                    _viewModel.BytePerLine = value;

                    // Update scrollbar to reflect new total lines
                    VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines);

                    // Update status bar
                    BytesPerLineText.Text = $"Bytes/Line: {value}";

                    // Refresh column headers to match new BytePerLine
                    RefreshColumnHeader();
                }
            }
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
                    VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines);
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
                new FrameworkPropertyMetadata(ByteSpacerGroup.EightByte, ByteSpacer_Changed));

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
                new FrameworkPropertyMetadata(ByteSpacerVisual.Empty, ByteSpacer_Changed));

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
                editor.Resources["AlternateByteForegroundBrush"] = new SolidColorBrush((Color)e.NewValue);
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
            HexViewport.BytesPerLine = _viewModel.BytePerLine;

            // Initialize byte spacer properties on viewport (V1 compatibility)
            HexViewport.ByteSpacerPositioning = ByteSpacerPositioning;
            HexViewport.ByteSpacerWidthTickness = ByteSpacerWidthTickness;
            HexViewport.ByteGrouping = ByteGrouping;
            HexViewport.ByteSpacerVisualStyle = ByteSpacerVisualStyle;

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
            VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines);
            VerticalScroll.ViewportSize = _viewModel.VisibleLines;

            // Raise FileOpened event
            OnFileOpened(EventArgs.Empty);

            // Update status bar
            StatusText.Text = $"Loaded: {System.IO.Path.GetFileName(filePath)}";
            var fileInfo = new System.IO.FileInfo(filePath);
            FileSizeText.Text = $"Size: {FormatFileSize(fileInfo.Length)}";
            BytesPerLineText.Text = $"Bytes/Line: {_viewModel.BytePerLine}";
            EditModeText.Text = $"Mode: {_viewModel.EditMode}";
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
            PositionInfo.Text = "Position: 0";
            EditModeText.Text = "Mode: Overwrite";
            BytesPerLineText.Text = "Bytes/Line: 16";
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
        }

        /// <summary>
        /// Redo last undone operation
        /// </summary>
        public void Redo()
        {
            _viewModel?.Redo();
            OnRedoCompleted(EventArgs.Empty);
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
            return _viewModel?.CopyToClipboard() ?? false;
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
                StatusText.Text = $"TBL loaded: {System.IO.Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to load TBL: {ex.Message}";
                _tblStream = null;
                _characterTableType = CharacterTableType.Ascii;
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

        #region Internal Events

        private void Content_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null || e.ChangedButton != MouseButton.Left)
                return;

            // Set keyboard focus to enable keyboard input
            HexViewport.Focus();

            // Get the virtual position at mouse coordinates
            var position = GetVirtualPositionAtMouse(e.GetPosition(HexViewport));
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

            // Check if click is in hex area
            double hexStartX = OffsetWidth;
            double hexEndX = OffsetWidth + (_viewModel.BytePerLine * (HexByteWidth + HexByteSpacing));

            if (x >= hexStartX && x < hexEndX)
            {
                // Click in hex area
                double relativeX = x - hexStartX;
                int byteIndex = (int)(relativeX / (HexByteWidth + HexByteSpacing));

                // Clamp to valid byte range
                byteIndex = Math.Max(0, Math.Min(byteIndex, line.Bytes.Count - 1));
                return line.Bytes[byteIndex].VirtualPos;
            }

            // Check if click is in ASCII area
            double separatorX = OffsetWidth + (_viewModel.BytePerLine * (HexByteWidth + HexByteSpacing)) + 8;
            double asciiStartX = separatorX + SeparatorWidth;
            double asciiEndX = asciiStartX + (_viewModel.BytePerLine * AsciiCharWidth);

            if (x >= asciiStartX && x < asciiEndX)
            {
                // Click in ASCII area
                double relativeX = x - asciiStartX;
                int byteIndex = (int)(relativeX / AsciiCharWidth);

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

            // Get current position (use SelectionStart if valid, otherwise 0)
            var currentPos = _viewModel.SelectionStart.IsValid
                ? _viewModel.SelectionStart
                : VirtualPosition.Zero;

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

                // Hex input editing (0-9, A-F)
                default:
                    if (!_viewModel.ReadOnlyMode && TryGetHexValue(e.Key, out byte hexValue))
                    {
                        HandleHexInput(hexValue, currentPos);
                        handled = true;
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

            // Calculate how many lines fit in the viewport (V1 formula: (int)(actualheight / (LineHeight * ZoomScale)) + 1)
            // V2 doesn't have ZoomScale, so we just use: (int)(actualheight / lineHeight) + 1
            int calculatedLines = (int)(actualHeight / lineHeight) + 1;

            // Clamp to reasonable range (minimum 5, maximum 100)
            calculatedLines = Math.Max(5, Math.Min(100, calculatedLines));

            // Only update if different (avoid thrashing)
            if (_viewModel.VisibleLines != calculatedLines)
            {
                _viewModel.VisibleLines = calculatedLines; // Property setter calls RefreshVisibleLines() internally
                VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines);
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
                    break;

                case nameof(HexEditorViewModel.SelectionStop):
                    // Update HexViewport cursor (active end) and selection
                    // Cursor is at SelectionStop (the end that moves during Shift+navigation)
                    HexViewport.CursorPosition = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value :
                                                 (_viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : 0);
                    HexViewport.SelectionStop = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value : -1;
                    break;

                case nameof(HexEditorViewModel.TotalLines):
                    VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines);
                    break;

                case nameof(HexEditorViewModel.EditMode):
                    EditModeText.Text = $"Mode: {_viewModel.EditMode}";
                    break;

                case nameof(HexEditorViewModel.BytePerLine):
                    BytesPerLineText.Text = $"Bytes/Line: {_viewModel.BytePerLine}";
                    HexViewport.BytesPerLine = _viewModel.BytePerLine;
                    break;
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
                return true;
            }

            // Numpad keys (0-9)
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                value = (byte)(key - Key.NumPad0);
                return true;
            }

            // Letter keys (A-F)
            if (key >= Key.A && key <= Key.F)
            {
                value = (byte)(key - Key.A + 10);
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Handle hex digit input for editing bytes
        /// </summary>
        private void HandleHexInput(byte hexValue, VirtualPosition currentPos)
        {
            if (_viewModel == null || _viewModel.ReadOnlyMode)
                return;

            // Start new byte edit if not currently editing, or position changed
            if (!_isEditingByte || _editingPosition != currentPos)
            {
                _isEditingByte = true;
                _editingPosition = currentPos;
                _editingHighNibble = true;

                // Get current byte value from provider
                var physicalPos = _viewModel.VirtualToPhysical(currentPos);
                _editingValue = physicalPos.IsValid
                    ? _viewModel.GetByteAt(currentPos)
                    : (byte)0;
            }

            // Update the appropriate nibble
            if (_editingHighNibble)
            {
                // Update high nibble (bits 4-7)
                _editingValue = (byte)((_editingValue & 0x0F) | (hexValue << 4));
                _editingHighNibble = false; // Move to low nibble

                // Update visual display immediately after high nibble
                UpdateBytePreview(_editingPosition, _editingValue);
            }
            else
            {
                // Update low nibble (bits 0-3)
                _editingValue = (byte)((_editingValue & 0xF0) | hexValue);

                // Byte is complete, write it
                CommitByteEdit();

                // Move to next byte
                var nextPos = new VirtualPosition(currentPos.Value + 1);
                if (nextPos.Value < _viewModel.VirtualLength)
                {
                    _viewModel.SetSelection(nextPos);
                    EnsurePositionVisible(nextPos);
                }
                else
                {
                    _isEditingByte = false;
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
                return;

            // Determine action based on edit mode
            if (_viewModel.EditMode == EditMode.Insert)
            {
                // Insert mode: insert new byte
                _viewModel.InsertByte(_editingPosition, _editingValue);
            }
            else
            {
                // Overwrite mode: modify existing byte
                _viewModel.ModifyByte(_editingPosition, _editingValue);
            }

            _isEditingByte = false;
            _editingHighNibble = true;

            // Update status
            StatusText.Text = $"Edited byte at {_editingPosition.Value:X8}";
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
                    Width = 22, // Match HexByteWidth from HexViewport
                    TextAlignment = TextAlignment.Center,
                    FontSize = 11,
                    FontWeight = FontWeights.Normal,
                    Foreground = Resources["HeaderTextBrush"] as System.Windows.Media.Brush,
                    Margin = new Thickness(0, 0, 2, 0) // HexByteSpacing
                };

                _hexHeaderStackPanel.Children.Add(headerText);
            }

            // Generate ASCII column headers (just spacing placeholders for now, or could show char positions)
            for (int i = 0; i < bytesPerLine; i++)
            {
                // Add byte spacer before this column if needed
                if (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                    ByteSpacerPositioning == ByteSpacerPosition.StringBytePanel)
                {
                    AddByteSpacer(_asciiHeaderStackPanel, i, forceEmpty: true);
                }

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
    }
}
