//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
// Scroll markers overlay for vertical scrollbar (V1 compatibility)
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfHexaEditor.Controls
{
    /// <summary>
    /// Panel that displays markers on top of the scrollbar
    /// Markers indicate bookmarks, modifications, search results, etc.
    /// </summary>
    public class ScrollMarkerPanel : Canvas
    {
        #region Fields

        private long _fileLength = 0;
        private HashSet<long> _bookmarkPositions = new();
        private HashSet<long> _modifiedPositions = new();
        private HashSet<long> _insertedPositions = new();
        private HashSet<long> _deletedPositions = new();
        private HashSet<long> _searchResultPositions = new();
        private Dictionary<long, Brush> _customMarkers = new();

        // Marker colors (V1 compatible)
        private Brush _bookmarkBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)); // Blue
        private Brush _modifiedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)); // Orange
        private Brush _searchBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x00));   // Yellow
        private Brush _addedBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));    // Green
        private Brush _deletedBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));  // Red

        // Marker dimensions
        private const double MarkerWidth = 4;
        private const double MarkerMinHeight = 2;
        private const double MarkerMaxHeight = 6;

        #endregion

        #region Constructor

        public ScrollMarkerPanel()
        {
            // Make sure we're transparent for click-through to scrollbar
            Background = Brushes.Transparent;
            IsHitTestVisible = false; // Don't intercept mouse events
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Total file length (for position scaling)
        /// </summary>
        public long FileLength
        {
            get => _fileLength;
            set
            {
                if (_fileLength != value)
                {
                    _fileLength = value;
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Bookmark positions to display
        /// </summary>
        public HashSet<long> BookmarkPositions
        {
            get => _bookmarkPositions;
            set
            {
                _bookmarkPositions = value ?? new HashSet<long>();
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Modified byte positions to display
        /// </summary>
        public HashSet<long> ModifiedPositions
        {
            get => _modifiedPositions;
            set
            {
                _modifiedPositions = value ?? new HashSet<long>();
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Search result positions to display
        /// </summary>
        public HashSet<long> SearchResultPositions
        {
            get => _searchResultPositions;
            set
            {
                _searchResultPositions = value ?? new HashSet<long>();
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Inserted byte positions to display (green markers)
        /// </summary>
        public HashSet<long> InsertedPositions
        {
            get => _insertedPositions;
            set
            {
                _insertedPositions = value ?? new HashSet<long>();
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Deleted byte positions to display (red markers)
        /// </summary>
        public HashSet<long> DeletedPositions
        {
            get => _deletedPositions;
            set
            {
                _deletedPositions = value ?? new HashSet<long>();
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Current selection start position (-1 if no selection)
        /// </summary>
        public long SelectionStart { get; set; } = -1;

        /// <summary>
        /// Current selection stop position (-1 if no selection)
        /// </summary>
        public long SelectionStop { get; set; } = -1;

        /// <summary>
        /// Update selection range
        /// </summary>
        public void SetSelection(long start, long stop)
        {
            SelectionStart = start;
            SelectionStop = stop;
            InvalidateVisual();
        }

        /// <summary>
        /// Clear selection
        /// </summary>
        public void ClearSelection()
        {
            SelectionStart = -1;
            SelectionStop = -1;
            InvalidateVisual();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Add a custom marker at a specific position
        /// </summary>
        public void AddCustomMarker(long position, Brush color)
        {
            _customMarkers[position] = color;
            InvalidateVisual();
        }

        /// <summary>
        /// Remove a custom marker
        /// </summary>
        public void RemoveCustomMarker(long position)
        {
            _customMarkers.Remove(position);
            InvalidateVisual();
        }

        /// <summary>
        /// Clear all custom markers
        /// </summary>
        public void ClearCustomMarkers()
        {
            _customMarkers.Clear();
            InvalidateVisual();
        }

        /// <summary>
        /// Clear all markers
        /// </summary>
        public void ClearAllMarkers()
        {
            _bookmarkPositions.Clear();
            _modifiedPositions.Clear();
            _insertedPositions.Clear();
            _deletedPositions.Clear();
            _searchResultPositions.Clear();
            _customMarkers.Clear();
            InvalidateVisual();
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_fileLength == 0 || ActualHeight < 10)
                return;

            double panelHeight = ActualHeight;
            double panelWidth = ActualWidth;

            // Draw selection bar first (behind markers)
            if (SelectionStart >= 0 && SelectionStop >= 0)
            {
                DrawSelectionBar(dc, panelHeight, panelWidth);
            }

            // Draw markers for each type
            // Order: Custom → Modified → Search → Bookmarks (bookmarks on top)

            // Custom markers
            foreach (var marker in _customMarkers)
            {
                DrawMarker(dc, marker.Key, marker.Value, panelHeight, panelWidth);
            }

            // Modified positions (orange)
            foreach (var position in _modifiedPositions)
            {
                DrawMarker(dc, position, _modifiedBrush, panelHeight, panelWidth);
            }

            // Inserted positions (green)
            foreach (var position in _insertedPositions)
            {
                DrawMarker(dc, position, _addedBrush, panelHeight, panelWidth);
            }

            // Deleted positions (red)
            foreach (var position in _deletedPositions)
            {
                DrawMarker(dc, position, _deletedBrush, panelHeight, panelWidth);
            }

            // Search results (yellow)
            foreach (var position in _searchResultPositions)
            {
                DrawMarker(dc, position, _searchBrush, panelHeight, panelWidth);
            }

            // Bookmarks (drawn last so they're on top)
            foreach (var position in _bookmarkPositions)
            {
                DrawMarker(dc, position, _bookmarkBrush, panelHeight, panelWidth);
            }
        }

        /// <summary>
        /// Draw a single marker at the specified position
        /// </summary>
        private void DrawMarker(DrawingContext dc, long position, Brush brush, double panelHeight, double panelWidth)
        {
            if (_fileLength == 0)
                return;

            // Calculate vertical position proportional to file position
            double ratio = (double)position / _fileLength;
            double y = ratio * panelHeight;

            // Clamp to visible area
            y = Math.Max(0, Math.Min(panelHeight - MarkerMinHeight, y));

            // Draw marker rectangle
            var rect = new Rect(
                panelWidth - MarkerWidth, // Right-aligned
                y,
                MarkerWidth,
                MarkerMinHeight
            );

            dc.DrawRectangle(brush, null, rect);
        }

        /// <summary>
        /// Draw selection bar showing current selection range
        /// </summary>
        private void DrawSelectionBar(DrawingContext dc, double panelHeight, double panelWidth)
        {
            if (_fileLength == 0 || SelectionStart < 0 || SelectionStop < 0)
                return;

            // Calculate start and stop positions
            long start = Math.Min(SelectionStart, SelectionStop);
            long stop = Math.Max(SelectionStart, SelectionStop);

            // Convert to vertical positions
            double startY = ((double)start / _fileLength) * panelHeight;
            double stopY = ((double)stop / _fileLength) * panelHeight;

            // Ensure minimum height for visibility
            double height = Math.Max(stopY - startY, 3.0);

            // Clamp to visible area
            startY = Math.Max(0, Math.Min(panelHeight - height, startY));

            // Draw semi-transparent blue bar for selection
            var selectionBrush = new SolidColorBrush(Color.FromArgb(80, 0x00, 0x78, 0xD4)); // Semi-transparent blue
            var rect = new Rect(0, startY, panelWidth, height);

            dc.DrawRectangle(selectionBrush, null, rect);
        }

        #endregion
    }
}
