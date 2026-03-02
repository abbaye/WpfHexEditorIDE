//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Highlights
    /// Contains methods for highlighting byte ranges
    /// </summary>
    public partial class HexEditor
    {
        #region Highlights

        /// <summary>
        /// Add highlight to a range of bytes
        /// </summary>
        /// <param name="startPosition">Start position (virtual)</param>
        /// <param name="length">Number of bytes to highlight</param>
        /// <param name="updateVisual">If true, refresh the display immediately</param>
        public void AddHighLight(long startPosition, long length, bool updateVisual = true)
        {
            if (startPosition < 0 || length <= 0 || startPosition >= VirtualLength)
                return;

            // Clamp length to file size
            long actualLength = Math.Min(length, VirtualLength - startPosition);

            // Add highlight range
            _highlights.Add((startPosition, actualLength));

            // Update visual if requested
            if (updateVisual)
            {
                RefreshView(false, true);
            }
        }

        /// <summary>
        /// Remove highlight from a range of bytes
        /// </summary>
        /// <param name="startPosition">Start position (virtual)</param>
        /// <param name="length">Number of bytes to un-highlight</param>
        /// <param name="updateVisual">If true, refresh the display immediately</param>
        public void RemoveHighLight(long startPosition, long length, bool updateVisual = true)
        {
            if (startPosition < 0 || length <= 0)
                return;

            // Remove matching highlight ranges
            _highlights.RemoveAll(h => h.start == startPosition && h.length == length);

            // Update visual if requested
            if (updateVisual)
            {
                RefreshView(false, true);
            }
        }

        /// <summary>
        /// Clear all highlights
        /// </summary>
        public void UnHighLightAll()
        {
            _highlights.Clear();
            RefreshView(false, true);
        }

        /// <summary>
        /// Check if a position is highlighted (internal helper)
        /// </summary>
        /// <param name="position">Position to check (virtual)</param>
        /// <returns>True if position is highlighted</returns>
        private bool IsHighlighted(long position)
        {
            foreach (var (start, length) in _highlights)
            {
                if (position >= start && position < start + length)
                    return true;
            }
            return false;
        }

        #endregion
    }
}
