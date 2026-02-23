//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfHexaEditor.Core;
using WpfHexaEditor.Models;

namespace WpfHexaEditor.Rendering
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
        /// Immutable struct representing viewport rendering configuration
        /// Used as cache key to detect when recalculation is needed
        /// </summary>
        private struct ViewportState : IEquatable<ViewportState>
        {
            public int BytesPerLine;
            public double FontSize;
            public string FontFamily;
            public double LineHeight;
            public double HexByteWidth;
            public double AsciiCharWidth;
            public ByteSpacerGroup ByteGrouping;
            public int ByteSpacerWidth;
            public bool ShowOffset;
            public bool ShowAscii;
            public bool HasAsciiSpacers; // Whether ASCII area should have spacers

            public bool Equals(ViewportState other)
            {
                return BytesPerLine == other.BytesPerLine &&
                       Math.Abs(FontSize - other.FontSize) < 0.001 &&
                       FontFamily == other.FontFamily &&
                       Math.Abs(LineHeight - other.LineHeight) < 0.001 &&
                       Math.Abs(HexByteWidth - other.HexByteWidth) < 0.001 &&
                       Math.Abs(AsciiCharWidth - other.AsciiCharWidth) < 0.001 &&
                       ByteGrouping == other.ByteGrouping &&
                       ByteSpacerWidth == other.ByteSpacerWidth &&
                       ShowOffset == other.ShowOffset &&
                       ShowAscii == other.ShowAscii &&
                       HasAsciiSpacers == other.HasAsciiSpacers;
            }

            public override bool Equals(object obj) => obj is ViewportState state && Equals(state);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + BytesPerLine.GetHashCode();
                    hash = hash * 31 + FontSize.GetHashCode();
                    hash = hash * 31 + (FontFamily?.GetHashCode() ?? 0);
                    hash = hash * 31 + ByteGrouping.GetHashCode();
                    hash = hash * 31 + HasAsciiSpacers.GetHashCode();
                    return hash;
                }
            }
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
        private const double OffsetWidth = 110;
        private const double HexByteSpacing = 2;
        private const double SeparatorWidth = 20;
        private const double TopMargin = 2;

        #endregion

        #region Public Methods

        /// <summary>
        /// Prepare blocks for rendering by pre-computing frozen brushes
        /// Call this when blocks change or viewport properties change
        /// </summary>
        /// <param name="blocks">Blocks to prepare</param>
        /// <param name="bytesPerLine">Bytes per line</param>
        /// <param name="fontSize">Font size</param>
        /// <param name="fontFamily">Font family name</param>
        /// <param name="lineHeight">Line height in pixels</param>
        /// <param name="hexByteWidth">Width of hex byte cell</param>
        /// <param name="asciiCharWidth">Width of ASCII character</param>
        /// <param name="byteGrouping">Byte grouping (4, 8, 16 bytes)</param>
        /// <param name="byteSpacerWidth">Width of each spacer in pixels</param>
        /// <param name="showOffset">Whether offset column is visible</param>
        /// <param name="showAscii">Whether ASCII column is visible</param>
        /// <param name="hasAsciiSpacers">Whether ASCII area should have spacers (depends on ByteSpacerPositioning and TBL)</param>
        public void PrepareBlocks(
            IEnumerable<CustomBackgroundBlock> blocks,
            int bytesPerLine,
            double fontSize,
            string fontFamily,
            double lineHeight,
            double hexByteWidth,
            double asciiCharWidth,
            ByteSpacerGroup byteGrouping,
            int byteSpacerWidth,
            bool showOffset,
            bool showAscii,
            bool hasAsciiSpacers = false)
        {
            var newState = new ViewportState
            {
                BytesPerLine = bytesPerLine,
                FontSize = fontSize,
                FontFamily = fontFamily,
                LineHeight = lineHeight,
                HexByteWidth = hexByteWidth,
                AsciiCharWidth = asciiCharWidth,
                ByteGrouping = byteGrouping,
                ByteSpacerWidth = byteSpacerWidth,
                ShowOffset = showOffset,
                ShowAscii = showAscii,
                HasAsciiSpacers = hasAsciiSpacers
            };

            // Check if cache is still valid
            if (_cacheValid && _cachedState.Equals(newState) &&
                _preparedBlocks.Count == (blocks?.Count() ?? 0))
            {
                // Cache hit - no preparation needed
                return;
            }

            // Cache miss - rebuild
            _cacheValid = false;
            _preparedBlocks.Clear();

            if (blocks == null)
            {
                _cachedState = newState;
                _cacheValid = true;
                return;
            }

            // Prepare each block (create frozen brushes)
            foreach (var block in blocks)
            {
                if (block == null || !block.IsValid)
                    continue;

                var prepared = new PreparedBlock
                {
                    Block = block,
                    FrozenBrush = block.GetTransparentBrush(), // Already frozen from CustomBackgroundBlock
                    IsValid = true
                };

                _preparedBlocks.Add(prepared);
            }

            _cachedState = newState;
            _cacheValid = true;
        }

        /// <summary>
        /// Draw prepared blocks to the viewport
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

            double hexStartX = _cachedState.ShowOffset ? OffsetWidth : 0;
            double asciiStartX = hexStartX +
                (_cachedState.BytesPerLine * (_cachedState.HexByteWidth + HexByteSpacing)) +
                4 + SeparatorWidth;

            // Calculate spacers width once
            int numSpacers = CalculateSpacerCount(_cachedState.BytesPerLine, (int)_cachedState.ByteGrouping);
            double spacersWidth = numSpacers * _cachedState.ByteSpacerWidth;
            asciiStartX += spacersWidth;

            // Draw each prepared block
            foreach (var prepared in _preparedBlocks)
            {
                // Skip blocks outside visible range (performance optimization)
                if (prepared.Block.StartOffset >= lastVisiblePos + 1 ||
                    prepared.Block.StopOffset <= firstVisiblePos)
                    continue;

                DrawBlock(dc, prepared, linesCached, hexStartX, asciiStartX);
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
        /// Draw a single block across multiple lines
        /// </summary>
        private void DrawBlock(
            DrawingContext dc,
            PreparedBlock prepared,
            List<HexLine> linesCached,
            double hexStartX,
            double asciiStartX)
        {
            double y = TopMargin;
            var block = prepared.Block;
            var brush = prepared.FrozenBrush;

            foreach (var line in linesCached)
            {
                if (line.Bytes == null || line.Bytes.Count == 0)
                {
                    y += _cachedState.LineHeight;
                    continue;
                }

                long lineStartPos = line.Bytes[0].VirtualPos;
                long lineEndPos = line.Bytes[line.Bytes.Count - 1].VirtualPos;

                // Check if block overlaps with this line
                if (block.StartOffset < lineEndPos + 1 && block.StopOffset > lineStartPos)
                {
                    // Find byte range in line that intersects with block
                    int startByteIndex = FindStartByteIndex(line, block.StartOffset);
                    int endByteIndex = FindEndByteIndex(line, block.StopOffset);

                    // Draw hex area background
                    var hexRect = CalculateHexRectangle(
                        startByteIndex, endByteIndex, hexStartX, y);
                    dc.DrawRectangle(brush, null, hexRect);

                    // Draw ASCII area background (if visible)
                    if (_cachedState.ShowAscii)
                    {
                        var asciiRect = CalculateAsciiRectangle(
                            startByteIndex, endByteIndex, asciiStartX, y);
                        dc.DrawRectangle(brush, null, asciiRect);
                    }
                }

                y += _cachedState.LineHeight;
            }
        }

        /// <summary>
        /// Find the starting byte index in a line for a block
        /// </summary>
        private int FindStartByteIndex(HexLine line, long blockStart)
        {
            for (int i = 0; i < line.Bytes.Count; i++)
            {
                if (line.Bytes[i].VirtualPos >= blockStart)
                    return i;
            }
            return 0;
        }

        /// <summary>
        /// Find the ending byte index in a line for a block
        /// </summary>
        private int FindEndByteIndex(HexLine line, long blockStop)
        {
            for (int i = line.Bytes.Count - 1; i >= 0; i--)
            {
                if (line.Bytes[i].VirtualPos < blockStop)
                    return i;
            }
            return line.Bytes.Count - 1;
        }

        /// <summary>
        /// Calculate rectangle for hex area background
        /// </summary>
        private Rect CalculateHexRectangle(
            int startByteIndex, int endByteIndex, double hexStartX, double y)
        {
            double x = hexStartX;

            // Account for spacers before start byte
            for (int i = 0; i < startByteIndex; i++)
            {
                if (_cachedState.BytesPerLine >= (int)_cachedState.ByteGrouping &&
                    i > 0 && i % (int)_cachedState.ByteGrouping == 0)
                {
                    x += _cachedState.ByteSpacerWidth;
                }
                x += _cachedState.HexByteWidth + HexByteSpacing;
            }

            double startX = x;
            double width = 0;

            // Calculate width including spacers
            for (int i = startByteIndex; i <= endByteIndex; i++)
            {
                if (_cachedState.BytesPerLine >= (int)_cachedState.ByteGrouping &&
                    i > 0 && i % (int)_cachedState.ByteGrouping == 0)
                {
                    width += _cachedState.ByteSpacerWidth;
                }
                width += _cachedState.HexByteWidth + HexByteSpacing;
            }

            return new Rect(startX, y, width, _cachedState.LineHeight);
        }

        /// <summary>
        /// Calculate rectangle for ASCII area background
        /// Note: ASCII area may have spacers depending on ByteSpacerPositioning and TBL settings
        /// </summary>
        private Rect CalculateAsciiRectangle(
            int startByteIndex, int endByteIndex, double asciiStartX, double y)
        {
            if (!_cachedState.HasAsciiSpacers)
            {
                // No spacers in ASCII area - simple calculation
                double startX = asciiStartX + (startByteIndex * _cachedState.AsciiCharWidth);
                double width = (endByteIndex - startByteIndex + 1) * _cachedState.AsciiCharWidth;
                return new Rect(startX, y, width, _cachedState.LineHeight);
            }
            else
            {
                // ASCII area has spacers - use same logic as hex area
                double x = asciiStartX;

                // Account for spacers before start byte
                for (int i = 0; i < startByteIndex; i++)
                {
                    if (_cachedState.BytesPerLine >= (int)_cachedState.ByteGrouping &&
                        i > 0 && i % (int)_cachedState.ByteGrouping == 0)
                    {
                        x += _cachedState.ByteSpacerWidth;
                    }
                    x += _cachedState.AsciiCharWidth;
                }

                double startX = x;
                double width = 0;

                // Calculate width including spacers
                for (int i = startByteIndex; i <= endByteIndex; i++)
                {
                    if (_cachedState.BytesPerLine >= (int)_cachedState.ByteGrouping &&
                        i > 0 && i % (int)_cachedState.ByteGrouping == 0)
                    {
                        width += _cachedState.ByteSpacerWidth;
                    }
                    width += _cachedState.AsciiCharWidth;
                }

                return new Rect(startX, y, width, _cachedState.LineHeight);
            }
        }

        /// <summary>
        /// Calculate number of spacers for byte grouping
        /// </summary>
        private int CalculateSpacerCount(int bytesPerLine, int grouping)
        {
            if (bytesPerLine < grouping || grouping <= 0) return 0;

            return (bytesPerLine % grouping == 0)
                ? (bytesPerLine / grouping) - 1
                : bytesPerLine / grouping;
        }

        #endregion
    }
}
