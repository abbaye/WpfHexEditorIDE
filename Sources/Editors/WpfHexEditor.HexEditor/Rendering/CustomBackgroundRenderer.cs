//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Models;

namespace WpfHexEditor.HexEditor.Rendering
{
    /// <summary>
    /// High-performance renderer for CustomBackgroundBlock highlights
    /// Caches rectangle calculations and frozen brushes to minimize per-frame overhead
    ///
    /// Performance optimizations:
    /// - Viewport state-based caching (only recalculates when layout changes)
    /// - Frozen brushes (pre-computed opacity, WPF caching)
    /// - Visible range culling (skips off-screen blocks)
    /// - Spatial calculations cached per viewport configuration
    /// </summary>
    public class CustomBackgroundRenderer
    {
        #region Viewport State (Cache Key)

        /// <summary>
        /// Simplified viewport state - only tracks whether ASCII area should be drawn
        /// All position calculations now use actual Rects stored on ByteData
        /// </summary>
        private struct ViewportState : IEquatable<ViewportState>
        {
            public bool ShowAscii;  // Only need this to know whether to draw ASCII backgrounds

            public bool Equals(ViewportState other) => ShowAscii == other.ShowAscii;
            public override bool Equals(object obj) => obj is ViewportState state && Equals(state);
            public override int GetHashCode() => ShowAscii.GetHashCode();
        }

        #endregion

        #region Prepared Block (Cache Data)

        /// <summary>
        /// Cached rendering data for a single block
        /// </summary>
        private class PreparedBlock
        {
            public CustomBackgroundBlock Block { get; set; }
            public SolidColorBrush FrozenBrush { get; set; }
            public bool IsValid { get; set; }
        }

        #endregion

        #region Fields

        // Cache state
        private ViewportState _cachedState;
        private List<PreparedBlock> _preparedBlocks = new List<PreparedBlock>();
        private bool _cacheValid = false;

        // Layout constants (from HexViewport for independence)
        private const double HexByteSpacing = 2;
        private const double SeparatorWidth = 20;
        private const double TopMargin = 2;

        #endregion

        #region Public Methods

        /// <summary>
        /// Prepare blocks for rendering by pre-computing frozen brushes
        /// Simplified - all position calculations now use actual Rects from ByteData
        /// </summary>
        /// <param name="blocks">Blocks to prepare</param>
        /// <param name="showAscii">Whether ASCII column is visible</param>
        public void PrepareBlocks(
            IEnumerable<CustomBackgroundBlock> blocks,
            bool showAscii)
        {
            var newState = new ViewportState { ShowAscii = showAscii };

            // Check cache validity
            if (_cacheValid && _cachedState.Equals(newState) &&
                _preparedBlocks.Count == (blocks?.Count() ?? 0))
            {
                return; // Cache hit
            }

            // Rebuild cache
            _cacheValid = false;
            _preparedBlocks.Clear();

            if (blocks == null)
            {
                _cachedState = newState;
                _cacheValid = true;
                return;
            }

            // Prepare frozen brushes
            foreach (var block in blocks)
            {
                if (block == null || !block.IsValid)
                    continue;

                _preparedBlocks.Add(new PreparedBlock
                {
                    Block = block,
                    FrozenBrush = block.GetTransparentBrush(),
                    IsValid = true
                });
            }

            _cachedState = newState;
            _cacheValid = true;
        }

        /// <summary>
        /// Draw prepared blocks to the viewport using actual Rects from ByteData
        /// Optimized for performance with visible range culling
        /// </summary>
        /// <param name="dc">Drawing context</param>
        /// <param name="linesCached">Cached lines from HexViewport</param>
        /// <param name="firstVisiblePos">First visible byte position</param>
        /// <param name="lastVisiblePos">Last visible byte position</param>
        public void DrawBlocks(
            DrawingContext dc,
            List<HexLine> linesCached,
            long firstVisiblePos,
            long lastVisiblePos)
        {
            if (!_cacheValid || _preparedBlocks.Count == 0 ||
                linesCached == null || linesCached.Count == 0)
                return;

            // Draw each prepared block
            foreach (var prepared in _preparedBlocks)
            {
                // Skip blocks outside visible range (performance optimization)
                if (prepared.Block.StartOffset >= lastVisiblePos + 1 ||
                    prepared.Block.StopOffset <= firstVisiblePos)
                    continue;

                DrawBlock(dc, prepared, linesCached, 0, 0); // Parameters unused now
            }
        }

        /// <summary>
        /// Invalidate cache to force recalculation
        /// Call when viewport properties change
        /// </summary>
        public void InvalidateCache()
        {
            _cacheValid = false;
            _preparedBlocks.Clear();
        }

        /// <summary>
        /// Check if cache is valid
        /// </summary>
        public bool IsCacheValid => _cacheValid;

        /// <summary>
        /// Get number of prepared blocks
        /// </summary>
        public int PreparedBlockCount => _preparedBlocks.Count;

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Draw a single block using actual rendered Rects from ByteData
        /// Guaranteed accurate positioning - no calculation, just read and draw
        /// Optimized: line-level culling only (byte-level foreach is faster than index search)
        /// </summary>
        private void DrawBlock(
            DrawingContext dc,
            PreparedBlock prepared,
            List<HexLine> linesCached,
            double hexStartX,
            double asciiStartX)
        {
            var block = prepared.Block;
            var brush = prepared.FrozenBrush;

            // Iterate through all visible lines
            foreach (var line in linesCached)
            {
                if (line.Bytes == null || line.Bytes.Count == 0)
                    continue;

                // Get line byte range for quick line-level culling
                long lineStartPos = line.Bytes[0].VirtualPos;
                long lineEndPos = line.Bytes[line.Bytes.Count - 1].VirtualPos;

                // Skip entire line if block doesn't overlap with it
                if (block.StartOffset >= lineEndPos + 1 || block.StopOffset <= lineStartPos)
                    continue;

                // Iterate through bytes in overlapping lines
                // Note: foreach with range check is faster than finding indices first
                foreach (var byteData in line.Bytes)
                {
                    long bytePos = byteData.VirtualPos;

                    // Skip bytes outside block range (fast early exit)
                    if (bytePos < block.StartOffset || bytePos >= block.StopOffset)
                        continue;

                    // Draw hex area background using stored Rect
                    if (byteData.HexRect.HasValue)
                    {
                        dc.DrawRectangle(brush, null, byteData.HexRect.Value);
                    }

                    // Draw ASCII area background using stored Rect (if visible)
                    if (_cachedState.ShowAscii && byteData.AsciiRect.HasValue)
                    {
                        dc.DrawRectangle(brush, null, byteData.AsciiRect.Value);
                    }
                }
            }
        }

        #endregion
    }
}
