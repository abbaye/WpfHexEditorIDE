// Apache 2.0 - 2026
// Virtual Scrolling Engine for JsonEditor - Phase 11
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;

namespace WpfHexEditor.JsonEditor.Helpers
{
    /// <summary>
    /// Engine for calculating visible lines in viewport for virtual scrolling.
    /// Inspired by HexViewport virtualization pattern.
    /// Supports 100K+ lines with constant memory usage.
    /// </summary>
    public class VirtualizationEngine
    {
        #region Properties

        /// <summary>
        /// Total number of lines in document
        /// </summary>
        public int TotalLines { get; set; }

        /// <summary>
        /// Height of viewport in pixels
        /// </summary>
        public double ViewportHeight { get; set; }

        /// <summary>
        /// Height of each line in pixels
        /// </summary>
        public double LineHeight { get; set; }

        /// <summary>
        /// Current vertical scroll offset in pixels
        /// </summary>
        public double ScrollOffset { get; set; }

        /// <summary>
        /// Number of extra lines to render above/below viewport for smooth scrolling
        /// </summary>
        public int RenderBuffer { get; set; }

        /// <summary>
        /// First visible line index (0-based)
        /// </summary>
        public int FirstVisibleLine { get; private set; }

        /// <summary>
        /// Last visible line index (0-based)
        /// </summary>
        public int LastVisibleLine { get; private set; }

        /// <summary>
        /// Number of lines visible in viewport
        /// </summary>
        public int VisibleLineCount => Math.Max(0, LastVisibleLine - FirstVisibleLine + 1);

        /// <summary>
        /// Total document height in pixels
        /// </summary>
        public double TotalHeight => TotalLines * LineHeight;

        #endregion

        #region Constructor

        public VirtualizationEngine()
        {
            TotalLines = 0;
            ViewportHeight = 0;
            LineHeight = 20; // Default
            ScrollOffset = 0;
            RenderBuffer = 10; // Default buffer
        }

        #endregion

        #region Visible Range Calculation

        /// <summary>
        /// Calculate which lines are visible in current viewport.
        /// Returns (FirstVisibleLine, LastVisibleLine) with render buffer applied.
        /// </summary>
        public (int first, int last) CalculateVisibleRange()
        {
            if (TotalLines == 0 || ViewportHeight <= 0 || LineHeight <= 0)
            {
                FirstVisibleLine = 0;
                LastVisibleLine = -1;
                return (0, -1);
            }

            // Calculate first visible line from scroll offset
            int firstLine = (int)(ScrollOffset / LineHeight);

            // Calculate last visible line
            int visibleLines = (int)Math.Ceiling(ViewportHeight / LineHeight);
            int lastLine = firstLine + visibleLines - 1;

            // Apply render buffer (render extra lines for smooth scrolling)
            firstLine = Math.Max(0, firstLine - RenderBuffer);
            lastLine = Math.Min(TotalLines - 1, lastLine + RenderBuffer);

            FirstVisibleLine = firstLine;
            LastVisibleLine = lastLine;

            return (firstLine, lastLine);
        }

        /// <summary>
        /// Check if a specific line is in visible range (with render buffer)
        /// </summary>
        public bool IsLineVisible(int lineIndex)
        {
            return lineIndex >= FirstVisibleLine && lineIndex <= LastVisibleLine;
        }

        /// <summary>
        /// Get Y position of a line relative to viewport top
        /// </summary>
        public double GetLineYPosition(int lineIndex)
        {
            return (lineIndex * LineHeight) - ScrollOffset;
        }

        /// <summary>
        /// Get line index from Y position in viewport
        /// </summary>
        public int GetLineAtYPosition(double y)
        {
            double absoluteY = y + ScrollOffset;
            int lineIndex = (int)(absoluteY / LineHeight);
            return Math.Max(0, Math.Min(TotalLines - 1, lineIndex));
        }

        #endregion

        #region Scroll Operations

        /// <summary>
        /// Scroll by line count (positive = down, negative = up)
        /// Returns new scroll offset
        /// </summary>
        public double ScrollByLines(int lineCount)
        {
            double newOffset = ScrollOffset + (lineCount * LineHeight);
            return ClampScrollOffset(newOffset);
        }

        /// <summary>
        /// Scroll by pixel amount (positive = down, negative = up)
        /// Returns new scroll offset
        /// </summary>
        public double ScrollByPixels(double pixels)
        {
            double newOffset = ScrollOffset + pixels;
            return ClampScrollOffset(newOffset);
        }

        /// <summary>
        /// Scroll to specific line (bring it to top of viewport)
        /// Returns new scroll offset
        /// </summary>
        public double ScrollToLine(int lineIndex)
        {
            double newOffset = lineIndex * LineHeight;
            return ClampScrollOffset(newOffset);
        }

        /// <summary>
        /// Ensure a specific line is visible in viewport
        /// Scrolls minimally to bring line into view
        /// Returns new scroll offset
        /// </summary>
        public double EnsureLineVisible(int lineIndex)
        {
            double lineY = lineIndex * LineHeight;

            // Already visible?
            if (lineY >= ScrollOffset && lineY + LineHeight <= ScrollOffset + ViewportHeight)
                return ScrollOffset;

            // Line is above viewport - scroll up
            if (lineY < ScrollOffset)
                return ClampScrollOffset(lineY);

            // Line is below viewport - scroll down to bring it to bottom
            double newOffset = lineY - ViewportHeight + LineHeight;
            return ClampScrollOffset(newOffset);
        }

        /// <summary>
        /// Clamp scroll offset to valid range [0, max]
        /// </summary>
        private double ClampScrollOffset(double offset)
        {
            double maxOffset = Math.Max(0, TotalHeight - ViewportHeight);
            return Math.Max(0, Math.Min(maxOffset, offset));
        }

        #endregion

        #region Scroll Percentage

        /// <summary>
        /// Get current scroll position as percentage (0.0 to 1.0)
        /// </summary>
        public double GetScrollPercentage()
        {
            double maxOffset = Math.Max(0, TotalHeight - ViewportHeight);
            if (maxOffset <= 0)
                return 0;

            return ScrollOffset / maxOffset;
        }

        /// <summary>
        /// Set scroll position from percentage (0.0 to 1.0)
        /// Returns new scroll offset
        /// </summary>
        public double SetScrollPercentage(double percentage)
        {
            percentage = Math.Max(0, Math.Min(1.0, percentage));
            double maxOffset = Math.Max(0, TotalHeight - ViewportHeight);
            double newOffset = maxOffset * percentage;
            return ClampScrollOffset(newOffset);
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get virtualization statistics for debugging
        /// </summary>
        public string GetStatistics()
        {
            return $"Total Lines: {TotalLines}\n" +
                   $"Viewport Height: {ViewportHeight:F1}px\n" +
                   $"Line Height: {LineHeight:F1}px\n" +
                   $"Scroll Offset: {ScrollOffset:F1}px\n" +
                   $"Visible Range: {FirstVisibleLine}-{LastVisibleLine} ({VisibleLineCount} lines)\n" +
                   $"Render Buffer: {RenderBuffer} lines\n" +
                   $"Total Height: {TotalHeight:F1}px\n" +
                   $"Scroll %: {GetScrollPercentage() * 100:F1}%";
        }

        #endregion
    }
}
