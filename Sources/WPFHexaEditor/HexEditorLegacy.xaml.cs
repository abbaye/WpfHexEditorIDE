//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributor: ehsan69h
// Contributor: Janus Tida
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.CharacterTable;
using WpfHexaEditor.Core.EventArguments;
using WpfHexaEditor.Core.Interfaces;
using WpfHexaEditor.Core.MethodExtention;
using WpfHexaEditor.Dialog;
using WpfHexaEditor.Services;
using static WpfHexaEditor.Core.Bytes.ByteConverters;
using static WpfHexaEditor.Core.Bytes.ByteProviderLegacy;
using Path = System.IO.Path;

namespace WpfHexaEditor
{
    /// <summary> 
    /// Wpf HexEditorLegacy control implementation
    /// </summary>
    /// <remarks>
    /// Wpf Hexeditor is a fast and fully customisable user control for editing file or stream as hexadecimal. 
    /// Can be used in Wpf or WinForm application
    /// </remarks>
    public partial class HexEditorLegacy : IDisposable
    {
        #region Global class variables

        /// <summary>
        /// Byte provider for work with file or stream currently loaded in control.
        /// </summary>
        private ByteProviderLegacy _provider;

        /// <summary>
        /// Service for undo/redo operations
        /// </summary>
        private readonly UndoRedoService _undoRedoService = new UndoRedoService();

        /// <summary>
        /// Service for clipboard operations
        /// </summary>
        private readonly ClipboardService _clipboardService = new ClipboardService();

        /// <summary>
        /// Service for selection operations
        /// </summary>
        private readonly SelectionService _selectionService = new SelectionService();

        /// <summary>
        /// Service for find/replace operations with caching
        /// </summary>
        private readonly FindReplaceService _findReplaceService = new FindReplaceService();

        /// <summary>
        /// Service for highlight operations (search results, marked bytes)
        /// </summary>
        private readonly HighlightService _highlightService = new HighlightService();

        /// <summary>
        /// Service for byte modification operations (insert, delete, modify)
        /// </summary>
        private readonly ByteModificationService _byteModificationService = new ByteModificationService();

        /// <summary>
        /// Service for bookmark management operations
        /// </summary>
        private readonly BookmarkService _bookmarkService = new BookmarkService();

        /// <summary>
        /// Service for TBL (character table) management operations
        /// </summary>
        private readonly TblService _tblService = new TblService();

        /// <summary>
        /// Service for position calculations and conversions
        /// </summary>
        private readonly PositionService _positionService = new PositionService();

        /// <summary>
        /// Service for custom background block management
        /// </summary>
        private readonly CustomBackgroundService _customBackgroundService = new CustomBackgroundService();

        /// <summary>
        /// The large change of scroll when clicked on scrollbar
        /// </summary>
        private double _scrollLargeChange = 100;

        /// <summary>
        /// List of byte are highlighted
        /// </summary>
        // REMOVED: _markedPositionList is now managed by HighlightService
        // private Dictionary<long, long> _markedPositionList = new();

        /// <summary>
        /// Cache for scroll markers: Key = "marker_position", Value = Rectangle
        /// </summary>
        private readonly Dictionary<string, Rectangle> _scrollMarkerCache = new();

        /// <summary>
        /// Cache for scroll marker brushes to avoid repeated TryFindResource calls
        /// </summary>
        private SolidColorBrush _bookMarkColorBrush;
        private SolidColorBrush _searchBookMarkColorBrush;
        private SolidColorBrush _selectionStartBookMarkColorBrush;
        private SolidColorBrush _byteModifiedMarkColorBrush;
        private SolidColorBrush _byteDeletedMarkColorBrush;

        /// <summary>
        /// Cache for byte color brushes to avoid recalculating on every property change
        /// </summary>
        private Brush _cachedSelectionFirstColor;
        private Brush _cachedSelectionSecondColor;
        private Brush _cachedByteModifiedColor;
        private Brush _cachedMouseOverColor;
        private Brush _cachedByteDeletedColor;
        private Brush _cachedByteAddedColor;
        private Brush _cachedHighLightColor;
        private Brush _cachedForeground;
        private Brush _cachedForegroundSecondColor;
        private Brush _cachedForegroundContrast;
        private Brush _cachedBackground;

        /// <summary>
        /// Flag to indicate if color cache needs update
        /// </summary>
        private bool _colorCacheNeedsUpdate = true;

        /// <summary>
        /// Byte position in file when mouse right click occurs.
        /// </summary>
        private long _rightClickBytePosition = -1;

        /// <summary>
        /// Custom character table loaded. Used for show byte as texte.
        /// Now managed by TblService - use _tblService.CharacterTable instead.
        /// </summary>
        // private TblStream _tblService.CharacterTable; // REMOVED - Now using _tblService.CharacterTable

        /// <summary>
        /// Hold the count of all byte in file.
        /// </summary>
        private long[] _bytecount;

        /// <summary>
        /// Save the view byte buffer as a field. 
        /// To save the time when Scolling i do not building them every time when scolling.
        /// </summary>
        private byte[] _viewBuffer;

        /// <summary>
        /// Save the view byte buffer position as a field. 
        /// To save the time when Scolling i do not building them every time when scolling.
        /// </summary>
        private long[] _viewBufferBytePosition;

        /// <summary>
        /// Used for control the view on refresh
        /// </summary>
        private long _priLevel;

        /// <summary>
        /// Used with VerticalMoveByTime methods/events to move the scrollbar.
        /// </summary>
        private bool _mouseOnBottom, _mouseOnTop;

        /// <summary>
        /// Used with VerticalMoveByTime methods/events to move the scrollbar.
        /// </summary>
        private long _bottomEnterTimes, _topEnterTimes;

        /// <summary>
        /// Caret used in control to view position
        /// </summary>
        private readonly Caret _caret = new();

        /// <summary>
        /// For detect redondants call when disposing control
        ///  </summary>
        private bool _disposedValue;

        /// <summary>
        /// For detect redondants call on SetFocusHexDataPanel and SetFocusStringDataPanel
        /// </summary>
        bool _setFocusTest;

        /// <summary>
        /// Highlight the header and offset on SelectionStart property
        /// </summary>
        private bool _highLightSelectionStart = true;

        /// <summary>
        /// Get is the first color...
        /// </summary>
        private FirstColor _firstColor = FirstColor.HexByteData;

        /// <summary>
        /// Get or set the scale transform to work with zoom
        /// </summary>
        private ScaleTransform _scaler;

        /// <summary>
        /// Allow or not the zoom in control
        /// </summary>
        private bool _allowZoom = true;

        #endregion Global Class variables

        #region Events

        /// <summary>
        /// Occurs when selection start are changed.
        /// </summary>
        public event EventHandler SelectionStartChanged;

        /// <summary>
        /// Occurs when selection stop are changed.
        /// </summary>
        public event EventHandler SelectionStopChanged;

        /// <summary>
        /// Occurs when the length of selection are changed.
        /// </summary>
        public event EventHandler SelectionLengthChanged;

        /// <summary>
        /// Occurs when data are copie to clipboard.
        /// </summary>
        public event EventHandler DataCopied;

        /// <summary>
        /// Occurs when the type of character table are changed.
        /// </summary>
        public event EventHandler TypeOfCharacterTableChanged;

        /// <summary>
        /// Occurs when a long process percent changed.
        /// </summary>
        public event EventHandler LongProcessProgressChanged;

        /// <summary>
        /// Occurs when a long process are started.
        /// </summary>
        public event EventHandler LongProcessProgressStarted;

        /// <summary>
        /// Occurs when a long process are completed.
        /// </summary>
        public event EventHandler LongProcessProgressCompleted;

        /// <summary>
        /// Occurs when readonly property are changed.
        /// </summary>
        public event EventHandler ReadOnlyChanged;

        /// <summary>
        /// Occurs when data are saved to stream/file.
        /// </summary>
        public event EventHandler ChangesSubmited;

        /// <summary>
        /// Occurs when the replace byte by byte are completed
        /// </summary>
        public event EventHandler ReplaceByteCompleted;

        /// <summary>
        /// Occurs when the fill with byte method are completed
        /// </summary>
        public event EventHandler FillWithByteCompleted;

        /// <summary>
        /// Occurs when bytes as deleted in control
        /// </summary>
        public event EventHandler BytesDeleted;

        /// <summary>
        /// Occurs when byte as modified in control
        /// </summary>
        public event EventHandler<ByteEventArgs> BytesModified;

        /// <summary>
        /// Occurs when undo are completed
        /// </summary>
        public event EventHandler Undone;

        /// <summary>
        /// Occurs when redo are completed
        /// </summary>
        public event EventHandler Redone;

        /// <summary>
        /// Occurs on byte click completed
        /// </summary>
        public event EventHandler<ByteEventArgs> ByteClick;

        /// <summary>
        /// Occurs on byte double click completed
        /// </summary>
        public event EventHandler<ByteEventArgs> ByteDoubleClick;

        /// <summary>
        /// Occurs when the zoom scale is changed
        /// </summary>
        public event EventHandler ZoomScaleChanged;

        /// <summary>
        /// Occurs when the vertical scroll bar position as changed
        /// </summary>
        public event EventHandler<ByteEventArgs> VerticalScrollBarChanged;

        #endregion Events

        #region Constructor

        public HexEditorLegacy()
        {
            InitializeComponent();

            // Initialize clipboard service default mode
            _clipboardService.DefaultCopyMode = CopyPasteMode.HexaString;

            //Refresh view
            CheckProviderIsOnProgress();
            UpdateScrollBar();
            InitializeCaret();
            RefreshView(true);
        }
        #endregion Contructor

        #region Build-in CTRL key support

        /// <summary>
        /// Get or set if the build-in CTRL+C (copy) are allowed
        /// </summary>
        public bool AllowBuildinCtrlc
        {
            get => (bool)GetValue(AllowBuildinCtrlcProperty);
            set => SetValue(AllowBuildinCtrlcProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlcProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrlc), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(true, Control_AllowBuildinCTRLPropertyChanged));

        /// <summary>
        /// Get or set if the build-in CTRL+V (paste) are allowed
        /// </summary>
        public bool AllowBuildinCtrlv
        {
            get => (bool)GetValue(AllowBuildinCtrlvProperty);
            set => SetValue(AllowBuildinCtrlvProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlvProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrlv), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(true, Control_AllowBuildinCTRLPropertyChanged));

        /// <summary>
        /// Get or set if the build-in CTRL+A (select all) are allowed
        /// </summary>
        public bool AllowBuildinCtrla
        {
            get => (bool)GetValue(AllowBuildinCtrlaProperty);
            set => SetValue(AllowBuildinCtrlaProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlaProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrla), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(true, Control_AllowBuildinCTRLPropertyChanged));

        /// <summary>
        /// Get or set if the build-in CTRL+Z (undo) are allowed
        /// </summary>
        public bool AllowBuildinCtrlz
        {
            get => (bool)GetValue(AllowBuildinCtrlzProperty);
            set => SetValue(AllowBuildinCtrlzProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlzProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrlz), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(true, Control_AllowBuildinCTRLPropertyChanged));

        /// <summary>
        /// Get or set if the build-in CTRL+Y (redo) are allowed
        /// </summary>
        public bool AllowBuildinCtrly
        {
            get => (bool)GetValue(AllowBuildinCtrlyProperty);
            set => SetValue(AllowBuildinCtrlyProperty, value);
        }

        public static readonly DependencyProperty AllowBuildinCtrlyProperty =
            DependencyProperty.Register(nameof(AllowBuildinCtrly), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(true, Control_AllowBuildinCTRLPropertyChanged));

