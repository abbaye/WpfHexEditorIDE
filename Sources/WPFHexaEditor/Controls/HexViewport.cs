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
using WpfHexaEditor.Rendering;

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
        private long _editingCellPosition = -1;
        private bool _editingBlinkVisible = false;
        private System.Windows.Threading.DispatcherTimer _editingBlinkTimer;
        private System.Windows.Media.Brush _editingCellBrush;
        private HashSet<long> _highlightedPositions = new();
        private List<Core.CustomBackgroundBlock> _customBackgroundBlocks = new();
        private CustomBackgroundRenderer _customBackgroundRenderer = new();
        private Core.CharacterTable.TblStream _tblStream; // Phase 7.5: TBL for character type detection
        private ByteToolTipDisplayMode _byteToolTipDisplayMode = ByteToolTipDisplayMode.None;
        private ByteToolTipDetailLevel _byteToolTipDetailLevel = ByteToolTipDetailLevel.Standard;
        private System.Windows.Controls.ToolTip _byteToolTip; // Custom tooltip that follows mouse

        // Viewport display properties (backing fields)
        private ByteSpacerGroup _byteGrouping = ByteSpacerGroup.EightByte;
        private bool _showOffset = true;
        private bool _showAscii = true;

        // Cached resources
        private Typeface _typeface;
        private Typeface _boldTypeface;
        private double _fontSize = 14;
        private double _charWidth;
        private double _charHeight;
        private double _lineHeight;

        // Layout constants
        // Note: OffsetWidth is now dynamic - use OffsetWidth property instead of constant
        private const double HexByteWidth = 24;
        private const double HexByteSpacing = 2;
        private const double SeparatorWidth = 20;
        private const double AsciiCharWidth = 10;
        private const double LeftMargin = 8;
        private const double TopMargin = 2;

        // Dynamic OffsetWidth property - calculates based on OffSetStringVisual format
        private double OffsetWidth => CalculateOffsetWidth();

        // Colors
        private Brush _backgroundBrush = Brushes.White;
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

        // V1 compatible TBL colors: ASCII=Black, DTE=Green (test), MTE=Red, EndBlock/EndLine=Blue
        private Brush _tblDteBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x00)); // Green (TEST - to distinguish from MTE)
        private Brush _tblMteBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)); // Red
        private Brush _tblEndBlockBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0xFF)); // Blue (V1)
        private Brush _tblEndLineBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0xFF)); // Blue (V1)
        private Brush _tblAsciiBrush = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x42)); // Dark gray (same as normal ASCII)
        private Brush _tblJaponaisBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)); // Red
        private Brush _tbl3ByteBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)); // Red
        private Brush _tbl4PlusByteBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)); // Red

        // Debug counter to avoid spamming logs every frame
        private int _debugRenderCount = 0;
        private Brush _tblDefaultBrush = new SolidColorBrush(Colors.White);

        // Auto-highlight (V1 compatible feature)
        private byte? _autoHighlightByteValue = null; // Byte value to highlight
        private Brush _autoHighLiteBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0x00)); // 40% Yellow

        // Search results highlight (yellow for better visibility)
        private Brush _doubleClickHighlightBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0x00)); // 50% Yellow

        // Cursor cell blink effect (simple on/off on active cursor)
        private bool _cursorBlinkVisible = true;     // État du clignotement (true = visible, false = caché)
        private System.Windows.Threading.DispatcherTimer _cursorBlinkTimer;  // Timer pour le clignotement
        private const int CURSOR_BLINK_INTERVAL = 500; // Intervalle en ms (500ms comme le caret)

        // Nibble editing visual feedback (bold the nibble being edited)
        private long _editingBytePosition = -1;      // Position of byte being edited (-1 = none)
        private int _editingNibbleIndex = 0;         // Which nibble is being edited (0 = first, 1 = second)

        // Cursor overlay visual (separate layer for performance - only redraws cursor, not entire viewport)
        private DrawingVisual _cursorOverlayVisual = null;

        // Captured cursor rects from OnRender (for overlay optimization)
        private Rect? _cursorHexRect = null;    // Hex panel cursor rect
        private Rect? _cursorAsciiRect = null;  // ASCII panel cursor rect

        // Mouse drag selection support
        private bool _isMouseDown = false;
        private long? _dragStartPosition = null;

        // Active panel tracking for dual-color selection
        private ActivePanelType _activePanel = ActivePanelType.Hex;

        // Mouse hover preview (shows which byte will be selected)
        private long _mouseHoverPosition = -1; // Position of byte under mouse cursor
        private bool _mouseHoverInHexArea = true; // True if hovering in hex area, false in ASCII area
        private Brush _mouseHoverBrush = new SolidColorBrush(Color.FromArgb(0x50, 100, 150, 255)); // Deep Blue - default from MouseOverColor DP

        // Refresh time tracking
        private System.Diagnostics.Stopwatch _refreshStopwatch = new System.Diagnostics.Stopwatch();
        private long _lastRefreshTimeMs = 0;

        // Phase 6 (Bug 4): Dynamic CellWidth cache for Font/DPI support
        // Cache stores calculated cell widths based on byte count, font size, font family, and data visual type
        // Key: (byteCount, fontSize, fontFamily, dataVisualType) → Value: calculated width in pixels
        // Invalidated when font settings change (FontSize, FontFamily, DPI) or DataStringVisual changes
        private Dictionary<(int byteCount, double fontSize, string fontFamily, Core.DataVisualType visualType), double> _cellWidthCache = new();
        private double _dpi = 1.0; // DPI scale factor (1.0 = 96 DPI, 1.5 = 144 DPI, etc.)

        // Dynamic OffsetWidth cache for different visual formats
        // Key: (fontSize, fontFamily, offsetVisualType) → Value: calculated width in pixels
        // Invalidated when font settings change or OffSetStringVisual changes
        private Dictionary<(double fontSize, string fontFamily, Core.DataVisualType visualType), double> _offsetWidthCache = new();

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

            // Initialize editing blink timer for cursor effect
            _editingBlinkTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(530)
            };
            _editingBlinkTimer.Tick += (s, e) =>
            {
                _editingBlinkVisible = !_editingBlinkVisible;
                InvalidateVisual();
            };
            _editingCellBrush = new SolidColorBrush(Color.FromArgb(128, 0, 120, 212));
            _editingCellBrush.Freeze();

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
            _tblAsciiBrush.Freeze();
            _tblJaponaisBrush.Freeze();
            _tblDefaultBrush.Freeze();
            _tbl3ByteBrush.Freeze();
            _tbl4PlusByteBrush.Freeze();

            // Initialize cursor cell blink timer (simple on/off blink)
            _cursorBlinkTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(CURSOR_BLINK_INTERVAL) // 500ms synchronized with caret
            };
            _cursorBlinkTimer.Tick += CursorBlinkTimer_Tick;
            _cursorBlinkTimer.Start(); // Always running for cursor highlight

            // Initialize cursor overlay visual (separate layer for optimized rendering)
            _cursorOverlayVisual = new DrawingVisual();
            AddVisualChild(_cursorOverlayVisual);
            AddLogicalChild(_cursorOverlayVisual);
        }

        /// <summary>
        /// One child visual: cursor overlay for optimized blink rendering
        /// </summary>
        protected override int VisualChildrenCount => 1;

        /// <summary>
        /// Returns the cursor overlay visual child
        /// </summary>
        protected override Visual GetVisualChild(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _cursorOverlayVisual;
        }

        /// <summary>
        /// Timer tick handler for cursor cell blink effect (simple on/off toggle)
        /// Optimized: only redraws cursor overlay using captured rects from OnRender
        /// </summary>
        private void CursorBlinkTimer_Tick(object sender, EventArgs e)
        {
            // Simple toggle: visible → hidden → visible
            _cursorBlinkVisible = !_cursorBlinkVisible;

            // Update only cursor overlay (uses captured rects - no recalculation needed)
            UpdateCursorOverlay();
        }

        /// <summary>
        /// Check if the cursor position is currently visible in the viewport
        /// </summary>
        private bool IsCursorVisibleInViewport()
        {
            if (_cursorPosition < 0 || _linesCached == null || _linesCached.Count == 0)
                return false;

            // Get first and last visible positions
            long firstVisiblePos = _linesCached[0].Bytes[0].VirtualPos;
            var lastLine = _linesCached[_linesCached.Count - 1];
            long lastVisiblePos = lastLine.Bytes[lastLine.Bytes.Count - 1].VirtualPos;

            return _cursorPosition >= firstVisiblePos && _cursorPosition <= lastVisiblePos;
        }

        /// <summary>
        /// Update cursor overlay visual (optimized: uses captured rects from OnRender)
        /// Only redraws cursor blink, not entire viewport
        /// </summary>
        private void UpdateCursorOverlay()
        {
            using (DrawingContext dc = _cursorOverlayVisual.RenderOpen())
            {
                // Only draw if cursor is blinking and visible, and we have captured rects
                if (!_cursorBlinkVisible)
                    return; // Clear overlay (empty drawing - cursor hidden phase)

                // Draw cursor blink using captured rect from OnRender
                Rect? cursorRect = (_activePanel == ActivePanelType.Hex) ? _cursorHexRect : _cursorAsciiRect;

                if (cursorRect.HasValue)
                {
                    // Use SelectionActiveBrush with 50% opacity for subtle effect
                    var blinkBrush = SelectionActiveBrush?.Clone() ?? _selectedBrush.Clone();
                    blinkBrush.Opacity = 0.5;

                    // Draw with corner radius matching the panel type
                    double cornerRadius = (_activePanel == ActivePanelType.Hex) ? 2 : 1;
                    dc.DrawRoundedRectangle(blinkBrush, null, cursorRect.Value, cornerRadius, cornerRadius);
                }
            }
        }

        /// <summary>
        /// Calculate the display width of an ASCII character at the given byte index
        /// EXACTLY replicates DrawAsciiByte's cellWidth calculation
        /// </summary>
        private double GetAsciiCharacterWidth(HexLine line, int byteIndex)
        {
            if (byteIndex >= line.Bytes.Count)
                return AsciiCharWidth;

            // Get display character (EXACT same call as DrawAsciiByte)
            var displayChar = GetDisplayCharacter(line, byteIndex);

            // Create FormattedText (EXACT same params as DrawAsciiByte line 1763-1770)
            var formattedText = new FormattedText(
                displayChar,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                13,
                _asciiBrush, // DrawAsciiByte uses textBrush but for width calculation color doesn't matter
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Calculate cellWidth (EXACT same logic as DrawAsciiByte line 1774-1776)
            double cellWidth = formattedText.Width > AsciiCharWidth
                ? formattedText.Width
                : AsciiCharWidth;

            return cellWidth;
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
        /// Phase 6 (Bug 4): Calculate dynamic cell width based on actual font/DPI settings and data visual type
        /// Uses FormattedText to measure real text width and caches results for performance
        /// </summary>
        /// <param name="byteCount">Number of bytes in the cell (1, 2, 3, or 4)</param>
        /// <returns>Cell width in pixels (includes 4px padding)</returns>
        private double CalculateCellWidth(int byteCount)
        {
            // Create cache key based on current font settings and visual type
            var key = (byteCount, _fontSize, _typeface.FontFamily.Source, DataStringVisual);

            // Return cached value if available
            if (_cellWidthCache.TryGetValue(key, out double cachedWidth))
                return cachedWidth;

            // Generate sample text based on data visual type
            // Use maximum-width characters for each format to ensure proper spacing
            string sampleText = DataStringVisual switch
            {
                Core.DataVisualType.Hexadecimal => new string('F', byteCount * 2), // 1 byte = "FF", 2 bytes = "FFFF", etc.
                Core.DataVisualType.Decimal => new string('8', byteCount * 3),     // 1 byte = "255" (3 chars), 2 bytes = "65535" (5 chars but use 6 for safety)
                Core.DataVisualType.Binary => new string('1', byteCount * 8),      // 1 byte = "11111111", 2 bytes = "1111111111111111", etc.
                _ => new string('F', byteCount * 2)
            };

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

        /// <summary>
        /// Calculate dynamic offset column width based on OffSetStringVisual format
        /// Uses FormattedText to measure real text width and caches results for performance
        /// </summary>
        /// <returns>Offset column width in pixels (includes padding)</returns>
        private double CalculateOffsetWidth()
        {
            // Return default width if typeface not initialized yet
            if (_typeface == null)
            {
                return OffSetStringVisual switch
                {
                    Core.DataVisualType.Hexadecimal => 110,  // Default for hex
                    Core.DataVisualType.Decimal => 110,      // Default for decimal
                    Core.DataVisualType.Binary => 290,       // Approximate for binary
                    _ => 110
                };
            }

            // Create cache key based on current font settings and offset visual type
            var key = (_fontSize, _typeface.FontFamily.Source, OffSetStringVisual);

            // Return cached value if available
            if (_offsetWidthCache.TryGetValue(key, out double cachedWidth))
                return cachedWidth;

            // Generate sample text based on offset visual type
            // Use maximum-width offset value to ensure proper spacing
            string sampleText = OffSetStringVisual switch
            {
                Core.DataVisualType.Hexadecimal => "0xFFFFFFFF",                              // 10 characters
                Core.DataVisualType.Decimal => "4294967295",                                   // 10 characters
                Core.DataVisualType.Binary => "0b11111111111111111111111111111111",          // 34 characters
                _ => "0xFFFFFFFF"
            };

            // Use fixed font size 13 for offsets (matches DrawOffset method)
            var formattedText = new FormattedText(
                sampleText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                13, // Fixed font size for offsets
                Brushes.Black,
                _dpi
            );

            // Add padding for left margin and spacing (20px total)
            double width = formattedText.Width + 20;

            // Cache the result
            _offsetWidthCache[key] = width;

            return width;
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
                InvalidateCustomBackgroundCache(); // Invalidate renderer cache
                InvalidateVisual(); // Trigger re-render
            }
        }

        /// <summary>
        /// Invalidate the custom background renderer cache
        /// Call this when viewport properties change (font size, bytes per line, etc.)
        /// </summary>
        public void InvalidateCustomBackgroundCache()
        {
            _customBackgroundRenderer?.InvalidateCache();
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
                InvalidateCustomBackgroundCache(); // Invalidate renderer cache
                InvalidateMeasure(); // Force layout recalculation
                InvalidateVisual();
            }
        }

        private Core.DataVisualType _dataStringVisual = Core.DataVisualType.Hexadecimal;
        private Core.DataVisualType _offSetStringVisual = Core.DataVisualType.Hexadecimal;

        /// <summary>
        /// Data string display format (Hexadecimal, Decimal, Binary)
        /// Invalidates cell width cache when changed to recalculate column widths
        /// </summary>
        public Core.DataVisualType DataStringVisual
        {
            get => _dataStringVisual;
            set
            {
                if (_dataStringVisual != value)
                {
                    _dataStringVisual = value;
                    // Invalidate cell width cache since different formats have different widths
                    _cellWidthCache.Clear();
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Offset string display format (Hexadecimal, Decimal, Binary)
        /// Invalidates offset width cache when changed to recalculate offset column width
        /// </summary>
        public Core.DataVisualType OffSetStringVisual
        {
            get => _offSetStringVisual;
            set
            {
                if (_offSetStringVisual != value)
                {
                    _offSetStringVisual = value;
                    // Invalidate offset width cache since different formats have different widths
                    _offsetWidthCache.Clear();
                    InvalidateVisual();
                }
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
        /// Position of the byte currently being edited (-1 if none)
        /// Used to display the edited nibble in bold
        /// </summary>
        public long EditingBytePosition
        {
            get => _editingBytePosition;
            set
            {
                if (_editingBytePosition != value)
                {
                    _editingBytePosition = value;
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Which nibble is being edited (0 = first hex char, 1 = second hex char)
        /// </summary>
        public int EditingNibbleIndex
        {
            get => _editingNibbleIndex;
            set
            {
                if (_editingNibbleIndex != value)
                {
                    _editingNibbleIndex = value;
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
        public ByteSpacerGroup ByteGrouping
        {
            get => _byteGrouping;
            set
            {
                if (_byteGrouping != value)
                {
                    _byteGrouping = value;
                    InvalidateCustomBackgroundCache();
                    InvalidateVisual();
                }
            }
        }

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
        public bool ShowOffset
        {
            get => _showOffset;
            set
            {
                if (_showOffset != value)
                {
                    _showOffset = value;
                    InvalidateCustomBackgroundCache();
                    InvalidateMeasure();
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Show or hide ASCII column (V1 compatible)
        /// </summary>
        public bool ShowAscii
        {
            get => _showAscii;
            set
            {
                if (_showAscii != value)
                {
                    _showAscii = value;
                    InvalidateCustomBackgroundCache();
                    InvalidateMeasure();
                    InvalidateVisual();
                }
            }
        }

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
            set
            {
                _activePanel = value;
                InvalidateVisual();
            }
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
                    _mouseHoverBrush = value ?? new SolidColorBrush(Color.FromArgb(0x50, 100, 150, 255)); // Deep Blue - default from MouseOverColor DP
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
            get => (_tblDteBrush as SolidColorBrush)?.Color ?? Colors.Red; // V1: Red
            set { _tblDteBrush = new SolidColorBrush(value); _tblDteBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// MTE (Multi-Title Encoding) color - Phase 7.5 V1 Compatibility
        /// </summary>
        public Color TblMteColor
        {
            get => (_tblMteBrush as SolidColorBrush)?.Color ?? Colors.Red; // V1: Red
            set { _tblMteBrush = new SolidColorBrush(value); _tblMteBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL End Block color - Phase 7.5 V1 Compatibility
        /// </summary>
        public Color TblEndBlockColor
        {
            get => (_tblEndBlockBrush as SolidColorBrush)?.Color ?? Colors.Blue; // V1: Blue
            set { _tblEndBlockBrush = new SolidColorBrush(value); _tblEndBlockBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL End Line color - Phase 7.5 V1 Compatibility
        /// </summary>
        public Color TblEndLineColor
        {
            get => (_tblEndLineBrush as SolidColorBrush)?.Color ?? Colors.Blue; // V1: Blue
            set { _tblEndLineBrush = new SolidColorBrush(value); _tblEndLineBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL ASCII color
        /// </summary>
        public Color TblAsciiColor
        {
            get => (_tblAsciiBrush as SolidColorBrush)?.Color ?? Color.FromRgb(0x42, 0x42, 0x42); // V1: Dark gray
            set { _tblAsciiBrush = new SolidColorBrush(value); _tblAsciiBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL Japanese color
        /// </summary>
        public Color TblJaponaisColor
        {
            get => (_tblJaponaisBrush as SolidColorBrush)?.Color ?? Colors.Red; // V1: Red
            set { _tblJaponaisBrush = new SolidColorBrush(value); _tblJaponaisBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL 3-byte sequences color
        /// </summary>
        public Color Tbl3ByteColor
        {
            get => (_tbl3ByteBrush as SolidColorBrush)?.Color ?? Colors.Red; // V1: Red
            set { _tbl3ByteBrush = new SolidColorBrush(value); _tbl3ByteBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// TBL 4+ byte sequences color
        /// </summary>
        public Color Tbl4PlusByteColor
        {
            get => (_tbl4PlusByteBrush as SolidColorBrush)?.Color ?? Colors.Red; // V1: Red
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
        /// Background color for the viewport content area
        /// </summary>
        public Color BackgroundColor
        {
            get => (_backgroundBrush as SolidColorBrush)?.Color ?? Colors.White;
            set { _backgroundBrush = new SolidColorBrush(value); _backgroundBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// Color for the separator line between hex and ASCII panels
        /// </summary>
        public Color SeparatorColor
        {
            get => (_separatorBrush as SolidColorBrush)?.Color ?? Color.FromRgb(0xE0, 0xE0, 0xE0);
            set { _separatorBrush = new SolidColorBrush(value); _separatorBrush.Freeze(); InvalidateVisual(); }
        }

        /// <summary>
        /// Foreground color for ASCII characters
        /// </summary>
        public Color AsciiForegroundColor
        {
            get => (_asciiBrush as SolidColorBrush)?.Color ?? Color.FromRgb(0x42, 0x42, 0x42);
            set { _asciiBrush = new SolidColorBrush(value); _asciiBrush.Freeze(); InvalidateVisual(); }
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
        /// Byte tooltip display mode (where to show tooltips)
        /// </summary>
        public ByteToolTipDisplayMode ByteToolTipDisplayMode
        {
            get => _byteToolTipDisplayMode;
            set
            {
                _byteToolTipDisplayMode = value;
                if (value == ByteToolTipDisplayMode.None && _byteToolTip != null)
                {
                    _byteToolTip.IsOpen = false;
                }
            }
        }

        /// <summary>
        /// Byte tooltip detail level (how much info to show)
        /// </summary>
        public ByteToolTipDetailLevel ByteToolTipDetailLevel
        {
            get => _byteToolTipDetailLevel;
            set => _byteToolTipDetailLevel = value;
        }

        /// <summary>
        /// Legacy V1 compatibility property - maps to ByteToolTipDisplayMode
        /// </summary>
        [Obsolete("Use ByteToolTipDisplayMode instead")]
        public bool ShowByteToolTip
        {
            get => _byteToolTipDisplayMode != ByteToolTipDisplayMode.None;
            set => _byteToolTipDisplayMode = value ? ByteToolTipDisplayMode.Everywhere : ByteToolTipDisplayMode.None;
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

            // Invalidate offset width cache (format-aware widths)
            _offsetWidthCache.Clear();

            // Invalidate custom background renderer cache
            InvalidateCustomBackgroundCache();

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

            // Clear cursor rects (will be captured during rendering if cursor is visible)
            _cursorHexRect = null;
            _cursorAsciiRect = null;

            // Draw background
            dc.DrawRectangle(_backgroundBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

            // PASS 1: Populate Rects first (fast pre-pass without drawing)
            PopulateByteRects();

            // Draw custom background blocks (using populated Rects)
            DrawCustomBackgroundBlocks(dc);

            // Draw TBL backgrounds for special types (EndBlock, EndLine, Japonais)
            DrawTblBackgrounds(dc);

            // Draw highlights (selection, auto-highlight, search, hover)
            DrawHighlights(dc);

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
                        // IMPORTANT: Never draw ByteSpacers in ASCII panel when TBL is loaded
                        // (TBL characters have variable byte consumption - spacers would be incorrect)
                        if (_tblStream == null &&
                            _bytesPerLine >= (int)ByteGrouping &&
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

            // Update cursor overlay after main render (keeps overlay in sync with scroll/changes)
            UpdateCursorOverlay();
        }

        /// <summary>
        /// Pre-populate HexRect and AsciiRect on all visible ByteData objects
        /// Called before DrawCustomBackgroundBlocks to ensure Rects are available
        /// Fast: only calculates positions, no drawing
        /// </summary>
        private void PopulateByteRects()
        {
            if (_linesCached == null || _linesCached.Count == 0)
                return;

            double y = TopMargin;

            foreach (var line in _linesCached)
            {
                if (line.Bytes == null || line.Bytes.Count == 0)
                    continue;

                // Calculate hex and ASCII positions (same as drawing loop)
                double hexX = ShowOffset ? OffsetWidth : 0;

                int stride = line.Bytes.Count > 0 ? (line.Bytes[0].ByteSize switch
                {
                    Core.ByteSizeType.Bit8 => 1,
                    Core.ByteSizeType.Bit16 => 2,
                    Core.ByteSizeType.Bit32 => 4,
                    _ => 1
                }) : 1;

                // Populate HexRects
                for (int i = 0; i < line.Bytes.Count; i++)
                {
                    int bytePosition = i * stride;

                    // Account for byte spacers (same logic as drawing)
                    if (_bytesPerLine >= (int)ByteGrouping &&
                        (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                         ByteSpacerPositioning == ByteSpacerPosition.HexBytePanel) &&
                        bytePosition % (int)ByteGrouping == 0 && i > 0)
                    {
                        hexX += (int)ByteSpacerWidthTickness;
                    }

                    var byteData = line.Bytes[i];
                    double cellWidth = GetDynamicCellWidth(byteData);
                    double byteWidth = cellWidth - HexByteSpacing;

                    // Populate HexRect
                    byteData.HexRect = new Rect(hexX, y, byteWidth, _lineHeight);

                    hexX += cellWidth + HexByteSpacing;
                }

                // Populate AsciiRects (if visible)
                if (ShowAscii)
                {
                    double separatorX = hexX + 4;
                    double asciiX = separatorX + SeparatorWidth;

                    for (int i = 0; i < line.Bytes.Count; i++)
                    {
                        int bytePosition = i * stride;

                        // Account for byte spacers in ASCII (same logic as drawing)
                        if (_tblStream == null &&
                            _bytesPerLine >= (int)ByteGrouping &&
                            (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                             ByteSpacerPositioning == ByteSpacerPosition.StringBytePanel) &&
                            bytePosition % (int)ByteGrouping == 0 && i > 0)
                        {
                            asciiX += (int)ByteSpacerWidthTickness;
                        }

                        var byteData = line.Bytes[i];

                        // Calculate ASCII cell width - must match DrawAsciiByte for accuracy
                        double cellWidth = AsciiCharWidth;

                        // TBL rendering only supported in Bit8 mode
                        if (_tblStream != null && byteData.ByteSize == Core.ByteSizeType.Bit8)
                        {
                            // TBL loaded in Bit8 mode: measure dynamic width for each character
                            try
                            {
                                var displayChar = GetDisplayCharacter(line, i);
                                var formattedText = new FormattedText(
                                    displayChar,
                                    System.Globalization.CultureInfo.CurrentCulture,
                                    FlowDirection.LeftToRight,
                                    _typeface,
                                    13,
                                    _asciiBrush,
                                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                                // Use actual text width if larger than single char width
                                cellWidth = formattedText.Width > AsciiCharWidth
                                    ? formattedText.Width
                                    : AsciiCharWidth;
                            }
                            catch
                            {
                                // Fallback to default on error
                                cellWidth = AsciiCharWidth;
                            }
                        }
                        else
                        {
                            // No TBL or multi-byte mode: fixed uniform width based on byte count
                            // Bit8: 1 char width, Bit16: 2 char widths, Bit32: 4 char widths
                            if (byteData.Values != null && byteData.Values.Length > 1)
                            {
                                cellWidth = AsciiCharWidth * byteData.Values.Length;
                            }
                            // else: cellWidth already set to AsciiCharWidth for Bit8
                        }

                        // Populate AsciiRect
                        byteData.AsciiRect = new Rect(asciiX, y, cellWidth, _lineHeight);

                        asciiX += cellWidth;
                    }
                }

                y += _lineHeight;
            }
        }

        /// <summary>
        /// Draw custom background blocks for visible lines
        /// Uses CustomBackgroundRenderer for performance (cached brushes, viewport state caching)
        /// Performance: 95%+ reduction in allocations via frozen brush caching
        /// </summary>
        private void DrawCustomBackgroundBlocks(DrawingContext dc)
        {
            if (_customBackgroundBlocks == null || _customBackgroundBlocks.Count == 0 ||
                _linesCached == null || _linesCached.Count == 0)
                return;

            // Prepare blocks (uses cache if unchanged)
            // Simplified - all position calculations now use actual Rects from ByteData
            _customBackgroundRenderer.PrepareBlocks(_customBackgroundBlocks, ShowAscii);

            // Get visible range for block culling
            long firstVisiblePos = _linesCached[0].Bytes[0].VirtualPos;
            long lastVisiblePos = _linesCached[_linesCached.Count - 1].Bytes[_linesCached[_linesCached.Count - 1].Bytes.Count - 1].VirtualPos;

            // Draw blocks using stored Rects
            _customBackgroundRenderer.DrawBlocks(dc, _linesCached, firstVisiblePos, lastVisiblePos);
        }

        /// <summary>
        /// Draw TBL background colors using stored Rects from ByteData
        /// Only draws backgrounds for special TBL types: EndBlock, EndLine, Japonais
        /// Called before DrawHighlights to ensure correct rendering order
        /// </summary>
        private void DrawTblBackgrounds(DrawingContext dc)
        {
            if (_linesCached == null || _linesCached.Count == 0 || _tblStream == null || !ShowAscii)
                return;

            // TBL rendering only supported in Bit8 mode (8-bit)
            // Check first line's first byte to determine current byte size mode
            if (_linesCached.Count > 0 && _linesCached[0].Bytes != null && _linesCached[0].Bytes.Count > 0)
            {
                if (_linesCached[0].Bytes[0].ByteSize != Core.ByteSizeType.Bit8)
                    return; // Skip TBL rendering in Bit16/Bit32 modes
            }

            foreach (var line in _linesCached)
            {
                if (line.Bytes == null || line.Bytes.Count == 0)
                    continue;

                for (int byteIndex = 0; byteIndex < line.Bytes.Count; byteIndex++)
                {
                    var byteData = line.Bytes[byteIndex];

                    // Skip if AsciiRect not available (byte not visible)
                    if (!byteData.AsciiRect.HasValue)
                        continue;

                    try
                    {
                        // Determine how many bytes this character consumes (uses greedy matching)
                        int byteCount = GetCharacterByteCount(line, byteIndex);

                        // Build hex key for the actual byte count
                        var hexKey = new StringBuilder();

                        // In multi-byte mode (Bit16/32), build hex key from ALL bytes in this ByteData group
                        if (byteData.Values != null && byteData.Values.Length > 1)
                        {
                            // Multi-byte: use Values[] array (contains all bytes in the group)
                            foreach (var b in byteData.Values)
                                hexKey.Append(b.ToString("X2"));
                        }
                        else
                        {
                            // Single-byte mode (Bit8): build from consecutive ByteData objects
                            for (int j = 0; j < byteCount && byteIndex + j < line.Bytes.Count; j++)
                                hexKey.Append(line.Bytes[byteIndex + j].Value.ToString("X2"));
                        }

                        var (text, dteType) = _tblStream.FindMatch(hexKey.ToString(), showSpecialValue: true);

                        // Only special types get background color
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
                                    dc.DrawRectangle(semiBrush, null, byteData.AsciiRect.Value);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore TBL lookup errors
                    }
                }
            }
        }

        /// <summary>
        /// Draw all highlight overlays using stored Rects from ByteData
        /// Includes: selection, auto-highlight, double-click highlight, mouse hover
        /// Centralized for consistency and performance
        /// </summary>
        private void DrawHighlights(DrawingContext dc)
        {
            if (_linesCached == null || _linesCached.Count == 0)
                return;

            foreach (var line in _linesCached)
            {
                if (line.Bytes == null || line.Bytes.Count == 0)
                    continue;

                foreach (var byteData in line.Bytes)
                {
                    long bytePos = byteData.VirtualPos;

                    // Auto-highlight (yellow) - matching byte values
                    if (_autoHighlightByteValue.HasValue && byteData.Value == _autoHighlightByteValue.Value)
                    {
                        if (byteData.HexRect.HasValue)
                            dc.DrawRoundedRectangle(_autoHighLiteBrush, null, byteData.HexRect.Value, 2, 2);
                        if (ShowAscii && byteData.AsciiRect.HasValue)
                            dc.DrawRectangle(_autoHighLiteBrush, null, byteData.AsciiRect.Value);
                    }

                    // Double-click highlight (light blue) - search results
                    if (_highlightedPositions?.Contains(bytePos) == true)
                    {
                        if (byteData.HexRect.HasValue)
                            dc.DrawRoundedRectangle(_doubleClickHighlightBrush, null, byteData.HexRect.Value, 2, 2);
                        if (ShowAscii && byteData.AsciiRect.HasValue)
                            dc.DrawRectangle(_doubleClickHighlightBrush, null, byteData.AsciiRect.Value);
                    }

                    // Mouse hover preview
                    if (_mouseHoverPosition >= 0 && bytePos == _mouseHoverPosition && _mouseHoverBrush != null)
                    {
                        // Only show in the area where mouse is hovering
                        if (_mouseHoverInHexArea && byteData.HexRect.HasValue)
                            dc.DrawRoundedRectangle(_mouseHoverBrush, null, byteData.HexRect.Value, 2, 2);
                        else if (!_mouseHoverInHexArea && ShowAscii && byteData.AsciiRect.HasValue)
                            dc.DrawRectangle(_mouseHoverBrush, null, byteData.AsciiRect.Value);
                    }

                    // Selection highlight (on top of other highlights)
                    if (IsPositionSelected(bytePos))
                    {
                        var selectionBrush = (_activePanel == ActivePanelType.Hex && SelectionActiveBrush != null)
                            ? SelectionActiveBrush
                            : (SelectionInactiveBrush ?? _selectedBrush);

                        if (byteData.HexRect.HasValue)
                            dc.DrawRoundedRectangle(selectionBrush, null, byteData.HexRect.Value, 2, 2);
                        if (ShowAscii && byteData.AsciiRect.HasValue)
                            dc.DrawRectangle(selectionBrush, null, byteData.AsciiRect.Value);
                    }
                }
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

            // Format offset according to OffSetStringVisual setting
            string offsetText = FormatOffset(line.StartPosition.Value, OffSetStringVisual);

            var formattedText = new FormattedText(
                offsetText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                13,
                _offsetBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(formattedText, new Point(LeftMargin, y + 2));
        }

        /// <summary>
        /// Format offset value according to the specified visual type
        /// </summary>
        private string FormatOffset(long position, Core.DataVisualType visualType)
        {
            return visualType switch
            {
                Core.DataVisualType.Hexadecimal => $"0x{position:X8}",
                Core.DataVisualType.Decimal => position.ToString("D10").PadLeft(10, ' '),
                Core.DataVisualType.Binary => $"0b{Convert.ToString(position, 2).PadLeft(32, '0')}",
                _ => $"0x{position:X8}"
            };
        }

        private void DrawHexByte(DrawingContext dc, ByteData byteData, double x, double y)
        {
            // Phase 3: Calculate cell width dynamically based on ByteSize
            // Bug 4: Use GetDynamicCellWidth() for Font/DPI support
            // Bit8/16/32 width now adapts to current font size and DPI
            double cellWidth = GetDynamicCellWidth(byteData);
            double byteWidth = cellWidth - HexByteSpacing;
            var rect = new Rect(x, y, byteWidth, _lineHeight);

            // Store rect for CustomBackgroundRenderer (guaranteed accurate)
            byteData.HexRect = rect;

            // Note: Highlights (selection, auto-highlight, search, hover) are now drawn globally
            // via DrawHighlights() for consistency and performance

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

            // Note: Cursor blink is drawn in overlay (UpdateCursorOverlay) using captured rect

            // Draw cursor border (thicker, on top)
            if (byteData.VirtualPos.Value == _cursorPosition)
            {
                // Capture cursor rect for overlay optimization
                _cursorHexRect = rect;

                dc.DrawRoundedRectangle(null, _cursorPen, rect, 2, 2);
            }

            // Draw hex text centered in the cell
            // V1 compatible: Alternate foreground color every byte for visual grouping
            long bytePosition = byteData.VirtualPos.Value;
            int byteIndexInLine = (int)(bytePosition % _bytesPerLine);
            bool useAlternateColor = byteIndexInLine % 2 == 1; // Alternate every single byte

            Brush textBrush = useAlternateColor ? _alternateByteBrush : _normalByteBrush;

            string hexText = byteData.GetHexText(DataStringVisual);

            // Check if this byte is being edited - if so, draw characters separately with bold
            bool isBeingEdited = _editingBytePosition >= 0 && bytePosition == _editingBytePosition;

            if (isBeingEdited && hexText.Length > 0) // For all formats (hex=2, decimal=3, binary=8)
            {
                // Draw each character separately with the one being edited in bold
                var boldTypeface = new Typeface(_typeface.FontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                double charX = x;

                for (int i = 0; i < hexText.Length; i++)
                {
                    var charTypeface = (i == _editingNibbleIndex) ? boldTypeface : _typeface;
                    var charText = new FormattedText(
                        hexText[i].ToString(),
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        charTypeface,
                        _fontSize,
                        textBrush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    double charY = y + (_lineHeight - charText.Height) / 2;
                    dc.DrawText(charText, new Point(charX, charY));
                    charX += charText.Width;
                }
            }
            else
            {
                // Normal rendering: draw full text at once
                var formattedText = new FormattedText(
                    hexText,
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
                    var hexKey = new StringBuilder();

                    // In multi-byte mode (Bit16/32), build hex key from ALL bytes in this ByteData group
                    if (byteData.Values != null && byteData.Values.Length > 1)
                    {
                        // Multi-byte: use Values[] array (contains all bytes in the group)
                        foreach (var b in byteData.Values)
                            hexKey.Append(b.ToString("X2"));
                    }
                    else
                    {
                        // Single-byte mode (Bit8): build from consecutive ByteData objects
                        for (int j = 0; j < byteCount && byteIndex + j < line.Bytes.Count; j++)
                            hexKey.Append(line.Bytes[byteIndex + j].Value.ToString("X2"));
                    }

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
                        // For normal types, use color based on byte count and type
                        else
                        {
                            textBrush = byteCount switch
                            {
                                // 1 byte: Ascii (dark gray) or DTE (red for compressed text like "CF=it")
                                1 => dteType == Core.CharacterTable.DteType.Ascii ? _tblAsciiBrush : _tblDteBrush,
                                // 2+ bytes: MTE (red for multi-byte tokens like "0400=Cecil")
                                2 => _tblMteBrush,
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

            // STEP 2: Calculate cell width - must match PopulateByteRects logic exactly
            double cellWidth;
            FormattedText formattedText;

            // TBL rendering only supported in Bit8 mode
            if (_tblStream != null && byteData.ByteSize == Core.ByteSizeType.Bit8)
            {
                // TBL loaded in Bit8 mode: measure dynamic width
                formattedText = new FormattedText(
                    displayChar,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    13,
                    textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                cellWidth = formattedText.Width > AsciiCharWidth
                    ? formattedText.Width
                    : AsciiCharWidth;
            }
            else
            {
                // No TBL or multi-byte mode: fixed uniform width
                if (byteData.Values != null && byteData.Values.Length > 1)
                {
                    cellWidth = AsciiCharWidth * byteData.Values.Length;
                }
                else
                {
                    cellWidth = AsciiCharWidth;
                }

                // Create FormattedText with fixed width
                formattedText = new FormattedText(
                    displayChar,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    13,
                    textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
            }

            // Create rect with calculated width (matches PopulateByteRects)
            var rect = new Rect(x, y, cellWidth, _lineHeight);

            // Store rect for CustomBackgroundRenderer (guaranteed accurate)
            byteData.AsciiRect = rect;

            // STEP 3: Draw backgrounds and borders
            // Note: Custom background blocks and TBL backgrounds are now drawn globally
            // via DrawCustomBackgroundBlocks() and DrawTblBackgrounds() for correct rendering order

            // Note: Highlights (selection, auto-highlight, search, hover) are now drawn globally
            // via DrawHighlights() for consistency and performance

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

            // Note: Cursor blink is drawn in overlay (UpdateCursorOverlay) using captured rect

            // Draw cursor border
            if (byteData.VirtualPos.Value == _cursorPosition)
            {
                // Capture cursor rect for overlay optimization
                _cursorAsciiRect = rect;

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
                        Core.CharacterTable.DteType.Ascii => _showTblAscii,
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
        /// CRITICAL for hit testing - must match DrawAsciiByte width calculation EXACTLY
        /// </summary>
        private double GetCharacterDisplayWidth(HexLine line, int byteIndex)
        {
            // Get the display character using the same logic as rendering
            var displayChar = GetDisplayCharacter(line, byteIndex);

            // Create FormattedText to measure actual width (EXACT SAME as DrawAsciiByte)
            var formattedText = new FormattedText(
                displayChar,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                13,
                _asciiBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // AUTO-SIZING: Use larger width if text is wider (matches DrawAsciiByte logic)
            // This works for both multi-byte mode AND TBL
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

            // Phase 4: Handle multi-byte mode (Bit16/32) FIRST
            if (byteData.Values != null && byteData.Values.Length > 1)
            {
                // Multi-byte mode: Try TBL match for the entire group first
                if (_tblStream != null)
                {
                    try
                    {
                        // Build hex key from ALL bytes in this ByteData group
                        var hexKey = new StringBuilder(byteData.Values.Length * 2);
                        foreach (var b in byteData.Values)
                            hexKey.Append(b.ToString("X2"));

                        var (text, type) = _tblStream.FindMatch(hexKey.ToString(), showSpecialValue: true);

                        // Check if this TBL type is enabled for display
                        bool shouldShow = type switch
                        {
                            Core.CharacterTable.DteType.Ascii => _showTblAscii,
                            Core.CharacterTable.DteType.DualTitleEncoding => _showTblDte,
                            Core.CharacterTable.DteType.MultipleTitleEncoding => _showTblMte,
                            Core.CharacterTable.DteType.EndBlock => _showTblEndBlock,
                            Core.CharacterTable.DteType.EndLine => _showTblEndLine,
                            Core.CharacterTable.DteType.Japonais => _showTblJaponais,
                            _ => false
                        };

                        // Return TBL match if found and enabled
                        if (text != "#" && shouldShow)
                            return text;
                    }
                    catch
                    {
                        // Fall back to ASCII on TBL error
                    }
                }

                // No TBL match: show all bytes in the group as ASCII
                // IMPORTANT: Respect ByteOrder for ASCII display (same as hex)
                var sb = new StringBuilder(byteData.Values.Length);

                // Reverse bytes if HiLo (big endian) to match hex display
                IEnumerable<byte> bytes = byteData.Values;
                if (byteData.ByteOrder == ByteOrderType.HiLo)
                    bytes = bytes.Reverse();

                foreach (var b in bytes)
                {
                    char c = (b >= 0x20 && b < 0x7F) ? (char)b : '.';
                    sb.Append(c);
                }
                return sb.ToString();
            }

            // Single-byte mode (Bit8): Use TBL greedy matching if loaded
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
                base.OnMouseDown(e);
                Focus();

                // Double-check the click position to ensure we're not clicking on a ByteSpacer or empty area
                var mousePos = e.GetPosition(this);

                var hitTestResult = HitTestByteWithArea(mousePos);

                // Only process click if we have a valid byte position (not on ByteSpacer or empty area)
                if (hitTestResult.Position.HasValue)
                {
                    long clickedPosition = hitTestResult.Position.Value;

                    // Handle double-click for auto-select same bytes (V1 compatible)
                    if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
                    {
                        ByteDoubleClicked?.Invoke(this, clickedPosition);
                    }
                    else if (e.ChangedButton == MouseButton.Left)
                    {
                        // Single LEFT click only - start selection drag
                        // Right-click is handled separately in OnMouseRightButtonDown to preserve selection
                        _isMouseDown = true;
                        _dragStartPosition = clickedPosition;
                        CaptureMouse();

                        ByteClicked?.Invoke(this, clickedPosition);
                    }
                }
            }
            catch (Exception ex)
            {
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
        /// Optimized: Uses stored HexRect/AsciiRect from ByteData instead of recalculating positions
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

            // Optimized approach: Use stored Rects from PopulateByteRects()
            // This eliminates ~100-200 arithmetic calculations per hit test
            foreach (var byteData in line.Bytes)
            {
                // Check hex area first (most common case)
                if (byteData.HexRect.HasValue && byteData.HexRect.Value.Contains(mousePos))
                {
                    return (byteData.VirtualPos.Value, true);
                }

                // Check ASCII area if visible
                if (ShowAscii && byteData.AsciiRect.HasValue && byteData.AsciiRect.Value.Contains(mousePos))
                {
                    return (byteData.VirtualPos.Value, false);
                }
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

            // Tooltip handling (new architecture with DisplayMode and DetailLevel)
            UpdateByteTooltip(mousePos, position);

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

        #region Tooltip Helpers

        /// <summary>
        /// Updates byte tooltip based on display mode and detail level
        /// </summary>
        private void UpdateByteTooltip(Point mousePos, long? position)
        {
            // Disabled mode
            if (_byteToolTipDisplayMode == ByteToolTipDisplayMode.None || _byteToolTip == null)
            {
                CloseTooltip();
                return;
            }

            // No valid byte position
            if (!position.HasValue)
            {
                CloseTooltip();
                return;
            }

            // Find byte data at position
            var byteData = FindByteDataAtPosition(position.Value);
            if (byteData == null)
            {
                CloseTooltip();
                return;
            }

            // Check if byte is in a CustomBackgroundBlock
            var block = _customBackgroundBlocks?.FirstOrDefault(b =>
                position.Value >= b.StartOffset && position.Value < b.StopOffset);

            // OnCustomBackgroundBlocks mode: only show if in a CBB
            if (_byteToolTipDisplayMode == ByteToolTipDisplayMode.OnCustomBackgroundBlocks)
            {
                if (block == null)
                {
                    CloseTooltip();
                    return;
                }
            }

            // Generate content based on detail level
            string content = GenerateTooltipContent(byteData, block, _byteToolTipDetailLevel);

            // Position tooltip (always mouse-follow for now)
            PositionTooltip(mousePos);

            // Update and show
            _byteToolTip.Content = content;
            _byteToolTip.IsOpen = true;
        }

        private ByteData FindByteDataAtPosition(long position)
        {
            return _linesCached?
                .SelectMany(line => line.Bytes)
                .FirstOrDefault(b => b.VirtualPos.IsValid && b.VirtualPos.Value == position);
        }

        private string GenerateTooltipContent(ByteData byteData, Core.CustomBackgroundBlock block, ByteToolTipDetailLevel detailLevel)
        {
            var sb = new StringBuilder();
            byte byteValue = byteData.Value;
            long position = byteData.VirtualPos.Value;
            char asciiChar = (byteValue >= 32 && byteValue < 127) ? (char)byteValue : '.';

            // === BASIC LEVEL (Always included) ===
            sb.AppendLine($"Position: 0x{position:X8} ({position})");
            sb.AppendLine($"Value: 0x{byteValue:X2} ({byteValue})");
            sb.AppendLine($"ASCII: '{asciiChar}'");

            // === STANDARD LEVEL (Add field info if available) ===
            if (detailLevel >= ByteToolTipDetailLevel.Standard)
            {
                if (block != null && !string.IsNullOrWhiteSpace(block.Description))
                {
                    sb.AppendLine();
                    sb.AppendLine($"📋 Field: {block.Description}");
                    sb.AppendLine($"Range: 0x{block.StartOffset:X8} - 0x{block.StopOffset:X8}");
                    sb.AppendLine($"Length: {block.Length} byte(s)");
                }
            }

            // === DETAILED LEVEL (Add interpretations) ===
            if (detailLevel >= ByteToolTipDetailLevel.Detailed)
            {
                AppendDetailedInfo(sb, byteValue, position);
            }

            return sb.ToString().TrimEnd();
        }

        private void AppendDetailedInfo(StringBuilder sb, byte byteValue, long position)
        {
            // Binary representation
            string binary = Convert.ToString(byteValue, 2).PadLeft(8, '0');
            sb.AppendLine($"Binary: {binary.Substring(0, 4)} {binary.Substring(4)}");

            sb.AppendLine();
            sb.AppendLine("━━━ INTERPRETATIONS ━━━");
            sb.AppendLine($"Signed (int8): {(sbyte)byteValue}");
            sb.AppendLine($"Unsigned (uint8): {byteValue}");

            // Try to read multi-byte values from cached lines
            if (_linesCached != null && _linesCached.Count > 0)
            {
                try
                {
                    // Collect up to 8 bytes starting from current position
                    var bytesList = new List<byte>();
                    foreach (var line in _linesCached)
                    {
                        if (line.Bytes != null)
                        {
                            foreach (var byteData in line.Bytes)
                            {
                                if (byteData.VirtualPos.IsValid && byteData.VirtualPos.Value >= position)
                                {
                                    bytesList.Add(byteData.Value);
                                    if (bytesList.Count >= 8)
                                        break;
                                }
                            }
                        }
                        if (bytesList.Count >= 8)
                            break;
                    }

                    if (bytesList.Count >= 2)
                    {
                        var bytes = bytesList.ToArray();
                        sb.AppendLine();
                        sb.AppendLine("━━━ MULTI-BYTE ━━━");

                        // Int16/UInt16 (Little Endian)
                        if (bytes.Length >= 2)
                        {
                            short int16LE = BitConverter.ToInt16(bytes, 0);
                            ushort uint16LE = BitConverter.ToUInt16(bytes, 0);
                            sb.AppendLine($"Int16 LE: {int16LE}");
                            sb.AppendLine($"UInt16 LE: {uint16LE}");
                        }

                        // Int32/Float (Little Endian)
                        if (bytes.Length >= 4)
                        {
                            int int32LE = BitConverter.ToInt32(bytes, 0);
                            float floatLE = BitConverter.ToSingle(bytes, 0);
                            sb.AppendLine($"Int32 LE: {int32LE} (0x{int32LE:X8})");
                            sb.AppendLine($"Float LE: {floatLE:F6}");
                        }
                    }
                }
                catch
                {
                    // Ignore errors (e.g., near end of file or data inconsistencies)
                }
            }
        }

        private void PositionTooltip(Point mousePos)
        {
            // Mouse-follow positioning (V1 behavior)
            _byteToolTip.HorizontalOffset = mousePos.X + 15;
            _byteToolTip.VerticalOffset = mousePos.Y + 20;
        }

        private void CloseTooltip()
        {
            if (_byteToolTip != null)
            {
                _byteToolTip.IsOpen = false;
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
