//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
// High-performance custom rendering viewport for HexEditorV2
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexaEditor.Core;
using WpfHexaEditor.V2.Models;

namespace WpfHexaEditor.V2.Controls
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
        private Brush _normalByteBrush = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21));
        private Brush _selectedBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0x78, 0xD4)); // #0078D4 with 40% opacity
        private Brush _modifiedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)); // Orange
        private Brush _addedBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Green
        private Brush _deletedBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)); // Red
        private Pen _cursorPen;
        private Pen _actionPen;
        private Brush _separatorBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        private Brush _asciiBrush = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x42));

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

            // Freeze brushes for performance
            _offsetBrush.Freeze();
            _normalByteBrush.Freeze();
            _selectedBrush.Freeze();
            _modifiedBrush.Freeze();
            _addedBrush.Freeze();
            _deletedBrush.Freeze();
            _separatorBrush.Freeze();
            _asciiBrush.Freeze();
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
        /// Gets the actual line height used for rendering
        /// </summary>
        public double LineHeight => _lineHeight;

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_linesCached == null || _linesCached.Count == 0)
                return;

            // Draw white background
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));

            double y = TopMargin;

            foreach (var line in _linesCached)
            {
                if (line.Bytes == null || line.Bytes.Count == 0)
                    continue;

                // Draw offset
                DrawOffset(dc, line.OffsetLabel, y);

                // Draw hex bytes
                double hexX = OffsetWidth;
                for (int i = 0; i < line.Bytes.Count; i++)
                {
                    var byteData = line.Bytes[i];
                    DrawHexByte(dc, byteData, hexX, y);
                    hexX += HexByteWidth + HexByteSpacing;
                }

                // Draw separator
                double separatorX = OffsetWidth + (_bytesPerLine * (HexByteWidth + HexByteSpacing)) + 8;
                dc.DrawRectangle(_separatorBrush, null, new Rect(separatorX, y, 1, _lineHeight));

                // Draw ASCII bytes
                double asciiX = separatorX + SeparatorWidth;
                for (int i = 0; i < line.Bytes.Count; i++)
                {
                    var byteData = line.Bytes[i];
                    DrawAsciiByte(dc, byteData, asciiX, y);
                    asciiX += AsciiCharWidth;
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

            // Draw selection background first (full cell)
            bool isSelected = IsPositionSelected(byteData.VirtualPos.Value);
            if (isSelected)
            {
                dc.DrawRoundedRectangle(_selectedBrush, null, rect, 2, 2);
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
            }

            // Draw cursor border (thicker, on top)
            if (byteData.VirtualPos.Value == _cursorPosition)
            {
                dc.DrawRoundedRectangle(null, _cursorPen, rect, 2, 2);
            }

            // Draw hex text centered in the cell
            var formattedText = new FormattedText(
                byteData.HexString,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                _normalByteBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Center the text within the byte cell
            double textX = x + (byteWidth - formattedText.Width) / 2;
            double textY = y + (_lineHeight - formattedText.Height) / 2;

            dc.DrawText(formattedText, new Point(textX, textY));
        }

        private void DrawAsciiByte(DrawingContext dc, ByteData byteData, double x, double y)
        {
            var rect = new Rect(x, y, AsciiCharWidth, _lineHeight);

            // Draw selection background
            bool isSelected = IsPositionSelected(byteData.VirtualPos.Value);
            if (isSelected)
            {
                dc.DrawRoundedRectangle(_selectedBrush, null, rect, 1, 1);
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

            // Calculate height needed for all lines
            double height = _linesCached.Count * _lineHeight + TopMargin;

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

            // TODO: Implement mouse selection
            // Calculate position from mouse coordinates
            // Raise event for parent to handle
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // TODO: Implement mouse drag selection
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            // TODO: Implement selection end
        }

        #endregion

        #region Keyboard Input

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // TODO: Implement keyboard navigation
            // Arrow keys, Page Up/Down, Home/End
            // Raise events for parent to handle
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when user clicks on a byte
        /// </summary>
        public event EventHandler<long> ByteClicked;

        /// <summary>
        /// Raised when user navigates with keyboard
        /// </summary>
        public event EventHandler<Key> NavigationKeyPressed;

        #endregion
    }
}
