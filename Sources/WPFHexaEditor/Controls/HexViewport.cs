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

        // TBL (Character Table) colors - Phase 7.5 V1 Compatibility
        private bool _tblShowMte = false;
        private Brush _tblDteBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x00)); // Yellow
        private Brush _tblMteBrush = new SolidColorBrush(Color.FromRgb(0xAD, 0xD8, 0xE6)); // LightBlue
        private Brush _tblEndBlockBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)); // Red
        private Brush _tblEndLineBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)); // Orange

        // Debug counter to avoid spamming logs every frame
        private int _debugRenderCount = 0;
        private Brush _tblDefaultBrush = new SolidColorBrush(Colors.White);

        // Auto-highlight (V1 compatible feature)
        private byte? _autoHighlightByteValue = null; // Byte value to highlight
        private Brush _autoHighLiteBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0x00)); // 40% Yellow

        // Double-click highlight (V1 compatible feature)
        private Brush _doubleClickHighlightBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x87, 0xCE, 0xFA)); // 50% Light sky blue

        // Caret for Insert mode (flashing vertical line)
        private Caret _caret;

        // Mouse drag selection support
        private bool _isMouseDown = false;
        private long? _dragStartPosition = null;

        #endregion

        #region Constructor

        public HexViewport()
        {
            // Initialize typeface
            _typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
            _boldTypeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

            // Calculate character dimensions
            CalculateCharacterDimensions();

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
        /// Show MTE (Multi-Title Encoding) in TBL - Phase 7.5 V1 Compatibility
        /// </summary>
        public bool TblShowMte
        {
            get => _tblShowMte;
            set { _tblShowMte = value; InvalidateVisual(); }
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
        /// TBL Default color - Phase 7.5 V1 Compatibility
        /// </summary>
        public Color TblDefaultColor
        {
            get => (_tblDefaultBrush as SolidColorBrush)?.Color ?? Colors.White;
            set { _tblDefaultBrush = new SolidColorBrush(value); _tblDefaultBrush.Freeze(); InvalidateVisual(); }
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

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_linesCached == null || _linesCached.Count == 0)
                return;

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
                    DrawOffset(dc, line.OffsetLabel, y);
                }

                // Draw hex bytes with byte spacers
                double hexX = ShowOffset ? OffsetWidth : 0;
                for (int i = 0; i < line.Bytes.Count; i++)
                {
                    // Draw byte spacer before this byte if needed
                    // Only draw separators if BytePerLine is large enough to have multiple groups
                    if (_bytesPerLine >= (int)ByteGrouping &&
                        (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                         ByteSpacerPositioning == ByteSpacerPosition.HexBytePanel) &&
                        i % (int)ByteGrouping == 0 && i > 0)
                    {
                        DrawByteSpacer(dc, hexX, y);
                        hexX += (int)ByteSpacerWidthTickness;
                    }

                    var byteData = line.Bytes[i];
                    DrawHexByte(dc, byteData, hexX, y);
                    hexX += HexByteWidth + HexByteSpacing;
                }

                // Draw separator and ASCII (if visible)
                if (ShowAscii)
                {
                    // Use actual hexX position after drawing all bytes (accounts for byte spacers)
                    // hexX is already at the position after last byte, just add a small margin
                    double separatorX = hexX + 4;
                    dc.DrawRectangle(_separatorBrush, null, new Rect(separatorX, y, 1, _lineHeight));

                    // Draw ASCII bytes with byte spacers
                    double asciiX = separatorX + SeparatorWidth;
                    for (int i = 0; i < line.Bytes.Count; i++)
                    {
                        // Draw byte spacer before this byte if needed
                        // Only draw separators if BytePerLine is large enough to have multiple groups
                        if (_bytesPerLine >= (int)ByteGrouping &&
                            (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                             ByteSpacerPositioning == ByteSpacerPosition.StringBytePanel) &&
                            i % (int)ByteGrouping == 0 && i > 0)
                        {
                            DrawByteSpacer(dc, asciiX, y);
                            asciiX += (int)ByteSpacerWidthTickness;
                        }

                        var byteData = line.Bytes[i];
                        DrawAsciiByte(dc, byteData, asciiX, y);
                        asciiX += AsciiCharWidth;
                    }
                }

                y += _lineHeight;
            }
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

        private void DrawOffset(DrawingContext dc, string offset, double y)
        {
            var formattedText = new FormattedText(
                offset,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                13,
                _offsetBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(formattedText, new Point(LeftMargin, y + 2));
        }

        private void DrawHexByte(DrawingContext dc, ByteData byteData, double x, double y)
        {
            // Calculate the actual byte cell rect (without spacing on the right)
            double byteWidth = HexByteWidth - HexByteSpacing;
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

            // Draw selection background (on top of custom background and auto-highlight)
            bool isSelected = IsPositionSelected(byteData.VirtualPos.Value);
            if (isSelected)
            {
                dc.DrawRoundedRectangle(_selectedBrush, null, rect, 2, 2);
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

            var formattedText = new FormattedText(
                byteData.HexString,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Center the text within the byte cell
            double textX = x + (byteWidth - formattedText.Width) / 2;
            double textY = y + (_lineHeight - formattedText.Height) / 2;

            dc.DrawText(formattedText, new Point(textX, textY));
        }

        private void DrawAsciiByte(DrawingContext dc, ByteData byteData, double x, double y)
        {
            var rect = new Rect(x, y, AsciiCharWidth, _lineHeight);

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

            // Phase 7.5: Draw TBL color background (between custom background and selection)
            if (_tblStream != null && _tblShowMte)
            {
                try
                {
                    // Convert byte to hex string for TBL lookup
                    string hexByte = byteData.Value.ToString("X2");
                    var (text, dteType) = _tblStream.FindMatch(hexByte, showSpecialValue: true);

                    // Select brush based on DTE type
                    Brush tblBrush = dteType switch
                    {
                        Core.CharacterTable.DteType.DualTitleEncoding => _tblDteBrush,
                        Core.CharacterTable.DteType.MultipleTitleEncoding => _tblMteBrush,
                        Core.CharacterTable.DteType.EndBlock => _tblEndBlockBrush,
                        Core.CharacterTable.DteType.EndLine => _tblEndLineBrush,
                        _ => _tblDefaultBrush
                    };

                    // Draw TBL color background
                    if (tblBrush != _tblDefaultBrush || dteType != Core.CharacterTable.DteType.Invalid)
                    {
                        dc.DrawRectangle(tblBrush, null, rect);
                    }
                }
                catch
                {
                    // Silently ignore TBL lookup errors
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

            // Draw selection background (on top of custom background, TBL colors, and auto-highlight)
            bool isSelected = IsPositionSelected(byteData.VirtualPos.Value);
            if (isSelected)
            {
                dc.DrawRoundedRectangle(_selectedBrush, null, rect, 1, 1);
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

            // Draw ASCII character centered in the cell
            var formattedText = new FormattedText(
                byteData.AsciiChar.ToString(),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                13,
                _asciiBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double textX = x + (AsciiCharWidth - formattedText.Width) / 2;
            double textY = y + (_lineHeight - formattedText.Height) / 2;

            dc.DrawText(formattedText, new Point(textX, textY));
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
            base.OnMouseDown(e);
            Focus();

            // Get mouse position
            Point mousePos = e.GetPosition(this);

            // Calculate which byte was clicked
            long? clickedPosition = HitTestByte(mousePos);
            if (clickedPosition.HasValue)
            {
                // Handle double-click for auto-select same bytes (V1 compatible)
                if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
                {
                    ByteDoubleClicked?.Invoke(this, clickedPosition.Value);
                }
                else
                {
                    // Single click - start selection drag
                    _isMouseDown = true;
                    _dragStartPosition = clickedPosition.Value;
                    CaptureMouse();

                    ByteClicked?.Invoke(this, clickedPosition.Value);
                }
            }
        }

        /// <summary>
        /// Hit test to determine which byte position was clicked
        /// </summary>
        private long? HitTestByte(Point mousePos)
        {
            if (_linesCached == null || _linesCached.Count == 0)
                return null;

            // Calculate which line was clicked
            double y = mousePos.Y - TopMargin;
            if (y < 0) return null;

            int lineIndex = (int)(y / _lineHeight);
            if (lineIndex < 0 || lineIndex >= _linesCached.Count)
                return null;

            var line = _linesCached[lineIndex];
            if (line.Bytes == null || line.Bytes.Count == 0)
                return null;

            double x = mousePos.X;

            // Check if click is in hex area
            double hexStartX = OffsetWidth;
            double hexEndX = OffsetWidth + (_bytesPerLine * (HexByteWidth + HexByteSpacing));

            if (x >= hexStartX && x < hexEndX)
            {
                // Click in hex area
                double relativeX = x - hexStartX;
                int byteIndex = (int)(relativeX / (HexByteWidth + HexByteSpacing));

                if (byteIndex >= 0 && byteIndex < line.Bytes.Count)
                {
                    return line.Bytes[byteIndex].VirtualPos.Value;
                }
            }

            // Check if click is in ASCII area
            double separatorX = OffsetWidth + (_bytesPerLine * (HexByteWidth + HexByteSpacing)) + 8;
            double asciiStartX = separatorX + SeparatorWidth;
            double asciiEndX = asciiStartX + (_bytesPerLine * AsciiCharWidth);

            if (x >= asciiStartX && x < asciiEndX)
            {
                // Click in ASCII area
                double relativeX = x - asciiStartX;
                int byteIndex = (int)(relativeX / AsciiCharWidth);

                if (byteIndex >= 0 && byteIndex < line.Bytes.Count)
                {
                    return line.Bytes[byteIndex].VirtualPos.Value;
                }
            }

            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // V1 compatible: Show byte tooltip on hover (follows mouse)
            if (_showByteToolTip && _byteToolTip != null)
            {
                var mousePos = e.GetPosition(this);
                var position = HitTestByte(mousePos);

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

            // Get position at mouse click for context menu
            var position = HitTestByte(e.GetPosition(this));
            if (position.HasValue)
            {
                ByteRightClick?.Invoke(this, new ByteRightClickEventArgs(position.Value));
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
