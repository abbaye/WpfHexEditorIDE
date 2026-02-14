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

            // Draw markers for each type
            // Order: Custom → Modified → Search → Bookmarks (bookmarks on top)

            // Custom markers
            foreach (var marker in _customMarkers)
            {
                DrawMarker(dc, marker.Key, marker.Value, panelHeight, panelWidth);
            }

            // Modified positions
            foreach (var position in _modifiedPositions)
            {
                DrawMarker(dc, position, _modifiedBrush, panelHeight, panelWidth);
            }

            // Search results
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

        #endregion
    }
}
