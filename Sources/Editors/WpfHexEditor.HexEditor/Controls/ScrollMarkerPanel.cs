// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: ScrollMarkerPanel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Custom Panel that renders colored scroll markers (bookmarks, search hits,
//     modifications) overlaid on the vertical scrollbar area of the HexEditor.
//     Supports Left/Right positioning and configurable marker colors and widths.
//
// Architecture Notes:
//     Extends Panel with custom ArrangeOverride/MeasureOverride for rendering.
//     Used by HexViewport and HexEditor to expose bookmark/search visualization.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor.Controls
{
    /// <summary>
    /// Horizontal position for scroll markers
    /// </summary>
    public enum MarkerPosition
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Type of scroll marker
    /// </summary>
    public enum ScrollMarkerType
    {
        Bookmark,
        Modified,
        Inserted,
        Deleted,
        SearchResult
    }

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
        private Brush _searchBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0x00));   // Bright orange
        private Brush _addedBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));    // Green
        private Brush _deletedBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));  // Red

        // Marker dimensions
        private const double MarkerWidth = 8;
        private const double MarkerMinHeight = 4;
        private const double MarkerMaxHeight = 6;

        #endregion

        #region Events

        /// <summary>
        /// Raised when a marker is clicked, providing the file position to navigate to
        /// </summary>
        public event EventHandler<long> MarkerClicked;

        #endregion

        #region Constructor

        public ScrollMarkerPanel()
        {
            // CRITICAL: Use null background (not Transparent!) to allow click-through
            // - Background = null → only drawn visuals are hit-testable (markers)
            // - Background = Transparent → entire panel captures mouse events (blocks scrollbar)
            Background = null;
            IsHitTestVisible = false; // Disable mouse events - markers are purely visual

            // Handle mouse clicks - DISABLED: clicking on markers complicates navigation
            //MouseLeftButtonDown += ScrollMarkerPanel_MouseLeftButtonDown;
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
        /// Enable or disable marker click navigation (default: enabled)
        /// </summary>
        public bool AllowMarkerClickNavigation { get; set; } = true;

        #region Marker Customization Properties

        // Modified markers (orange)
        public double ModifiedMarkerWidth { get; set; } = 8;
        public double ModifiedMarkerHeight { get; set; } = 3;
        public MarkerPosition ModifiedMarkerPosition { get; set; } = MarkerPosition.Center;

        // Added/Inserted markers (green)
        public double AddedMarkerWidth { get; set; } = 8;
        public double AddedMarkerHeight { get; set; } = 3;
        public MarkerPosition AddedMarkerPosition { get; set; } = MarkerPosition.Left;

        // Deleted markers (red)
        public double DeletedMarkerWidth { get; set; } = 8;
        public double DeletedMarkerHeight { get; set; } = 3;
        public MarkerPosition DeletedMarkerPosition { get; set; } = MarkerPosition.Right;

        // Bookmark markers (blue)
        public double BookmarkMarkerWidth { get; set; } = 8;
        public double BookmarkMarkerHeight { get; set; } = 3;
        public MarkerPosition BookmarkMarkerPosition { get; set; } = MarkerPosition.Center;

        // Search result markers (yellow)
        public double SearchMarkerWidth { get; set; } = 8;
        public double SearchMarkerHeight { get; set; } = 3;
        public MarkerPosition SearchMarkerPosition { get; set; } = MarkerPosition.Center;

        #endregion

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

        /// <summary>
        /// Add a marker of specific type at position
        /// </summary>
        public void AddMarker(long position, ScrollMarkerType markerType)
        {
            switch (markerType)
            {
                case ScrollMarkerType.Bookmark:
                    _bookmarkPositions.Add(position);
                    break;
                case ScrollMarkerType.Modified:
                    _modifiedPositions.Add(position);
                    break;
                case ScrollMarkerType.Inserted:
                    _insertedPositions.Add(position);
                    break;
                case ScrollMarkerType.Deleted:
                    _deletedPositions.Add(position);
                    break;
                case ScrollMarkerType.SearchResult:
                    _searchResultPositions.Add(position);
                    break;
            }
            InvalidateVisual();
        }

        /// <summary>
        /// Clear markers of specific type
        /// </summary>
        public void ClearMarkers(ScrollMarkerType markerType)
        {
            switch (markerType)
            {
                case ScrollMarkerType.Bookmark:
                    _bookmarkPositions.Clear();
                    break;
                case ScrollMarkerType.Modified:
                    _modifiedPositions.Clear();
                    break;
                case ScrollMarkerType.Inserted:
                    _insertedPositions.Clear();
                    break;
                case ScrollMarkerType.Deleted:
                    _deletedPositions.Clear();
                    break;
                case ScrollMarkerType.SearchResult:
                    _searchResultPositions.Clear();
                    break;
            }
            InvalidateVisual();
        }

        #endregion

        #region Rendering

        /// <summary>
        /// Calculate X position based on marker position and width
        /// </summary>
        private double GetMarkerX(MarkerPosition position, double markerWidth, double panelWidth)
        {
            return position switch
            {
                MarkerPosition.Left => 0,
                MarkerPosition.Center => (panelWidth - markerWidth) / 2,
                MarkerPosition.Right => panelWidth - markerWidth,
                _ => (panelWidth - markerWidth) / 2
            };
        }

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

            // Create a white border pen for better marker visibility
            var borderPen = new Pen(Brushes.White, 0.5);
            borderPen.Freeze(); // Freeze for performance

            // Track which (Y, X) pixel positions have been drawn to avoid duplicates
            // This improves performance by not drawing multiple markers at the same pixel
            var drawnPositions = new HashSet<(int y, int x)>();

            // Draw markers for each type with configurable size and position

            // Inserted positions (green) - configurable position
            double addedX = GetMarkerX(AddedMarkerPosition, AddedMarkerWidth, panelWidth);
            foreach (var position in _insertedPositions)
            {
                DrawMarkerWithDedup(dc, position, _addedBrush, panelHeight, addedX, AddedMarkerWidth, AddedMarkerHeight, drawnPositions, borderPen);
            }

            // Modified positions (orange) - configurable position
            double modifiedX = GetMarkerX(ModifiedMarkerPosition, ModifiedMarkerWidth, panelWidth);
            foreach (var position in _modifiedPositions)
            {
                DrawMarkerWithDedup(dc, position, _modifiedBrush, panelHeight, modifiedX, ModifiedMarkerWidth, ModifiedMarkerHeight, drawnPositions, borderPen);
            }

            // Deleted positions (red) - configurable position
            double deletedX = GetMarkerX(DeletedMarkerPosition, DeletedMarkerWidth, panelWidth);
            foreach (var position in _deletedPositions)
            {
                DrawMarkerWithDedup(dc, position, _deletedBrush, panelHeight, deletedX, DeletedMarkerWidth, DeletedMarkerHeight, drawnPositions, borderPen);
            }

            // Search results (yellow) - configurable position
            double searchX = GetMarkerX(SearchMarkerPosition, SearchMarkerWidth, panelWidth);
            foreach (var position in _searchResultPositions)
            {
                DrawMarkerWithDedup(dc, position, _searchBrush, panelHeight, searchX, SearchMarkerWidth, SearchMarkerHeight, drawnPositions, borderPen);
            }

            // Bookmarks (blue) - drawn on exterior left of scrollbar (more distinctive)
            // Position bookmarks outside the panel bounds on the left side
            double bookmarkX = -BookmarkMarkerWidth - 1; // 1 pixel gap from panel edge
            foreach (var position in _bookmarkPositions)
            {
                DrawMarkerWithDedup(dc, position, _bookmarkBrush, panelHeight, bookmarkX, BookmarkMarkerWidth, BookmarkMarkerHeight, drawnPositions, borderPen);
            }

            // Custom markers - CENTER (default)
            double customX = (panelWidth - MarkerWidth) / 2;
            foreach (var marker in _customMarkers)
            {
                DrawMarkerWithDedup(dc, marker.Key, marker.Value, panelHeight, customX, MarkerWidth, MarkerMinHeight, drawnPositions, borderPen);
            }
        }

        /// <summary>
        /// Draw a single marker at the specified position with deduplication
        /// Avoids drawing multiple markers at the same pixel position for better performance
        /// </summary>
        private void DrawMarkerWithDedup(DrawingContext dc, long position, Brush brush, double panelHeight,
            double x, double width, double height, HashSet<(int, int)> drawnPositions, Pen borderPen = null)
        {
            if (_fileLength == 0)
                return;

            // Calculate vertical position proportional to file position
            double ratio = (double)position / _fileLength;
            double y = ratio * panelHeight;

            // Clamp to visible area
            y = Math.Max(0, Math.Min(panelHeight - height, y));

            // Round to integer pixel coordinates for deduplication
            int yPixel = (int)Math.Round(y);
            int xPixel = (int)Math.Round(x);

            // Check if already drawn at this (Y, X) position
            if (!drawnPositions.Add((yPixel, xPixel)))
            {
                return; // Already drawn at this pixel, skip for performance
            }

            // Draw marker rectangle at specified position with custom size and optional border
            var rect = new Rect(x, y, width, height);
            dc.DrawRectangle(brush, borderPen, rect);
        }

        /// <summary>
        /// Draw a single marker at the specified position (legacy method, right-aligned)
        /// </summary>
        [Obsolete("Use DrawMarkerWithDedup for better performance")]
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

        #region Mouse Event Handlers

        /// <summary>
        /// Handle mouse click to navigate to the nearest marker
        /// With Background=null, this only fires when clicking on an actual drawn marker
        /// </summary>
        private void ScrollMarkerPanel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Check if marker click navigation is enabled
            if (!AllowMarkerClickNavigation)
                return;

            if (_fileLength == 0 || ActualHeight < 10)
                return;

            // Get mouse position relative to panel
            Point mousePos = e.GetPosition(this);
            double clickY = mousePos.Y;
            double panelHeight = ActualHeight;

            // Find the closest marker to the click position
            // Since Background=null, this event only fires when clicking on a drawn marker,
            // so we can use a reasonable tolerance for finding the closest one
            var closestPosition = FindClosestMarker(clickY, panelHeight);

            if (closestPosition.HasValue)
            {
                // Raise event to notify parent control
                MarkerClicked?.Invoke(this, closestPosition.Value);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Find the marker position closest to the given Y coordinate
        /// </summary>
        private long? FindClosestMarker(double clickY, double panelHeight)
        {
            var (position, _) = FindClosestMarkerWithDistance(clickY, panelHeight);
            return position;
        }

        /// <summary>
        /// Find the marker position closest to the given Y coordinate and return distance
        /// </summary>
        private (long? position, double distance) FindClosestMarkerWithDistance(double clickY, double panelHeight)
        {
            long? closestPos = null;
            double closestDistance = double.MaxValue;

            // Check all marker types
            CheckMarkerDistance(_insertedPositions, clickY, panelHeight, ref closestPos, ref closestDistance);
            CheckMarkerDistance(_modifiedPositions, clickY, panelHeight, ref closestPos, ref closestDistance);
            CheckMarkerDistance(_deletedPositions, clickY, panelHeight, ref closestPos, ref closestDistance);
            CheckMarkerDistance(_searchResultPositions, clickY, panelHeight, ref closestPos, ref closestDistance);
            CheckMarkerDistance(_bookmarkPositions, clickY, panelHeight, ref closestPos, ref closestDistance);

            // Check custom markers
            foreach (var marker in _customMarkers)
            {
                double markerY = ((double)marker.Key / _fileLength) * panelHeight;
                double distance = Math.Abs(markerY - clickY);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPos = marker.Key;
                }
            }

            return (closestPos, closestDistance);
        }

        /// <summary>
        /// Helper method to check distance from click to markers in a set
        /// </summary>
        private void CheckMarkerDistance(HashSet<long> positions, double clickY, double panelHeight,
            ref long? closestPos, ref double closestDistance)
        {
            foreach (var position in positions)
            {
                double markerY = ((double)position / _fileLength) * panelHeight;
                double distance = Math.Abs(markerY - clickY);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPos = position;
                }
            }
        }

        #endregion
    }
}