        private static void Control_AllowBuildinCTRLPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorLegacy ctrl && e.NewValue != e.OldValue) ctrl.RefreshView();
        }

        private void Control_CTRLZKey(object sender, EventArgs e)
        {
            if (AllowBuildinCtrlz) Undo();
        }

        private void Control_CTRLYKey(object sender, EventArgs e)
        {
            if (AllowBuildinCtrly) Redo();
        }

        private void Control_CTRLCKey(object sender, EventArgs e)
        {
            if (AllowBuildinCtrlc) CopyToClipboard();
        }

        private void Control_CTRLAKey(object sender, EventArgs e)
        {
            if (AllowBuildinCtrla) SelectAll();
        }

        private void Control_CTRLVKey(object sender, EventArgs e)
        {
            if (AllowBuildinCtrlv) Paste(AllowExtend);
        }

        #endregion Build-in CTRL key support

        #region Colors/fonts property and methods

        /// <summary>
        /// Get or set the first selection color
        /// </summary>
        public Brush SelectionFirstColor
        {
            get => (Brush)GetValue(SelectionFirstColorProperty);
            set => SetValue(SelectionFirstColorProperty, value);
        }

        public static readonly DependencyProperty SelectionFirstColorProperty =
            DependencyProperty.Register(nameof(SelectionFirstColor), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.CornflowerBlue, Control_ColorPropertyChanged));

        /// <summary>
        /// Get or set the second selection color
        /// </summary>
        public Brush SelectionSecondColor
        {
            get => (Brush)GetValue(SelectionSecondColorProperty);
            set => SetValue(SelectionSecondColorProperty, value);
        }

        public static readonly DependencyProperty SelectionSecondColorProperty =
            DependencyProperty.Register(nameof(SelectionSecondColor), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.LightSteelBlue, Control_ColorPropertyChanged));

        /// <summary>
        /// Get or set the byte modified colors
        /// </summary>
        public Brush ByteModifiedColor
        {
            get => (Brush)GetValue(ByteModifiedColorProperty);
            set => SetValue(ByteModifiedColorProperty, value);
        }

        public static readonly DependencyProperty ByteModifiedColorProperty =
            DependencyProperty.Register(nameof(ByteModifiedColor), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.DarkGray, Control_ColorPropertyChanged));

        /// <summary>
        /// Get or set the mouse over colors
        /// </summary>
        public Brush MouseOverColor
        {
            get => (Brush)GetValue(MouseOverColorProperty);
            set => SetValue(MouseOverColorProperty, value);
        }

        public static readonly DependencyProperty MouseOverColorProperty =
            DependencyProperty.Register(nameof(MouseOverColor), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.LightSkyBlue, Control_ColorPropertyChanged));

        /// <summary>
        /// Get or set the byte deleted colors
        /// </summary>
        /// <remarks>
        /// Visible only when HideByteDelete are set to false 
        /// </remarks>
        public Brush ByteDeletedColor
        {
            get => (Brush)GetValue(ByteDeletedColorProperty);
            set => SetValue(ByteDeletedColorProperty, value);
        }

        public static readonly DependencyProperty ByteDeletedColorProperty =
            DependencyProperty.Register(nameof(ByteDeletedColor), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.Red, Control_ColorPropertyChanged));

        /// <summary>
        /// Get or set the byte added colors
        /// </summary>
        public Brush ByteAddedColor
        {
            get => (Brush)GetValue(ByteAddedColorProperty);
            set => SetValue(ByteAddedColorProperty, value);
        }

        public static readonly DependencyProperty ByteAddedColorProperty =
            DependencyProperty.Register(nameof(ByteAddedColor), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.Transparent, Control_ColorPropertyChanged));


        /// <summary>
        /// Get or set the Highlight color
        /// </summary>
        public Brush HighLightColor
        {
            get => (Brush)GetValue(HighLightColorProperty);
            set => SetValue(HighLightColorProperty, value);
        }

        public static readonly DependencyProperty HighLightColorProperty =
            DependencyProperty.Register(nameof(HighLightColor), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.Gold, Control_ColorPropertyChanged));

        /// <summary>
        /// Get or set the Highlight color of the header
        /// </summary>
        public Brush ForegroundHighLightOffSetHeaderColor
        {
            get => (Brush)GetValue(ForegroundHighLightOffSetHeaderColorProperty);
            set => SetValue(ForegroundHighLightOffSetHeaderColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundHighLightOffSetHeaderColorProperty =
            DependencyProperty.Register(nameof(ForegroundHighLightOffSetHeaderColor), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.Black, Control_ForegroundOffSetHeaderColorPropertyChanged));

        /// <summary>
        /// Get or set the header color
        /// </summary>
        public Brush ForegroundOffSetHeaderColor
        {
            get => (Brush)GetValue(ForegroundOffSetHeaderColorProperty);
            set => SetValue(ForegroundOffSetHeaderColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundOffSetHeaderColorProperty =
            DependencyProperty.Register(nameof(ForegroundOffSetHeaderColor), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.Gray, Control_ForegroundOffSetHeaderColorPropertyChanged));

        private static void Control_ForegroundOffSetHeaderColorPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

            ctrl.UpdateHeader();
            ctrl.UpdateLinesInfo();
        }

        /// <summary>
        /// Get or set the backgroung color
        /// </summary>
        public new Brush Background
        {
            get => (Brush)GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        /// <summary>
        /// Get or set the foreground color
        /// </summary>
        public new Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public new static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.Black, Control_ColorPropertyChanged));

        /// <summary>
        /// Second foreground colors used in iByteControl
        /// </summary>
        public Brush ForegroundSecondColor
        {
            get => (Brush)GetValue(ForegroundSecondColorProperty);
            set => SetValue(ForegroundSecondColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundSecondColorProperty =
            DependencyProperty.Register(nameof(ForegroundSecondColor), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.Blue, Control_ColorPropertyChanged));

        /// <summary>
        /// Foreground colors used in iByteControl
        /// </summary>
        public Brush ForegroundContrast
        {
            get => (Brush)GetValue(ForegroundContrastProperty);
            set => SetValue(ForegroundContrastProperty, value);
        }

        public static readonly DependencyProperty ForegroundContrastProperty =
            DependencyProperty.Register(nameof(ForegroundContrast), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.White, Control_ColorPropertyChanged));

        public new static readonly DependencyProperty BackgroundProperty =
            DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.White, Control_BackgroundColorPropertyChanged));



        public Brush BarChartColor
        {
            get { return (Brush)GetValue(BarChartColorProperty); }
            set { SetValue(BarChartColorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for BarChartColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BarChartColorProperty =
            DependencyProperty.Register(nameof(BarChartColor), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.DarkGray, Control_ColorPropertyChanged));

        private static void Control_BackgroundColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

            ctrl.BaseGrid.Background = (Brush)e.NewValue;
        }

        /// <summary>
        /// Update color cache from dependency properties
        /// </summary>
        private void UpdateColorCache()
        {
            _cachedSelectionFirstColor = SelectionFirstColor;
            _cachedSelectionSecondColor = SelectionSecondColor;
            _cachedByteModifiedColor = ByteModifiedColor;
            _cachedMouseOverColor = MouseOverColor;
            _cachedByteDeletedColor = ByteDeletedColor;
            _cachedByteAddedColor = ByteAddedColor;
            _cachedHighLightColor = HighLightColor;
            _cachedForeground = Foreground;
            _cachedForegroundSecondColor = ForegroundSecondColor;
            _cachedForegroundContrast = ForegroundContrast;
            _cachedBackground = Background;
            _colorCacheNeedsUpdate = false;
        }

        /// <summary>
        /// Get cached colors for fast access - avoids DependencyProperty overhead
        /// </summary>
        internal Brush GetCachedSelectionFirstColor()
        {
            if (_colorCacheNeedsUpdate) UpdateColorCache();
            return _cachedSelectionFirstColor;
        }

        internal Brush GetCachedSelectionSecondColor()
        {
            if (_colorCacheNeedsUpdate) UpdateColorCache();
            return _cachedSelectionSecondColor;
        }

        internal Brush GetCachedByteModifiedColor()
        {
            if (_colorCacheNeedsUpdate) UpdateColorCache();
            return _cachedByteModifiedColor;
        }

        internal Brush GetCachedMouseOverColor()
        {
            if (_colorCacheNeedsUpdate) UpdateColorCache();
            return _cachedMouseOverColor;
        }

        internal Brush GetCachedByteDeletedColor()
        {
            if (_colorCacheNeedsUpdate) UpdateColorCache();
            return _cachedByteDeletedColor;
        }

        internal Brush GetCachedHighLightColor()
        {
            if (_colorCacheNeedsUpdate) UpdateColorCache();
            return _cachedHighLightColor;
        }

        internal Brush GetCachedForeground()
        {
            if (_colorCacheNeedsUpdate) UpdateColorCache();
            return _cachedForeground;
        }

        internal Brush GetCachedForegroundSecondColor()
        {
            if (_colorCacheNeedsUpdate) UpdateColorCache();
            return _cachedForegroundSecondColor;
        }

        internal Brush GetCachedBackground()
        {
            if (_colorCacheNeedsUpdate) UpdateColorCache();
            return _cachedBackground;
        }

        /// <summary>
        /// Update only colors without full refresh - OPTIMIZED
        /// </summary>
        private void UpdateColorsOnly()
        {
            if (_colorCacheNeedsUpdate)
                UpdateColorCache();

            // Batch update with BeginInit/EndInit for better performance
            HexDataStackPanel.BeginInit();
            StringDataStackPanel.BeginInit();

            try
            {
                // Update all visible bytes with new colors
                TraverseHexAndStringBytes(ctrl =>
                {
                    ctrl.UpdateVisual();
                }, force: false);
            }
            finally
            {
                HexDataStackPanel.EndInit();
                StringDataStackPanel.EndInit();
            }
        }

        private static void Control_ColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

            // Mark color cache as needing update
            ctrl._colorCacheNeedsUpdate = true;

            // Use optimized color-only update instead of full RefreshView
            ctrl.UpdateColorsOnly();
        }

        public new FontFamily FontFamily
        {
            get => (FontFamily)GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public new static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(new FontFamily("Courier New"), FontFamily_Changed));

        private static void FontFamily_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

            ctrl.RefreshView(true);
        }

        /// <summary>
        /// Call Updatevisual methods for all IByteControl
        /// </summary>
        public void UpdateVisual() => TraverseHexAndStringBytes(ctrl => { ctrl.UpdateVisual(); });

        #endregion Colors/fonts property and methods

        #region Miscellaneous property/methods

        /// <summary>
        /// The name of your application to be showing in messagebox title
        /// </summary>
        public string ApplicationName
        {
            get => (string)GetValue(ApplicationNameProperty);
            set => SetValue(ApplicationNameProperty, value);
        }

        public static readonly DependencyProperty ApplicationNameProperty =
            DependencyProperty.Register(nameof(ApplicationName), typeof(string), typeof(HexEditorLegacy),
                new PropertyMetadata("Wpf HexEditorLegacy"));

        /// <summary>
        /// Height of data line. 
        /// </summary>
        public double LineHeight
        {
            get => (double)GetValue(LineHeightProperty);
            set => SetValue(LineHeightProperty, value);
        }

        public static readonly DependencyProperty LineHeightProperty =
            DependencyProperty.Register(nameof(LineHeight), typeof(double), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(18D, (d, _) => 
                {
                    if (d is HexEditorLegacy ctrl)
                        ctrl.RefreshView();
                }));

        /// <summary>
        /// Get or set the visual of line info panel
        /// </summary>
        public OffSetPanelType OffSetPanelVisual
        {
            get => (OffSetPanelType)GetValue(OffSetPanelVisualProperty);
            set => SetValue(OffSetPanelVisualProperty, value);
        }

        public static readonly DependencyProperty OffSetPanelVisualProperty =
            DependencyProperty.Register(nameof(OffSetPanelVisual), typeof(OffSetPanelType), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(OffSetPanelType.OffsetOnly, OffSetPanelVisual_PropertyChanged));

        private static void OffSetPanelVisual_PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorLegacy ctrl)
                ctrl.UpdateLinesInfo();
        }

        /// <summary>
        /// Get or set if the visual of line info panel width are dynamic or fixed
        /// </summary>
        public OffSetPanelFixedWidth OffSetPanelFixedWidthVisual
        {
            get => (OffSetPanelFixedWidth)GetValue(OffSetPanelFixedWidthVisualProperty);
            set => SetValue(OffSetPanelFixedWidthVisualProperty, value);
        }

        public static readonly DependencyProperty OffSetPanelFixedWidthVisualProperty =
            DependencyProperty.Register(nameof(OffSetPanelFixedWidthVisual), typeof(OffSetPanelFixedWidth), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(OffSetPanelFixedWidth.Dynamic, OffSetPanelVisual_PropertyChanged));


        /// <summary>
        /// Get or set the offset base, which is added to all offsets before they are displayed
        /// </summary>
        public long OffsetBase
        {
            get => (long)GetValue(OffsetBaseProperty);
            set => SetValue(OffsetBaseProperty, value);
        }

        public static readonly DependencyProperty OffsetBaseProperty =
            DependencyProperty.Register(nameof(OffsetBase), typeof(long), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(0L, OffSetPanelVisual_PropertyChanged));

        /// <summary>
        /// Get or set of the tooltip are shown over the IByteControl
        /// </summary>
        public bool ShowByteToolTip
        {
            get => (bool)GetValue(ShowByteToolTipProperty);
            set => SetValue(ShowByteToolTipProperty, value);
        }

        public static readonly DependencyProperty ShowByteToolTipProperty =
            DependencyProperty.Register(nameof(ShowByteToolTip), typeof(bool), typeof(HexEditorLegacy),
                new PropertyMetadata(false));


        /// <summary>
        /// Get all bytes modified of the specified action
        /// </summary>
        /// <returns>Return byte modified of specified action. Return null if provider is closed</returns>
        public IDictionary<long, ByteModified> GetByteModifieds(ByteAction act)
        {
            if (!CheckIsOpen(_provider)) return null;

            return _provider.GetByteModifieds(act);
        }

        public (byte? singleByte, bool succes) GetByte(long position, bool copyChange = true)
        {
            if (!CheckIsOpen(_provider)) return (null, false);

            return _provider.GetByte(position, copyChange);
        }
        #endregion Miscellaneous property/methods

        #region Data visual type support

        /// <summary>
        /// Set or get the visual of line offset header
        /// </summary>
        public DataVisualType OffSetStringVisual
        {
            get => (DataVisualType)GetValue(OffSetStringVisualProperty);
            set => SetValue(OffSetStringVisualProperty, value);
        }

        public static readonly DependencyProperty OffSetStringVisualProperty =
            DependencyProperty.Register(nameof(OffSetStringVisual), typeof(DataVisualType), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(DataVisualType.Hexadecimal, (d, e) =>
                {
                    if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

                    ctrl.UpdateLinesInfo();
                }));

        /// <summary>
        /// Visually change de state of the byte
        /// </summary>
        public DataVisualState DataStringState
        {
            get => (DataVisualState)GetValue(OffSetDataStringStateProperty);
            set => SetValue(OffSetDataStringStateProperty, value);
        }

        public static readonly DependencyProperty OffSetDataStringStateProperty =
           DependencyProperty.Register(nameof(DataStringState), typeof(DataVisualState), typeof(HexEditorLegacy),
               new FrameworkPropertyMetadata(DataVisualState.Default, (d, e) =>
               {
                   if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

                   ctrl.UpdateHeader(true);

                   ctrl.TraverseHexBytes(hctrl =>
                   {
                       hctrl.UpdateDataVisualWidth();
                       hctrl.UpdateTextRenderFromByte();
                   });
               }));

        public ByteOrderType ByteOrder
        {
            get => (ByteOrderType)GetValue(OffSetByteOrderProperty);
            set => SetValue(OffSetByteOrderProperty, value);
        }

        public static readonly DependencyProperty OffSetByteOrderProperty =
           DependencyProperty.Register(nameof(ByteOrder), typeof(ByteOrderType), typeof(HexEditorLegacy),
               new FrameworkPropertyMetadata(ByteOrderType.LoHi, (d, e) =>
               {
                   if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

                   ctrl.UpdateHeader(true);

                   ctrl.TraverseHexBytes(hctrl =>
                   {
                       hctrl.UpdateDataVisualWidth();
                       hctrl.UpdateTextRenderFromByte();
                   });
               }));


        /// <summary>
        /// Obtains the ration from the ByteSize
        /// </summary>
        private int ByteSizeRatio => ByteSize switch
        {
            ByteSizeType.Bit8 => 1,
            ByteSizeType.Bit16 => 2,
            ByteSizeType.Bit32 => 4,
            _ => throw new NotImplementedException()
        };

        /// <summary>
        /// Get or set the byte size
        /// </summary>
        public ByteSizeType ByteSize
        {
            get => (ByteSizeType)GetValue(OffSetByteSizeProperty);
            set => SetValue(OffSetByteSizeProperty, value);
        }

        public static readonly DependencyProperty OffSetByteSizeProperty =
           DependencyProperty.Register(nameof(ByteSize), typeof(ByteSizeType), typeof(HexEditorLegacy),
               new FrameworkPropertyMetadata(ByteSizeType.Bit8, (d, e) =>
               {
                   if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

                   ctrl.UpdateViewers(true);
                   ctrl.UpdateHeader(true);

                   ctrl.TraverseHexBytes(c =>
                   {
                       c.UpdateDataVisualWidth();
                       c.UpdateTextRenderFromByte();
                   });

                   ctrl.UpdateByteModified();
                   ctrl.UpdateScrollBar();
               }));

        /// <summary>
        /// Get or set the visual data format of HexByte 
        /// </summary>
        public DataVisualType DataStringVisual
        {
            get => (DataVisualType)GetValue(DataStringVisualProperty);
            set => SetValue(DataStringVisualProperty, value);
        }

        public static readonly DependencyProperty DataStringVisualProperty =
            DependencyProperty.Register(nameof(DataStringVisual), typeof(DataVisualType), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(DataVisualType.Hexadecimal, (d, e) =>
                {
                    if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

                    ctrl.UpdateHeader(true);

                    ctrl.TraverseHexBytes(c =>
                    {
                        c.UpdateDataVisualWidth();
                        c.UpdateTextRenderFromByte();
                    });
                }));

        #endregion Data visual type support

        #region Characters tables property/methods

        /// <summary>
        /// Type of caracter table are used un hexacontrol.
        /// For now, somes character table can be readonly but will change in future
        /// </summary>
        public CharacterTableType TypeOfCharacterTable
        {
            get => (CharacterTableType)GetValue(TypeOfCharacterTableProperty);
            set => SetValue(TypeOfCharacterTableProperty, value);
        }

        public static readonly DependencyProperty TypeOfCharacterTableProperty =
            DependencyProperty.Register(nameof(TypeOfCharacterTable), typeof(CharacterTableType), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(CharacterTableType.Ascii, (d, _) =>
                {
                    if (d is not HexEditorLegacy ctrl) return;

                    ctrl.RefreshView(true);
                    ctrl.TypeOfCharacterTableChanged?.Invoke(ctrl, EventArgs.Empty);
                }));

        /// <summary>
        /// Custom encoding to use when TypeOfCharacterTable is set to CustomEncoding
        /// Allows any encoding supported by System.Text.Encoding (e.g., Shift-JIS, EUC-KR, Windows-1252, etc.)
        /// </summary>
        /// <example>
        /// <code>
        /// // Use Shift-JIS encoding for Japanese text
        /// hexEditor.TypeOfCharacterTable = CharacterTableType.CustomEncoding;
        /// hexEditor.CustomEncoding = Encoding.GetEncoding("shift_jis");
        ///
        /// // Use EUC-KR encoding for Korean text
        /// hexEditor.CustomEncoding = Encoding.GetEncoding("euc-kr");
        ///
        /// // Use Windows-1252 (Western European)
        /// hexEditor.CustomEncoding = Encoding.GetEncoding(1252);
        /// </code>
        /// </example>
        public Encoding CustomEncoding
        {
            get => (Encoding)GetValue(CustomEncodingProperty);
            set => SetValue(CustomEncodingProperty, value);
        }

        public static readonly DependencyProperty CustomEncodingProperty =
            DependencyProperty.Register(nameof(CustomEncoding), typeof(Encoding), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Encoding.UTF8, (d, _) =>
                {
                    if (d is not HexEditorLegacy ctrl) return;
                    // Only refresh if CustomEncoding is actually being used
                    if (ctrl.TypeOfCharacterTable == CharacterTableType.CustomEncoding)
                        ctrl.RefreshView(true);
                }));

        /// <summary>
        /// Show or not Multi Title Enconding (MTE) are loaded in TBL file
        /// </summary>
        public bool TblShowMte
        {
            get => (bool)GetValue(TblShowMteProperty);
            set => SetValue(TblShowMteProperty, value);
        }

        public static readonly DependencyProperty TblShowMteProperty =
            DependencyProperty.Register(nameof(TblShowMte), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(true, (d, _) =>
                {
                    if (d is HexEditorLegacy ctrl)
                        ctrl.RefreshView();
                }));

        /// <summary>
        /// Load TBL Character table file in control. (Used for ROM reverse engineering)
        /// Load TBL Bookmark into control.
        /// Change CharacterTable property for use.
        /// </summary>
        public void LoadTblFile(string fileName)
        {
            // Load via service (data layer)
            if (!_tblService.LoadFromFile(fileName))
                return;

            // Service now manages CharacterTable internally - no manual assignment needed

            // Update UI (visual layer)
            TblLabel.Visibility = Visibility.Visible;
            TblLabel.ToolTip = $"TBL file : {fileName}";

            UpdateTblBookMark();

            BuildDataLines(MaxVisibleLine, true);
            RefreshView(true);
        }

        /// <summary>
        /// Load TBL Character table file in control. (Used for ROM reverse engineering)
        /// Load TBL Bookmark into control.
        /// Change CharacterTable property for use.
        /// </summary>
        public void LoadDefaultTbl(DefaultCharacterTableType type = DefaultCharacterTableType.Ascii)
        {
            // Load via service (data layer)
            if (!_tblService.LoadDefault(type))
                return;

            // Service now manages CharacterTable internally - no manual assignment needed
            TblShowMte = false;

            // Update UI (visual layer)
            TblLabel.Visibility = Visibility.Visible;
            TblLabel.ToolTip = $"{Properties.Resources.DefaultTBLString} : {type}";

            RefreshView();
        }

        /// <summary>
        /// Update TBL bookmark in control
        /// </summary>
        private void UpdateTblBookMark()
        {
            //Load from loaded TBL bookmark
            if (_tblService.CharacterTable == null) return;

            foreach (var mark in _tblService.CharacterTable.BookMarks)
                SetScrollMarker(mark);
        }

        /// <summary>
        /// Get or set the color of DTE in string panel.
        /// </summary>
        public SolidColorBrush TbldteColor
        {
            get => (SolidColorBrush)GetValue(TbldteColorProperty);
            set => SetValue(TbldteColorProperty, value);
        }

        public static readonly DependencyProperty TbldteColorProperty =
            DependencyProperty.Register(nameof(TbldteColor), typeof(SolidColorBrush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.Red, TBLColor_Changed));

        private static void TBLColor_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorLegacy ctrl)
                ctrl.RefreshView();
        }

        /// <summary>
        /// Get or set the color of MTE in string panel.
        /// </summary>
        public SolidColorBrush TblmteColor
        {
            get => (SolidColorBrush)GetValue(TblmteColorProperty);
            set => SetValue(TblmteColorProperty, value);
        }

        public static readonly DependencyProperty TblmteColorProperty =
            DependencyProperty.Register(nameof(TblmteColor), typeof(SolidColorBrush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.DarkSlateGray, TBLColor_Changed));

        /// <summary>
        /// Get or set the color of EndBlock in string panel.
        /// </summary>
        public SolidColorBrush TblEndBlockColor
        {
            get => (SolidColorBrush)GetValue(TblEndBlockColorProperty);
            set => SetValue(TblEndBlockColorProperty, value);
        }

        public static readonly DependencyProperty TblEndBlockColorProperty =
            DependencyProperty.Register(nameof(TblEndBlockColor), typeof(SolidColorBrush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.Blue, TBLColor_Changed));

        /// <summary>
        /// Get or set the color of EndBlock in string panel.
        /// </summary>
        public SolidColorBrush TblEndLineColor
        {
            get => (SolidColorBrush)GetValue(TblEndLineColorProperty);
            set => SetValue(TblEndLineColorProperty, value);
        }

        public static readonly DependencyProperty TblEndLineColorProperty =
            DependencyProperty.Register(nameof(TblEndLineColor), typeof(SolidColorBrush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.Blue, TBLColor_Changed));

        /// <summary>
        /// Get or set the color of EndBlock in string panel.
        /// </summary>
        public SolidColorBrush TblDefaultColor
        {
            get => (SolidColorBrush)GetValue(TblDefaultColorProperty);
            set => SetValue(TblDefaultColorProperty, value);
        }

        public static readonly DependencyProperty TblDefaultColorProperty =
            DependencyProperty.Register(nameof(TblDefaultColor), typeof(SolidColorBrush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.Black, TBLColor_Changed));

        #endregion Characters tables property/methods

        #region ReadOnly property/event
        public bool IsLockedFile => CheckIsOpen(_provider) && _provider.IsLockedFile;

        /// <summary>
        /// Put the control on readonly mode.
        /// </summary>
        public bool ReadOnlyMode
        {
            get => (bool)GetValue(ReadOnlyModeProperty);
            set => SetValue(ReadOnlyModeProperty, value);
        }

        public static readonly DependencyProperty ReadOnlyModeProperty =
            DependencyProperty.Register(nameof(ReadOnlyMode), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(false, (d, e) =>
                {
                    if (d is not HexEditorLegacy ctrl) return;
                    if (!CheckIsOpen(ctrl._provider)) return;
                    if (e.NewValue == e.OldValue) return;

                    ctrl._provider.ReadOnlyMode = ctrl._provider.IsLockedFile || (bool)e.NewValue;
                    ctrl.RefreshView(true);
                }));

        private void Provider_ReadOnlyChanged(object sender, EventArgs e)
        {
            if (!CheckIsOpen(_provider)) return;

            SetCurrentValue(ReadOnlyModeProperty, _provider.IsLockedFile || _provider.ReadOnlyMode);

            ReadOnlyChanged?.Invoke(sender, e);
        }

        #endregion ReadOnly property/event

        #region ByteModified methods/event/property
        /// <summary>
        /// Stream or file are modified when IsModified are set to true.
        /// </summary>
        public bool IsModified { get; internal set; }

        private void Control_ByteModified(object sender, ByteEventArgs e)
        {
            if (!CheckIsOpen(_provider)) return;
            if (_provider.ReadOnlyMode) return;

            if (sender is IByteControl ctrl)
            {
                ModifyByte(ctrl.Byte.Byte[e.Index], ctrl.BytePositionInStream + e.Index);

                e.BytePositionInStream = ctrl.BytePositionInStream;
            }

            BytesModified?.Invoke(this, e);
            _findReplaceService.ClearCache();  // Clear search cache after modification
        }

        private void Control_ByteDeleted(object sender, EventArgs e) => DeleteSelection();

        /// <summary>
        /// Add a bytemodified to this instance 
        /// it's not call the ByteModified event
        /// </summary>
        public void ModifyByte(byte? @byte, long bytePositionInStream, long undoLength = 1)
        {
            if (!_byteModificationService.ModifyByte(_provider, @byte, bytePositionInStream, undoLength, ReadOnlyMode))
                return;

            SetScrollMarker(bytePositionInStream, ScrollMarker.ByteModified);
            _findReplaceService.ClearCache();  // Clear search cache after modification
            UpdateByteModified();

            UpdateStatusBar();
        }

        #endregion ByteModified methods/event/methods

        #region Lines methods
        /// <summary>
        /// Obtain the max line for vertical scrollbar
        /// </summary>
        private long MaxLine
        {
            get
            {
                long byteDeletedCount = 0;
                if (CheckIsOpen(_provider) && HideByteDeleted)
                    byteDeletedCount = _provider.GetByteModifieds(ByteAction.Deleted).Count;

                return AllowVisualByteAddress
                          ? CheckIsOpen(_provider) ? ((VisualByteAdressLength - byteDeletedCount) / (BytePerLine * ByteSizeRatio)) + 1 : 0
                          : CheckIsOpen(_provider) ? ((_provider.Length - byteDeletedCount) / (BytePerLine * ByteSizeRatio)) + 1 : 0;
            }
        }

        /// <summary>
        /// Get the number of row visible in control
        /// </summary>
        private int MaxVisibleLine
        {
            get
            {
                var actualheight = BaseGrid.RowDefinitions[1].ActualHeight;

                if (actualheight < 0) actualheight = 0;

                return (int)(actualheight / (LineHeight * ZoomScale)) + 1;
            }
        }

        /// <summary>
        /// Obtain the number of line preloaded in the control
        /// </summary>
        public int MaxLinePreloaded => StringDataStackPanel.Children.Count;

        /// <summary>
        /// Get the maximum number of row visible in control
        /// </summary>
        private int MaxScreenVisibleLine
        {
            get
            {
                var actualheight = SystemParameters.PrimaryScreenHeight;

                if (actualheight < 0) actualheight = 0;

                return (int)(actualheight / (LineHeight * ZoomScale)) + 1;
            }
        }

        #endregion Lines methods

        #region Selection Property/Methods/Event

        /// <summary>
        /// Get the selected line of focus control
        /// </summary>
        public long SelectionLine
        {
            get => (long)GetValue(SelectionLineProperty);
            internal set => SetValue(SelectionLineProperty, value);
        }

        public static readonly DependencyProperty SelectionLineProperty =
            DependencyProperty.Register(nameof(SelectionLine), typeof(long), typeof(HexEditorLegacy), new FrameworkPropertyMetadata(0L));

        private void LinesInfoLabel_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not FastTextLine line || e.LeftButton != MouseButtonState.Pressed) return;

            var position = HexLiteralToLong((string)line.Tag).position;
            var deleted = CheckIsOpen(_provider)
                ? _provider.GetByteModifieds(ByteAction.Deleted).Where(b =>
                    b.Value.BytePositionInStream >= SelectionStart &&
                    b.Value.BytePositionInStream <= SelectionStop).Count()
                : 0;

            SelectionStop = GetValidPositionFrom(position + deleted, BytePerLine) - 1;
        }

        private void LinesInfoLabel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FastTextLine line) return;

            SelectionStart = HexLiteralToLong((string)line.Tag).position;
            SelectionStop = SelectionStart + BytePerLine - 1 - (HideByteDeleted
                                                                   ? 0 //TODO: add fix here...
                                                                   : GetColumnNumber(SelectionStart));

            HideCaret();
        }

        private void Control_EscapeKey(object sender, EventArgs e)
        {
            UnSelectAll();
            UnHighLightAll();
            Focus();
        }

        private void Control_KeyDown(object sender, KeyEventArgs e)
        {
            //TODO: need to fix... not occurs all times as needed.

            switch (e.Key)
            {
                case Key.Delete:
                    DeleteSelection();
                    break;
                case Key.Escape:
                    UnSelectAll();
                    UnHighLightAll();
                    Focus();
                    break;
            }
        }

        private void Control_MovePageUp(object sender, ByteEventArgs e)
        {
            //Prevent infinite loop
            _setFocusTest = false;

            //Get the new position from SelectionStart down one page
            var newPosition = GetValidPositionFrom(SelectionStart, -(BytePerLine * MaxVisibleLine));

            if (Keyboard.Modifiers == ModifierKeys.Shift)
                SelectionStart = newPosition < _provider.Length ? newPosition : 0;
            else
            {
                FixSelectionStartStop();

                if (newPosition > -1)
                    SelectionStart = SelectionStop = newPosition;
            }

            if (AllowVisualByteAddress && SelectionStart > VisualByteAdressStart)
                SelectionStart = VisualByteAdressStart;

            if (SelectionStart < FirstVisibleBytePosition)
                VerticalScrollBar.Value--;

            if (sender is HexByte || sender is StringByte)
            {
                VerticalScrollBar.Value -= MaxVisibleLine - 1;
                SetFocusAtSelectionStart();
            }
        }

        private void Control_MovePageDown(object sender, ByteEventArgs e)
        {
            //Prevent infinite loop
            _setFocusTest = false;

            //Get the new position from SelectionStart down one page
            var newPosition = GetValidPositionFrom(SelectionStart, BytePerLine * MaxVisibleLine);

            if (Keyboard.Modifiers == ModifierKeys.Shift)
                SelectionStart = newPosition < _provider.Length ? newPosition : _provider.Length;
            else
            {
                FixSelectionStartStop();

                if (newPosition < _provider.Length)
                    SelectionStart = SelectionStop = newPosition;
            }

            if (AllowVisualByteAddress && SelectionStart > VisualByteAdressStop)
                SelectionStart = VisualByteAdressStop;

            if (SelectionStart > LastVisibleBytePosition)
                VerticalScrollBar.Value++;

            if (sender is HexByte || sender is StringByte)
            {
                VerticalScrollBar.Value += MaxVisibleLine - 1;
                SetFocusAtSelectionStart();
            }
        }

        private void Control_MoveDown(object sender, ByteEventArgs e)
        {
            //Prevent infinite loop
            _setFocusTest = false;

            var newPosition = GetValidPositionFrom(SelectionStart, BytePerLine);

            if (Keyboard.Modifiers == ModifierKeys.Shift)
                SelectionStart = newPosition < _provider.Length ? newPosition : _provider.Length;
            else
            {
                FixSelectionStartStop();

                if (newPosition < _provider.Length)
                    SelectionStart = SelectionStop = newPosition;
            }

            if (AllowVisualByteAddress && SelectionStart > VisualByteAdressStop)
                SelectionStart = VisualByteAdressStop;

            if (SelectionStart > LastVisibleBytePosition)
                VerticalScrollBar.Value++;

            SetFocusAtSelectionStart();
        }

        private void Control_MoveUp(object sender, ByteEventArgs e)
        {
            //Prevent infinite loop
            _setFocusTest = false;

            //Get the new position from SelectionStart
            var newPosition = GetValidPositionFrom(SelectionStart, -BytePerLine);

            if (Keyboard.Modifiers == ModifierKeys.Shift)
                SelectionStart = newPosition > -1 ? newPosition : 0;
            else
            {
                FixSelectionStartStop();

                if (newPosition > -1)
                    SelectionStart = SelectionStop = newPosition;
            }

            if (AllowVisualByteAddress && SelectionStart < VisualByteAdressStart)
                SelectionStart = VisualByteAdressStart;

            if (SelectionStart < FirstVisibleBytePosition)
                VerticalScrollBar.Value--;

            SetFocusAtSelectionStart();
        }

        private void Control_MouseSelection(object sender, EventArgs e)
        {
            //Prevent false mouse selection on file open
            if (SelectionStart == -1) return;
            if (sender is not IByteControl bCtrl) return;

            var focusedControl = Keyboard.FocusedElement;

            //update selection
            SelectionStop = bCtrl.BytePositionInStream != -1
                ? bCtrl.BytePositionInStream
                : LastVisibleBytePosition;

            UpdateSelectionColor(focusedControl is HexByte ? FirstColor.HexByteData : FirstColor.StringByteData);
            UpdateSelection();
        }

        /// <summary>
        /// Set the start byte position of selection
        /// </summary>
        public long SelectionStart
        {
            get => (long)GetValue(SelectionStartProperty);
            set => SetValue(SelectionStartProperty, value);
        }

        public static readonly DependencyProperty SelectionStartProperty =
            DependencyProperty.Register(nameof(SelectionStart), typeof(long), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(-1L, 
                    (d, e) =>
                    {
                        if (d is not HexEditorLegacy ctrl) return;
                        if (e.NewValue == e.OldValue) return;
                        if (!CheckIsOpen(ctrl._provider)) return;

                        ctrl.SelectionByte = ctrl._provider.GetByte(ctrl.SelectionStart).singleByte;

                        ctrl.UpdateSelection();
                        ctrl.UpdateSelectionLine();
                        ctrl.UpdateVisual();
                        ctrl.UpdateStatusBar(false);
                        ctrl.UpdateLinesInfo();
                        ctrl.UpdateHeader(true);
                        ctrl.SetScrollMarker(0, ScrollMarker.SelectionStart);

                        ctrl.SelectionStartChanged?.Invoke(ctrl, EventArgs.Empty);
                        ctrl.SelectionLengthChanged?.Invoke(ctrl, EventArgs.Empty);
                    }, (d, baseValue) =>
                    {
                        if (d is not HexEditorLegacy ctrl) return -1L;

                        var (start, _) = ctrl._selectionService.ValidateSelection(ctrl._provider, (long)baseValue, ctrl.SelectionStop);
                        return start;
                    }));


        /// <summary>
        /// Set the end byte position of selection
        /// </summary>
        public long SelectionStop
        {
            get => (long)GetValue(SelectionStopProperty);
            set => SetValue(SelectionStopProperty, value);
        }

        public static readonly DependencyProperty SelectionStopProperty =
            DependencyProperty.Register(nameof(SelectionStop), typeof(long), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(-1L, 
                    (d, e) =>
                    {
                        if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

                        ctrl.UpdateSelection();
                        ctrl.UpdateSelectionLine();

                        ctrl.SelectionStopChanged?.Invoke(ctrl, EventArgs.Empty);
                        ctrl.SelectionLengthChanged?.Invoke(ctrl, EventArgs.Empty);
                    }, (d, baseValue) =>
                    {
                        if (d is not HexEditorLegacy ctrl) return baseValue;

                        var (_, stop) = ctrl._selectionService.ValidateSelection(ctrl._provider, ctrl.SelectionStart, (long)baseValue);
                        return stop;
                    }));

        /// <summary>
        /// Fix the selection start and stop when needed
        /// </summary>
        private void FixSelectionStartStop()
        {
            var (start, stop) = _selectionService.FixSelectionRange(SelectionStart, SelectionStop);
            SelectionStart = start;
            SelectionStop = stop;
        }

        /// <summary>
        /// Reset selection to -1
        /// </summary>
        public void UnSelectAll(bool cleanFocus = false)
        {
            SelectionStart = SelectionStop = -1;

            if (cleanFocus)
            {
                HideCaret();
                Focus();
            }
        }

        /// <summary>
        /// Select the entire file
        /// If file are closed the selection will be set to -1
        /// </summary>
        public void SelectAll()
        {
            if (CheckIsOpen(_provider))
            {
                SelectionStart = _selectionService.GetSelectAllStart(_provider);
                SelectionStop = _selectionService.GetSelectAllStop(_provider);
            }
            else
            {
                SelectionStart = -1;
                SelectionStop = -1;
            }

            UpdateSelection();
        }

        /// <summary>
        /// Get the length of byte are selected (base 1)
        /// </summary>
        public long SelectionLength => _selectionService.GetSelectionLength(SelectionStart, SelectionStop);

        /// <summary>
        /// Get byte array from current selection
        /// </summary>
        public byte[] GetSelectionByteArray()
        {
            return _selectionService.GetSelectionBytes(_provider, SelectionStart, SelectionStop, true);
        }

        /// <summary>
        /// Get string from current selection
        /// </summary>
        public string SelectionString
        {
            get
            {
                using var ms = new MemoryStream();
                CopyToStream(ms, true);
                return BytesToString(ms.ToArray());
            }
        }

        /// <summary>
        /// Get Hexadecimal from current selection
        /// </summary>
        public string SelectionHex
        {
            get
            {
                using var ms = new MemoryStream();
                CopyToStream(ms, true);
                return ByteToHex(ms.ToArray());
            }
        }

        private void Control_MoveRight(object sender, ByteEventArgs e)
        {
            //Prevent infinite loop
            _setFocusTest = false;

            //Get the new position from SelectionStart down one page
            var newPosition = GetValidPositionFrom(SelectionStart, 1);

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                SelectionStart = newPosition <= _provider.Length
                    ? GetValidPositionFrom(SelectionStart, 1)
                    : _provider.Length;
            }
            else
            {
                FixSelectionStartStop();

                if (newPosition < _provider.Length)
                    SelectionStart = SelectionStop = newPosition;
            }

            if (SelectionStart >= _provider.Length)
                SelectionStart = _provider.Length - 1;

            if (AllowVisualByteAddress && SelectionStart > VisualByteAdressStop)
                SelectionStart = VisualByteAdressStop;

            if (SelectionStart > LastVisibleBytePosition)
                VerticalScrollBar.Value++;

            SetFocusAtSelectionStart();
        }

        private void Control_MoveLeft(object sender, ByteEventArgs e)
        {
            //Prevent infinite loop
            _setFocusTest = false;

            //Get the new position from SelectionStart down one page
            var newPosition = GetValidPositionFrom(SelectionStart, -1);

            if (Keyboard.Modifiers == ModifierKeys.Shift)
                SelectionStart = newPosition > -1 ? newPosition : 0;
            else
            {
                FixSelectionStartStop();

                if (newPosition > -1)
                    SelectionStart = SelectionStop = newPosition;
            }

            if (SelectionStart < 0)
                SelectionStart = 0;

            if (AllowVisualByteAddress && SelectionStart < VisualByteAdressStart)
                SelectionStart = VisualByteAdressStart;

            if (SelectionStart < FirstVisibleBytePosition)
                VerticalScrollBar.Value--;

            SetFocusAtSelectionStart();
        }

        private void Control_MovePrevious(object sender, ByteEventArgs e)
        {
            UpdateByteModified();

            //Call move left event
            Control_MoveLeft(sender, e);
        }

        private void Control_MoveNext(object sender, ByteEventArgs e)
        {
            UpdateByteModified();

            //Call moveright event
            Control_MoveRight(sender, e);
        }

        #endregion Selection Property/Methods/Event

        #region Copy/Paste/Cut Methods

        /// <summary>
        /// Set or get the default copy to clipboard mode
        /// </summary>
        public CopyPasteMode DefaultCopyToClipboardMode
        {
            get => _clipboardService.DefaultCopyMode;
            set => _clipboardService.DefaultCopyMode = value;
        }

        /// <summary>
        /// Paste clipboard string without inserting byte at selection start
        /// </summary>
        /// <param name="expendIfneeded">Set AllowExpend to true for working</param>
        private void Paste(bool expendIfneeded)
        {
            if (!CheckIsOpen(_provider) || SelectionStart <= -1 || ReadOnlyMode) return;

            var clipBoardText = Clipboard.GetText();
            var (success, byteArray) = IsHexaByteStringValue(clipBoardText);

            #region Expend stream if needed

            var pastelength = success ? byteArray.Length : clipBoardText.Length;
            var needToBeExtent = _provider.Position + pastelength > _provider.Length;
            var expend = false;
            if (expendIfneeded && AllowExtend && needToBeExtent)
                if (AppendNeedConfirmation)
                {
                    if (MessageBox.Show(Properties.Resources.PasteExtendByteConfirmationString, ApplicationName,
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
                        expend = true;
                }
                else
                    expend = true;

            #endregion

            if (success)
                _provider.Paste(SelectionStart, byteArray, expend);
            else
                _provider.Paste(SelectionStart, clipBoardText, expend);

            SetScrollMarker(SelectionStart, ScrollMarker.ByteModified, Properties.Resources.PasteFromClipboardString);
            _findReplaceService.ClearCache();  // Clear search cache after modification
            RefreshView();

            BytesModified?.Invoke(this, new ByteEventArgs(SelectionStart));
        }

        /// <summary>
        /// Fill the selection with a Byte at selection start
        /// </summary>
        public void FillWithByte(byte val) => FillWithByte(SelectionStart, SelectionLength, val);

        /// <summary>
        /// Fill with a Byte at start position
        /// </summary>
        public void FillWithByte(long startPosition, long length, byte val)
        {
            _clipboardService.FillWithByte(_provider, startPosition, length, val, ReadOnlyMode);
            SetScrollMarker(SelectionStart, ScrollMarker.ByteModified, Properties.Resources.FillSelectionAloneString);
            _findReplaceService.ClearCache();  // Clear search cache after modification
            RefreshView();
        }

        /// <summary>
        /// Get all bytes from file or stream opened
        /// </summary>
        public byte[] GetAllBytes(bool copyChange)
        {
            return _clipboardService.GetAllBytes(_provider, copyChange);
        }

        /// <summary>
        /// Get all bytes from file or stream opened and copy change
        /// </summary>
        public byte[] GetAllBytes() => GetAllBytes(true);

        /// <summary>
        /// Return true if Copy method could be invoked.
        /// </summary>
        public bool CanCopy => _clipboardService.CanCopy(SelectionLength, _provider);

        /// <summary>
        /// Return true if delete method could be invoked.
        /// </summary>
        public bool CanDelete => _clipboardService.CanDelete(SelectionLength, _provider, ReadOnlyMode, AllowDeleteByte);

        /// <summary>
        /// Copy to clipboard with default CopyPasteMode.ASCIIString
        /// </summary>
        public void CopyToClipboard() => CopyToClipboard(DefaultCopyToClipboardMode);

        /// <summary>
        /// Copy to clipboard the current selection with actual change in control
        /// </summary>
        public void CopyToClipboard(CopyPasteMode copypastemode) =>
            CopyToClipboard(copypastemode, SelectionStart, SelectionStop, true, _tblService.CharacterTable);

        /// <summary>
        /// Copy to clipboard
        /// </summary>
        public void CopyToClipboard(CopyPasteMode copypastemode, long selectionStart, long selectionStop, bool copyChange, TblStream tbl)
        {
            if (!CanCopy) return;

            _clipboardService.CopyToClipboard(_provider, copypastemode, selectionStart, selectionStop, copyChange, tbl);
        }

        /// <summary>
        /// Copy selection to a stream
        /// </summary>
        /// <param name="output">Output stream is not closed after copy</param>
        /// <param name="copyChange">Copy change or not</param>
        public void CopyToStream(Stream output, bool copyChange) =>
            CopyToStream(output, SelectionStart, SelectionStop, copyChange);

        /// <summary>
        /// Copy selection to a stream
        /// </summary>
        /// <param name="output">Output stream is not closed after copy</param>
        /// <param name="selectionStart"></param>
        /// <param name="selectionStop"></param>
        /// <param name="copyChange"></param>
        public void CopyToStream(Stream output, long selectionStart, long selectionStop, bool copyChange)
        {
            if (!CanCopy) return;

            _clipboardService.CopyToStream(_provider, output, selectionStart, selectionStop, copyChange);
        }

        /// <summary>
        /// Return a byte array with the copy of data defined by selection start/stop
        /// </summary>
        public byte[] GetCopyData(long selectionStart, long selectionStop, bool copyChange)
        {
            if (!CanCopy) return null;

            return _clipboardService.GetCopyData(_provider, selectionStart, selectionStop, copyChange);
        }

        /// <summary>
        /// Occurs when data is copied in byteprovider instance
        /// </summary>
        private void Provider_DataCopied(object sender, EventArgs e) => DataCopied?.Invoke(sender, e);

        #endregion Copy/Paste/Cut Methods

        #region Position methods
        /// <summary>
        /// Get the line number of position in parameter
        /// </summary>
        public long GetLineNumber(long position) =>
            _positionService.GetLineNumber(position, ByteShiftLeft, HideByteDeleted,
                BytePerLine, ByteSizeRatio, _provider);

        /// <summary>
        /// Get the column number of the position
        /// </summary>
        public long GetColumnNumber(long position) =>
            _positionService.GetColumnNumber(position, HideByteDeleted, AllowVisualByteAddress,
                VisualByteAdressStart, ByteShiftLeft, BytePerLine, _provider);

        /// <summary>
        /// Set position of cursor
        /// </summary>
        /// <remarks>
        /// TODO: Need to be fixed on HideDeletedByte...
        /// </remarks>
        public void SetPosition(long position, long byteLength)
        {
            SelectionStart = position;
            SelectionStop = position + byteLength - 1;

            VerticalScrollBar.Value = CheckIsOpen(_provider) ? GetLineNumber(position) : 0;
        }

        /// <summary>
        /// Set position in control at position in parameter
        /// </summary>
        public void SetPosition(long position) => SetPosition(position, 0);

        /// <summary>
        /// Set position in control at position in parameter
        /// </summary>
        public void SetPosition(string hexLiteralPosition) =>
            SetPosition(HexLiteralToLong(hexLiteralPosition).position);

        /// <summary>
        /// Set position in control at position in parameter with specified selected length
        /// </summary>
        public void SetPosition(string hexLiteralPosition, long byteLength) =>
            SetPosition(HexLiteralToLong(hexLiteralPosition).position, byteLength);

        /// <summary>
        /// Give a next valid position
        /// </summary>
        /// <param name="position">Start position for compute the correction</param>
        /// <param name="positionCorrection">Positive or negative position number to add/substract from position</param>
        private long GetValidPositionFrom(long position, long positionCorrection) =>
            _positionService.GetValidPositionFrom(position, positionCorrection, CheckIsOpen(_provider) ? _provider : null);

        #endregion position methods

        #region Visibility property

        /// <summary>
        /// Set or Get value for change visibility of hexadecimal panel
        /// </summary>
        public Visibility HexDataVisibility
        {
            get => (Visibility)GetValue(HexDataVisibilityProperty);
            set => SetValue(HexDataVisibilityProperty, value);
        }

        public static readonly DependencyProperty HexDataVisibilityProperty =
            DependencyProperty.Register(nameof(HexDataVisibility), typeof(Visibility), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Visibility.Visible,
                    HexDataVisibility_PropertyChanged, Visibility_CoerceValue));

        private static object Visibility_CoerceValue(DependencyObject d, object baseValue) =>
            (Visibility)baseValue == Visibility.Hidden ? Visibility.Collapsed : (Visibility)baseValue;

        private static void HexDataVisibility_PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl) return;

            switch ((Visibility)e.NewValue)
            {
                case Visibility.Visible:
                    ctrl.HexDataStackPanel.Visibility = Visibility.Visible;

                    if (ctrl.HeaderVisibility == Visibility.Visible)
                        ctrl.HexHeaderStackPanel.Visibility = Visibility.Visible;
                    break;

                case Visibility.Collapsed:
                    ctrl.HexDataStackPanel.Visibility = Visibility.Collapsed;
                    ctrl.HexHeaderStackPanel.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        /// <summary>
        /// Set or Get value for change visibility of hexadecimal header
        /// </summary>
        public Visibility HeaderVisibility
        {
            get => (Visibility)GetValue(HeaderVisibilityProperty);
            set => SetValue(HeaderVisibilityProperty, value);
        }

        public static readonly DependencyProperty HeaderVisibilityProperty =
            DependencyProperty.Register(nameof(HeaderVisibility), typeof(Visibility), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Visibility.Visible,
                    HeaderVisibility_PropertyChanged, Visibility_CoerceValue));

        private static void HeaderVisibility_PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl) return;

            switch ((Visibility)e.NewValue)
            {
                case Visibility.Visible:
                    if (ctrl.HexDataVisibility == Visibility.Visible)
                    {
                        ctrl.HexHeaderStackPanel.Visibility = Visibility.Visible;
                        ctrl.TopRectangle.Visibility = Visibility.Visible;
                    }
                    break;

                case Visibility.Collapsed:
                    ctrl.HexHeaderStackPanel.Visibility = Visibility.Collapsed;
                    ctrl.TopRectangle.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        /// <summary>
        /// Set or Get value for change visibility of string panel
        /// </summary>
        public Visibility StringDataVisibility
        {
            get => (Visibility)GetValue(StringDataVisibilityProperty);
            set => SetValue(StringDataVisibilityProperty, value);
        }

        public static readonly DependencyProperty StringDataVisibilityProperty =
            DependencyProperty.Register(nameof(StringDataVisibility), typeof(Visibility), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Visibility.Visible,
                    StringDataVisibility_ValidateValue,
                    Visibility_CoerceValue));

        private static void StringDataVisibility_ValidateValue(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl) return;

            ctrl.StringDataStackPanel.Visibility = (Visibility)e.NewValue == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
            if ((Visibility)e.NewValue == Visibility.Visible)
            {
                ctrl.BarChartPanelVisibility = Visibility.Collapsed;
            }
            ctrl.RefreshView(true);
        }

        /// <summary>
        /// Set or Get value for change visibility of bar chart panel
        /// </summary>
        public Visibility BarChartPanelVisibility
        {
            get => (Visibility)GetValue(BarChartPanelVisibilityProperty);
            set => SetValue(BarChartPanelVisibilityProperty, value);
        }

        public static readonly DependencyProperty BarChartPanelVisibilityProperty =
            DependencyProperty.Register(nameof(BarChartPanelVisibility), typeof(Visibility), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Visibility.Collapsed,
                    BarChartPanelVisibility_ValidateValue, Visibility_CoerceValue));

        private static void BarChartPanelVisibility_ValidateValue(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl) return;

            if ((Visibility)e.NewValue != (Visibility)e.OldValue)
                ctrl.RefreshView(true);
        }

        /// <summary>
        /// Set or Get value for change visibility of status bar
        /// </summary>
        public Visibility StatusBarVisibility
        {
            get => (Visibility)GetValue(StatusBarVisibilityProperty);
            set => SetValue(StatusBarVisibilityProperty, value);
        }

        public static readonly DependencyProperty StatusBarVisibilityProperty =
            DependencyProperty.Register(nameof(StatusBarVisibility), typeof(Visibility), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Visibility.Visible,
                    StatusBarVisibility_ValueChange, Visibility_CoerceValue));

        private static void StatusBarVisibility_ValueChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl) return;

            ctrl.ReadOnlyLabel.Visibility =
            ctrl.StatusBarGrid.Visibility = ctrl.BottomRectangle.Visibility =
                (Visibility)e.NewValue == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            ctrl.RefreshView(true);
        }

        /// <summary>
        /// Set or Get value for change visibility of the lineinfo panel
        /// </summary>
        public Visibility LineInfoVisibility
        {
            get => (Visibility)GetValue(LineInfoVisibilityProperty);
            set => SetValue(LineInfoVisibilityProperty, value);
        }

        public static readonly DependencyProperty LineInfoVisibilityProperty =
            DependencyProperty.Register(nameof(LineInfoVisibility), typeof(Visibility), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Visibility.Visible,
                    LineInfoVisibility_ValueChange, Visibility_CoerceValue));

        private static void LineInfoVisibility_ValueChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl) return;

            ctrl.LinesInfoStackPanel.Visibility = (Visibility)e.NewValue == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;

            ctrl.RefreshView(true);
        }


        #endregion Visibility property

        #region Undo / Redo

        /// <summary>
        /// Clear undo and change
        /// </summary>
        public void ClearAllChange()
        {
            if (!CheckIsOpen(_provider)) return;

            _undoRedoService.ClearAll(_provider);
        }

        /// <summary>
        /// Make undo of last the last bytemodified
        /// TODO: Fixe when HideByteDeleted
        /// </summary>
        public void Undo(int repeat = 1)
        {
            UnSelectAll();

            if (!CheckIsOpen(_provider)) return;

            var position = _undoRedoService.Undo(_provider, repeat);

            RefreshView();

            //Update focus
            if (position >= 0)
            {
                if (!IsBytePositionAreVisible(position))
                    SetPosition(position);

                SetFocusAt(position);
            }

            Undone?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Make Redo of the last bytemodified
        /// </summary>
        public void Redo(int repeat = 1)
        {
            UnSelectAll();

            if (!CheckIsOpen(_provider)) return;

            var position = _undoRedoService.Redo(_provider, repeat);

            RefreshView();

            //Update focus
            if (position >= 0)
            {
                if (!IsBytePositionAreVisible(position))
                    SetPosition(position);

                SetFocusAt(position);
            }

            Redone?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clear the scroll marker when undone 
        /// </summary>
        /// <param name="sender">List of long representing position in file are undone</param>
        /// <param name="e"></param>
        private void Provider_Undone(object sender, EventArgs e)
        {
            switch (sender)
            {
                case List<long> bytePosition:
                    foreach (var position in bytePosition)
                        ClearScrollMarker(position);
                    break;
            }

            IsModified = _provider.UndoCount > 0;
            _findReplaceService.ClearCache();  // Clear search cache after undo
        }

        /// <summary>
        /// Get the undo count
        /// </summary>
        public long UndoCount => _undoRedoService.GetUndoCount(_provider);

        /// <summary>
        /// Get the undo count
        /// </summary>
        public long RedoCount => _undoRedoService.GetRedoCount(_provider);

        /// <summary>
        /// Get the undo stack
        /// </summary>
        public Stack<ByteModified> UndoStack => _undoRedoService.GetUndoStack(_provider);

        /// <summary>
        /// Get the Redo stack
        /// </summary>
        public Stack<ByteModified> RedoStack => _undoRedoService.GetRedoStack(_provider);

        #endregion Undo / Redo

        #region Open, Close, Save, byte provider ...

        /// <summary>
        /// Return true if a file/stream is load in the control
        /// </summary>
        public bool IsFileOrStreamLoaded
        {
            get => (bool)GetValue(IsFileOrStreamLoadedProperty);
            set => SetValue(IsFileOrStreamLoadedProperty, value);
        }

        public static readonly DependencyProperty IsFileOrStreamLoadedProperty =
            DependencyProperty.Register(nameof(IsFileOrStreamLoaded), typeof(bool),
                typeof(HexEditorLegacy), new PropertyMetadata(false));

        private void Provider_ChangesSubmited(object sender, EventArgs e)
        {
            if (sender is not ByteProviderLegacy bp) return;

            //Refresh filename
            var filename = bp.FileName;
            CloseProvider();
            FileName = filename;

            ChangesSubmited?.Invoke(this, EventArgs.Empty);
        }

        private void ProviderStream_ChangesSubmited(object sender, EventArgs e)
        {
            //Refresh stream
            if (!CheckIsOpen(_provider)) return;

            RefreshView(true);

            ChangesSubmited?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Set or Get the file with the control will show hex
        /// </summary>
        public string FileName
        {
            get => (string)GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register(nameof(FileName), typeof(string), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(string.Empty, FileName_PropertyChanged));

        private static void FileName_PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl) return;

            ctrl.OpenFile((string)e.NewValue);
            ctrl.IsFileOrStreamLoaded = true;
        }

        /// <summary>
        /// Set the Stream are used by ByteProviderLegacy
        /// </summary>
        public Stream Stream
        {
            get => (Stream)GetValue(StreamProperty);
            set => SetValue(StreamProperty, value);
        }

        public static readonly DependencyProperty StreamProperty =
            DependencyProperty.Register(nameof(Stream), typeof(Stream), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(null, Stream_PropertyChanged));

        private static void Stream_PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl) return;

            ctrl.CloseProvider();

            if (e.NewValue is not null)
            {
                ctrl.OpenStream((Stream)e.NewValue);
                ctrl.IsFileOrStreamLoaded = true;
            }
        }

        /// <summary>
        /// Get the length of byteprovider are opened in control
        /// </summary>
        public long Length => CheckIsOpen(_provider) ? _provider.Length : -1;

        /// <summary>
        /// Close file and clear control
        /// ReadOnlyMode is reset to false
        /// </summary>
        public void CloseProvider(bool clearFileName = true)
        {
            if (CheckIsOpen(_provider))
            {
                if (clearFileName)
                    FileName = string.Empty;

                _provider.Close();
                VerticalScrollBar.Value = 0;
            }

            UnHighLightAll();
            ClearAllScrollMarker();
            UnSelectAll();
            RefreshView();
            UpdateHeader(true);
            UpdateScrollBar();

            IsFileOrStreamLoaded = false;

            //Debug
            Debug.Print("PROVIDER CLOSED");
        }

        /// <summary>
        /// Save to the current stream/file
        /// </summary>
        public void SubmitChanges()
        {
            if (!CheckIsOpen(_provider) || _provider.ReadOnlyMode || !IsModified) return;

            _provider.SubmitChanges();
        }

        /// <summary>
        /// Save as to another file
        /// </summary>
        public void SubmitChanges(string newfilename, bool overwrite = false)
        {
            if (!CheckIsOpen(_provider) || _provider.ReadOnlyMode) return;

            _provider.SubmitChanges(newfilename, overwrite);
        }

        /// <summary>
        /// Open file
        /// </summary>
        private void OpenFile(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return;
            if (!File.Exists(filename)) return;

            CloseProvider(false);

            //Preload byte to MaxScreenVisibleLine
            if (PreloadByteInEditorMode == PreloadByteInEditor.MaxScreenVisibleLineAtDataLoad)
                BuildDataLines(MaxScreenVisibleLine);

            _provider = new ByteProviderLegacy(filename, ReadOnlyMode, CanInsertAnywhere);

            _provider.With(p =>
            {
                if (p.IsEmpty)
                {
                    CloseProvider(false);
                    return;
                }

                #region Attach event
                p.ReadOnlyChanged += Provider_ReadOnlyChanged;
                p.DataCopiedToClipboard += Provider_DataCopied;
                p.ChangesSubmited += Provider_ChangesSubmited;
                p.Undone += Provider_Undone;
                p.LongProcessChanged += Provider_LongProcessProgressChanged;
                p.LongProcessStarted += Provider_LongProcessProgressStarted;
                p.LongProcessCompleted += Provider_LongProcessProgressCompleted;
                p.LongProcessCanceled += Provider_LongProcessProgressCompleted;
                p.FillWithByteCompleted += Provider_FillWithByteCompleted;
                p.ReplaceByteCompleted += Provider_ReplaceByteCompleted;
                p.BytesAppendCompleted += Provider_BytesAppendCompleted;
                #endregion
            });

            UpdateScrollBar();
            UpdateHeader();
            UpdateStatusBar();
            RefreshView(true);

            UpdateTblBookMark();

            //Update count of byte on file open
            UpdateByteCount();

            //Debug
            Debug.Print("FILE OPENED");
        }

        /// <summary>
        /// Open stream
        /// </summary>
        private void OpenStream(Stream stream)
        {
            if (!stream.CanRead) return;

            CloseProvider();

            //Preload byte to MaxScreenVisibleLine
            if (PreloadByteInEditorMode == PreloadByteInEditor.MaxScreenVisibleLineAtDataLoad)
                BuildDataLines(MaxScreenVisibleLine);

            _provider = new ByteProviderLegacy(stream, CanInsertAnywhere);

            _provider.With(p =>
            {
                if (_provider.IsEmpty)
                {
                    CloseProvider();
                    return;
                }

                #region Attach event
                p.ReadOnlyChanged += Provider_ReadOnlyChanged;
                p.DataCopiedToClipboard += Provider_DataCopied;
                p.ChangesSubmited += ProviderStream_ChangesSubmited;
                p.Undone += Provider_Undone;
                p.LongProcessChanged += Provider_LongProcessProgressChanged;
                p.LongProcessStarted += Provider_LongProcessProgressStarted;
                p.LongProcessCompleted += Provider_LongProcessProgressCompleted;
                p.LongProcessCanceled += Provider_LongProcessProgressCompleted;
                p.FillWithByteCompleted += Provider_FillWithByteCompleted;
                p.ReplaceByteCompleted += Provider_ReplaceByteCompleted;
                p.BytesAppendCompleted += Provider_BytesAppendCompleted;
                #endregion
            });

            UpdateScrollBar();
            UpdateHeader();
            UpdateStatusBar();
            RefreshView(true);

            UpdateTblBookMark();

            //Update count of byte
            UpdateByteCount();

            //Debug
            Debug.Print("STREAM OPENED");
        }

        private void Provider_LongProcessProgressCompleted(object sender, EventArgs e)
        {
            CheckProviderIsOnProgress();

            //Launch event
            LongProcessProgressCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void Provider_LongProcessProgressStarted(object sender, EventArgs e)
        {
            CheckProviderIsOnProgress();

            //Launch event
            LongProcessProgressStarted?.Invoke(this, EventArgs.Empty);
        }

        private void Provider_LongProcessProgressChanged(object sender, EventArgs e)
        {
            //Update progress bar
            LongProgressProgressBar.Value = (double)sender;

            //Prevent freezing UI
            Application.Current.DoEvents();

            //Launch event
            LongProcessProgressChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Update scrollbar when append are completed
        /// </summary>
        private void Provider_BytesAppendCompleted(object sender, EventArgs e) => UpdateScrollBar();

        /// <summary>
        /// Invoke ReplaceByteCompleted event
        /// </summary>
        private void Provider_ReplaceByteCompleted(object sender, EventArgs e) =>
            ReplaceByteCompleted?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Invoke FillWithByteCompleted event
        /// </summary>
        private void Provider_FillWithByteCompleted(object sender, EventArgs e) =>
            FillWithByteCompleted?.Invoke(this, EventArgs.Empty);

        private void CancelLongProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckIsOpen(_provider)) return;

            _provider.IsOnLongProcess = false;
        }

        /// <summary>
        /// Check if byteprovider is on long progress and update control
        /// </summary>
        private void CheckProviderIsOnProgress()
        {
            bool enableCtrl;

            if (CheckIsOpen(_provider) && _provider.IsOnLongProcess)
            {
                CancelLongProcessButton.Visibility = Visibility.Visible;
                LongProgressProgressBar.Visibility = Visibility.Visible;
                enableCtrl = false;
            }
            else
            {
                CancelLongProcessButton.Visibility = Visibility.Collapsed;
                LongProgressProgressBar.Visibility = Visibility.Collapsed;
                enableCtrl = true;
            }

            //Enable/Disable controls
            TraverseHexAndStringBytes(ctrl => ctrl.IsEnabled = enableCtrl);
            TraverseLineInfo(ctrl => ctrl.IsEnabled = enableCtrl);
            TraverseHeader(ctrl => ctrl.IsEnabled = enableCtrl);
            TopRectangle.IsEnabled = BottomRectangle.IsEnabled = enableCtrl;
            VerticalScrollBar.IsEnabled = enableCtrl;
        }

        #endregion Open, Close, Save, byte provider ...

        #region Easy powerful traverses methods

        /// <summary>
        /// Used to make action on all visible hexbyte
        /// </summary>
        private void TraverseHexBytes(Action<HexByte> act, ref bool exit, bool force = false)
        {
            var visibleLine = MaxVisibleLine;
            var hexStackCount = HexDataStackPanel.Children.Count;
            var maxLines = force ? hexStackCount : Math.Min(visibleLine, hexStackCount);

            //HexByte panel - optimized with for loop to avoid enumerator allocation
            for (var lineIdx = 0; lineIdx < maxLines; lineIdx++)
            {
                if (!(HexDataStackPanel.Children[lineIdx] is StackPanel hexDataStack)) continue;

                var childCount = hexDataStack.Children.Count;
                for (var i = 0; i < childCount; i++)
                {
                    if (hexDataStack.Children[i] is HexByte hexCtrl)
                        act(hexCtrl);
                }

                if (exit) return;
            }
        }

        /// <summary>
        /// Used to make action on all visible hexbyte
        /// </summary>
        private void TraverseHexBytes(Action<HexByte> act)
        {
            var exit = false;
            TraverseHexBytes(act, ref exit);
        }

        /// <summary>
        /// Used to make action on all visible stringbyte
        /// </summary>
        private void TraverseStringBytes(Action<StringByte> act, ref bool exit, bool force = false)
        {
            var visibleLine = MaxVisibleLine;
            var stringStackCount = StringDataStackPanel.Children.Count;
            var maxLines = force ? stringStackCount : Math.Min(visibleLine, stringStackCount);

            //Stringbyte panel - optimized with for loop to avoid enumerator allocation
            for (var lineIdx = 0; lineIdx < maxLines; lineIdx++)
            {
                if (!(StringDataStackPanel.Children[lineIdx] is StackPanel stringDataStack)) continue;

                var childCount = stringDataStack.Children.Count;
                for (var i = 0; i < childCount; i++)
                {
                    if (stringDataStack.Children[i] is StringByte sbControl)
                        act(sbControl);
                }

                if (exit) return;
            }
        }

        /// <summary>
        /// Used to make action on all visible stringbyte
        /// </summary>
        private void TraverseStringBytes(Action<StringByte> act)
        {
            var exit = false;
            TraverseStringBytes(act, ref exit);
        }

        /// <summary>
        /// Used to make action on all visible hexbyte and stringbyte.
        /// </summary>
        private void TraverseHexAndStringBytes(Action<IByteControl> act, ref bool exit, bool force = false)
        {
            TraverseStringBytes(act, ref exit, force);
            TraverseHexBytes(act, ref exit, force);
        }

        /// <summary>
        /// Used to make action on all visible hexbyte and stringbyte.
        /// </summary>
        private void TraverseHexAndStringBytes(Action<IByteControl> act, bool force = false)
        {
            var exit = false;
            TraverseHexAndStringBytes(act, ref exit, force);
        }

        /// <summary>
        /// Used to make action on all visible lineinfos
        /// </summary>
        private void TraverseLineInfo(Action<FastTextLine> act)
        {
            var childCount = LinesInfoStackPanel.Children.Count;
            var maxLines = Math.Min(MaxVisibleLine, childCount);

            //lines infos panel - optimized with for loop
            for (var i = 0; i < maxLines; i++)
            {
                if (LinesInfoStackPanel.Children[i] is FastTextLine lineInfo)
                    act(lineInfo);
            }
        }

        /// <summary>
        /// Used to make action on all visible header
        /// </summary>
        private void TraverseHeader(Action<FastTextLine> act)
        {
            var childCount = HexHeaderStackPanel.Children.Count;
            var maxLines = Math.Min(MaxVisibleLine, childCount);

            //header panel - optimized with for loop
            for (var i = 0; i < maxLines; i++)
            {
                if (HexHeaderStackPanel.Children[i] is FastTextLine column)
                    act(column);
            }
        }

        /// <summary>
        /// Used to make action on ScrollMarker
        /// </summary>
        private void TraverseScrollMarker(Action<Rectangle> act, ref bool exit)
        {
            var children = MarkerGrid.Children;
            var count = children.Count;

            // Iterate in reverse order, caching the collection reference
            for (var i = count - 1; i >= 0; i--)
            {
                if (children[i] is Rectangle rect)
                    act(rect);

                if (exit) return;
            }
        }

        /// <summary>
        /// Used to make action on ScrollMarker
        /// </summary>
        private void TraverseScrollMarker(Action<Rectangle> act)
        {
            var exit = false;
            TraverseScrollMarker(act, ref exit);
        }

        #endregion Traverse methods

        #region BytePerLine property/methods

        /// <summary>
        /// Get or set the number of byte are show in control
        /// </summary>
        public int BytePerLine
        {
            get => (int)GetValue(BytePerLineProperty);
            set => SetValue(BytePerLineProperty, value);
        }

        public static readonly DependencyProperty BytePerLineProperty =
            DependencyProperty.Register(nameof(BytePerLine), typeof(int), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(16, (d, e) =>
                {
                    if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

                    ctrl.With(c =>
                    {
                        //Get previous state
                        var firstPos = c.FirstVisibleBytePosition;
                        var startPos = c.SelectionStart;
                        var stopPos = c.SelectionStop;

                        //refresh
                        c.UpdateScrollBar();
                        c.BuildDataLines(c.MaxVisibleLine, true);
                        c.RefreshView(true);
                        c.UpdateHeader(true);

                        //Set previous state
                        c.SetPosition(firstPos);
                        c.SelectionStart = startPos;
                        c.SelectionStop = stopPos;
                    });
                }, (_, baseValue) => (int)baseValue < 1 ? 1 : ((int)baseValue > 64 ? 64 : baseValue)));

        #endregion

        #region ByteWidth property/methods

        /// <summary>
        /// Get or set the width of string bytes
        /// </summary>
        public double StringByteWidth
        {
            get => (double)GetValue(StringByteWidthProperty);
            set => SetValue(StringByteWidthProperty, value);
        }

        public static readonly DependencyProperty StringByteWidthProperty =
            DependencyProperty.Register(nameof(StringByteWidth), typeof(double), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(10d, (d, e) =>
                {
                    if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

                    ctrl.With(c =>
                    {
                        //Get previous state
                        var firstPos = c.FirstVisibleBytePosition;
                        var startPos = c.SelectionStart;
                        var stopPos = c.SelectionStop;

                        //refresh
                        c.UpdateScrollBar();
                        c.BuildDataLines(c.MaxVisibleLine, true);
                        c.RefreshView(true);
                        c.UpdateHeader(true);

                        //Set previous state
                        c.SetPosition(firstPos);
                        c.SelectionStart = startPos;
                        c.SelectionStop = stopPos;
                    });
                }, (_, baseValue) => (double)baseValue < 1 ? 1 : ((double)baseValue > 64 ? 64 : baseValue)));

        #endregion

        #region vertical scrollbar property/methods

        /// <summary>
        /// Vertical scrollbar large change on click
        /// </summary>
        public double ScrollLargeChange
        {
            get => _scrollLargeChange;
            set
            {
                _scrollLargeChange = value;
                UpdateScrollBar();
            }
        }

        private void VerticalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RefreshView();

            VerticalScrollBarChanged?.Invoke(sender, new ByteEventArgs(FirstVisibleBytePosition));
        }

        /// <summary>
        /// Update vertical scrollbar with file info
        /// </summary>
        private void UpdateScrollBar() =>
            VerticalScrollBar.With(c =>
                {
                    c.Visibility = Visibility.Collapsed;

                    if (!CheckIsOpen(_provider)) return;

                    c.Visibility = Visibility.Visible;
                    c.SmallChange = 1;
                    c.LargeChange = ScrollLargeChange;
                    c.Maximum = MaxLine - MaxVisibleLine + 1;
                });
        #endregion

        #region General update methods / Refresh view

        public bool IsAutoRefreshOnResize { get; set; } = true;

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.HeightChanged || !IsAutoRefreshOnResize) return;

            if (!CheckIsOpen(_provider)) BuildDataLines(MaxVisibleLine);

            RefreshView(true);
        }

        /// <summary>
        /// Clear all IByteControl in hexeditor
        /// </summary>
        /// <param name="force">Set to true for clear all byte visible (set by MaxVisibleLine) or not visible in control.</param>
        private void ClearAllBytes(bool force = false) =>
            TraverseHexAndStringBytes(ctrl => { ctrl.Clear(); }, force);

        /// <summary>
        /// Clear all lines infos...
        /// </summary>
        private void ClearLineInfo() =>
            TraverseLineInfo(ctrl => { ctrl.Tag = ctrl.Text = string.Empty; });

        /// <summary>
        /// Optimized update that combines UpdateByteModified, UpdateSelection, UpdateHighLight and UpdateVisual in one traversal
        /// </summary>
        private void UpdateByteControlsOptimized()
        {
            if (!CheckIsOpen(_provider)) return;

            // Pre-calculate all values once
            var modifiedBytesDictionary = _provider.GetByteModifieds(ByteAction.All);
            var minSelect = SelectionStart <= SelectionStop ? SelectionStart : SelectionStop;
            var maxSelect = SelectionStart <= SelectionStop ? SelectionStop : SelectionStart;
            var hasHighlights = _highlightService.HasHighlights();

            // Single traversal for StringBytes
            var exit = false;
            TraverseStringBytes(ctrl =>
            {
                // Update byte modified
                if (modifiedBytesDictionary.TryGetValue(ctrl.BytePositionInStream, out var byteModified))
                {
                    ctrl.InternalChange = true;
                    if (byteModified.Byte.HasValue)
                        ctrl.Byte.ChangeByteValue(byteModified.Byte.Value, ctrl.BytePositionInStream);

                    if (byteModified.Action == ByteAction.Modified || byteModified.Action == ByteAction.Deleted)
                        ctrl.Action = byteModified.Action;

                    ctrl.InternalChange = false;
                }

                // Update selection
                ctrl.IsSelected = ctrl.BytePositionInStream >= minSelect &&
                                  ctrl.BytePositionInStream <= maxSelect &&
                                  ctrl.BytePositionInStream != -1 &&
                                  ctrl.Action != ByteAction.Deleted;

                // Update highlight
                ctrl.IsHighLight = hasHighlights && _highlightService.IsHighlighted(ctrl.BytePositionInStream);

                // Update visual
                ctrl.UpdateVisual();
            }, ref exit);

            // Single traversal for HexBytes
            exit = false;
            TraverseHexBytes(ctrl =>
            {
                // Update byte modified
                for (var i = 0; i < ByteSizeRatio; i++)
                {
                    if (modifiedBytesDictionary.TryGetValue(ctrl.BytePositionInStream + i, out var byteModified))
                    {
                        ctrl.InternalChange = true;
                        if (byteModified.Byte.HasValue)
                            ctrl.Byte.ChangeByteValue(byteModified.Byte.Value, ctrl.BytePositionInStream + i);

                        if (byteModified.Action == ByteAction.Modified || byteModified.Action == ByteAction.Deleted)
                            ctrl.Action = byteModified.Action;

                        ctrl.InternalChange = false;
                    }
                }

                // Update selection
                ctrl.IsSelected = ctrl.BytePositionInStream >= minSelect &&
                                  ctrl.BytePositionInStream <= maxSelect &&
                                  ctrl.BytePositionInStream != -1 &&
                                  ctrl.Action != ByteAction.Deleted;

                // Update highlight
                ctrl.IsHighLight = hasHighlights && _highlightService.IsHighlighted(ctrl.BytePositionInStream);

                // Update visual
                ctrl.UpdateVisual();
            }, ref exit);

            IsModified = _provider.UndoCount > 0;
        }

        /// <summary>
        /// Refresh currentview of hexeditor
        /// </summary>
        public void RefreshView(bool controlResize = false, bool refreshData = true)
        {
#if DEBUG
            var watch = new Stopwatch();
            watch.Start();
#endif
            UpdateLinesInfo();

            if (refreshData)
                UpdateViewers(controlResize);

            //Update visual of byte control - optimized: combines UpdateByteModified, UpdateSelection, UpdateHighLight, and UpdateVisual
            UpdateByteControlsOptimized();
            UpdateStatusBar(false);
            UpdateFocus();

            if (controlResize)
            {
                UpdateScrollMarkerPosition();
                UpdateHeader(true);
            }

#if DEBUG
            watch.Stop();
            Debug.Print($"REFRESH TIME: {watch.Elapsed.Milliseconds} ms");
#endif
        }

        /// <summary>
        /// Update the selection of byte in hexadecimal panel
        /// </summary>
        private void UpdateSelectionColor(FirstColor coloring)
        {
            switch (coloring)
            {
                case FirstColor.HexByteData:
                    TraverseHexBytes(ctrl => { ctrl.FirstSelected = true; });
                    TraverseStringBytes(ctrl => { ctrl.FirstSelected = false; });
                    _firstColor = FirstColor.HexByteData;
                    break;
                case FirstColor.StringByteData:
                    TraverseHexBytes(ctrl => { ctrl.FirstSelected = false; });
                    TraverseStringBytes(ctrl => { ctrl.FirstSelected = true; });
                    _firstColor = FirstColor.StringByteData;
                    break;
            }
        }

        /// <summary>
        /// Build the StringByte and HexByte control used byte hexeditor
        /// </summary>
        /// <param name="maxline">Number of line to build</param>
        /// <param name="rebuild">Rebuild data line</param>
        private void BuildDataLines(int maxline, bool rebuild = false)
        {
            var reAttachEvents = false;

            if (rebuild)
            {
                reAttachEvents = true;

                StringDataStackPanel.Children.Clear();
                HexDataStackPanel.Children.Clear();
            }

            for (var lineIndex = StringDataStackPanel.Children.Count; lineIndex < maxline; lineIndex++)
            {
                #region Build StringByte

                var dataLineStack = new StackPanel
                {
                    Height = LineHeight,
                    Orientation = Orientation.Horizontal
                };

                for (var i = 0; i < BytePerLine * ByteSizeRatio; i++)
                {
                    if (_tblService.CharacterTable == null && (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                                                       ByteSpacerPositioning == ByteSpacerPosition.StringBytePanel))
                        AddByteSpacer(dataLineStack, i);

                    new StringByte(this, BarChartPanelVisibility == Visibility.Visible, StringByteWidth).With(c =>
                    {
                        c.Clear();
                        dataLineStack.Children.Add(c);
                    });
                }
                StringDataStackPanel.Children.Add(dataLineStack);

                #endregion

                #region Build HexByte

                var hexaDataLineStack = new StackPanel
                {
                    Height = LineHeight,
                    Orientation = Orientation.Horizontal
                };

                for (var i = 0; i < BytePerLine; i++)
                {
                    if (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                        ByteSpacerPositioning == ByteSpacerPosition.HexBytePanel)
                        AddByteSpacer(hexaDataLineStack, i);

                    var byteControl = new HexByte(this);
                    byteControl.Clear();

                    hexaDataLineStack.Children.Add(byteControl);
                }

                HexDataStackPanel.Children.Add(hexaDataLineStack);

                #endregion

                reAttachEvents = true;
            }

            #region Attach/detach events to each IByteControl

            if (reAttachEvents)
                TraverseHexAndStringBytes(ctrl =>
                {
                    ctrl.With(c =>
                    {
                        #region Detach events
                        c.ByteModified -= Control_ByteModified;
                        c.MoveNext -= Control_MoveNext;
                        c.MovePrevious -= Control_MovePrevious;
                        c.MouseSelection -= Control_MouseSelection;
                        c.Click -= Control_Click;
                        c.DoubleClick -= Control_DoubleClick;
                        c.RightClick -= Control_RightClick;
                        c.MoveUp -= Control_MoveUp;
                        c.MoveDown -= Control_MoveDown;
                        c.MoveLeft -= Control_MoveLeft;
                        c.MoveRight -= Control_MoveRight;
                        c.MovePageDown -= Control_MovePageDown;
                        c.MovePageUp -= Control_MovePageUp;
                        c.ByteDeleted -= Control_ByteDeleted;
                        c.EscapeKey -= Control_EscapeKey;
                        c.CtrlaKey -= Control_CTRLAKey;
                        c.CtrlzKey -= Control_CTRLZKey;
                        c.CtrlcKey -= Control_CTRLCKey;
                        c.CtrlvKey -= Control_CTRLVKey;
                        c.CtrlyKey -= Control_CTRLYKey;
                        #endregion

                        #region Attach events
                        c.ByteModified += Control_ByteModified;
                        c.MoveNext += Control_MoveNext;
                        c.MovePrevious += Control_MovePrevious;
                        c.MouseSelection += Control_MouseSelection;
                        c.Click += Control_Click;
                        c.DoubleClick += Control_DoubleClick;
                        c.RightClick += Control_RightClick;
                        c.MoveUp += Control_MoveUp;
                        c.MoveDown += Control_MoveDown;
                        c.MoveLeft += Control_MoveLeft;
                        c.MoveRight += Control_MoveRight;
                        c.MovePageDown += Control_MovePageDown;
                        c.MovePageUp += Control_MovePageUp;
                        c.ByteDeleted += Control_ByteDeleted;
                        c.EscapeKey += Control_EscapeKey;
                        c.CtrlaKey += Control_CTRLAKey;
                        c.CtrlzKey += Control_CTRLZKey;
                        c.CtrlcKey += Control_CTRLCKey;
                        c.CtrlvKey += Control_CTRLVKey;
                        c.CtrlyKey += Control_CTRLYKey;

                        #endregion
                    });

                });

            #endregion
        }

        /// <summary>
        /// Update the data and string panels to current view.
        /// Only load what is needed in the view
        /// </summary>
        private void UpdateViewers(bool controlResize)
        {
            var curLevel = ++_priLevel;
            if (CheckIsOpen(_provider))
            {
                var bufferlength = MaxVisibleLine * (BytePerLine + 1 + ByteShiftLeft) * ByteSizeRatio;

                #region Build the buffer length if needed

                if (controlResize)
                {
                    if (_viewBuffer is not null)
                    {
                        BuildDataLines(MaxVisibleLine, MaxLinePreloaded < MaxVisibleLine);

                        if (_viewBuffer.Length < bufferlength)
                        {
                            _viewBuffer = new byte[bufferlength];
                            _viewBufferBytePosition = new long[bufferlength];
                        }
                    }
                    else
                    {
                        _viewBuffer = new byte[bufferlength];
                        _viewBufferBytePosition = new long[bufferlength];
                        BuildDataLines(MaxVisibleLine);
                    }
                }
                #endregion

                if (LinesInfoStackPanel.Children.Count == 0) return;

                var startPosition = FirstVisibleBytePosition;

                if (AllowVisualByteAddress && startPosition < VisualByteAdressStart)
                    startPosition = VisualByteAdressStart;

                #region Read the data from the provider and warns if necessary to load the bytes that have been deleted
                _provider.Position = startPosition;
                var readSize = 0;
                if (HideByteDeleted || CanInsertAnywhere)
                    for (var i = 0; i < _viewBuffer.Length; i++)
                    {
                        //BYTE INSERT ANYWHERE IS IN DEVELOPMENT!! ///////////
                        //DOES NOT WORK CLEANLY! BE PATIENT

                        // Issue #137 Optimization: Cache the check result instead of calling twice
                        var addedCheck = _provider.CheckIfIsByteModified(_provider.Position, ByteAction.Added);
                        if (addedCheck.success)
                        {
                            if (_provider.Eof) continue;

                            // Reuse cached result instead of calling CheckIfIsByteModified again
                            if (addedCheck.val.Byte != null)
                                _viewBuffer[readSize] = addedCheck.val.Byte.Value;
                            _viewBufferBytePosition[readSize] = addedCheck.val.BytePositionInStream;
                            _provider.Position++;
                            readSize++;
                        }
                        /////////////////////////////////////////////////////
                        else
                        {
                            // Issue #137 Optimization: Cache the check result
                            var (deletedSuccess, _) = _provider.CheckIfIsByteModified(_provider.Position, ByteAction.Deleted);
                            if (!deletedSuccess)
                            {
                                if (_provider.Eof) continue;

                                _viewBuffer[readSize] = (byte)_provider.ReadByte();
                                _viewBufferBytePosition[readSize] = _provider.Position - 1;
                                readSize++;
                            }
                            else
                            {
                                _viewBufferBytePosition[readSize] = -1;
                                _provider.Position++;
                                i--;
                            }
                        }
                    }
                else
                {
                    readSize = _provider.Read(_viewBuffer, 0, bufferlength <= _viewBuffer.Length
                        ? bufferlength
                        : _viewBuffer.Length);
                }
                #endregion

                var index = 0;

                #region HexByte panel refresh

                TraverseHexBytes(c =>
                {
                    c.Action = ByteAction.Nothing;
                    c.ReadOnlyMode = ReadOnlyMode;
                    c.InternalChange = true;

                    var nextPos = startPosition + index;

                    //Prevent load if byte are deleted from file
                    if (HideByteDeleted)
                        while (_provider.CheckIfIsByteModified(nextPos, ByteAction.Deleted).success)
                            nextPos++;

                    //Insert byte 
                    //if (CanInsertAnywhere)
                    //{
                    //    var (success, val) = _provider.CheckIfIsByteModified(nextPos, ByteAction.Added);
                    //}

                    if (index < readSize && _priLevel == curLevel)
                    {
                        c.Byte = ByteSize switch
                        {
                            ByteSizeType.Bit8 => new Byte_8bit(_viewBuffer[index]),
                            ByteSizeType.Bit16 => new Byte_16bit(new[] { _viewBuffer[index], _viewBuffer[index + 1] }),
                            ByteSizeType.Bit32 => new Byte_32bit(new[] { _viewBuffer[index], _viewBuffer[index + 1], _viewBuffer[index + 2], _viewBuffer[index + 3] }),
                            _ => throw new NotImplementedException()
                        };
                        c.BytePositionInStream = !HideByteDeleted ? nextPos : _viewBufferBytePosition[index];

                        if (AllowVisualByteAddress && nextPos > VisualByteAdressStop)
                            c.Clear();
                    }
                    else
                        c.Clear();

                    c.InternalChange = false;
                    index += ByteSizeRatio;
                });

                #endregion

                index = 0;

                #region StringByte / Barchart panel refresh

                var skipNextIsMte = false;
                TraverseStringBytes(c =>
                {
                    c.Action = ByteAction.Nothing;
                    c.ReadOnlyMode = ReadOnlyMode;
                    c.InternalChange = true;
                    c.TblCharacterTable = _tblService.CharacterTable;
                    c.TypeOfCharacterTable = TypeOfCharacterTable;

                    var nextPos = startPosition + index;

                    //Prevent load if byte are deleted from file
                    if (HideByteDeleted)
                        while (_provider.CheckIfIsByteModified(nextPos, ByteAction.Deleted).success)
                            nextPos++;

                    if (index < readSize)
                    {
                        if (!skipNextIsMte)
                        {
                            c.BytePositionInStream = !HideByteDeleted ? nextPos : _viewBufferBytePosition[index];

                            #region Load ByteNext for TBL MTE matching
                            if (_tblService.CharacterTable is not null)
                            {
                                var (singleByte, succes) = _provider.GetByte(c.BytePositionInStream + 1);
                                c.ByteNext = succes ? singleByte : null;
                            }
                            #endregion

                            //update byte
                            c.Byte = new Byte_8bit(_viewBuffer[index]);

                            //Bar chart value
                            c.PercentValue = _viewBuffer[index] * 100 / 256;

                            skipNextIsMte = c.IsMTE;

                            if (AllowVisualByteAddress && nextPos > VisualByteAdressStop)
                                c.Clear();
                        }
                        else
                        {
                            skipNextIsMte = false;
                            c.Clear();
                        }
                    }
                    else
                        c.Clear();

                    c.InternalChange = false;
                    index++;
                });

                #endregion
            }
            else
            {
                //Clear IByteControl
                _viewBuffer = null;
                ClearAllBytes();
            }
        }

        /// <summary>
        /// Update byte are modified
        /// </summary>
        private void UpdateByteModified()
        {
            if (!CheckIsOpen(_provider)) return;

            var modifiedBytesDictionary =
                _provider.GetByteModifieds(ByteAction.All);

            var exit = false;
            TraverseStringBytes(ctrl =>
            {
                if (modifiedBytesDictionary.TryGetValue(ctrl.BytePositionInStream, out var byteModified))
                {
                    ctrl.InternalChange = true;
                    if (byteModified.Byte.HasValue)
                    {
                        ctrl.Byte.ChangeByteValue(byteModified.Byte.Value, ctrl.BytePositionInStream);
                    }

                    if (byteModified.Action == ByteAction.Modified || byteModified.Action == ByteAction.Deleted)
                        ctrl.Action = byteModified.Action;

                    ctrl.InternalChange = false;
                }
            }, ref exit);

            TraverseHexBytes(ctrl =>
            {
                for (var i = 0; i < ByteSizeRatio; i++)
                {
                    if (modifiedBytesDictionary.TryGetValue(ctrl.BytePositionInStream + i, out var byteModified))
                    {
                        ctrl.InternalChange = true;
                        if (byteModified.Byte.HasValue)
                        {
                            ctrl.Byte.ChangeByteValue(byteModified.Byte.Value, ctrl.BytePositionInStream + i);
                            //ctrl.Byte.Byte = new List<byte> { byteModified.Byte.Value };
                        }

                        if (byteModified.Action == ByteAction.Modified || byteModified.Action == ByteAction.Deleted)
                            ctrl.Action = byteModified.Action;

                        ctrl.InternalChange = false;
                    }
                }

            }, ref exit);

            IsModified = _provider.UndoCount > 0;
        }

        /// <summary>
        /// Update the selection of byte
        /// </summary>
        private void UpdateSelection()
        {
            var minSelect = SelectionStart <= SelectionStop ? SelectionStart : SelectionStop;
            var maxSelect = SelectionStart <= SelectionStop ? SelectionStop : SelectionStart;

            TraverseHexAndStringBytes(ctrl =>
            {
                ctrl.IsSelected = ctrl.BytePositionInStream >= minSelect &&
                                  ctrl.BytePositionInStream <= maxSelect &&
                                  ctrl.BytePositionInStream != -1
&& ctrl.Action != ByteAction.Deleted;
            });
        }
        /// <summary>
        /// Update de SelectionLine property
        /// </summary>
        private void UpdateSelectionLine() =>
            SelectionLine = CheckIsOpen(_provider)
                ? GetLineNumber(SelectionStart)
                : 0;


        /// <summary>
        /// Update bytes as marked on findall()
        /// </summary>
        private void UpdateHighLight()
        {
            if (_highlightService.HasHighlights())
                TraverseHexAndStringBytes(ctrl => ctrl.IsHighLight = _highlightService.IsHighlighted(ctrl.BytePositionInStream));
            else //Un highlight all
                TraverseHexAndStringBytes(ctrl => ctrl.IsHighLight = false);
        }

        /// <summary>
        /// Update the position info panel at left of the control
        /// </summary>
        private void UpdateHeader(bool clear = false)
        {
            //Clear before refresh
            if (clear) HexHeaderStackPanel.Children.Clear();

            if (!CheckIsOpen(_provider)) return;

            for (var i = HexHeaderStackPanel.Children.Count; i < BytePerLine; i++)
            {
                if (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                    ByteSpacerPositioning == ByteSpacerPosition.HexBytePanel)
                    AddByteSpacer(HexHeaderStackPanel, i, true);

                var hlHeader = HighLightSelectionStart &&
                               GetColumnNumber(SelectionStart) == i &&
                               SelectionStart > -1;

                //Create control
                var headerLabel = new FastTextLine(this)
                {
                    Height = LineHeight,
                    AutoWidth = false,
                    FontWeight = hlHeader ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = hlHeader ? ForegroundHighLightOffSetHeaderColor : ForegroundOffSetHeaderColor,
                    RenderPoint = new Point(2, 2),
                };

                #region Set text visual of header

                switch (DataStringVisual)
                {
                    case DataVisualType.Hexadecimal:
                        headerLabel.Text = ByteToHex((byte)i);
                        break;
                    case DataVisualType.Decimal:
                        headerLabel.Text = i.ToString("d3");
                        break;
                    case DataVisualType.Binary:
                        headerLabel.Text = Convert.ToString(i, 2).PadLeft(8, '0');
                        break;
                }
                headerLabel.Width = HexByte.CalculateCellWidth(ByteSize, DataStringVisual, DataStringState);
                #endregion

                //Add to stackpanel
                HexHeaderStackPanel.Children.Add(headerLabel);
            }
        }

        /// <summary>
        /// Update the position info panel at left of the control
        /// </summary>
        private void UpdateLinesInfo()
        {
            var maxVisibleLine = MaxVisibleLine;

            #region If the lines are less than "visible lines" create them

            var linesCount = LinesInfoStackPanel.Children.Count;

            if (linesCount < maxVisibleLine)
                for (var i = 0; i < maxVisibleLine - linesCount; i++)
                    new FastTextLine(this).With(l =>
                    {
                        l.Height = LineHeight;
                        l.Foreground = ForegroundOffSetHeaderColor;
                        l.HorizontalAlignment = HorizontalAlignment.Left;
                        l.VerticalAlignment = VerticalAlignment.Center;
                        l.RenderPoint = new Point(2, 2);

                        //Events
                        l.MouseDown += LinesInfoLabel_MouseDown;
                        l.MouseMove += LinesInfoLabel_MouseMove;

                        LinesInfoStackPanel.Children.Add(l);
                    });

            #endregion

            ClearLineInfo();

            if (!CheckIsOpen(_provider)) return;

            var firstByteInLine = FirstVisibleBytePosition;
            long actualPosition = 0;

            for (var i = 0; i < maxVisibleLine; i++)
            {
                var lineOffsetLabel = (FastTextLine)LinesInfoStackPanel.Children[i];

                lineOffsetLabel.With(l =>
                {

                    if (i > 0) firstByteInLine = GetValidPositionFrom(firstByteInLine, BytePerLine * ByteSizeRatio);
                    var endLine = firstByteInLine + (BytePerLine * ByteSizeRatio) - 1;
                    #region Set text visual
                    if (HighLightSelectionStart &&
                        SelectionStart > -1 &&
                        SelectionStart >= firstByteInLine &&
                        SelectionStart <= endLine)
                    {
                        l.FontWeight = FontWeights.Bold;
                        l.Foreground = ForegroundHighLightOffSetHeaderColor;
                        l.ToolTip = $"{Properties.Resources.FirstByteString} : {SelectionStart}";
                        l.Tag = $"0x{LongToHex(SelectionStart).ToUpperInvariant()}";
                        actualPosition = SelectionStart;
                    }
                    else
                    {
                        l.FontWeight = FontWeights.Normal;
                        l.Foreground = ForegroundOffSetHeaderColor;
                        l.ToolTip = $"{Properties.Resources.FirstByteString} : {firstByteInLine}";
                        l.Tag = $"0x{LongToHex(firstByteInLine).ToUpperInvariant()}";

                        actualPosition = firstByteInLine;
                    }

                    actualPosition += OffsetBase;

                    //update the visual
                    switch (OffSetStringVisual)
                    {
                        case DataVisualType.Hexadecimal:
                            l.Text = OffSetPanelVisual switch
                            {
                                OffSetPanelType.OffsetOnly => $"0x{LongToHex(actualPosition, OffSetPanelFixedWidthVisual).ToUpperInvariant()}",
                                OffSetPanelType.LineOnly => $"ln {LongToHex(GetLineNumber(actualPosition), OffSetPanelFixedWidthVisual).ToUpperInvariant()}",
                                OffSetPanelType.Both => $"ln {LongToHex(GetLineNumber(actualPosition), OffSetPanelFixedWidthVisual)} 0x{LongToHex(actualPosition, OffSetPanelFixedWidthVisual).ToUpperInvariant()}",
                                _ => throw new NotImplementedException()
                            };
                            break;
                        case DataVisualType.Decimal:
                            var format = OffSetPanelFixedWidthVisual == OffSetPanelFixedWidth.Dynamic ? "G" : "D8";

                            l.Text = OffSetPanelVisual switch
                            {
                                OffSetPanelType.OffsetOnly => $"d{actualPosition.ToString(format)}",
                                OffSetPanelType.LineOnly => $"ln {GetLineNumber(actualPosition).ToString(format)}",
                                OffSetPanelType.Both => $"ln {GetLineNumber(actualPosition).ToString(format)} d{actualPosition.ToString(format)}",
                                _ => throw new NotImplementedException()
                            };
                            break;
                        case DataVisualType.Binary:
                            format = OffSetPanelFixedWidthVisual == OffSetPanelFixedWidth.Dynamic ? "G" : "D8";

                            l.Text = OffSetPanelVisual switch
                            {
                                OffSetPanelType.OffsetOnly => $"d{actualPosition.ToString(format)}",
                                OffSetPanelType.LineOnly => $"ln {GetLineNumber(actualPosition).ToString(format)}",
                                OffSetPanelType.Both => $"ln {GetLineNumber(actualPosition).ToString(format)} d{actualPosition.ToString(format)}",
                                _ => throw new NotImplementedException()
                            };
                            break;
                    }

                    if (AllowVisualByteAddress && firstByteInLine > VisualByteAdressStop)
                        l.Tag = l.Text = string.Empty;
                });
                #endregion
            }
        }
        #endregion Update view

        #region First/Last visible byte methods
        /// <summary>
        /// Get first visible byte position in control
        /// TODO: fix the first visible byte when HideByteDeleted are activated... 95% completed
        /// </summary>
        private long FirstVisibleBytePosition =>
            _positionService.GetFirstVisibleBytePosition((long)VerticalScrollBar.Value,
                BytePerLine, ByteShiftLeft, ByteSizeRatio, HideByteDeleted, AllowVisualByteAddress,
                VisualByteAdressStart, _provider);

        /// <summary>
        /// Get the number of byte are deleted before the position in parameter
        /// </summary>
        private long GetCountOfByteDeletedBeforePosition(long position) =>
            _positionService.GetCountOfByteDeletedBeforePosition(position, CheckIsOpen(_provider) ? _provider : null);

        /// <summary>
        /// Return true if SelectionStart are visible in control
        /// </summary>
        public bool SelectionStartIsVisible => IsBytePositionAreVisible(SelectionStart);

        /// <summary>
        /// Return true if the byteposition are visible in viewer
        /// </summary>
        public bool IsBytePositionAreVisible(long bytePosition) =>
            _positionService.IsBytePositionVisible(bytePosition, FirstVisibleBytePosition, LastVisibleBytePosition);

        /// <summary>
        /// Get last visible byte position in control
        /// </summary>
        private long LastVisibleBytePosition
        {
            get
            {
                long lastByte = 0;

                TraverseHexBytes(ctrl =>
                {
                    if (ctrl.BytePositionInStream != -1)
                        lastByte = ctrl.BytePositionInStream;
                });

                return lastByte;
            }
        }
        #endregion First/Last visible byte methods

        #region Focus Methods
        /// <summary>
        /// Update the focus to selection start
        /// </summary>
        public void UpdateFocus()
        {
            if (SelectionStartIsVisible)
                SetFocusAtSelectionStart();
            else
                try //sometimes crash in AvalonDock
                {
                    Focus();
                }
                catch
                {
                    // ignored
                }
        }

        /// <summary>
        /// Set the focus to the selection start
        /// </summary>
        public void SetFocusAtSelectionStart() => SetFocusAt(SelectionStart);

        /// <summary>
        /// Set focus at position in parameter
        /// </summary>
        private void SetFocusAt(long bytePositionInStream)
        {
            if (!CheckIsOpen(_provider)) return;
            if (bytePositionInStream >= _provider.Length) return;

            var rtn = false;

            #region Set focus in data panel
            switch (_firstColor)
            {
                case FirstColor.HexByteData:
                    TraverseHexBytes(ctrl =>
                    {
                        if (ctrl.BytePositionInStream == bytePositionInStream)
                        {
                            ctrl.Focus();
                            rtn = true;
                        }
                    }, ref rtn);
                    break;
                case FirstColor.StringByteData:
                    TraverseStringBytes(ctrl =>
                    {
                        if (ctrl.BytePositionInStream == bytePositionInStream)
                        {
                            ctrl.Focus();
                            rtn = true;
                        }
                    }, ref rtn);
                    break;
            }
            #endregion

            if (rtn) return;

            if (VerticalScrollBar.Value < VerticalScrollBar.Maximum && !_setFocusTest)
            {
                _setFocusTest = true;
                VerticalScrollBar.Value++;
            }

            if (!SelectionStartIsVisible && SelectionLength == 1)
                SetPosition(SelectionStart, 1);
        }

        #endregion Focus Methods

        #region Find/replace methods

        /// <summary>
        /// Find first occurence of string in stream. Search start as startPosition.
        /// </summary>
        public long FindFirst(string text, long startPosition = 0) =>
            FindFirst(StringToByte(text), startPosition);

        /// <summary>
        /// Find first occurence of byte[] in stream. Search start as startPosition. - OPTIMIZED
        /// </summary>
        public long FindFirst(byte[] data, long startPosition = 0, bool highLight = false)
        {
            if (data == null) return -1;

            UnHighLightAll();

            try
            {
                var position = _findReplaceService.FindFirst(_provider, data, startPosition);

                if (position == -1)
                {
                    UnSelectAll();
                    UnHighLightAll();
                    return -1;
                }

                SetPosition(position, data.Length);

                if (!highLight) return position;

                AddHighLight(position, data.Length, false);

                SetScrollMarker(position, ScrollMarker.SearchHighLight);

                SelectionStart = position;
                UpdateHighLight();
                return position;
            }
            catch
            {
                UnSelectAll();
                UnHighLightAll();
                return -1;
            }
        }

        /// <summary>
        /// Find next occurence of string in stream search start at SelectionStart.
        /// </summary>
        public long FindNext(string text) =>
            FindNext(StringToByte(text));

        /// <summary>
        /// Find next occurence of byte[] in stream search start at SelectionStart.
        /// </summary>
        public long FindNext(byte[] data, bool highLight = false) =>
            FindFirst(data, SelectionStart + 1, highLight);

        /// <summary>
        /// Find last occurence of string in stream search start at SelectionStart.
        /// </summary>
        public long FindLast(string text) => FindLast(StringToByte(text));

        /// <summary>
        /// Find last occurence of byte[] in stream. - OPTIMIZED
        /// </summary>
        /// <returns>Return the position</returns>
        public long FindLast(byte[] data, bool highLight = false)
        {
            if (data == null) return -1;

            UnHighLightAll();

            try
            {
                var position = _findReplaceService.FindLast(_provider, data, SelectionStart);

                if (position == -1)
                {
                    UnSelectAll();
                    UnHighLightAll();
                    return -1;
                }

                SetPosition(position, data.Length);

                if (!highLight) return position;

                AddHighLight(position, data.Length, false);

                SetScrollMarker(position, ScrollMarker.SearchHighLight);

                SelectionStart = position;
                UpdateHighLight();
                return position;
            }
            catch
            {
                UnSelectAll();
                UnHighLightAll();
                return -1;
            }
        }

        /// <summary>
        /// Find all occurence of string in stream.
        /// </summary>
        /// <returns>Return null if no occurence found</returns>
        public IEnumerable<long> FindAll(string text) => FindAll(StringToByte(text));

        /// <summary>
        /// Find all occurence of byte[] in stream.
        /// </summary>
        /// <returns>Return null if no occurence found</returns>
        public IEnumerable<long> FindAll(byte[] data)
        {
            if (data == null) return null;

            UnHighLightAll();

            return _findReplaceService.FindAll(_provider, data);
        }

        /// <summary>
        /// Find all occurence of string in stream.
        /// </summary>
        /// <returns>Return null if no occurence found</returns>
        public IEnumerable<long> FindAll(string text, bool highLight) =>
            FindAll(StringToByte(text), highLight);

        /// <summary>
        /// Find all occurence of string in stream. Highlight occurance in stream is MarcAll as true - OPTIMIZED
        /// </summary>
        /// <returns>Return null if no occurence found</returns>
        public IEnumerable<long> FindAll(byte[] data, bool highLight)
        {
            if (data == null) return null;

            ClearScrollMarker(ScrollMarker.SearchHighLight);

            if (highLight)
            {
                // Use cached FindAll from service
                var positions = _findReplaceService.FindAllCached(_provider, data);

                if (positions == null) return null;

                var findAll = positions as IList<long> ?? positions.ToList();

                // OPTIMIZED: Batch operations with BeginInit/EndInit for better performance
                MarkerGrid.BeginInit();

                try
                {
                    // Add all highlights and markers in a batch
                    foreach (var position in findAll)
                    {
                        AddHighLight(position, data.Length, false);
                        SetScrollMarker(position, ScrollMarker.SearchHighLight);
                    }
                }
                finally
                {
                    MarkerGrid.EndInit();
                }

                UnSelectAll();
                UpdateHighLight();

                return findAll;
            }

            return FindAll(data);
        }

        /// <summary>
        /// Find all occurence of SelectionByteArray in stream. Highlight byte finded
        /// </summary>
        /// <returns>Return null if no occurence found</returns>
        public IEnumerable<long> FindAllSelection(bool highLight) =>
            SelectionLength > 0
                ? FindAll(GetSelectionByteArray(), highLight)
                : null;

        /// <summary>
        /// Replace byte with another at selection position
        /// </summary>
        public void ReplaceByte(byte original, byte replace) =>
            ReplaceByte(SelectionStart, SelectionLength, original, replace);

        /// <summary>
        /// Replace byte with another at start position
        /// </summary>
        public void ReplaceByte(long startPosition, long length, byte original, byte replace)
        {
            _findReplaceService.ReplaceByte(_provider, startPosition, length, original, replace, ReadOnlyMode);
            SetScrollMarker(SelectionStart, ScrollMarker.ByteModified, Properties.Resources.ReplaceWithByteString);
            _findReplaceService.ClearCache();  // Clear search cache after modification
            RefreshView();
        }

        /// <summary>
        /// Replace the first byte array define by findData in byteprovider at start position. 
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceFirst(byte[] findData, byte[] replaceData, long startPosition = 0, bool hightlight = false) => 
            ReplaceFirst(findData, replaceData, true, startPosition, hightlight);

        /// <summary>
        /// Replace the first byte array define by findData in byteprovider at start position.
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceFirst(byte[] findData, byte[] replaceData, bool truckLength = true, long startPosition = 0, bool hightlight = false)
        {
            if (findData == null || replaceData == null) return -1;

            var position = _findReplaceService.ReplaceFirst(_provider, findData, replaceData, startPosition, truckLength, ReadOnlyMode);

            if (position > -1)
            {
                // UI logic
                SetScrollMarker(position, ScrollMarker.ByteModified);

                if (hightlight)
                    AddHighLight(position, findData.Length, false);

                UnSelectAll();
                _findReplaceService.ClearCache();  // Clear search cache after modification
                RefreshView();

                return position;
            }

            return -1;
        }

        /// <summary>
        /// Replace the first byte array define by findData in byteprovider at SelectionStart. Start the search at SelectionStart. 
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceFirst(byte[] findData, byte[] replaceData, bool truckLength = true, bool hightlight = false) =>
            ReplaceFirst(findData, replaceData, truckLength, SelectionStart, hightlight);

        /// <summary>
        /// Replace the first byte array define by findData in byteprovider at SelectionStart. 
        /// Start the search at SelectionStart. 
        /// No highlight
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceFirst(byte[] findData, byte[] replaceData, bool truckLength = true) =>
            ReplaceFirst(findData, replaceData, truckLength, SelectionStart);

        /// <summary>
        /// Replace the first byte array define by findData in byteprovider at SelectionStart. 
        /// Start the search at SelectionStart. 
        /// No highlight
        /// Truck replace data to length of findData
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceFirst(byte[] findData, byte[] replaceData) =>
            ReplaceFirst(findData, replaceData, true, SelectionStart);

        /// <summary>
        /// Replace the first string define by find in byteprovider at SelectionStart. Start the search at SelectionStart. 
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceFirst(string find, string replace, bool truckLength = true, bool hightlight = false) =>
            ReplaceFirst(StringToByte(find), StringToByte(replace), truckLength, SelectionStart, hightlight);

        /// <summary>
        /// Replace the first string define by find in byteprovider at SelectionStart. 
        /// Start the search at SelectionStart. 
        /// No highlight
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>        
        public long ReplaceFirst(string find, string replace, bool truckLength = true) =>
            ReplaceFirst(StringToByte(find), StringToByte(replace), truckLength, SelectionStart);

        /// <summary>
        /// Replace the first string define by find in byteprovider at start position. 
        /// Start the search at SelectionStart. 
        /// No highlight
        /// Truck replace data to length of findData
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>        
        public long ReplaceFirst(string find, string replace) =>
            ReplaceFirst(StringToByte(find), StringToByte(replace), true, SelectionStart);

        /// <summary>
        /// Replace the next byte array define by findData in byteprovider at SelectionStart. 
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceNext(byte[] findData, byte[] replaceData, bool truckLength = true, bool hightlight = false) =>
            ReplaceFirst(findData, replaceData, truckLength, SelectionStart + 1, hightlight);

        /// <summary>
        /// Replace the next byte array define by findData in byteprovider at SelectionStart. 
        /// No highlight
        /// Truck replace data to length of findData
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceNext(byte[] findData, byte[] replaceData) =>
            ReplaceFirst(findData, replaceData, true, SelectionStart + 1);

        /// <summary>
        /// Replace the next byte array define by findData in byteprovider at SelectionStart. 
        /// No highlight
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceNext(byte[] findData, byte[] replaceData, bool truckLength = true) =>
            ReplaceFirst(findData, replaceData, truckLength, SelectionStart + 1);

        /// <summary>
        /// Replace the next string define by find in byteprovider at SelectionStart. 
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceNext(string find, string replace, bool truckLength = true, bool hightlight = false) =>
            ReplaceFirst(StringToByte(find), StringToByte(replace), truckLength, SelectionStart + 1, hightlight);

        /// <summary>
        /// Replace the next string define by find in byteprovider at SelectionStart. 
        /// No highlight
        /// Truck replace data to length of findData
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceNext(string find, string replace) =>
            ReplaceFirst(StringToByte(find), StringToByte(replace), true, SelectionStart + 1);

        /// <summary>
        /// Replace the next string define by find in byteprovider at SelectionStart. 
        /// No highlight
        /// </summary>
        /// <returns>Return the position of replace. Return -1 on error/no replace</returns>
        public long ReplaceNext(string find, string replace, bool truckLength = true) =>
            ReplaceFirst(StringToByte(find), StringToByte(replace), truckLength, SelectionStart + 1);

        /// <summary>
        /// Replace the all byte array define by findData in byteprovider.
        /// </summary>
        /// <returns>Return the an IEnumerable contains all positions are replaced. Return null on error/no replace</returns>
        public IEnumerable<long> ReplaceAll(byte[] findData, byte[] replaceData, bool truckLength = true, bool hightlight = false)
        {
            if (findData == null || replaceData == null) return null;

            var positions = _findReplaceService.ReplaceAll(_provider, findData, replaceData, truckLength, ReadOnlyMode);

            if (positions == null) return null;

            // UI logic
            foreach (var position in positions)
            {
                SetScrollMarker(position, ScrollMarker.ByteModified);

                if (hightlight)
                    AddHighLight(position, findData.Length, false);
            }

            UnSelectAll();
            _findReplaceService.ClearCache();  // Clear search cache after modification
            RefreshView();

            return positions;

        }

        /// <summary>
        /// Replace the all byte array define by findData in byteprovider. 
        /// No highlight
        /// </summary>
        /// <returns>Return the position of replace. Return null on error/no replace</returns>
        public IEnumerable<long> ReplaceAll(byte[] findData, byte[] replaceData, bool truckLength = true) =>
            ReplaceAll(findData, replaceData, truckLength, false);

        /// <summary>
        /// Replace the all string define by find in byteprovider. 
        /// </summary>
        /// <returns>Return the position of replace. Return null on error/no replace</returns>
        public IEnumerable<long> ReplaceAll(string find, string replace, bool truckLength = true, bool hightlight = false) =>
            ReplaceAll(StringToByte(find), StringToByte(replace), truckLength, hightlight);

        /// <summary>
        /// Replace the all string define by find in byteprovider.  
        /// No highlight
        /// Truck replace data to length of find
        /// </summary>
        /// <returns>Return the position of replace. Return null on error/no replace</returns>
        public IEnumerable<long> ReplaceAll(string find, string replace) =>
            ReplaceAll(StringToByte(find), StringToByte(replace), true, false);

        /// <summary>
        /// Replace the all byte array define by findData in byteprovider.  
        /// No highlight
        /// </summary>
        /// <returns>Return the position of replace. Return null on error/no replace</returns>
        public IEnumerable<long> ReplaceAll(string find, string replace, bool truckLength = true) =>
            ReplaceAll(StringToByte(find), StringToByte(replace), truckLength, false);

        #endregion Find/replace methods

        #region Statusbar

        /// <summary>
        /// Update statusbar for somes property dont support dependency property
        /// </summary>
        private void UpdateStatusBar(bool updateFilelength = true)
        {
            if (StatusBarVisibility != Visibility.Visible) return;

            if (!CheckIsOpen(_provider))
            {
                FileLengthKbLabel.Content = 0;
                CountOfByteLabel.Content = 0;
                return;
            }

            #region Show length
            if (updateFilelength)
            {
                double length;
                string unit;

                if (_provider.LengthAjusted < 1024)
                {
                    length = _provider.LengthAjusted;
                    unit = Properties.Resources.BytesTagString;
                }
                else if (_provider.LengthAjusted < 1048576) // 1048576 bytes = 1 MiB
                {
                    length = _provider.LengthAjusted / 1024;
                    unit = Properties.Resources.KBTagString;
                }
                else
                {
                    length = _provider.LengthAjusted / 1048576;
                    unit = Properties.Resources.MBTagString;
                }

                FileLengthKbLabel.Content = Math.Round(length, 2) + " " + unit;
            }
            #endregion

            #region Byte count of selectionStart

            if (AllowByteCount && _bytecount is not null && SelectionStart > -1)
            {
                ByteCountPanel.Visibility = Visibility.Visible;

                var singleByte = _provider.GetByte(SelectionStart).singleByte;
                if (singleByte == null) return;

                var val = singleByte.Value;
                CountOfByteSumLabel.Content = _bytecount[val];
                CountOfByteLabel.Content = $"0x{LongToHex(val)}";
            }
            else
                ByteCountPanel.Visibility = Visibility.Collapsed;

            #endregion

        }
        #endregion Statusbar

        #region Bookmark and other scrollmarker

        /// <summary>
        /// Get all bookmark are currently set
        /// </summary>
        public IEnumerable<BookMark> BookMarks
        {
            get
            {
                return _bookmarkService.GetBookmarksByMarker(ScrollMarker.Bookmark);
            }
        }

        /// <summary>
        /// Set bookmark at specified position
        /// </summary>
        public void SetBookMark(long position)
        {
            // Add to service (data layer)
            _bookmarkService.AddBookmark(position, string.Empty, ScrollMarker.Bookmark);

            // Update UI (visual layer)
            SetScrollMarker(position, ScrollMarker.Bookmark);
        }

        /// <summary>
        /// Set bookmark at selection start
        /// </summary>
        public void SetBookMark()
        {
            var position = SelectionStart;

            // Add to service (data layer)
            _bookmarkService.AddBookmark(position, string.Empty, ScrollMarker.Bookmark);

            // Update UI (visual layer)
            SetScrollMarker(position, ScrollMarker.Bookmark);
        }

        /// <summary>
        /// Initialize brush cache to avoid repeated TryFindResource calls
        /// </summary>
        private void InitializeScrollMarkerBrushes()
        {
            _bookMarkColorBrush ??= (SolidColorBrush)TryFindResource("BookMarkColor");
            _searchBookMarkColorBrush ??= (SolidColorBrush)TryFindResource("SearchBookMarkColor");
            _selectionStartBookMarkColorBrush ??= (SolidColorBrush)TryFindResource("SelectionStartBookMarkColor");
            _byteModifiedMarkColorBrush ??= (SolidColorBrush)TryFindResource("ByteModifiedMarkColor");
            _byteDeletedMarkColorBrush ??= (SolidColorBrush)TryFindResource("ByteDeletedMarkColor");
        }

        /// <summary>
        /// Set marker at position using bookmark object
        /// </summary>
        private void SetScrollMarker(BookMark mark) =>
            SetScrollMarker(mark.BytePositionInStream, mark.Marker, mark.Description);

        /// <summary>
        /// Set marker at position - OPTIMIZED VERSION with cache
        /// </summary>
        private void SetScrollMarker(long position, ScrollMarker marker, string description = "")
        {
            if (!CheckIsOpen(_provider)) return;

            // Initialize brushes cache if not done
            InitializeScrollMarkerBrushes();

            double rightPosition = 0;

            //create bookmark
            var bookMark = new BookMark
            {
                Marker = marker,
                BytePositionInStream = position,
                Description = description
            };

            #region Remove selection start marker and set position

            if (marker == ScrollMarker.SelectionStart)
            {
                // Use cache to quickly remove existing SelectionStart marker
                var selectionStartKey = $"{ScrollMarker.SelectionStart}_";
                var keysToRemove = _scrollMarkerCache.Keys.Where(k => k.StartsWith(selectionStartKey)).ToList();
                foreach (var key in keysToRemove)
                {
                    if (_scrollMarkerCache.TryGetValue(key, out var existingRect))
                    {
                        MarkerGrid.Children.Remove(existingRect);
                        _scrollMarkerCache.Remove(key);
                    }
                }

                bookMark.BytePositionInStream = SelectionStart;
            }

            #endregion

            #region Set position in scrollbar

            var topPosition = (VerticalScrollBar.Track == null) ? double.NaN :
                (GetLineNumber(bookMark.BytePositionInStream) * VerticalScrollBar.Track.TickHeight(MaxLine) - 1).Round(1);

            if (double.IsNaN(topPosition))
                topPosition = 0;

            #endregion

            #region Check if position already exist using cache

            var cacheKey = $"{marker}_{(int)topPosition}";

            if (marker != ScrollMarker.SelectionStart)
            {
                // Check cache instead of traversing all markers
                if (_scrollMarkerCache.ContainsKey(cacheKey))
                    return;
            }

            #endregion

            #region Set somes properties for different marker

            var r = new Rectangle
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Tag = bookMark,
                Width = 5,
                Height = 3,
                DataContext = bookMark
            };

            var tooltip = TryFindResource("ScrollMarkerSearchToolTip");

            switch (marker)
            {
                case ScrollMarker.TblBookmark:
                case ScrollMarker.Bookmark:
                    r.ToolTip = tooltip;
                    r.Fill = _bookMarkColorBrush;
                    break;
                case ScrollMarker.SearchHighLight:
                    r.ToolTip = tooltip;
                    r.Fill = _searchBookMarkColorBrush;
                    r.HorizontalAlignment = HorizontalAlignment.Center;
                    break;
                case ScrollMarker.SelectionStart:
                    r.Fill = _selectionStartBookMarkColorBrush;
                    r.Width = VerticalScrollBar.ActualWidth;
                    r.Height = 3;
                    break;
                case ScrollMarker.ByteModified:
                    r.ToolTip = tooltip;
                    r.Fill = _byteModifiedMarkColorBrush;
                    r.HorizontalAlignment = HorizontalAlignment.Right;
                    break;
                case ScrollMarker.ByteDeleted:
                    r.ToolTip = tooltip;
                    r.Fill = _byteDeletedMarkColorBrush;
                    r.HorizontalAlignment = HorizontalAlignment.Right;
                    rightPosition = 4;
                    break;
            }

            r.MouseDown += Rect_MouseDown;
            r.Margin = new Thickness(0, topPosition, rightPosition, 0);

            //Add to cache and grid
            _scrollMarkerCache[cacheKey] = r;
            MarkerGrid.Children.Add(r);

            #endregion
        }

        private void Rect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Rectangle rect || rect.Tag is not BookMark bm) return;

            //Set position at the scrollmark...
            SetPosition(bm.Marker != ScrollMarker.SelectionStart ? bm.BytePositionInStream : SelectionStart, 1);
        }

        /// <summary>
        /// Update all scroll marker position - OPTIMIZED with batch updates
        /// </summary>
        private void UpdateScrollMarkerPosition()
        {
            if (VerticalScrollBar.Track == null) return;

            // Suspend layout updates during batch modification
            MarkerGrid.BeginInit();

            try
            {
                var tickHeight = VerticalScrollBar.Track.TickHeight(MaxLine);

                // Use cache for faster iteration
                foreach (var rect in _scrollMarkerCache.Values)
                {
                    if (rect.Tag is not BookMark bm) continue;

                    try
                    {
                        var newTop = GetLineNumber(bm.BytePositionInStream) * tickHeight - rect.ActualHeight;
                        rect.Margin = new Thickness(0, newTop, 0, 0);
                    }
                    catch
                    {
                        rect.Margin = new Thickness(0);
                    }
                }
            }
            finally
            {
                // Resume layout updates - single redraw
                MarkerGrid.EndInit();
            }
        }

        /// <summary>
        /// Clear ScrollMarker - OPTIMIZED with cache clear
        /// </summary>
        public void ClearAllScrollMarker()
        {
            MarkerGrid.Children.Clear();
            _scrollMarkerCache.Clear();
        }

        /// <summary>
        /// Clear ScrollMarker - OPTIMIZED to avoid removing during traversal
        /// </summary>
        /// <param name="marker">Type of marker to clear</param>
        public void ClearScrollMarker(ScrollMarker marker)
        {
            // Clear from service (data layer)
            if (marker == ScrollMarker.Bookmark)
                _bookmarkService.RemoveAllBookmarks(marker);

            // Collect markers to remove (avoid modification during iteration)
            var keysToRemove = _scrollMarkerCache.Keys
                .Where(k => k.StartsWith($"{marker}_"))
                .ToList();

            // Batch remove from UI
            foreach (var key in keysToRemove)
            {
                if (_scrollMarkerCache.TryGetValue(key, out var rect))
                {
                    MarkerGrid.Children.Remove(rect);
                    _scrollMarkerCache.Remove(key);
                }
            }
        }

        /// <summary>
        /// Clear ScrollMarker - OPTIMIZED with cache lookup
        /// </summary>
        /// <param name="marker">Type of marker to clear</param>
        /// <param name="position"></param>
        public void ClearScrollMarker(ScrollMarker marker, long position)
        {
            // Clear from service (data layer)
            if (marker == ScrollMarker.Bookmark)
                _bookmarkService.RemoveBookmark(position, marker);

            var topPosition = (VerticalScrollBar.Track == null) ? 0 :
                (int)(GetLineNumber(position) * VerticalScrollBar.Track.TickHeight(MaxLine) - 1).Round(1);

            var cacheKey = $"{marker}_{topPosition}";

            if (_scrollMarkerCache.TryGetValue(cacheKey, out var rect))
            {
                MarkerGrid.Children.Remove(rect);
                _scrollMarkerCache.Remove(cacheKey);
            }
        }

        /// <summary>
        /// Clear ScrollMarker at position - OPTIMIZED
        /// </summary>
        public void ClearScrollMarker(long position)
        {
            // Find all markers at this position across all types
            var keysToRemove = _scrollMarkerCache
                .Where(kvp => kvp.Value.Tag is BookMark bm && bm.BytePositionInStream == position)
                .Select(kvp => kvp.Key)
                .ToList();

            // Batch remove
            foreach (var key in keysToRemove)
            {
                if (_scrollMarkerCache.TryGetValue(key, out var rect))
                {
                    MarkerGrid.Children.Remove(rect);
                    _scrollMarkerCache.Remove(key);
                }
            }
        }

        #endregion Bookmark and other scrollmarker

        #region Context menu

        /// <summary>
        /// Allow or not the context menu to appear on right-click
        /// </summary>
        public bool AllowContextMenu { get; set; } = true;

        private void Control_RightClick(object sender, EventArgs e)
        {
            if (!AllowContextMenu) return;

            //position                
            if (sender is IByteControl ctrl)
                _rightClickBytePosition = ctrl.BytePositionInStream;

            if (SelectionLength <= 1)
            {
                SelectionStart = _rightClickBytePosition;
                SelectionStop = _rightClickBytePosition;
            }

            #region Disable ctrl

            CopyAsCMenu.IsEnabled = false;
            CopyAsciicMenu.IsEnabled = false;
            FindAllCMenu.IsEnabled = false;
            CopyHexaCMenu.IsEnabled = false;
            UndoCMenu.IsEnabled = false;
            DeleteCMenu.IsEnabled = false;
            FillByteCMenu.IsEnabled = false;
            CopyTblcMenu.IsEnabled = false;
            PasteMenu.IsEnabled = false;
            ReplaceByteCMenu.IsEnabled = false;

            #endregion

            if (SelectionLength > 0)
            {
                CopyAsciicMenu.IsEnabled = true;
                CopyAsCMenu.IsEnabled = true;
                FindAllCMenu.IsEnabled = true;
                CopyHexaCMenu.IsEnabled = true;

                if (!ReadOnlyMode)
                {
                    DeleteCMenu.IsEnabled = true;
                    FillByteCMenu.IsEnabled = true;
                    PasteMenu.IsEnabled = true;
                    ReplaceByteCMenu.IsEnabled = true;

                }

                if (_tblService.CharacterTable is not null)
                    CopyTblcMenu.IsEnabled = true;
            }

            if (UndoCount > 0)
                UndoCMenu.IsEnabled = true;

            //Show context menu
            Focus();
            CMenu.Visibility = Visibility.Visible;
        }

        private void FindAllCMenu_Click(object sender, RoutedEventArgs e) => FindAll(GetSelectionByteArray(), true);

        private void CopyToClipBoardCMenu_Click(object sender, RoutedEventArgs e)
        {
            //Copy to clipboard
            switch ((sender as MenuItem)?.Name)
            {
                case nameof(CopyHexaCMenu):
                    CopyToClipboard(CopyPasteMode.HexaString);
                    break;
                case nameof(CopyAsciicMenu):
                    CopyToClipboard(CopyPasteMode.AsciiString);
                    break;
                case nameof(CopyCSharpCMenu):
                    CopyToClipboard(CopyPasteMode.CSharpCode);
                    break;
                case nameof(CopyFSharpCMenu):
                    CopyToClipboard(CopyPasteMode.FSharpCode);
                    break;
                case nameof(CopyCcMenu):
                    CopyToClipboard(CopyPasteMode.CCode);
                    break;
                case nameof(CopyJavaCMenu):
                    CopyToClipboard(CopyPasteMode.JavaCode);
                    break;
                case nameof(CopyVbNetCMenu):
                    CopyToClipboard(CopyPasteMode.VbNetCode);
                    break;
                case nameof(CopyPascalCMenu):
                    CopyToClipboard(CopyPasteMode.PascalCode);
                    break;
                case nameof(CopyTblcMenu):
                    CopyToClipboard(CopyPasteMode.TblString);
                    break;
            }
        }

        private void DeleteCMenu_Click(object sender, RoutedEventArgs e) => DeleteSelection();

        private void UndoCMenu_Click(object sender, RoutedEventArgs e) => Undo();

        private void BookMarkCMenu_Click(object sender, RoutedEventArgs e) => SetBookMark(_rightClickBytePosition);

        private void ClearBookMarkCMenu_Click(object sender, RoutedEventArgs e) =>
            ClearScrollMarker(ScrollMarker.Bookmark);

        private void PasteMenu_Click(object sender, RoutedEventArgs e) => Paste(false); //Paste Without Insert

        private void SelectAllCMenu_Click(object sender, RoutedEventArgs e) => SelectAll();

        private void FillByteCMenu_Click(object sender, RoutedEventArgs e)
        {
            var window = new GiveByteWindow();

            //For present crash When used in Winform
            try
            {
                window.Owner = Application.Current.MainWindow;
            }
            catch
            {
                // TODO : add Winform code
            }

            if (window.ShowDialog() == true && window.HexTextBox.LongValue <= 255)
                FillWithByte((byte)window.HexTextBox.LongValue);
        }

        private void ReplaceByteCMenu_Click(object sender, RoutedEventArgs e)
        {
            var window = new ReplaceByteWindow();

            //For present crash When used in Winform
            try
            {
                window.Owner = Application.Current.MainWindow;
            }
            catch
            {
                // TODO : add Winform code
            }

            if (window.ShowDialog() == true && window.HexTextBox.LongValue <= 255 &&
                window.ReplaceHexTextBox.LongValue <= 255)
                ReplaceByte((byte)window.HexTextBox.LongValue, (byte)window.ReplaceHexTextBox.LongValue);
        }

        #endregion Context menu

        #region Bottom and top rectangle

        /// <summary>
        /// Vertical Move Method By Time,
        /// </summary>
        /// <param name="readToMove">whether the veticalbar value should be changed</param>
        /// <param name="distance">the value that vertical value move down(negative for up)</param>
        private void VerticalMoveByTime(Func<bool> readToMove, Func<double> distance) =>
            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (readToMove())
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (Mouse.LeftButton != MouseButtonState.Pressed) return;

                        VerticalScrollBar.Value += distance();

                        //Selection stop
                        if (_mouseOnBottom)
                            SelectionStop = LastVisibleBytePosition;
                        else if (_mouseOnTop)
                            SelectionStop = FirstVisibleBytePosition;

                        //Give the control to dispatcher for do events
                        Application.Current.DoEvents();

                    });
                }
            });

        private void BottomRectangle_MouseEnter(object sender, MouseEventArgs e)
        {
            _mouseOnBottom = true;
            var curTime = ++_bottomEnterTimes;

            VerticalMoveByTime
            (
                () => _mouseOnBottom && curTime == _bottomEnterTimes,
                () => (int)MouseWheelSpeed
            );
        }

        private void TopRectangle_MouseEnter(object sender, MouseEventArgs e)
        {
            var curTime = ++_topEnterTimes;
            _mouseOnTop = true;

            VerticalMoveByTime
            (
                () => _mouseOnTop && curTime == _topEnterTimes,
                () => -(int)MouseWheelSpeed
            );
        }

        private void BottomRectangle_MouseLeave(object sender, MouseEventArgs e) => _mouseOnBottom = false;

        private void TopRectangle_MouseLeave(object sender, MouseEventArgs e) => _mouseOnTop = false;

        private void BottomRectangle_MouseDown(object sender, MouseButtonEventArgs e) => _mouseOnBottom = false;

        private void TopRectangle_MouseDown(object sender, MouseButtonEventArgs e) => _mouseOnTop = false;

        #endregion Bottom and Top rectangle

        #region MouseWheel support
        /// <summary>
        /// Control the mouse wheel speed
        /// </summary>
        public MouseWheelSpeed MouseWheelSpeed { get; set; } = MouseWheelSpeed.System;

        private void Control_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            #region Mousewheel Zoom support
            if (_scaler == null) InitialiseZoom();

            if (Keyboard.Modifiers == ModifierKeys.Control && AllowZoom)
            {
                if (e.Delta > 0) //Zoom
                    ZoomScale += 0.1;

                if (e.Delta < 0) //UnZoom
                    ZoomScale -= 0.1;

                return;
            }
            #endregion

            #region Scroll up and down in the hex editor
            if (_provider == null || !_provider.IsOnLongProcess)
            {
                if (e.Delta > 0) //UP
                    VerticalScrollBar.Value -= e.Delta / 120 *
                        (MouseWheelSpeed == MouseWheelSpeed.System
                            ? SystemParameters.WheelScrollLines
                            : (int)MouseWheelSpeed);

                if (e.Delta < 0) //Down
                    VerticalScrollBar.Value += e.Delta / 120 *
                       -(MouseWheelSpeed == MouseWheelSpeed.System
                            ? SystemParameters.WheelScrollLines
                            : (int)MouseWheelSpeed);
            }
            #endregion
        }
        #endregion

        #region Highlight support
        /// <summary>
        /// Byte at selection start
        /// </summary>
        internal byte? SelectionByte { get; set; }

        /// <summary>
        /// Set to true for highlight the same byte are selected in view.
        /// </summary>
        public bool AllowAutoHighLightSelectionByte
        {
            get => (bool)GetValue(AllowAutoHighLightSelectionByteProperty);
            set => SetValue(AllowAutoHighLightSelectionByteProperty, value);
        }

        public static readonly DependencyProperty AllowAutoHighLightSelectionByteProperty =
            DependencyProperty.Register(nameof(AllowAutoHighLightSelectionByte), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(true, AutoHighLiteSelectionByte_Changed));

        /// <summary>
        /// Brush used to color the selectionbyte
        /// </summary>
        public Brush AutoHighLiteSelectionByteBrush
        {
            get => (Brush)GetValue(AutoHighLiteSelectionByteBrushProperty);
            set => SetValue(AutoHighLiteSelectionByteBrushProperty, value);
        }

        public static readonly DependencyProperty AutoHighLiteSelectionByteBrushProperty =
            DependencyProperty.Register(nameof(AutoHighLiteSelectionByteBrush), typeof(Brush), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(Brushes.LightBlue, AutoHighLiteSelectionByte_Changed));

        private static void AutoHighLiteSelectionByte_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorLegacy ctrl)
                ctrl.UpdateVisual();
        }

        /// <summary>
        /// Un highlight all byte as highlighted with find all methods
        /// </summary>
        public void UnHighLightAll()
        {
            _highlightService.UnHighLightAll();
            UpdateHighLight();
            ClearScrollMarker(ScrollMarker.SearchHighLight);
        }

        /// <summary>
        /// Add highlight at position start
        /// </summary>
        /// <param name="startPosition">Position to start the highlight</param>
        /// <param name="length">The length to highlight</param>
        /// <param name="updateVisual">Set to true for update the visual after adding</param>
        public void AddHighLight(long startPosition, long length, bool updateVisual = true)
        {
            _highlightService.AddHighLight(startPosition, length);

            if (updateVisual) UpdateHighLight();
        }

        /// <summary>
        /// Remove highlight from position start
        /// </summary>
        /// <param name="startPosition">Position to start the remove of highlight</param>
        /// <param name="length">The length of highlight to removing</param>
        /// <param name="updateVisual">Set to true for update the visual after removing</param>
        public void RemoveHighLight(long startPosition, long length, bool updateVisual = true)
        {
            _highlightService.RemoveHighLight(startPosition, length);

            if (updateVisual) UpdateHighLight();
        }
        #endregion Highlight support

        #region ByteCount property/methods

        /// <summary>
        /// Get or set if the count of byte are allowed
        /// </summary>
        public bool AllowByteCount
        {
            get => (bool)GetValue(AllowByteCountProperty);
            set => SetValue(AllowByteCountProperty, value);
        }

        public static readonly DependencyProperty AllowByteCountProperty =
            DependencyProperty.Register(nameof(AllowByteCount), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(false, AllowByteCount_PropertyChanged));

        private static void AllowByteCount_PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl) return;

            if (e.NewValue != e.OldValue)
                ctrl.UpdateByteCount();

            ctrl.UpdateStatusBar();
        }

        /// <summary>
        /// Update the bytecount var.
        /// </summary>
        private void UpdateByteCount()
        {
            _bytecount = null;

            if (CheckIsOpen(_provider) && AllowByteCount)
                _bytecount = _provider.GetByteCount();
        }

        #endregion ByteCount Property

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue) return;

            //Dispose managed object
            if (disposing)
            {
                _provider?.Dispose();
                _tblService.CharacterTable?.Dispose();
                _viewBuffer = null;
                _viewBufferBytePosition = null;
                // _markedPositionList is now managed by HighlightService (no need to null)
                _highlightService.Clear();
            }

            _disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region IByteControl grouping support
        /// <summary>
        /// Get or set the byte spacing
        /// </summary>
        public ByteSpacerPosition ByteSpacerPositioning
        {
            get => (ByteSpacerPosition)GetValue(ByteSpacerPositioningProperty);
            set => SetValue(ByteSpacerPositioningProperty, value);
        }

        public static readonly DependencyProperty ByteSpacerPositioningProperty =
            DependencyProperty.Register(nameof(ByteSpacerPositioning), typeof(ByteSpacerPosition), typeof(HexEditorLegacy),
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
            DependencyProperty.Register(nameof(ByteSpacerWidthTickness), typeof(ByteSpacerWidth), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(ByteSpacerWidth.Normal, ByteSpacer_Changed));

        private static void ByteSpacer_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorLegacy ctrl)
                ctrl.RefreshView(true);
        }

        /// <summary>
        /// Get or set the byte grouping
        /// </summary>
        public ByteSpacerGroup ByteGrouping
        {
            get => (ByteSpacerGroup)GetValue(ByteGroupingProperty);
            set => SetValue(ByteGroupingProperty, value);
        }

        public static readonly DependencyProperty ByteGroupingProperty =
            DependencyProperty.Register(nameof(ByteGrouping), typeof(ByteSpacerGroup), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(ByteSpacerGroup.EightByte, ByteSpacer_Changed));

        /// <summary>
        /// Get or set the visual of byte spacer 
        /// </summary>
        public ByteSpacerVisual ByteSpacerVisualStyle
        {
            get => (ByteSpacerVisual)GetValue(ByteSpacerVisualStyleProperty);
            set => SetValue(ByteSpacerVisualStyleProperty, value);
        }

        public static readonly DependencyProperty ByteSpacerVisualStyleProperty =
            DependencyProperty.Register(nameof(ByteSpacerVisualStyle), typeof(ByteSpacerVisual), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(ByteSpacerVisual.Empty, ByteSpacer_Changed));

        /// <summary>
        /// Add byte spacer
        /// </summary>
        private void AddByteSpacer(StackPanel stack, int colomn, bool forceEmpty = false)
        {
            if (colomn % (int)ByteGrouping != 0 || colomn <= 0) return;

            if (!forceEmpty)
                switch (ByteSpacerVisualStyle)
                {
                    case ByteSpacerVisual.Empty:
                        stack.Children.Add(new TextBlock { Width = (int)ByteSpacerWidthTickness });
                        break;
                    case ByteSpacerVisual.Line:

                        #region Line

                        stack.Children.Add(new Line
                        {
                            Y2 = LineHeight,
                            X1 = (int)ByteSpacerWidthTickness / 2D,
                            X2 = (int)ByteSpacerWidthTickness / 2D,
                            Stroke = BorderBrush,
                            StrokeThickness = 1,
                            Width = (int)ByteSpacerWidthTickness
                        });

                        #endregion

                        break;
                    case ByteSpacerVisual.Dash:

                        #region LineDash

                        stack.Children.Add(new Line
                        {
                            Y2 = LineHeight - 1,
                            X1 = (int)ByteSpacerWidthTickness / 2D,
                            X2 = (int)ByteSpacerWidthTickness / 2D,
                            Stroke = BorderBrush,
                            StrokeDashArray = new DoubleCollection(new double[] { 2 }),
                            StrokeThickness = 1,
                            Width = (int)ByteSpacerWidthTickness
                        });

                        #endregion

                        break;
                }
            else
                stack.Children.Add(new TextBlock { Width = (int)ByteSpacerWidthTickness });
        }

        #endregion IByteControl grouping

        #region Caret support

        public CaretMode VisualCaretMode
        {
            get => (CaretMode)GetValue(VisualCaretModeProperty);
            set => SetValue(VisualCaretModeProperty, value);
        }

        // Using a DependencyProperty as the backing store for VisualCaretMode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty VisualCaretModeProperty =
            DependencyProperty.Register(nameof(VisualCaretMode), typeof(CaretMode), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(CaretMode.Insert, VisualCaretMode_Changed));

        private static void VisualCaretMode_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl) return;

            ctrl.SetCaretMode((CaretMode)e.NewValue);
            ctrl.RefreshView(true);
        }

        /// <summary>
        /// Initialize the caret
        /// </summary>
        private void InitializeCaret()
        {
            if (DesignerProperties.GetIsInDesignMode(this)) return;

            BaseGrid.Children.Add(_caret);
            _caret.CaretHeight = LineHeight;
            _caret.BlinkPeriod = 600;
            _caret.Hide();
        }

        /// <summary>
        /// Move the caret in a screen point
        /// </summary>
        internal void MoveCaret(Point point) => _caret.MoveCaret(point);

        /// <summary>
        /// Hide the caret
        /// </summary>
        internal void HideCaret() => _caret.Hide();

        /// <summary>
        /// Return true if caret is visible
        /// </summary>
        public bool IsCaretVisible => _caret.IsVisibleCaret;

        /// <summary>
        /// Set the site of caret
        /// </summary>
        internal void SetCaretSize(double width, double height)
        {
            _caret.CaretWidth = width;
            _caret.CaretHeight = height;
        }

        /// <summary>
        /// Set the mode of caret (Insert or Overtwrite)
        /// </summary>
        internal void SetCaretMode(CaretMode mode) => _caret.CaretMode = mode;

        #endregion

        #region Append/expend bytes to end of file
        //////////
        // TODO: Will be updated soon as possible with the possibility to insert byte anywhere :)
        //////////

        /// <summary>
        /// Allow control to append/expend byte at end of file
        /// </summary>
        public bool AllowExtend { get; set; }

        /// <summary>
        /// Show a message box is true before append byte at end of file
        /// </summary>
        public bool AppendNeedConfirmation { get; set; } = true;

        /// <summary>
        /// Append one byte at end of file
        /// </summary>
        internal void AppendByte(byte[] bytesToAppend)
        {
            if (!AllowExtend) return;
            if (!CheckIsOpen(_provider)) return;

            if (AppendNeedConfirmation)
                if (MessageBox.Show(Properties.Resources.AppendByteConfirmationString, ApplicationName,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question, MessageBoxResult.Yes) != MessageBoxResult.Yes) return;

            _provider?.AppendByte(bytesToAppend);
            RefreshView();
        }

        #endregion

        #region Drag and drop support

        /// <summary>
        /// Allow the control to catch the file dropping 
        /// Note : AllowDrop need to be true
        /// </summary>
        public bool AllowFileDrop { get; set; } = true;

        /// <summary>
        /// Allow the control to catch the text dropping 
        /// Note : AllowDrop need to be true
        /// </summary>
        public bool AllowTextDrop { get; set; } = true;

        /// <summary>
        /// Show a messagebox for confirm open when a file are already open
        /// </summary>
        public bool FileDroppingConfirmation { get; set; } = true;

        private void Control_Drop(object sender, DragEventArgs e)
        {
            #region Text Dropping

            var textDrop = e.Data.GetData(DataFormats.Text);
            if (textDrop is not null && AllowTextDrop)
            {
                var textDropped = textDrop as string;

                if (!string.IsNullOrEmpty(textDropped) && CheckIsOpen(_provider))
                {
                    #region Insert at mouve over position
                    var position = SelectionStart;
                    var rtn = false;
                    TraverseHexAndStringBytes(ctrl =>
                    {
                        Application.Current.DoEvents();

                        if (ctrl.IsMouseOverMe)
                        {
                            position = ctrl.BytePositionInStream;
                            rtn = true;
                        }
                    }, ref rtn);
                    #endregion

                    _provider.Paste(position, textDropped, AllowExtend);

                    RefreshView();
                }

                return;
            }

            #endregion

            #region File dropping (Only open first selected file catched in GetData)

            var fileDrop = e.Data.GetData(DataFormats.FileDrop);
            if (fileDrop is not null && AllowFileDrop)
            {
                var filename = fileDrop as string[];

                if (!CheckIsOpen(_provider))
                {
                    if (filename != null) FileName = filename[0];
                }
                else
                {
                    if (FileDroppingConfirmation)
                    {
                        if (MessageBox.Show(
                            $"{Properties.Resources.FileDroppingConfirmationString} {Path.GetFileName(filename[0])} ?",
                                ApplicationName, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                            FileName = filename[0];
                    }
                    else if (filename != null) FileName = filename[0];
                }
            }

            #endregion
        }

        #endregion

        #region Save/Load control state (Can be used to implement multi-file support in client editor... ;)

        /// <summary>
        /// Save the current state of ByteProviderLegacy in a xml text file.
        /// </summary>
        public void SaveCurrentState(string filename)
        {
            if (!CheckIsOpen(_provider)) return;
            GetState().Save(filename, SaveOptions.None);
        }

        /// <summary>
        /// Load state of control from a xml text file.
        /// </summary>
        public void LoadCurrentState(string filename)
        {
            if (!CheckIsOpen(_provider)) return;
            if (!File.Exists(filename)) return;

            SetState(XDocument.Load(filename));
        }

        /// <summary>
        /// Set or Get the current state of control from XDocument.
        /// </summary>
        /// <remarks>
        /// TODO: Add validation for catch error from wrong XDocument
        /// </remarks>
        public XDocument CurrentState
        {
            get => CheckIsOpen(_provider)
                ? GetState()
                : null;
            set
            {
                if (!CheckIsOpen(_provider)) return;

                //Clear aller scroll marker and will be updated by Provider_SetStateByteModifiedAdded()
                ClearAllScrollMarker();

                SetState(value);

                RefreshView();
            }
        }

        /// <summary>
        /// Get the current state of hexeditor
        /// </summary>
        /// <returns>
        /// Return a XDocument that include all changes in the byte provider, Selecton Start/Stop, position ...
        /// </returns>
        private XDocument GetState()
        {
            if (!CheckIsOpen(_provider)) return null;

            var doc = new XDocument(new XElement("WpfHexEditorLegacy",
                new XAttribute("Version", "0.1"),
                new XAttribute(nameof(SelectionStart), SelectionStart),
                new XAttribute(nameof(SelectionStop), SelectionStop),
                new XAttribute(nameof(FirstVisibleBytePosition), FirstVisibleBytePosition),
                new XAttribute(nameof(ReadOnlyMode), ReadOnlyMode),
                    new XElement("ByteModifieds", new XAttribute("Count", _provider.GetByteModifieds(ByteAction.All).Count)),
                    new XElement("BookMarks", new XAttribute("Count", BookMarks.Count())),
                    new XElement("TBL", new XAttribute("Loaded", _tblService.CharacterTable is not null)),
                    new XElement("HighLights", new XAttribute("Count", _highlightService.GetHighlightCount()))));

            #region Create ByteModifieds tag

            var bmRootBm = doc.Element("WpfHexEditorLegacy")?.Element("ByteModifieds");

            foreach (var bm in _provider.GetByteModifieds(ByteAction.All))
                bmRootBm?.Add(new XElement("ByteModified",
                    new XAttribute("Action", bm.Value.Action),
                    new XAttribute("HexByte",
                        bm.Value.Byte.HasValue
                            ? new string(ByteToHexCharArray((byte)bm.Value.Byte))
                            : string.Empty),
                    new XAttribute("Position", bm.Value.BytePositionInStream)));

            #endregion

            #region Create hightlight tag

            var bmRootHl = doc.Element("WpfHexEditorLegacy")?.Element("HighLights");

            foreach (var position in _highlightService.GetHighlightedPositions())
                bmRootHl?.Add(new XElement("HighLight", new XAttribute("Position", position)));
            #endregion

            #region Create bookmarks tag

            var bmRootBMs = doc.Element("WpfHexEditorLegacy")?.Element("BookMarks");

            foreach (var bm in BookMarks)
                bmRootBMs?.Add(new XElement("BookMark",
                    new XAttribute("Position", bm.BytePositionInStream),
                    new XAttribute("Description", bm.Description)));

            #endregion

            #region Create TBL tag

            if (_tblService.CharacterTable is not null)
                doc.Element("WpfHexEditorLegacy")
                    ?.Element("TBL")
                    ?.Add(new XElement("TBLData",
                            new XAttribute("Filename", _tblService.CharacterTable.FileName),
                            new XAttribute("Data", _tblService.CharacterTable.ToString())));
            #endregion

            return doc;
        }

        /// <summary>
        /// Set the state of hexeditor and update the visual...
        /// </summary>
        private void SetState(XDocument doc)
        {
            if (doc is null) return;
            if (!CheckIsOpen(_provider)) return;

            //Clear current state
            _provider.ClearUndoChange();
            ClearScrollMarker(ScrollMarker.Bookmark);

            #region Load ByteModifieds
            var bmList = doc.Element("WpfHexEditorLegacy")?.Element("ByteModifieds")?.Elements().Select(i => i);

            if (bmList != null)
                foreach (var element in bmList)
                {
                    var bm = new ByteModified();

                    #region Set attribute of bytemodified...

                    foreach (var at in element.Attributes())
                        switch (at.Name.ToString())
                        {
                            case "Action":

                                #region Set action

                                bm.Action = at.Value switch
                                {
                                    "Modified" => ByteAction.Modified,
                                    "Deleted" => ByteAction.Deleted,
                                    "Added" => ByteAction.Added,
                                    _ => throw new NotImplementedException()
                                };

                                #endregion

                                break;
                            case "HexByte":
                                bm.Byte = IsHexaByteStringValue(at.Value).value[0];
                                break;
                            case "Position":
                                bm.BytePositionInStream = long.Parse(at.Value);
                                break;
                        }

                    #endregion

                    #region Add bytemodified to dictionary

                    switch (bm.Action)
                    {
                        case ByteAction.Deleted:
                            _provider.AddByteDeleted(bm.BytePositionInStream, 1);
                            SetScrollMarker(bm.BytePositionInStream, ScrollMarker.ByteDeleted);
                            break;
                        case ByteAction.Modified:
                            _provider.AddByteModified(bm.Byte, bm.BytePositionInStream);
                            SetScrollMarker(bm.BytePositionInStream, ScrollMarker.ByteModified);
                            break;
                        case ByteAction.Added:
                            //TODO: Add action when byte added will be supported
                            break;
                    }

                    #endregion
                }

            #endregion

            #region Load highlight            
            UnHighLightAll();

            var hlList = doc.Element("WpfHexEditorLegacy")?.Element("HighLights")?.Elements().Select(i => i);

            if (hlList != null)
                foreach (var element in hlList)
                    AddHighLight(long.TryParse(element.Attribute("Position")?.Value, out var pos)
                        ? pos
                        : -1, 1);

            #endregion

            #region Load bookmarks
            var bmsList = doc.Element("WpfHexEditorLegacy")?.Element("BookMarks")?.Elements().Select(i => i);

            if (bmsList != null)
                foreach (var element in bmsList)
                {
                    var bm = new BookMark
                    {
                        Marker = ScrollMarker.Bookmark
                    };

                    #region Set attribute of bookmark...

                    foreach (var at in element.Attributes())
                        switch (at.Name.ToString())
                        {
                            case "Description":
                                bm.Description = at.Value;
                                break;
                            case "Position":
                                bm.BytePositionInStream = long.Parse(at.Value);
                                break;
                        }

                    #endregion

                    SetScrollMarker(bm);
                }

            #endregion

            #region Load TBL            
            var tblList = doc.Element("WpfHexEditorLegacy").Element("TBL").Elements().Select(i => i);

            foreach (var element in tblList)
            {
                foreach (var at in element.Attributes())
                    switch (at.Name.ToString())
                    {
                        case "Data":
                            _tblService.CharacterTable.Load(at.Value);
                            break;
                        case "Filename":
                            //TODO
                            break;
                    }
            }

            #endregion

            #region Update the visual
            //Update position
            SetPosition(long.TryParse(doc.Element("WpfHexEditorLegacy").Attribute(nameof(FirstVisibleBytePosition)).Value, out var position)
                ? position
                : 0);

            //Update selection
            SelectionStart = long.TryParse(doc.Element("WpfHexEditorLegacy").Attribute(nameof(SelectionStart)).Value, out var selectionStart)
                ? selectionStart
                : 0;

            SelectionStop = long.TryParse(doc.Element("WpfHexEditorLegacy").Attribute(nameof(SelectionStop)).Value, out var selectionStop)
                ? selectionStop
                : 0;

            //Refresh view
            RefreshView(true);
            #endregion

            #region Set the readonly mode

            SetCurrentValue(ReadOnlyModeProperty, bool.TryParse(doc.Element("WpfHexEditorLegacy").Attribute(nameof(ReadOnlyMode)).Value, out var readOnlyMode) && readOnlyMode);

            #endregion
        }

        #endregion

        #region Shift the first visible byte in the views to the left ...

        /// <summary>
        /// Shift the first visible byte in the view to the left. 
        /// </summary>
        /// <remarks>
        /// Very useful for editing fixed-width tables. Use with BytePerLine to create visual tables ...
        /// The value is the number of byte to shift visualy by the left.
        /// </remarks>
        public int ByteShiftLeft
        {
            get => (int)GetValue(ByteShiftLeftProperty);
            set => SetValue(ByteShiftLeftProperty, value);
        }

        public static readonly DependencyProperty ByteShiftLeftProperty =
            DependencyProperty.Register(nameof(ByteShiftLeft), typeof(int), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(0, ByteShiftLeft_Changed, ByteShiftLeft_CoerceValue));

        private static void ByteShiftLeft_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorLegacy ctrl)
                ctrl.RefreshView(true);
        }

        private static object ByteShiftLeft_CoerceValue(DependencyObject d, object basevalue) =>
            (int)basevalue < 0
                ? 0
                : basevalue;

        #endregion

        #region Reverse bytes selection

        /// <summary>
        /// Reverse selection of bytes array like this {AA, FF, EE, DC} => {DC, EE, FF, AA}
        /// </summary>
        public void ReverseSelection()
        {
            if (!CheckIsOpen(_provider) || ReadOnlyMode) return;

            _provider.Reverse(SelectionStart, SelectionStop);

            RefreshView();
        }

        #endregion

        #region Line offset coloring...

        /// <summary>
        /// High light header and offset on SelectionStart
        /// </summary>
        public bool HighLightSelectionStart
        {
            get => _highLightSelectionStart;
            set
            {
                _highLightSelectionStart = value;
                RefreshView();
            }
        }

        #endregion

        #region Configure the start/stop bytes that are loaded visually into the hexadecimal editor
        public bool AllowVisualByteAddress
        {
            get => (bool)GetValue(AllowVisualByteAdressProperty);
            set => SetValue(AllowVisualByteAdressProperty, value);
        }

        public static readonly DependencyProperty AllowVisualByteAdressProperty =
            DependencyProperty.Register(nameof(AllowVisualByteAddress), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(false, AllowVisualByteAddress_Changed));

        private static void AllowVisualByteAddress_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

            ctrl.UpdateScrollBar();
            ctrl.RefreshView(true);
        }

        /// <summary>
        /// Set the last byte are virtually loaded in the control. 
        /// Note that all other bytes address after will not be shown in control.
        /// This property are read only
        /// </summary>
        public long VisualByteAdressStop => VisualByteAdressStart + VisualByteAdressLength;

        /// <summary>
        /// Set the length from first byte set by VisualStartByteAdress property are virtually loaded in the control. 
        /// </summary>
        public long VisualByteAdressLength
        {
            get => (long)GetValue(VisualByteAdressLengthProperty);
            set => SetValue(VisualByteAdressLengthProperty, value);
        }

        public static readonly DependencyProperty VisualByteAdressLengthProperty =
            DependencyProperty.Register(nameof(VisualByteAdressLength), typeof(long), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(1L, VisualByteAdressLength_Changed, VisualByteAdressLength_CoerceValue));

        private static object VisualByteAdressLength_CoerceValue(DependencyObject d, object baseValue)
        {
            if (d is not HexEditorLegacy ctrl) return baseValue;

            var value = (long)baseValue;

            if (value < 1 || !CheckIsOpen(ctrl._provider)) return 1L;

            return value >= ctrl._provider.Length
                ? ctrl._provider.Length
                : baseValue;
        }

        private static void VisualByteAdressLength_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

            ctrl.UpdateScrollBar();
            ctrl.RefreshView();
        }

        /// <summary>
        /// Set the first byte are virtually loaded in the control. 
        /// Note that all other bytes address before will not be shown in control.
        /// </summary>
        public long VisualByteAdressStart
        {
            get => (long)GetValue(VisualByteAdressStartProperty);
            set => SetValue(VisualByteAdressStartProperty, value);
        }

        public static readonly DependencyProperty VisualByteAdressStartProperty =
            DependencyProperty.Register(nameof(VisualByteAdressStart), typeof(long), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(0L, VisualByteAdressStart_Changed, VisualByteAdressStart_CoerceValue));

        private static object VisualByteAdressStart_CoerceValue(DependencyObject d, object baseValue)
        {
            if (d is not HexEditorLegacy ctrl) return baseValue;

            var value = (long)baseValue;

            if (!CheckIsOpen(ctrl._provider)) return 0L;

            return value >= ctrl._provider.Length
                ? ctrl._provider.Length
                : baseValue;
        }

        private static void VisualByteAdressStart_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

            ctrl.UpdateScrollBar();
            ctrl.RefreshView();
        }
        #endregion

        #region Preload IByteControl at control creation

        /// <summary>
        /// Used to define how many line will be created at control creation
        /// </summary>
        public PreloadByteInEditor PreloadByteInEditorMode
        {
            get => (PreloadByteInEditor)GetValue(PreloadByteInEditorModeProperty);
            set => SetValue(PreloadByteInEditorModeProperty, value);
        }

        // Using a DependencyProperty as the backing store for PreloadByteInEditorMode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PreloadByteInEditorModeProperty =
            DependencyProperty.Register(nameof(PreloadByteInEditorMode), typeof(PreloadByteInEditor),
                typeof(HexEditorLegacy), new PropertyMetadata(PreloadByteInEditor.None));

        private void Control_Loaded(object sender, RoutedEventArgs e) =>
            ForcePreloadByteInEditor(PreloadByteInEditorMode); //Preload byte or not...

        /// <summary>
        /// Number of line to preload in editor
        /// </summary>
        /// <param name="preloadByte">Preload byte precalculated</param>
        /// <param name="refreshView"></param>
        public void ForcePreloadByteInEditor(PreloadByteInEditor preloadByte, bool refreshView = true)
        {
            //Set the number of row to preload in the editor at the control creation. 
            //It will be faster when resizing of control but will be longer to load at first time
            var nbLine = preloadByte switch
            {
                PreloadByteInEditor.None => 0, //Not preload byte
                PreloadByteInEditor.MaxVisibleLine => MaxVisibleLine,
                PreloadByteInEditor.MaxVisibleLineExtended => MaxVisibleLine + 10,
                PreloadByteInEditor.MaxScreenVisibleLine => MaxScreenVisibleLine,
                PreloadByteInEditor.MaxScreenVisibleLineAtDataLoad => MaxVisibleLine, //other line loaded at first file/stream loaded
                _ => throw new NotImplementedException()
            };

            if (PreloadByteInEditorMode != PreloadByteInEditor.None)
                BuildDataLines(nbLine, true);

            if (refreshView) RefreshView();
        }

        /// <summary>
        /// Number of line to preload in editor
        /// </summary>
        /// <param name="nbLine">Number of line to preload in control</param>
        /// <param name="refreshView"></param>
        public void ForcePreloadByteInEditor(int nbLine, bool refreshView = true)
        {
            if (nbLine <= 0) return;

            BuildDataLines(nbLine, true);

            if (refreshView) RefreshView();
        }

        #endregion

        #region Zoom in/out support
        /// <summary>
        /// Get or set the zoom scale 
        /// Posible Scale : 0.5 to 2.0 (50% to 200%)
        /// </summary>
        public double ZoomScale
        {
            get => (double)GetValue(ZoomScaleProperty);
            set => SetValue(ZoomScaleProperty, value);
        }

        public static readonly DependencyProperty ZoomScaleProperty =
            DependencyProperty.Register(nameof(ZoomScale), typeof(double), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(1D, ZoomScale_ChangedCallBack, ZoomScale_CoerceValueCallBack));

        private static object ZoomScale_CoerceValueCallBack(DependencyObject d, object baseValue) =>
            (double)baseValue >= 0.5 && (double)baseValue <= 2.0001
                ? (double)baseValue
                : d.GetValue(ZoomScaleProperty);

        private static void ZoomScale_ChangedCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl) return;
            if (e.NewValue == e.OldValue) return;

            ctrl.UpdateZoom();
        }

        /// <summary>
        /// Allow or not the capability to zoom control content (use with ScaleX and ScaleY property)
        /// </summary>
        public bool AllowZoom
        {
            get => _allowZoom;

            set
            {
                _allowZoom = value;

                InitialiseZoom();
            }
        }

        /// <summary>
        /// Initialize the support of zoom
        /// </summary>
        private void InitialiseZoom()
        {
            if (_scaler is not null) return;

            _scaler = new ScaleTransform(ZoomScale, ZoomScale);

            HexHeaderStackPanel.LayoutTransform = _scaler;
            HexDataStackPanel.LayoutTransform = _scaler;
            StringDataStackPanel.LayoutTransform = _scaler;
            LinesInfoStackPanel.LayoutTransform = _scaler;

            _caret.LayoutTransform = _scaler;
        }

        /// <summary>
        /// Update the zoom to ScaleX, ScaleY value if AllowZoom is true
        /// </summary>
        private void UpdateZoom()
        {
            if (!AllowZoom) return;

            if (_scaler == null) InitialiseZoom();
            if (_scaler != null)
            {
                _scaler.ScaleY = ZoomScale;
                _scaler.ScaleX = ZoomScale;
            }

            //TODO: Update caret size...

            ClearLineInfo();
            ClearAllBytes(true);
            RefreshView(true);

            ZoomScaleChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Reset the zoom to 100%
        /// </summary>
        public void ResetZoom() => ZoomScale = 1.0;

        private void ZoomResetButton_Click(object sender, RoutedEventArgs e) => ResetZoom();
        #endregion

        #region Delete byte support
        /// <summary>
        /// Allow to delete byte on control
        /// </summary>
        public bool AllowDeleteByte
        {
            get => (bool)GetValue(AllowDeleteByteProperty);
            set => SetValue(AllowDeleteByteProperty, value);
        }

        public static readonly DependencyProperty AllowDeleteByteProperty =
            DependencyProperty.Register(nameof(AllowDeleteByte), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(true, Control_DeletePropertyChanged));

        /// <summary>
        /// Hide bytes that are deleted in the viewers
        /// </summary>
        public bool HideByteDeleted
        {
            get => (bool)GetValue(HideByteDeletedProperty);
            set => SetValue(HideByteDeletedProperty, value);
        }

        public static readonly DependencyProperty HideByteDeletedProperty =
            DependencyProperty.Register(nameof(HideByteDeleted), typeof(bool), typeof(HexEditorLegacy),
                 new FrameworkPropertyMetadata(true, Control_DeletePropertyChanged));

        private static void Control_DeletePropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorLegacy ctrl && e.NewValue != e.OldValue)
                ctrl.RefreshView(true);
        }

        /// <summary>
        /// Select bytes setted by bytePositionInStream with the length
        /// </summary>
        /// <param name="bytePositionInStream">First byte to delete</param>
        /// <param name="length">Number of byte to delete</param>
        public void DeleteBytesAtPosition(long bytePositionInStream, long length = 1)
        {
            var previousSelectionStart = SelectionStart;
            var previousSelectionStop = SelectionStop;

            SelectionStart = bytePositionInStream;
            SelectionStop = bytePositionInStream + length;

            DeleteSelection();

            SelectionStart = previousSelectionStart;
            SelectionStop = previousSelectionStop;
        }

        /// <summary>
        /// Delete selection, add scroll marker and update control
        /// </summary>
        public void DeleteSelection()
        {
            var position = SelectionStart > SelectionStop
                ? SelectionStop
                : SelectionStart;

            var lastPosition = _byteModificationService.DeleteBytes(_provider, position, SelectionLength, ReadOnlyMode, CanDelete);

            if (lastPosition == -1) return;

            //Prevent to move down the scrollbar
            _setFocusTest = true;

            SetScrollMarker(position, ScrollMarker.ByteDeleted);
            _findReplaceService.ClearCache();  // Clear search cache after deletion

            UpdateScrollBar();
            RefreshView(true);
            UpdateStatusBar();

            //Update selection and focus
            SetPosition(FirstVisibleBytePosition);
            SelectionStart = SelectionStop = GetValidPositionFrom(lastPosition, 0);
            SetFocusAtSelectionStart();

            //Launch deleted event
            BytesDeleted?.Invoke(this, EventArgs.Empty);

            //Prevent to move down the scrollbar
            _setFocusTest = false;
        }

        #endregion

        #region Byte click and double click support

        /// <summary>
        /// Select all same byte of SelectionStart in rage of SelectionStart at double click
        /// </summary>
        public bool AllowAutoSelectSameByteAtDoubleClick
        {
            get => (bool)GetValue(AllowAutoSelectSameByteAtDoubleClickProperty);
            set => SetValue(AllowAutoSelectSameByteAtDoubleClickProperty, value);
        }

        public static readonly DependencyProperty AllowAutoSelectSameByteAtDoubleClickProperty =
            DependencyProperty.Register(nameof(AllowAutoSelectSameByteAtDoubleClick), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(true, Control_AllowAutoSelectSameByteAtDoubleClick));

        private static void Control_AllowAutoSelectSameByteAtDoubleClick(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

            ctrl.RefreshView();
        }

        private void Control_Click(object sender, EventArgs e)
        {
            if (sender is not IByteControl ctrl) return;

            if (Keyboard.Modifiers == ModifierKeys.Shift)
                SelectionStop = ctrl.BytePositionInStream;
            else
                SelectionStart = SelectionStop = ctrl.BytePositionInStream;

            UpdateSelectionColor(ctrl is StringByte ? FirstColor.StringByteData : FirstColor.HexByteData);
            UpdateVisual();

            //launch click event 
            ByteClick?.Invoke(sender, new ByteEventArgs(SelectionStart));
        }

        private void Control_DoubleClick(object sender, EventArgs e)
        {
            if (sender is not IByteControl ctrl) return;
            if (!CheckIsOpen(_provider)) return;

            #region Select all same byte of SelectionStart in rage of selectionStart
            if (AllowAutoSelectSameByteAtDoubleClick)
            {
                var (singleByte, succes) = _provider.GetByte(SelectionStart);
                if (succes)
                {
                    var startPosition = SelectionStart;
                    var stopPosition = SelectionStop;

                    //Selection start
                    while (_provider.GetByte(GetValidPositionFrom(startPosition--, -1)).singleByte == singleByte && startPosition > 0)
                        SelectionStart = startPosition;

                    //Selection stop
                    while (_provider.GetByte(GetValidPositionFrom(stopPosition++, 1)).singleByte == singleByte && stopPosition < _provider.Length)
                        SelectionStop = stopPosition;
                }
            }
            #endregion

            UpdateSelectionColor(ctrl is StringByte ? FirstColor.StringByteData : FirstColor.HexByteData);
            UpdateVisual();

            //launch click event 
            ByteDoubleClick?.Invoke(sender, new ByteEventArgs(SelectionStart));
        }
        #endregion

        #region Custom Background Block implementation

        /// <summary>
        /// Get or set if Custom Background Block are allowed
        /// </summary>
        public bool AllowCustomBackgroundBlock
        {
            get => (bool)GetValue(AllowCustomBackgroundBlockProperty);
            set => SetValue(AllowCustomBackgroundBlockProperty, value);
        }

        public static readonly DependencyProperty AllowCustomBackgroundBlockProperty =
            DependencyProperty.Register(nameof(AllowCustomBackgroundBlock), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(false, Control_CustomBackgroundBlockPropertyChanged));

        private static void Control_CustomBackgroundBlockPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

            ctrl.RefreshView();
        }

        /// <summary>
        /// Add of remove CustomBackgroundBlock in this list to use in hexeditor
        /// </summary>
        public List<CustomBackgroundBlock> CustomBackgroundBlockItems
        {
            get => _customBackgroundService.GetAllBlocks().ToList();
            set
            {
                _customBackgroundService.ClearAll();
                if (value != null)
                    _customBackgroundService.AddBlocks(value);
            }
        }

        /// <summary>
        /// Get the first CustomBackgroundBlock finded in list.
        /// </summary>
        public CustomBackgroundBlock GetCustomBackgroundBlock(long position) =>
            _customBackgroundService.GetBlockAt(position);

        /// <summary>
        /// Clear the list of custom background block
        /// </summary>
        public void ClearCustomBackgroundBlock()
        {
            _customBackgroundService.ClearAll();
            RefreshView(true);
        }

        #endregion

        #region Compare file and find difference methods

        /// <summary>
        /// Compare this stream with another and get all bytes difference
        /// </summary>
        /// <returns>Return each byte not equal in the two provider</returns>
        public IEnumerable<ByteDifference> Compare(ByteProviderLegacy providerToCompare, bool compareChange = false) =>
            _provider.Compare(providerToCompare, compareChange);

        /// <summary>
        /// Compare this stream with another and get all bytes difference
        /// </summary>
        /// <returns>Return each byte not equal in the two provider</returns>
        public IEnumerable<ByteDifference> Compare(HexEditorLegacy hexeditor, bool compareChange = false) =>
            _provider?.Compare(hexeditor?._provider, compareChange);

        #endregion

        #region Commands implementation (In early stage of development)

        public static readonly DependencyProperty RefreshViewCommandProperty =
            DependencyProperty.Register(
                nameof(RefreshViewCommand),
                typeof(ICommand),
                typeof(HexEditorLegacy)
            );

        public static readonly DependencyProperty SubmitChangesCommandProperty =
            DependencyProperty.Register(
                nameof(SubmitChangesCommand),
                typeof(ICommand),
                typeof(HexEditorLegacy)
            );

        public ICommand RefreshViewCommand
        {
            get => (ICommand)GetValue(RefreshViewCommandProperty);
            set => SetValue(RefreshViewCommandProperty, value);
        }

        public ICommand SubmitChangesCommand
        {
            get => (ICommand)GetValue(SubmitChangesCommandProperty);
            set => SetValue(SubmitChangesCommandProperty, value);
        }

        #endregion

        #region Insert byte Anywhere support (In early stage of development)

        /// <summary>
        /// Give the possibility to inserts byte Anywhere.
        /// </summary>
        public bool CanInsertAnywhere
        {
            get { return (bool)GetValue(CanInsertAnywhereProperty); }
            set { SetValue(CanInsertAnywhereProperty, value); }
        }

        public static readonly DependencyProperty CanInsertAnywhereProperty =
            DependencyProperty.Register(nameof(CanInsertAnywhere), typeof(bool), typeof(HexEditorLegacy),
                new FrameworkPropertyMetadata(false, Control_CanInsertAnywhereChanged));

        private static void Control_CanInsertAnywhereChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditorLegacy ctrl || e.NewValue == e.OldValue) return;

            if (CheckIsOpen(ctrl._provider))
                ctrl._provider.CanInsertAnywhere = (bool)e.NewValue;

            ctrl.RefreshView();
        }

        /// <summary>
        /// Insert byte at the specified position
        /// </summary>
        public void InsertByte(byte @byte, long bytePositionInStream) =>
            InsertByte(@byte, bytePositionInStream, 1);

        /// <summary>
        /// Insert byte at the specified position for the length
        /// </summary>
        public void InsertByte(byte @byte, long bytePositionInStream, long length)
        {
            if (!_byteModificationService.InsertByte(_provider, @byte, bytePositionInStream, length, CanInsertAnywhere))
                return;

            _findReplaceService.ClearCache();  // Clear search cache after modification
            RefreshView();
        }

        /// <summary>
        /// Insert an array of byte at specified position
        /// </summary>
        public void InsertBytes(byte[] bytes, long bytePositionInStream)
        {
            if (_byteModificationService.InsertBytes(_provider, bytes, bytePositionInStream, CanInsertAnywhere) == 0)
                return;

            _findReplaceService.ClearCache();  // Clear search cache after modification
            RefreshView();
        }
        #endregion
    }
}