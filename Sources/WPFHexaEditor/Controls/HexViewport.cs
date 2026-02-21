//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
// High-performance custom rendering viewport for HexEditor (V2 architecture)
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexaEditor.Core;
using WpfHexaEditor.Models;

namespace WpfHexaEditor.Controls
{
    /// <summary>
    /// Active panel type for dual-color selection
    /// </summary>
    public enum ActivePanelType
    {
        Hex,
        Ascii
    }

    /// <summary>
    /// High-performance custom rendering viewport that draws hex bytes directly using DrawingContext.
    /// Eliminates WPF binding/template/virtualization overhead for maximum performance.
    /// </summary>
    public class HexViewport : FrameworkElement
    {
        #region Fields

        private ObservableCollection<HexLine> _linesSource;
        private List<HexLine> _linesCached = new();
        private int _bytesPerLine = 16;
        private long _cursorPosition = 0;
        private long _selectionStart = -1;
        private long _selectionStop = -1;
        private HashSet<long> _highlightedPositions = new();
        private List<Core.CustomBackgroundBlock> _customBackgroundBlocks = new();
        private Core.CharacterTable.TblStream _tblStream; // Phase 7.5: TBL for character type detection
        private bool _showByteToolTip = false; // V1 compatible: Show tooltip on byte hover
        private System.Windows.Controls.ToolTip _byteToolTip; // Custom tooltip that follows mouse

        // Cached resources
        private Typeface _typeface;
        private Typeface _boldTypeface;
        private double _fontSize = 14;
        private double _charWidth;
        private double _charHeight;
        private double _lineHeight;

        // Layout constants
        private const double OffsetWidth = 110;
        private const double HexByteWidth = 24;
        private const double HexByteSpacing = 2;
        private const double SeparatorWidth = 20;
        private const double AsciiCharWidth = 10;
        private const double LeftMargin = 8;
        private const double TopMargin = 2;

        // Colors
        private Brush _offsetBrush = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));
        private Brush _normalByteBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00)); // Black (V1 default)
        private Brush _alternateByteBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0xFF)); // Blue (V1 default)
        private Brush _selectedBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0x78, 0xD4)); // #0078D4 with 40% opacity
        private Brush _modifiedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)); // Orange
        private Brush _addedBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Green
        private Brush _addedBackgroundBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xC8, 0xE6, 0xC9)); // Light green background for inserted bytes (50% opacity, lighter shade #C8E6C9)
        private Brush _deletedBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)); // Red
        private Pen _cursorPen;
        private Pen _actionPen;
        private Brush _separatorBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        private Brush _asciiBrush = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x42));

        // TBL type visibility flags (all true by default)
        private bool _showTblAscii = true;
        private bool _showTblDte = true;
        private bool _showTblMte = true;
        private bool _showTblJaponais = true;
        private bool _showTblEndBlock = true;
        private bool _showTblEndLine = true;

        private Brush _tblDteBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x00)); // Yellow
        private Brush _tblMteBrush = new SolidColorBrush(Color.FromRgb(0xAD, 0xD8, 0xE6)); // LightBlue
        private Brush _tblEndBlockBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)); // Red
        private Brush _tblEndLineBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)); // Orange
        private Brush _tblAsciiBrush = new SolidColorBrush(Color.FromRgb(0x90, 0xEE, 0x90)); // LightGreen
        private Brush _tblJaponaisBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xC0, 0xCB)); // Pink
        private Brush _tbl3ByteBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xFF)); // Cyan
        private Brush _tbl4PlusByteBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0xFF)); // Magenta

        // Debug counter to avoid spamming logs every frame
        private int _debugRenderCount = 0;
        private Brush _tblDefaultBrush = new SolidColorBrush(Colors.White);

        // Auto-highlight (V1 compatible feature)
        private byte? _autoHighlightByteValue = null; // Byte value to highlight
        private Brush _autoHighLiteBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0x00)); // 40% Yellow

        // Search results highlight (yellow for better visibility)
        private Brush _doubleClickHighlightBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0x00)); // 50% Yellow

        // Caret for Insert mode (flashing vertical line)
        private Caret _caret;

        // Mouse drag selection support
        private bool _isMouseDown = false;
        private long? _dragStartPosition = null;

        // Active panel tracking for dual-color selection
        private ActivePanelType _activePanel = ActivePanelType.Hex;

        // Mouse hover preview (shows which byte will be selected)
        private long _mouseHoverPosition = -1; // Position of byte under mouse cursor
        private bool _mouseHoverInHexArea = true; // True if hovering in hex area, false if in ASCII area
        private Brush _mouseHoverBrush = new SolidColorBrush(Color.FromRgb(255, 140, 0)); // Dark orange FULLY OPAQUE - HDR compatible

        // Refresh time tracking
        private System.Diagnostics.Stopwatch _refreshStopwatch = new System.Diagnostics.Stopwatch();
        private long _lastRefreshTimeMs = 0;

        // Phase 6 (Bug 4): Dynamic CellWidth cache for Font/DPI support
        // Cache stores calculated cell widths based on byte count, font size, and font family
        // Key: (byteCount, fontSize, fontFamily) → Value: calculated width in pixels
        // Invalidated when font settings change (FontSize, FontFamily, DPI)
        private Dictionary<(int byteCount, double fontSize, string fontFamily), double> _cellWidthCache = new();
        private double _dpi = 1.0; // DPI scale factor (1.0 = 96 DPI, 1.5 = 144 DPI, etc.)

        /// <summary>
        /// Get the last visible byte position in the viewport (matches Legacy behavior)
        /// Only counts lines that are FULLY visible (not cut off by status bar or bottom edge)
        /// </summary>
        public long LastVisibleBytePosition
        {
            get
            {
                if (_linesCached == null || _linesCached.Count == 0)
                    return -1;

                // Simple approach: Exclude last 3 lines to account for status bar and UI elements
                // This matches Legacy behavior where status bar hides bottom lines
                const int linesToExclude = 3;
                int lastVisibleLineIndex = _linesCached.Count - linesToExclude - 1;

                if (lastVisibleLineIndex >= 0 && lastVisibleLineIndex < _linesCached.Count)
                {
                    var line = _linesCached[lastVisibleLineIndex];
                    if (line.Bytes != null && line.Bytes.Count > 0)
                    {
                        var lastByte = line.Bytes[line.Bytes.Count - 1];
                        return lastByte.VirtualPos.IsValid ? lastByte.VirtualPos.Value : -1;
                    }
                }

                return -1;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when the refresh time is updated after rendering
        /// </summary>
        public event EventHandler<long> RefreshTimeUpdated;

        #endregion

        #region Constructor

        public HexViewport()
        {
            // Initialize typeface (matches HexEditorLegacy default)
            _typeface = new Typeface(new FontFamily("Courier New"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
            _boldTypeface = new Typeface(new FontFamily("Courier New"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

            // Calculate character dimensions
            CalculateCharacterDimensions();

            // Initialize DPI scale factor (Bug 4 - Font/DPI support)
            _dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Cursor pen (blue, thick)
            _cursorPen = new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), 2.5);
            _cursorPen.Freeze();

            // Action border pen
            _actionPen = new Pen(Brushes.Transparent, 1.5);

            // Make focusable for keyboard input
            Focusable = true;

            // Initialize custom tooltip that follows mouse
            _byteToolTip = new System.Windows.Controls.ToolTip
            {
                Placement = PlacementMode.Relative,
                PlacementTarget = this,
                HorizontalOffset = 0,
                VerticalOffset = 20 // Offset below cursor
            };
            ToolTip = _byteToolTip;

            // Freeze brushes for performance
            _offsetBrush.Freeze();
            _normalByteBrush.Freeze();
            _alternateByteBrush.Freeze();
            _selectedBrush.Freeze();
            _modifiedBrush.Freeze();
            _addedBrush.Freeze();
            _addedBackgroundBrush.Freeze();
            _deletedBrush.Freeze();
            _separatorBrush.Freeze();
            _asciiBrush.Freeze();

            // Freeze TBL brushes (Phase 7.5)
            _tblDteBrush.Freeze();
            _tblMteBrush.Freeze();
            _tblEndBlockBrush.Freeze();
            _tblEndLineBrush.Freeze();
            _tblDefaultBrush.Freeze();
            _tbl3ByteBrush.Freeze();
            _tbl4PlusByteBrush.Freeze();

            // Initialize caret for Insert mode
            _caret = new Caret(new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4))); // Blue caret
            _caret.CaretHeight = _charHeight;
            _caret.CaretWidth = _charWidth;
            _caret.CaretMode = CaretMode.Insert; // Start in Insert mode (vertical line)
            _caret.BlinkPeriod = 500; // Blink every 500ms
            AddVisualChild(_caret);
            AddLogicalChild(_caret);
            _caret.Start(); // Start blinking
        }

        /// <summary>
        /// Required for custom FrameworkElement with child visuals (caret)
        /// </summary>
        protected override int VisualChildrenCount => 1; // Only the caret

        /// <summary>
        /// Required for custom FrameworkElement with child visuals (caret)
        /// </summary>
        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _caret;
        }

        #endregion

        #region Character Dimension Calculation

        private void CalculateCharacterDimensions()
        {
            var formattedText = new FormattedText(
                "FF",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            _charWidth = formattedText.Width / 2.0;
            _charHeight = formattedText.Height;
            _lineHeight = _charHeight + 4; // Add padding
        }

        /// <summary>
        /// Phase 6 (Bug 4): Calculate dynamic cell width based on actual font/DPI settings
        /// Uses FormattedText to measure real text width and caches results for performance
        /// </summary>
        /// <param name="byteCount">Number of bytes in the cell (1, 2, 3, or 4)</param>
        /// <returns>Cell width in pixels (includes 4px padding)</returns>
        private double CalculateCellWidth(int byteCount)
        {
            // Create cache key based on current font settings
            var key = (byteCount, _fontSize, _typeface.FontFamily.Source);

            // Return cached value if available
            if (_cellWidthCache.TryGetValue(key, out double cachedWidth))
                return cachedWidth;

            // Measure actual text width using FormattedText
            // Use "FF" repeated to simulate hex string (1 byte = "FF", 2 bytes = "FFFF", etc.)
            string sampleText = new string('F', byteCount * 2);
            var formattedText = new FormattedText(
                sampleText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                Brushes.Black,
                _dpi
            );

            // Add small padding to prevent text clipping (4px total)
            double width = formattedText.Width + 4;

            // Cache the result
            _cellWidthCache[key] = width;

            return width;
        }

        /// <summary>
        /// Phase 6 (Bug 4): Get dynamic cell width for a ByteData object
        /// Wrapper method that extracts byte count and calls CalculateCellWidth
        /// </summary>
        /// <param name="byteData">ByteData object with Values array</param>
        /// <returns>Cell width in pixels</returns>
        private double GetDynamicCellWidth(ByteData byteData)
        {
            if (byteData == null || byteData.Values == null || byteData.Values.Length == 0)
                return CalculateCellWidth(1); // Fallback to 1-byte width

            return CalculateCellWidth(byteData.Values.Length);
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Lines to display (from ViewModel's ObservableCollection)
        /// </summary>
        public ObservableCollection<HexLine> LinesSource
        {
            get => _linesSource;
            set
            {
                // Unsubscribe from old collection
                if (_linesSource != null)
                {
                    _linesSource.CollectionChanged -= LinesSource_CollectionChanged;
                }

                _linesSource = value;

                // Subscribe to new collection
                if (_linesSource != null)
                {
                    _linesSource.CollectionChanged += LinesSource_CollectionChanged;
                    UpdateCachedLines();
                }
                else
                {
                    _linesCached.Clear();
                    InvalidateVisual();
                }
            }
        }

        private void LinesSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateCachedLines();
        }

        /// <summary>
        /// Gets or sets the list of custom background blocks for highlighting byte ranges.
        /// Phase 7.1 - V1 Compatible feature.
        /// </summary>
        public List<Core.CustomBackgroundBlock> CustomBackgroundBlocks
        {
            get => _customBackgroundBlocks;
            set
            {
                _customBackgroundBlocks = value ?? new List<Core.CustomBackgroundBlock>();
                InvalidateVisual(); // Trigger re-render
            }
        }

        private void UpdateCachedLines()
        {
            _linesCached.Clear();
            if (_linesSource != null)
            {
                foreach (var line in _linesSource)
                {
                    _linesCached.Add(line);
                }
            }
            InvalidateVisual();
        }

        /// <summary>
        /// Bytes per line
        /// </summary>
        public int BytesPerLine
        {
            get => _bytesPerLine;
            set
            {
                _bytesPerLine = value;
                _linesCached.Clear(); // Clear cached lines - they use old BytesPerLine
                InvalidateMeasure(); // Force layout recalculation
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Cursor position
        /// </summary>
        public long CursorPosition
        {
            get => _cursorPosition;
            set
            {
                if (_cursorPosition != value)
                {
                    _cursorPosition = value;
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Selection start position
        /// </summary>
        public long SelectionStart
        {
            get => _selectionStart;
            set
            {
                if (_selectionStart != value)
                {
                    _selectionStart = value;
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Selection stop position
        /// </summary>
        public long SelectionStop
        {
            get => _selectionStop;
            set
            {
                if (_selectionStop != value)
                {
                    _selectionStop = value;
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Highlighted positions (search results)
        /// </summary>
        public HashSet<long> HighlightedPositions
        {
            get => _highlightedPositions;
            set
            {
                _highlightedPositions = value ?? new();
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Auto-highlight byte value (V1 compatible) - All bytes with this value will be highlighted
        /// </summary>
        public byte? AutoHighlightByteValue
        {
            get => _autoHighlightByteValue;
            set
            {
                if (_autoHighlightByteValue != value)
                {
                    _autoHighlightByteValue = value;
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Auto-highlight brush color (V1 compatible)
        /// </summary>
        public Brush AutoHighLiteBrush
        {
            get => _autoHighLiteBrush;
            set
            {
                _autoHighLiteBrush = value;
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Gets the actual line height used for rendering
        /// </summary>
        public double LineHeight => _lineHeight;

        /// <summary>
        /// Byte spacer positioning (V1 compatible)
        /// </summary>
        public ByteSpacerPosition ByteSpacerPositioning { get; set; } = ByteSpacerPosition.Both;

        /// <summary>
        /// Byte spacer width (V1 compatible)
        /// </summary>
        public ByteSpacerWidth ByteSpacerWidthTickness { get; set; } = ByteSpacerWidth.Normal;

        /// <summary>
        /// Byte grouping for spacers (V1 compatible)
        /// </summary>
        public ByteSpacerGroup ByteGrouping { get; set; } = ByteSpacerGroup.EightByte;

        /// <summary>
        /// Byte spacer visual style (V1 compatible)
        /// </summary>
        public ByteSpacerVisual ByteSpacerVisualStyle { get; set; } = ByteSpacerVisual.Empty;

        /// <summary>
        /// Current edit mode (Insert or Overwrite) for caret display
        /// </summary>
        public EditMode EditMode { get; set; } = EditMode.Overwrite;

        /// <summary>
        /// Show or hide offset column (V1 compatible)
        /// </summary>
        public bool ShowOffset { get; set; } = true;

        /// <summary>
        /// Show or hide ASCII column (V1 compatible)
        /// </summary>
        public bool ShowAscii { get; set; } = true;

        /// <summary>
        /// Gets the actual offset column width (0 if ShowOffset is false, 110 if true)
        /// </summary>
        public double ActualOffsetWidth => ShowOffset ? OffsetWidth : 0;

        /// <summary>
        /// Gets the starting X position for hex bytes panel
        /// </summary>
        public double HexPanelStartX => ActualOffsetWidth;

        /// <summary>
        /// Gets the starting X position for separator (after hex bytes)
        /// Accounts for bytes per line and byte spacers
        /// </summary>
        public double SeparatorStartX
        {
            get
            {
                double hexStartX = ActualOffsetWidth;

                // Calculate number of byte spacers for a full line
                int numSpacers = 0;
                if (_bytesPerLine >= (int)ByteGrouping)
                {
                    numSpacers = (_bytesPerLine % (int)ByteGrouping == 0)
                        ? (_bytesPerLine / (int)ByteGrouping) - 1
                        : _bytesPerLine / (int)ByteGrouping;
                }
                double spacersWidth = numSpacers * (int)ByteSpacerWidthTickness;

                // Separator position (matching OnRender line 693)
                return hexStartX + (_bytesPerLine * (HexByteWidth + HexByteSpacing)) + spacersWidth + 4;
            }
        }

        /// <summary>
        /// Gets the starting X position for ASCII panel
        /// </summary>
        public double AsciiPanelStartX => SeparatorStartX + SeparatorWidth;

        /// <summary>
        /// Gets or sets the active panel (Hex or ASCII)
        /// </summary>
        public ActivePanelType ActivePanel
        {
            get => _activePanel;
            set { _activePanel = value; InvalidateVisual(); }
        }

        /// <summary>
        /// Brush for selection in the active panel
        /// </summary>
        public Brush SelectionActiveBrush { get; set; }

        /// <summary>
        /// Brush for selection in the inactive panel
        /// </summary>
        public Brush SelectionInactiveBrush { get; set; }

        /// <summary>
        /// Brush for mouse hover preview (shows which byte will be selected)
        /// </summary>
        public Brush MouseHoverBrush
        {
            get => _mouseHoverBrush;
            set
            {
                // Ensure the brush is not null - keep fully opaque for HDR screens
                if (value is SolidColorBrush solidBrush)
                {
                    // Use the color as-is (don't reduce opacity - important for HDR screens)
                    _mouseHoverBrush = value;
                }
                else
                {
                    _mouseHoverBrush = value ?? new SolidColorBrush(Color.FromRgb(255, 140, 0)); // Dark orange fully opaque
                }
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Force refresh of cached lines and visual rendering
        /// </summary>
        public void Refresh()
        {
            UpdateCachedLines();
        }

        /// <summary>
        /// Get visible lines for highlighting (used by double-click select feature)
        /// </summary>
        public List<HexLine> GetVisibleLinesForHighlight()
        {
            return _linesCached ?? new List<HexLine>();
        }

        /// <summary>
        /// Show ASCII characters in TBL
        /// </summary>
        public bool ShowTblAscii
        {
            get => _showTblAscii;
            set { _showTblAscii = value; InvalidateVisual(); }
        }

        /// <summary>
        /// Show DTE (Dual-Title Encoding) in TBL
        /// </summary>
        public bool ShowTblDte
        {
            get => _showTblDte;
            set { _showTblDte = value; InvalidateVisual(); }
        }

        /// <summary>
        /// Show MTE (Multi-Title Encoding) in TBL
        /// </summary>
        public bool ShowTblMte
        {
            get => _showTblMte;
            set { _showTblMte = value; InvalidateVisual(); }
        }

        /// <summary>
        /// Show Japanese characters in TBL
        /// </summary>
        public bool ShowTblJaponais
        {
            get => _showTblJaponais;
            set { _showTblJaponais = value; InvalidateVisual(); }
        }

        /// <summary>
        /// Show End Block markers in TBL
        /// </summary>
        public bool ShowTblEndBlock
        {
            get => _showTblEndBlock;
            set { _showTblEndBlock = value; InvalidateVisual(); }
        }

        /// <summary>
        /// Show End Line markers in TBL
        /// </summary>
        public bool ShowTblEndLine
        {
            get => _showTblEndLine;
            set { _showTblEndLine = value; InvalidateVisual(); }
        }

        /// <summary>
        /// DTE (Double Tile Encoding) color - Phase 7.5 V1 Compatibility
        /// </summary>
        public Color TblDteColor
        {
            get => (_tblDteBrush as SolidColorBrush)?.Color ?? Colors.Yellow;
            set { _tblDteBrush = new SolidColorBrush(value); _tblDteBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// MTE (Multi-Title Encoding) color - Phase 7.5 V1 Compatibility
        /// </summary>
        public Color TblMteColor
        {
            get => (_tblMteBrush as SolidColorBrush)?.Color ?? Colors.LightBlue;
            set { _tblMteBrush = new SolidColorBrush(value); _tblMteBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL End Block color - Phase 7.5 V1 Compatibility
        /// </summary>
        public Color TblEndBlockColor
        {
            get => (_tblEndBlockBrush as SolidColorBrush)?.Color ?? Colors.Red;
            set { _tblEndBlockBrush = new SolidColorBrush(value); _tblEndBlockBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL End Line color - Phase 7.5 V1 Compatibility
        /// </summary>
        public Color TblEndLineColor
        {
            get => (_tblEndLineBrush as SolidColorBrush)?.Color ?? Colors.Orange;
            set { _tblEndLineBrush = new SolidColorBrush(value); _tblEndLineBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL ASCII color
        /// </summary>
        public Color TblAsciiColor
        {
            get => (_tblAsciiBrush as SolidColorBrush)?.Color ?? Colors.LightGreen;
            set { _tblAsciiBrush = new SolidColorBrush(value); _tblAsciiBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL Japanese color
        /// </summary>
        public Color TblJaponaisColor
        {
            get => (_tblJaponaisBrush as SolidColorBrush)?.Color ?? Colors.Pink;
            set { _tblJaponaisBrush = new SolidColorBrush(value); _tblJaponaisBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL 3-byte sequences color
        /// </summary>
        public Color Tbl3ByteColor
        {
            get => (_tbl3ByteBrush as SolidColorBrush)?.Color ?? Colors.Cyan;
            set { _tbl3ByteBrush = new SolidColorBrush(value); _tbl3ByteBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL 4+ byte sequences color
        /// </summary>
        public Color Tbl4PlusByteColor
        {
            get => (_tbl4PlusByteBrush as SolidColorBrush)?.Color ?? Colors.Magenta;
            set { _tbl4PlusByteBrush = new SolidColorBrush(value); _tbl4PlusByteBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL Default color - Phase 7.5 V1 Compatibility
        /// </summary>
        public Color TblDefaultColor
        {
            get => (_tblDefaultBrush as SolidColorBrush)?.Color ?? Colors.White;
            set { _tblDefaultBrush = new SolidColorBrush(value); _tblDefaultBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// Color for modified bytes
        /// </summary>
        public Color ModifiedByteColor
        {
            get => (_modifiedBrush as SolidColorBrush)?.Color ?? Color.FromRgb(0xFF, 0xA5, 0x00);
            set { _modifiedBrush = new SolidColorBrush(value); _modifiedBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// Color for added bytes
        /// </summary>
        public Color AddedByteColor
        {
            get => (_addedBrush as SolidColorBrush)?.Color ?? Color.FromRgb(0x4C, 0xAF, 0x50);
            set { _addedBrush = new SolidColorBrush(value); _addedBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// Color for selected bytes
        /// </summary>
        public Color SelectionColor
        {
            get => (_selectedBrush as SolidColorBrush)?.Color ?? Color.FromArgb(0x66, 0x00, 0x78, 0xD4);
            set { _selectedBrush = new SolidColorBrush(value); _selectedBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// Color for highlighted bytes (double-click highlight)
        /// </summary>
        public Color HighlightColor
        {
            get => (_doubleClickHighlightBrush as SolidColorBrush)?.Color ?? Color.FromArgb(0x80, 0xFF, 0xFF, 0x00);
            set { _doubleClickHighlightBrush = new SolidColorBrush(value); _doubleClickHighlightBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// Color for offset header foreground
        /// </summary>
        public Color OffsetForegroundColor
        {
            get => (_offsetBrush as SolidColorBrush)?.Color ?? Color.FromRgb(0x75, 0x75, 0x75);
            set { _offsetBrush = new SolidColorBrush(value); _offsetBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// Color for normal bytes (even columns: 00, 02, 04...)
        /// </summary>
        public Color NormalByteColor
        {
            get => (_normalByteBrush as SolidColorBrush)?.Color ?? Color.FromRgb(0x00, 0x00, 0x00);
            set { _normalByteBrush = new SolidColorBrush(value); _normalByteBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// Color for alternate bytes (odd columns: 01, 03, 05...)
        /// </summary>
        public Color AlternateByteColor
        {
            get => (_alternateByteBrush as SolidColorBrush)?.Color ?? Color.FromRgb(0x00, 0x00, 0xFF);
            set { _alternateByteBrush = new SolidColorBrush(value); _alternateByteBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL Stream for character type detection - Phase 7.5 V1 Compatibility
        /// </summary>
        public Core.CharacterTable.TblStream TblStream
        {
            get => _tblStream;
            set { _tblStream = value; InvalidateVisual(); }
        }

        /// <summary>
        /// Show tooltip on byte hover (V1 compatible - tooltip follows mouse)
        /// </summary>
        public bool ShowByteToolTip
        {
            get => _showByteToolTip;
            set
            {
                _showByteToolTip = value;
                if (!value && _byteToolTip != null)
                {
                    _byteToolTip.IsOpen = false;
                }
            }
        }

        /// <summary>
        /// Update byte foreground colors (V1 compatible - Phase 7.6)
        /// </summary>
        public void SetByteForegroundColors(Brush normalBrush, Brush alternateBrush)
        {
            _normalByteBrush = normalBrush?.Clone() ?? _normalByteBrush;
            _alternateByteBrush = alternateBrush?.Clone() ?? _alternateByteBrush;

            if (_normalByteBrush.CanFreeze && !_normalByteBrush.IsFrozen)
                _normalByteBrush.Freeze();
            if (_alternateByteBrush.CanFreeze && !_alternateByteBrush.IsFrozen)
                _alternateByteBrush.Freeze();

            InvalidateVisual();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Phase 6 (Bug 4): Calculate cell width for a given byte count based on current font/DPI settings
        /// Public method for external use (e.g., RefreshColumnHeader in UIHelpers)
        /// </summary>
        /// <param name="byteCount">Number of bytes (1, 2, 3, or 4)</param>
        /// <returns>Cell width in pixels</returns>
        public double CalculateCellWidthForByteCount(int byteCount)
        {
            return CalculateCellWidth(byteCount);
        }

        /// <summary>
        /// Phase 6 (Bug 4): Update font settings and invalidate CellWidth cache
        /// Call this method whenever FontFamily, FontSize, or DPI changes
        /// </summary>
        /// <param name="fontFamily">New font family (e.g., "Consolas", "Courier New")</param>
        /// <param name="fontSize">New font size in points</param>
        public void UpdateFont(string fontFamily, double fontSize)
        {
            // Update typeface if font family changed
            if (_typeface?.FontFamily.Source != fontFamily)
            {
                _typeface = new Typeface(new FontFamily(fontFamily), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
                _boldTypeface = new Typeface(new FontFamily(fontFamily), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            }

            // Update font size
            _fontSize = fontSize;

            // Update DPI (may have changed on high-DPI displays)
            _dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Recalculate character dimensions
            CalculateCharacterDimensions();

            // CRITICAL: Invalidate cell width cache (Bug 4 fix)
            _cellWidthCache.Clear();

            // Trigger re-render
            InvalidateVisual();
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            // Start timing the refresh
            _refreshStopwatch.Restart();

            base.OnRender(dc);

            if (_linesCached == null || _linesCached.Count == 0)
            {
                _refreshStopwatch.Stop();
                return;
            }

            // Draw white background
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));

            // Draw custom background blocks (before drawing bytes)
            DrawCustomBackgroundBlocks(dc);

            double y = TopMargin;

            foreach (var line in _linesCached)
            {
                if (line.Bytes == null || line.Bytes.Count == 0)
                    continue;

                // Draw offset (if visible)
                if (ShowOffset)
                {
                    DrawOffset(dc, line, y);
                }

                // Draw hex bytes with byte spacers
                double hexX = ShowOffset ? OffsetWidth : 0;

                // Get stride for byte position calculation (in multi-byte mode, i represents groups, not bytes)
                int stride = line.Bytes.Count > 0 ? (line.Bytes[0].ByteSize switch
                {
                    Core.ByteSizeType.Bit8 => 1,
                    Core.ByteSizeType.Bit16 => 2,
                    Core.ByteSizeType.Bit32 => 4,
                    _ => 1
                }) : 1;

                for (int i = 0; i < line.Bytes.Count; i++)
                {
                    // Calculate byte position from group index (i) and stride
                    // In Bit8: bytePos = i (1:1 mapping)
                    // In Bit16: bytePos = i * 2 (each group represents 2 bytes)
                    // In Bit32: bytePos = i * 4 (each group represents 4 bytes)
                    int bytePosition = i * stride;

                    // Draw byte spacer before this byte if needed
                    // Only draw separators if BytePerLine is large enough to have multiple groups
                    if (_bytesPerLine >= (int)ByteGrouping &&
                        (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                         ByteSpacerPositioning == ByteSpacerPosition.HexBytePanel) &&
                        bytePosition % (int)ByteGrouping == 0 && i > 0)
                    {
                        DrawByteSpacer(dc, hexX, y);
                        hexX += (int)ByteSpacerWidthTickness;
                    }

                    var byteData = line.Bytes[i];
                    DrawHexByte(dc, byteData, hexX, y);
                    // Phase 3: Use dynamic cell width based on ByteSize instead of fixed HexByteWidth
                    // Bug 4: Use GetDynamicCellWidth() for Font/DPI support
                    hexX += GetDynamicCellWidth(byteData) + HexByteSpacing;
                }

                // Draw separator and ASCII (if visible)
                if (ShowAscii)
                {
                    // Phase 4: Calculate separator position using actual hexX position (same as hit testing!)
                    // hexX now contains the end of hex area after iterating through all bytes
                    double separatorX = hexX + 4; // Small margin before separator (same as hit testing)
                    dc.DrawRectangle(_separatorBrush, null, new Rect(separatorX, y, 1, _lineHeight));

                    // Phase 4: Draw ASCII bytes - SAME LOGIC as hit testing (simple loop)
                    double asciiX = separatorX + SeparatorWidth;
                    for (int i = 0; i < line.Bytes.Count; i++)
                    {
                        // Calculate byte position from group index (matches hex area logic)
                        int bytePosition = i * stride;

                        // Draw byte spacer before this byte if needed
                        // Only draw separators if BytePerLine is large enough to have multiple groups
                        if (_bytesPerLine >= (int)ByteGrouping &&
                            (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                             ByteSpacerPositioning == ByteSpacerPosition.StringBytePanel) &&
                            bytePosition % (int)ByteGrouping == 0 && i > 0)
                        {
                            DrawByteSpacer(dc, asciiX, y);
                            asciiX += (int)ByteSpacerWidthTickness;
                        }

                        // Draw this ByteData's ASCII character(s)
                        // DrawAsciiByte returns the actual width used
                        double usedWidth = DrawAsciiByte(dc, line, i, asciiX, y);
                        asciiX += usedWidth;

                        // Simple increment (no skipping) - same as Hex and hit testing
                    }
                }

                y += _lineHeight;
            }

            // Stop timing and raise event
            _refreshStopwatch.Stop();
            _lastRefreshTimeMs = _refreshStopwatch.ElapsedMilliseconds;
            RefreshTimeUpdated?.Invoke(this, _lastRefreshTimeMs);
        }

        private void DrawCustomBackgroundBlocks(DrawingContext dc)
        {
            // Use existing custom background blocks from HexViewport
            if (_customBackgroundBlocks == null || _customBackgroundBlocks.Count == 0 || _linesCached == null || _linesCached.Count == 0)
                return;

            var blocks = _customBackgroundBlocks;

            // Calculate visible range
            long firstVisiblePos = _linesCached[0].Bytes[0].VirtualPos;
            long lastVisiblePos = _linesCached[_linesCached.Count - 1].Bytes[_linesCached[_linesCached.Count - 1].Bytes.Count - 1].VirtualPos;

            foreach (var block in blocks)
            {
                // Skip blocks outside visible range (manual overlap check)
                if (block.StartOffset >= lastVisiblePos + 1 || block.StopOffset <= firstVisiblePos)
                    continue;

                // Draw block (may span multiple lines)
                DrawCustomBackgroundBlock(dc, block, firstVisiblePos, lastVisiblePos);
            }
        }

        private void DrawCustomBackgroundBlock(DrawingContext dc, Core.CustomBackgroundBlock block, long firstVisiblePos, long lastVisiblePos)
        {
            double y = TopMargin;
            double hexStartX = ShowOffset ? OffsetWidth : 0;
            double asciiStartX = hexStartX + (_bytesPerLine * (HexByteWidth + HexByteSpacing)) + 4 + SeparatorWidth;

            // Calculate byte spacers width
            int numSpacers = 0;
            if (_bytesPerLine >= (int)ByteGrouping)
            {
                numSpacers = (_bytesPerLine % (int)ByteGrouping == 0)
                    ? (_bytesPerLine / (int)ByteGrouping) - 1
                    : _bytesPerLine / (int)ByteGrouping;
            }
            double spacersWidth = numSpacers * (int)ByteSpacerWidthTickness;
            asciiStartX += spacersWidth;

            // Clone brush and make semi-transparent
            var brush = block.Color.Clone();
            brush.Opacity = 0.3; // Semi-transparent

            foreach (var line in _linesCached)
            {
                if (line.Bytes == null || line.Bytes.Count == 0)
                {
                    y += _lineHeight;
                    continue;
                }

                long lineStartPos = line.Bytes[0].VirtualPos;
                long lineEndPos = line.Bytes[line.Bytes.Count - 1].VirtualPos;

                // Check if block overlaps with this line (manual check)
                if (block.StartOffset < lineEndPos + 1 && block.StopOffset > lineStartPos)
                {
                    // Calculate which bytes in this line are part of the block
                    int startByteIndex = 0;
                    int endByteIndex = line.Bytes.Count - 1;

                    for (int i = 0; i < line.Bytes.Count; i++)
                    {
                        if (line.Bytes[i].VirtualPos >= block.StartOffset)
                        {
                            startByteIndex = i;
                            break;
                        }
                    }

                    for (int i = line.Bytes.Count - 1; i >= 0; i--)
                    {
                        if (line.Bytes[i].VirtualPos < block.StopOffset)
                        {
                            endByteIndex = i;
                            break;
                        }
                    }

                    // Draw background for hex bytes
                    double hexX = hexStartX;
                    // Account for byte spacers before start byte
                    for (int i = 0; i < startByteIndex; i++)
                    {
                        if (_bytesPerLine >= (int)ByteGrouping && i > 0 && i % (int)ByteGrouping == 0)
                        {
                            hexX += (int)ByteSpacerWidthTickness;
                        }
                        hexX += HexByteWidth + HexByteSpacing;
                    }

                    double blockStartX = hexX;
                    double blockWidth = 0;

                    for (int i = startByteIndex; i <= endByteIndex; i++)
                    {
                        if (_bytesPerLine >= (int)ByteGrouping && i > 0 && i % (int)ByteGrouping == 0)
                        {
                            blockWidth += (int)ByteSpacerWidthTickness;
                        }
                        blockWidth += HexByteWidth + HexByteSpacing;
                    }

                    dc.DrawRectangle(brush, null, new Rect(blockStartX, y, blockWidth, _lineHeight));

                    // Draw background for ASCII bytes (if visible)
                    if (ShowAscii)
                    {
                        double asciiX = asciiStartX;
                        for (int i = 0; i < startByteIndex; i++)
                        {
                            if (_bytesPerLine >= (int)ByteGrouping && i > 0 && i % (int)ByteGrouping == 0)
                            {
                                asciiX += (int)ByteSpacerWidthTickness;
                            }
                            asciiX += AsciiCharWidth;
                        }

                        double asciiBlockStartX = asciiX;
                        double asciiBlockWidth = 0;

                        for (int i = startByteIndex; i <= endByteIndex; i++)
                        {
                            if (_bytesPerLine >= (int)ByteGrouping && i > 0 && i % (int)ByteGrouping == 0)
                            {
                                asciiBlockWidth += (int)ByteSpacerWidthTickness;
                            }
                            asciiBlockWidth += AsciiCharWidth;
                        }

                        dc.DrawRectangle(brush, null, new Rect(asciiBlockStartX, y, asciiBlockWidth, _lineHeight));
                    }
                }

                y += _lineHeight;
            }
        }

        private void DrawOffset(DrawingContext dc, HexLine line, double y)
        {
            // Check if SelectionStart is on this line (use bold typeface if true)
            bool isSelectionStartLine = false;
            if (_selectionStart >= 0 && line.StartPosition.IsValid && line.Bytes.Count > 0)
            {
                long lineStart = line.StartPosition.Value;
                long lineEnd = lineStart + line.Bytes.Count - 1;
                isSelectionStartLine = (_selectionStart >= lineStart && _selectionStart <= lineEnd);
            }

            var typeface = isSelectionStartLine ? _boldTypeface : _typeface;

            var formattedText = new FormattedText(
                line.OffsetLabel,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                13,
                _offsetBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(formattedText, new Point(LeftMargin, y + 2));
        }

        private void DrawHexByte(DrawingContext dc, ByteData byteData, double x, double y)
        {
            // Phase 3: Calculate cell width dynamically based on ByteSize
            // Bug 4: Use GetDynamicCellWidth() for Font/DPI support
            // Bit8/16/32 width now adapts to current font size and DPI
            double cellWidth = GetDynamicCellWidth(byteData);
            double byteWidth = cellWidth - HexByteSpacing;
            var rect = new Rect(x, y, byteWidth, _lineHeight);

            // Phase 7.1: Draw custom background block FIRST (underneath everything)
            if (_customBackgroundBlocks != null && _customBackgroundBlocks.Count > 0)
            {
                long position = byteData.VirtualPos.Value;
                foreach (var block in _customBackgroundBlocks)
                {
                    if (position >= block.StartOffset && position <= block.StopOffset)
                    {
                        dc.DrawRoundedRectangle(block.Color, null, rect, 2, 2);
                        break; // Only draw first matching block
                    }
                }
            }

            // Auto-highlight matching bytes (V1 compatible feature)
            if (_autoHighlightByteValue.HasValue && byteData.Value == _autoHighlightByteValue.Value)
            {
                dc.DrawRoundedRectangle(_autoHighLiteBrush, null, rect, 2, 2);
            }

            // Double-click highlighted positions (V1 compatible feature) - light blue background
            if (_highlightedPositions != null && _highlightedPositions.Contains(byteData.VirtualPos.Value))
            {
                dc.DrawRoundedRectangle(_doubleClickHighlightBrush, null, rect, 2, 2);
            }

            // Draw mouse hover preview (shows which byte will be selected on click)
            // Only show in hex area if mouse is hovering in hex area (matches Legacy behavior)
            if (_mouseHoverPosition >= 0 && _mouseHoverInHexArea &&
                byteData.VirtualPos.Value == _mouseHoverPosition &&
                _mouseHoverBrush != null)
            {
                dc.DrawRoundedRectangle(_mouseHoverBrush, null, rect, 2, 2);
            }

            // Draw selection background (on top of custom background and auto-highlight)
            bool isSelected = IsPositionSelected(byteData.VirtualPos.Value);
            if (isSelected)
            {
                // Use active or inactive brush based on which panel is active
                Brush selectionBrush = (_activePanel == ActivePanelType.Hex && SelectionActiveBrush != null)
                    ? SelectionActiveBrush
                    : (SelectionInactiveBrush != null ? SelectionInactiveBrush : _selectedBrush);
                dc.DrawRoundedRectangle(selectionBrush, null, rect, 2, 2);
            }

            // Draw added byte background (light green) to make inserted bytes more visible
            if (byteData.Action == ByteAction.Added)
            {
                dc.DrawRoundedRectangle(_addedBackgroundBrush, null, rect, 2, 2);
            }

            // Draw action border (slightly inset to show selection underneath)
            if (byteData.Action != ByteAction.Nothing)
            {
                Brush borderBrush = byteData.Action switch
                {
                    ByteAction.Modified => _modifiedBrush,
                    ByteAction.Added => _addedBrush,
                    ByteAction.Deleted => _deletedBrush,
                    _ => Brushes.Transparent
                };

                var borderPen = new Pen(borderBrush, 1.5);
                dc.DrawRoundedRectangle(null, borderPen, rect, 2, 2);

                // Debug: Log action borders (only for Added/Modified, not every frame)
                if ((byteData.Action == ByteAction.Added || byteData.Action == ByteAction.Modified) && _debugRenderCount++ % 60 == 0)
                {
                }
            }

            // Draw cursor border (thicker, on top)
            if (byteData.VirtualPos.Value == _cursorPosition)
            {
                dc.DrawRoundedRectangle(null, _cursorPen, rect, 2, 2);

                // Position caret at cursor byte (only visible in Insert mode)
                // Caret should appear as a flashing vertical line at the left edge of the byte (between bytes)
                if (_caret != null && EditMode == EditMode.Insert)
                {
                    _caret.MoveCaret(x, y);
                    _caret.CaretMode = CaretMode.Insert; // Vertical line
                }
                else if (_caret != null)
                {
                    // In Overwrite mode, use block caret or hide it (V1 shows block)
                    _caret.Hide();
                }
            }

            // Draw hex text centered in the cell
            // V1 compatible: Alternate foreground color every byte for visual grouping
            long bytePosition = byteData.VirtualPos.Value;
            int byteIndexInLine = (int)(bytePosition % _bytesPerLine);
            bool useAlternateColor = byteIndexInLine % 2 == 1; // Alternate every single byte

            Brush textBrush = useAlternateColor ? _alternateByteBrush : _normalByteBrush;

            // Phase 3: Use GetHexText() for multi-byte support with ByteOrder
            var formattedText = new FormattedText(
                byteData.GetHexText(),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Phase 3: Set max width to prevent text overflow in multi-byte cells
            formattedText.MaxTextWidth = byteWidth;

            // Center the text within the byte cell
            double textX = x + (byteWidth - formattedText.Width) / 2;
            double textY = y + (_lineHeight - formattedText.Height) / 2;

            dc.DrawText(formattedText, new Point(textX, textY));
        }

        /// <summary>
        /// Draw ASCII byte with TBL auto-sizing support
        /// </summary>
        /// <returns>Actual width used (may be larger than AsciiCharWidth for TBL characters)</returns>
        private double DrawAsciiByte(DrawingContext dc, HexLine line, int byteIndex, double x, double y)
        {
            var byteData = line.Bytes[byteIndex];

            // STEP 1: Calculate display character and determine TBL type/color FIRST
            var displayChar = GetDisplayCharacter(line, byteIndex);

            // Determine text color based on TBL type
            Brush textBrush = _asciiBrush; // Default
            Core.CharacterTable.DteType dteType = Core.CharacterTable.DteType.Invalid;

            if (_tblStream != null)
            {
                try
                {
                    // Determine how many bytes this character consumes (uses greedy matching)
                    int byteCount = GetCharacterByteCount(line, byteIndex);

                    // Build hex key for the actual byte count
                    var hexKey = new StringBuilder(byteCount * 2);
                    for (int j = 0; j < byteCount && byteIndex + j < line.Bytes.Count; j++)
                        hexKey.Append(line.Bytes[byteIndex + j].Value.ToString("X2"));

                    var (text, type) = _tblStream.FindMatch(hexKey.ToString(), showSpecialValue: true);
                    dteType = type;

                    // Select TEXT color based on TBL type (only if type is visible)
                    bool shouldShow = dteType switch
                    {
                        Core.CharacterTable.DteType.Ascii => _showTblAscii,
                        Core.CharacterTable.DteType.DualTitleEncoding => _showTblDte,
                        Core.CharacterTable.DteType.MultipleTitleEncoding => _showTblMte,
                        Core.CharacterTable.DteType.EndBlock => _showTblEndBlock,
                        Core.CharacterTable.DteType.EndLine => _showTblEndLine,
                        Core.CharacterTable.DteType.Japonais => _showTblJaponais,
                        _ => false
                    };

                    if (shouldShow && text != "#")
                    {
                        // Special types get their specific colors (override byte count logic)
                        if (dteType == Core.CharacterTable.DteType.Japonais)
                            textBrush = _tblJaponaisBrush;
                        else if (dteType == Core.CharacterTable.DteType.EndBlock)
                            textBrush = _tblEndBlockBrush;
                        else if (dteType == Core.CharacterTable.DteType.EndLine)
                            textBrush = _tblEndLineBrush;
                        // For normal types, use color based on byte count
                        else
                        {
                            textBrush = byteCount switch
                            {
                                1 => dteType == Core.CharacterTable.DteType.Ascii ? _tblAsciiBrush : _asciiBrush,
                                2 => dteType == Core.CharacterTable.DteType.DualTitleEncoding ? _tblDteBrush : _tblMteBrush,
                                3 => _tbl3ByteBrush,
                                >= 4 => _tbl4PlusByteBrush,
                                _ => _asciiBrush
                            };
                        }
                    }
                }
                catch
                {
                    // Fall back to default brush on error
                }
            }

            var formattedText = new FormattedText(
                displayChar,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                13,
                textBrush, // Use TBL color for text
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Phase 4: AUTO-SIZING for TBL and multi-byte mode
            // Use actual text width if larger than single char width
            double cellWidth = formattedText.Width > AsciiCharWidth
                ? formattedText.Width
                : AsciiCharWidth;

            // STEP 2: Create rect with dynamic width
            var rect = new Rect(x, y, cellWidth, _lineHeight);

            // STEP 3: Draw backgrounds and borders
            // Phase 7.1: Draw custom background block FIRST (underneath everything)
            if (_customBackgroundBlocks != null && _customBackgroundBlocks.Count > 0)
            {
                long position = byteData.VirtualPos.Value;
                foreach (var block in _customBackgroundBlocks)
                {
                    if (position >= block.StartOffset && position <= block.StopOffset)
                    {
                        dc.DrawRoundedRectangle(block.Color, null, rect, 1, 1);
                        break; // Only draw first matching block
                    }
                }
            }

            // Phase 7.5: Draw TBL background ONLY for special types (EndBlock, EndLine, Japonais)
            if (_tblStream != null && dteType != Core.CharacterTable.DteType.Invalid)
            {
                // Only special types get background color (in addition to text color)
                bool isSpecialType = dteType == Core.CharacterTable.DteType.EndBlock ||
                                     dteType == Core.CharacterTable.DteType.EndLine ||
                                     dteType == Core.CharacterTable.DteType.Japonais;

                if (isSpecialType)
                {
                    bool shouldShow = dteType switch
                    {
                        Core.CharacterTable.DteType.EndBlock => _showTblEndBlock,
                        Core.CharacterTable.DteType.EndLine => _showTblEndLine,
                        Core.CharacterTable.DteType.Japonais => _showTblJaponais,
                        _ => false
                    };

                    if (shouldShow)
                    {
                        // Select background brush for special types
                        Brush bgBrush = dteType switch
                        {
                            Core.CharacterTable.DteType.Japonais => _tblJaponaisBrush,
                            Core.CharacterTable.DteType.EndBlock => _tblEndBlockBrush,
                            Core.CharacterTable.DteType.EndLine => _tblEndLineBrush,
                            _ => null
                        };

                        // Draw semi-transparent background for special types
                        if (bgBrush != null)
                        {
                            var semiBrush = bgBrush.Clone();
                            semiBrush.Opacity = 0.3; // Semi-transparent background
                            dc.DrawRectangle(semiBrush, null, rect);
                        }
                    }
                }
            }

            // Auto-highlight matching bytes (V1 compatible feature)
            if (_autoHighlightByteValue.HasValue && byteData.Value == _autoHighlightByteValue.Value)
            {
                dc.DrawRectangle(_autoHighLiteBrush, null, rect);
            }

            // Double-click highlighted positions (V1 compatible feature) - light blue background
            if (_highlightedPositions != null && _highlightedPositions.Contains(byteData.VirtualPos.Value))
            {
                var doubleClickBrush = new SolidColorBrush(Color.FromArgb(128, 135, 206, 250)); // Light sky blue, semi-transparent
                dc.DrawRectangle(doubleClickBrush, null, rect);
            }

            // Draw mouse hover preview (shows which byte will be selected on click)
            // Only show in ASCII area if mouse is hovering in ASCII area (matches Legacy behavior)
            if (_mouseHoverPosition >= 0 && !_mouseHoverInHexArea && byteData.VirtualPos.Value == _mouseHoverPosition)
            {
                dc.DrawRectangle(_mouseHoverBrush, null, rect);
            }

            // Draw selection background (on top of custom background, TBL colors, and auto-highlight)
            bool isSelected = IsPositionSelected(byteData.VirtualPos.Value);
            if (isSelected)
            {
                // Use active or inactive brush based on which panel is active
                Brush selectionBrush = (_activePanel == ActivePanelType.Ascii && SelectionActiveBrush != null)
                    ? SelectionActiveBrush
                    : (SelectionInactiveBrush != null ? SelectionInactiveBrush : _selectedBrush);
                dc.DrawRoundedRectangle(selectionBrush, null, rect, 1, 1);
            }

            // Draw added byte background (light green) to make inserted bytes more visible
            if (byteData.Action == ByteAction.Added)
            {
                dc.DrawRoundedRectangle(_addedBackgroundBrush, null, rect, 1, 1);
            }

            // Draw action border (Modified/Added/Deleted indicator)
            if (byteData.Action != ByteAction.Nothing)
            {
                Brush borderBrush = byteData.Action switch
                {
                    ByteAction.Modified => _modifiedBrush,
                    ByteAction.Added => _addedBrush,
                    ByteAction.Deleted => _deletedBrush,
                    _ => Brushes.Transparent
                };

                var borderPen = new Pen(borderBrush, 1.5);
                dc.DrawRoundedRectangle(null, borderPen, rect, 1, 1);
            }

            // Draw cursor border
            if (byteData.VirtualPos.Value == _cursorPosition)
            {
                dc.DrawRoundedRectangle(null, _cursorPen, rect, 1, 1);
            }

            // STEP 4: Draw text centered in the cell (formattedText already calculated in STEP 1)
            double textX = x + (cellWidth - formattedText.Width) / 2;
            double textY = y + (_lineHeight - formattedText.Height) / 2;

            dc.DrawText(formattedText, new Point(textX, textY));

            // Return actual width used so caller can adjust x position
            return cellWidth;
        }

        /// <summary>
        /// Get how many ByteData this character consumes (1 for ASCII, 2-8 for TBL multi-byte)
        /// CRITICAL: Must be called before GetDisplayCharacter to know if we should skip next bytes
        /// Uses greedy matching - tries longest matches first (8 bytes down to 2)
        /// Phase 4: In multi-byte mode (Bit16/32), always returns 1 (each ByteData is independent)
        /// </summary>
        private int GetCharacterByteCount(HexLine line, int byteIndex)
        {
            if (byteIndex >= line.Bytes.Count)
                return 1;

            // Phase 4: In multi-byte mode (Bit16/32), each ByteData is already a group
            // Don't try to match across multiple ByteData for TBL - just display each group independently
            var byteData = line.Bytes[byteIndex];
            if (byteData.Values != null && byteData.Values.Length > 1)
            {
                // Multi-byte mode: each ByteData displays independently
                return 1;
            }

            if (_tblStream == null)
                return 1;

            try
            {
                // GREEDY MATCHING: Try multi-byte matches from longest to shortest (8 bytes down to 2)
                int maxLen = Math.Min(8, line.Bytes.Count - byteIndex);
                for (int len = maxLen; len >= 2; len--)
                {
                    // Build hex string for this length
                    var hexKey = new StringBuilder(len * 2);
                    for (int j = 0; j < len; j++)
                    {
                        if (byteIndex + j >= line.Bytes.Count)
                            break;
                        hexKey.Append(line.Bytes[byteIndex + j].Value.ToString("X2"));
                    }

                    var (text, type) = _tblStream.FindMatch(hexKey.ToString(), showSpecialValue: true);

                    // Check if this TBL type is enabled for display
                    bool shouldShow = type switch
                    {
                        Core.CharacterTable.DteType.DualTitleEncoding => _showTblDte,
                        Core.CharacterTable.DteType.MultipleTitleEncoding => _showTblMte,
                        Core.CharacterTable.DteType.EndBlock => _showTblEndBlock,
                        Core.CharacterTable.DteType.EndLine => _showTblEndLine,
                        Core.CharacterTable.DteType.Japonais => _showTblJaponais,
                        _ => false
                    };

                    if (text != "#" && shouldShow)
                        return len; // Return actual byte count consumed (2-8 bytes)
                }

                // Single byte fallback
                return 1;
            }
            catch
            {
                return 1; // Fallback to single byte on error
            }
        }

        /// <summary>
        /// Get the actual rendered width of a character (accounts for TBL auto-sizing AND multi-byte mode)
        /// CRITICAL for hit testing - must match DrawAsciiByte width calculation
        /// </summary>
        private double GetCharacterDisplayWidth(HexLine line, int byteIndex)
        {
            var byteData = line.Bytes[byteIndex];

            // Phase 4: In multi-byte mode (Bit16/32), width = stride * AsciiCharWidth
            // Each ByteData displays multiple ASCII characters
            if (byteData.Values != null && byteData.Values.Length > 1)
            {
                // Multi-byte mode: width is proportional to number of bytes in group
                return byteData.Values.Length * AsciiCharWidth;
            }

            // If no TBL loaded, use fixed ASCII width for single byte
            if (_tblStream == null)
                return AsciiCharWidth;

            // Get the display character using the same logic as rendering (for TBL)
            var displayChar = GetDisplayCharacter(line, byteIndex);

            // Create FormattedText to measure actual width (same as DrawAsciiByte)
            var formattedText = new FormattedText(
                displayChar,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                13,
                _asciiBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // TBL AUTO-SIZING: Use larger width if TBL character is wider (matches DrawAsciiByte logic)
            return formattedText.Width > AsciiCharWidth
                ? formattedText.Width
                : AsciiCharWidth;
        }

        /// <summary>
        /// Get display character for a byte - uses TBL if loaded (respects type visibility flags), otherwise ASCII
        /// </summary>
        private string GetDisplayCharacter(HexLine line, int byteIndex)
        {
            var byteData = line.Bytes[byteIndex];

            // If TBL stream is loaded, use it for character conversion (respecting type filters)
            if (_tblStream != null)
            {
                try
                {
                    // GREEDY MATCHING: Try multi-byte matches from longest to shortest (8 bytes down to 2)
                    int maxLen = Math.Min(8, line.Bytes.Count - byteIndex);
                    for (int len = maxLen; len >= 2; len--)
                    {
                        // Build hex string for this length
                        var hexKey = new StringBuilder(len * 2);
                        for (int j = 0; j < len; j++)
                        {
                            if (byteIndex + j >= line.Bytes.Count)
                                break;
                            hexKey.Append(line.Bytes[byteIndex + j].Value.ToString("X2"));
                        }

                        var (mteText, mteType) = _tblStream.FindMatch(hexKey.ToString(), showSpecialValue: true);

                        // Check if this TBL type is enabled for display
                        bool shouldShow = mteType switch
                        {
                            Core.CharacterTable.DteType.DualTitleEncoding => _showTblDte,
                            Core.CharacterTable.DteType.MultipleTitleEncoding => _showTblMte,
                            Core.CharacterTable.DteType.EndBlock => _showTblEndBlock,
                            Core.CharacterTable.DteType.EndLine => _showTblEndLine,
                            Core.CharacterTable.DteType.Japonais => _showTblJaponais,
                            _ => false
                        };

                        // If multi-byte match found and type is enabled, return it
                        if (mteText != "#" && shouldShow)
                            return mteText;
                    }

                    // Single byte match
                    string hexByte = byteData.Value.ToString("X2");
                    var (text, dteType) = _tblStream.FindMatch(hexByte, showSpecialValue: true);

                    // Check if this TBL type is enabled for display
                    bool shouldShowSingle = dteType switch
                    {
                        Core.CharacterTable.DteType.Ascii => _showTblAscii,
                        Core.CharacterTable.DteType.DualTitleEncoding => _showTblDte,
                        Core.CharacterTable.DteType.MultipleTitleEncoding => _showTblMte,
                        Core.CharacterTable.DteType.EndBlock => _showTblEndBlock,
                        Core.CharacterTable.DteType.EndLine => _showTblEndLine,
                        Core.CharacterTable.DteType.Japonais => _showTblJaponais,
                        _ => false
                    };

                    // Return TBL character if found and type is enabled, otherwise fall back to ASCII
                    if (text != "#" && shouldShowSingle)
                        return text;
                }
                catch
                {
                    // Fall back to ASCII on any TBL error
                }
            }

            // Phase 4: In multi-byte mode (Bit16/32), display ALL bytes in the group as ASCII
            // Each ByteData.Values contains 'stride' bytes that should all be displayed
            if (byteData.Values != null && byteData.Values.Length > 1)
            {
                // Multi-byte mode: show all bytes in the group
                var sb = new StringBuilder(byteData.Values.Length);
                foreach (var b in byteData.Values)
                {
                    char c = (b >= 0x20 && b < 0x7F) ? (char)b : '.';
                    sb.Append(c);
                }
                string result = sb.ToString();
                return result;
            }

            // Default: Single byte mode (Bit8) - Use standard ASCII conversion
            return byteData.AsciiChar.ToString();
        }

        /// <summary>
        /// Draw a byte spacer separator at the specified position (V1 compatible)
        /// </summary>
        private void DrawByteSpacer(DrawingContext dc, double x, double y)
        {
            int width = (int)ByteSpacerWidthTickness;

            switch (ByteSpacerVisualStyle)
            {
                case ByteSpacerVisual.Empty:
                    // Empty spacer - just space, nothing to draw
                    break;

                case ByteSpacerVisual.Line:
                    // Solid vertical line
                    var linePen = new Pen(_separatorBrush, 1);
                    double lineX = x + width / 2.0;
                    dc.DrawLine(linePen, new Point(lineX, y), new Point(lineX, y + _lineHeight));
                    break;

                case ByteSpacerVisual.Dash:
                    // Dashed vertical line
                    var dashPen = new Pen(_separatorBrush, 1);
                    dashPen.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
                    double dashX = x + width / 2.0;
                    dc.DrawLine(dashPen, new Point(dashX, y), new Point(dashX, y + _lineHeight));
                    break;
            }
        }

        private bool IsPositionSelected(long position)
        {
            if (_selectionStart < 0 || _selectionStop < 0)
                return false;

            long start = Math.Min(_selectionStart, _selectionStop);
            long stop = Math.Max(_selectionStart, _selectionStop);

            return position >= start && position <= stop;
        }

        #endregion

        #region Measure/Arrange

        protected override Size MeasureOverride(Size availableSize)
        {
            // Calculate width needed for all columns
            double hexWidth = OffsetWidth + (_bytesPerLine * (HexByteWidth + HexByteSpacing)) + SeparatorWidth + (_bytesPerLine * AsciiCharWidth) + 20;

            // Take all available height (fill the viewport), not just what we need for cached lines
            // This ensures the control fills the ScrollViewer and triggers proper SizeChanged events
            double height = double.IsInfinity(availableSize.Height) || availableSize.Height <= 0
                ? _linesCached.Count * _lineHeight + TopMargin  // Fallback if no constraint
                : availableSize.Height;  // Fill available space

            return new Size(hexWidth, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return finalSize;
        }

        #endregion

        #region Mouse Input

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[CLICK] === OnMouseDown START ===");
                base.OnMouseDown(e);
                Focus();

                // Double-check the click position to ensure we're not clicking on a ByteSpacer or empty area
                var mousePos = e.GetPosition(this);
                System.Diagnostics.Debug.WriteLine($"[CLICK] Mouse position: {mousePos}");

                var hitTestResult = HitTestByteWithArea(mousePos);
                System.Diagnostics.Debug.WriteLine($"[CLICK] HitTest completed");

                // DEBUG: Log click position
                System.Diagnostics.Debug.WriteLine($"[CLICK] Position.HasValue = {hitTestResult.Position.HasValue}");
                System.Diagnostics.Debug.WriteLine($"[CLICK] Position: {(hitTestResult.Position.HasValue ? $"0x{hitTestResult.Position.Value:X}" : "NULL")}, IsHex: {hitTestResult.IsHexArea}");

                // Only process click if we have a valid byte position (not on ByteSpacer or empty area)
                System.Diagnostics.Debug.WriteLine($"[CLICK] About to check if condition...");
                if (hitTestResult.Position.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"[CLICK] INSIDE IF BLOCK!");
                    long clickedPosition = hitTestResult.Position.Value;

                    System.Diagnostics.Debug.WriteLine($"[CLICK] Processing click at 0x{clickedPosition:X}");

                    // Handle double-click for auto-select same bytes (V1 compatible)
                    if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CLICK] Double-click detected");
                        ByteDoubleClicked?.Invoke(this, clickedPosition);
                    }
                    else if (e.ChangedButton == MouseButton.Left)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CLICK] Single left-click detected");
                        // Single LEFT click only - start selection drag
                        // Right-click is handled separately in OnMouseRightButtonDown to preserve selection
                        _isMouseDown = true;
                        _dragStartPosition = clickedPosition;
                        CaptureMouse();

                        System.Diagnostics.Debug.WriteLine($"[CLICK] About to raise ByteClicked event for 0x{clickedPosition:X}");
                        ByteClicked?.Invoke(this, clickedPosition);
                        System.Diagnostics.Debug.WriteLine($"[CLICK] ByteClicked event raised successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[CLICK] Not left button, button was: {e.ChangedButton}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CLICK] Position.HasValue is FALSE - no byte clicked");
                }

                System.Diagnostics.Debug.WriteLine($"[CLICK] === OnMouseDown END ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CLICK] EXCEPTION: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CLICK] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Hit test to determine which byte position was clicked
        /// </summary>
        private long? HitTestByte(Point mousePos)
        {
            var result = HitTestByteWithArea(mousePos);
            return result.Position;
        }

        /// <summary>
        /// Hit test to determine which byte position was clicked and which area (hex or ASCII)
        /// Phase 4: Changed to internal so HexEditor can use it for consistent hit testing
        /// </summary>
        internal (long? Position, bool IsHexArea) HitTestByteWithArea(Point mousePos)
        {
            if (_linesCached == null || _linesCached.Count == 0)
                return (null, true);

            // Calculate which line was clicked
            double y = mousePos.Y - TopMargin;
            if (y < 0) return (null, true);

            int lineIndex = (int)(y / _lineHeight);
            if (lineIndex < 0 || lineIndex >= _linesCached.Count)
                return (null, true);

            var line = _linesCached[lineIndex];
            if (line.Bytes == null || line.Bytes.Count == 0)
                return (null, true);

            double x = mousePos.X;

            // Start position depends on ShowOffset setting (matches drawing logic)
            double hexStartX = ShowOffset ? OffsetWidth : 0;

            // Check if click is in hex area
            // Must account for ByteSpacers to get accurate byte position
            double hexX = hexStartX;

            // Get stride for byte position calculation (matches drawing logic)
            int stride = line.Bytes.Count > 0 ? (line.Bytes[0].ByteSize switch
            {
                Core.ByteSizeType.Bit8 => 1,
                Core.ByteSizeType.Bit16 => 2,
                Core.ByteSizeType.Bit32 => 4,
                _ => 1
            }) : 1;

            System.Diagnostics.Debug.WriteLine($"[HitTest] MouseX={x:F1}, LineIndex={lineIndex}, Stride={stride}, ByteCount={line.Bytes.Count}");

            for (int i = 0; i < line.Bytes.Count; i++)
            {
                // Calculate byte position from group index (matches drawing logic)
                int bytePosition = i * stride;

                // Add ByteSpacer width if needed (matches drawing logic)
                if (_bytesPerLine >= (int)ByteGrouping &&
                    (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                     ByteSpacerPositioning == ByteSpacerPosition.HexBytePanel) &&
                    bytePosition % (int)ByteGrouping == 0 && i > 0)
                {
                    hexX += (int)ByteSpacerWidthTickness;
                }

                // Check if click is within this byte's rect
                // Phase 6: Use dynamic CellWidth instead of fixed HexByteWidth
                // Bug 4: Use GetDynamicCellWidth() for Font/DPI support
                var byteData = line.Bytes[i];
                double byteHitWidth = GetDynamicCellWidth(byteData) + HexByteSpacing;
                if (x >= hexX && x < hexX + byteHitWidth)
                {
                    // Click is within this byte's area (including spacing after it)
                    System.Diagnostics.Debug.WriteLine($"[HitTest] HIT! i={i}, VirtualPos={byteData.VirtualPos.Value}, hexX={hexX:F1}, width={byteHitWidth:F1}");
                    return (byteData.VirtualPos.Value, true);
                }

                hexX += byteHitWidth;
            }

            // Phase 6: Use hexX final position as separator start (already accounts for dynamic widths)
            // hexX now contains the end of the hex area (after all bytes + spacers)
            double separatorX = hexX + 4; // Small margin before separator
            double asciiX = separatorX + SeparatorWidth;

            // Phase 4: ASCII hit testing - SAME LOGIC as Hex (simple and works for all cases)
            // Iterate through bytes in ASCII area - simple loop like Hex panel
            for (int i = 0; i < line.Bytes.Count; i++)
            {
                // Calculate byte position from group index (matches drawing logic)
                int bytePosition = i * stride;

                // Add ByteSpacer width if needed (matches drawing logic)
                if (_bytesPerLine >= (int)ByteGrouping &&
                    (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                     ByteSpacerPositioning == ByteSpacerPosition.StringBytePanel) &&
                    bytePosition % (int)ByteGrouping == 0 && i > 0)
                {
                    asciiX += (int)ByteSpacerWidthTickness;
                }

                // Get ByteData and calculate character width (same approach as Hex)
                var byteData = line.Bytes[i];
                double charWidth = GetCharacterDisplayWidth(line, i);

                // Check if click is within this ASCII character's rect
                if (x >= asciiX && x < asciiX + charWidth)
                {
                    // Click is within this ASCII character's visual rect
                    return (byteData.VirtualPos.Value, false);
                }

                // Advance position (simple, no skipping - same as Hex)
                asciiX += charWidth;
            }

            return (null, true);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var mousePos = e.GetPosition(this);
            var hitTestResult = HitTestByteWithArea(mousePos);
            var position = hitTestResult.Position;
            var isHexArea = hitTestResult.IsHexArea;

            // DEBUG: Log mouseover position
            if (position.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"[MOUSEOVER] Position: 0x{position.Value:X}, IsHex: {isHexArea}");
            }

            // Always show standard arrow cursor in editing area (user preference)
            this.Cursor = Cursors.Arrow;

            // Update mouse hover position and area for visual preview (Legacy compatible)
            long newHoverPosition = position.HasValue ? position.Value : -1;
            if (_mouseHoverPosition != newHoverPosition || _mouseHoverInHexArea != isHexArea)
            {
                _mouseHoverPosition = newHoverPosition;
                _mouseHoverInHexArea = isHexArea;
                InvalidateVisual(); // Redraw to show/hide hover highlight
            }

            // V1 compatible: Show byte tooltip on hover (follows mouse)
            if (_showByteToolTip && _byteToolTip != null)
            {

                if (position.HasValue)
                {
                    // Find the byte data at this position
                    var byteData = _linesCached?
                        .SelectMany(line => line.Bytes)
                        .FirstOrDefault(b => b.VirtualPos.IsValid && b.VirtualPos.Value == position.Value);

                    if (byteData != null)
                    {
                        byte byteValue = byteData.Value;
                        char asciiChar = (byteValue >= 32 && byteValue < 127) ? (char)byteValue : '.';

                        string tooltipText = $"Position: 0x{position.Value:X8} ({position.Value})\n" +
                                           $"Value: 0x{byteValue:X2} ({byteValue})\n" +
                                           $"ASCII: '{asciiChar}'";

                        // Update tooltip position to follow mouse
                        _byteToolTip.HorizontalOffset = mousePos.X + 15;
                        _byteToolTip.VerticalOffset = mousePos.Y + 20;
                        _byteToolTip.Content = tooltipText;
                        _byteToolTip.IsOpen = true;
                        return;
                    }
                }

                // Close tooltip if not over a byte
                _byteToolTip.IsOpen = false;
            }
            else if (_byteToolTip != null)
            {
                _byteToolTip.IsOpen = false;
            }

            // Mouse drag selection
            if (_isMouseDown && _dragStartPosition.HasValue)
            {
                var currentPosition = HitTestByte(e.GetPosition(this));
                if (currentPosition.HasValue)
                {
                    // Raise event for parent to handle selection extension
                    ByteDragSelection?.Invoke(this, new ByteDragSelectionEventArgs(
                        _dragStartPosition.Value,
                        currentPosition.Value));
                }
            }
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);

            // Double-check the click position to ensure we're not clicking on a ByteSpacer or empty area
            var mousePos = e.GetPosition(this);
            var hitTestResult = HitTestByteWithArea(mousePos);

            // Only process right-click if we have a valid byte position (not on ByteSpacer or empty area)
            if (hitTestResult.Position.HasValue)
            {
                ByteRightClick?.Invoke(this, new ByteRightClickEventArgs(hitTestResult.Position.Value));
                e.Handled = true; // Prevent event from bubbling up
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            // End drag selection
            if (_isMouseDown)
            {
                _isMouseDown = false;
                _dragStartPosition = null;
                ReleaseMouseCapture();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            // Clear hover highlight when mouse leaves control
            if (_mouseHoverPosition != -1)
            {
                _mouseHoverPosition = -1;
                InvalidateVisual();
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when a byte is right-clicked (for context menu)
        /// </summary>
        public event EventHandler<ByteRightClickEventArgs> ByteRightClick;

        #endregion

        #region Keyboard Input

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Keyboard navigation - raise event for parent to handle
            // Parent (HexEditor) will update ViewModel selection/cursor
            if (e.Key == Key.Up || e.Key == Key.Down ||
                e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.PageUp || e.Key == Key.PageDown ||
                e.Key == Key.Home || e.Key == Key.End)
            {
                KeyboardNavigation?.Invoke(this, new KeyboardNavigationEventArgs(e.Key,
                    Keyboard.Modifiers.HasFlag(ModifierKeys.Shift),
                    Keyboard.Modifiers.HasFlag(ModifierKeys.Control)));
                e.Handled = true;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when user clicks on a byte
        /// </summary>
        public event EventHandler<long> ByteClicked;

        /// <summary>
        /// Raised when user double-clicks a byte (V1 compatible)
        /// </summary>
        public event EventHandler<long> ByteDoubleClicked;

        /// <summary>
        /// Raised when user drags to select multiple bytes
        /// </summary>
        public event EventHandler<ByteDragSelectionEventArgs> ByteDragSelection;

        /// <summary>
        /// Raised when user navigates with keyboard
        /// </summary>
        public event EventHandler<KeyboardNavigationEventArgs> KeyboardNavigation;

        #endregion
    }

    /// <summary>
    /// Event args for ByteRightClick event (context menu)
    /// </summary>
    public class ByteRightClickEventArgs : EventArgs
    {
        public long Position { get; }

        public ByteRightClickEventArgs(long position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// Event args for ByteDragSelection event (mouse drag selection)
    /// </summary>
    public class ByteDragSelectionEventArgs : EventArgs
    {
        public long StartPosition { get; }
        public long EndPosition { get; }

        public ByteDragSelectionEventArgs(long startPosition, long endPosition)
        {
            StartPosition = startPosition;
            EndPosition = endPosition;
        }
    }

    /// <summary>
    /// Event args for KeyboardNavigation event
    /// </summary>
    public class KeyboardNavigationEventArgs : EventArgs
    {
        public Key Key { get; }
        public bool IsShiftPressed { get; }
        public bool IsControlPressed { get; }

        public KeyboardNavigationEventArgs(Key key, bool isShiftPressed, bool isControlPressed)
        {
            Key = key;
            IsShiftPressed = isShiftPressed;
            IsControlPressed = isControlPressed;
        }
    }
}
