//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Service responsible for highlight operations (search results, marked bytes, etc.)
    /// </summary>
    public class HighlightService
    {
        #region Private Fields

        /// <summary>
        /// Dictionary of marked positions for highlighting
        /// Key = position, Value = position (allows fast lookup)
        /// </summary>
        private readonly Dictionary<long, long> _markedPositionList = new();

        #endregion

        #region Public Methods

        /// <summary>
        /// Add highlight at position start
        /// </summary>
        /// <param name="startPosition">Position to start the highlight</param>
        /// <param name="length">The length to highlight</param>
        /// <returns>Number of positions added</returns>
        public int AddHighLight(long startPosition, long length)
        {
            if (startPosition < 0) return 0;

            var count = 0;
            for (var i = startPosition; i < startPosition + length; i++)
            {
                if (!_markedPositionList.ContainsKey(i))
                {
                    _markedPositionList.Add(i, i);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Remove highlight from position start
        /// </summary>
        /// <param name="startPosition">Position to start the remove of highlight</param>
        /// <param name="length">The length of highlight to remove</param>
        /// <returns>Number of positions removed</returns>
        public int RemoveHighLight(long startPosition, long length)
        {
            var count = 0;
            for (var i = startPosition; i < startPosition + length; i++)
            {
                if (_markedPositionList.ContainsKey(i))
                {
                    _markedPositionList.Remove(i);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Remove all highlights
        /// </summary>
        /// <returns>Number of positions cleared</returns>
        public int UnHighLightAll()
        {
            var count = _markedPositionList.Count;
            _markedPositionList.Clear();
            return count;
        }

        /// <summary>
        /// Check if a position is highlighted
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>True if position is highlighted</returns>
        public bool IsHighlighted(long position)
        {
            return _markedPositionList.ContainsKey(position);
        }

        /// <summary>
        /// Get the number of highlighted positions
        /// </summary>
        /// <returns>Count of highlighted positions</returns>
        public int GetHighlightCount()
        {
            return _markedPositionList.Count;
        }

        /// <summary>
        /// Check if any position is highlighted
        /// </summary>
        /// <returns>True if at least one position is highlighted</returns>
        public bool HasHighlights()
        {
            return _markedPositionList.Count > 0;
        }

        /// <summary>
        /// Get all highlighted positions
        /// </summary>
        /// <returns>Enumerable of highlighted positions</returns>
        public IEnumerable<long> GetHighlightedPositions()
        {
            return _markedPositionList.Keys.ToList();
        }

        /// <summary>
        /// Get highlighted ranges (consecutive positions grouped)
        /// </summary>
        /// <returns>List of (start, length) tuples</returns>
        public IEnumerable<(long start, long length)> GetHighlightedRanges()
        {
            if (_markedPositionList.Count == 0)
                yield break;

            var positions = _markedPositionList.Keys.OrderBy(p => p).ToList();
            var start = positions[0];
            var length = 1L;

            for (var i = 1; i < positions.Count; i++)
            {
                if (positions[i] == positions[i - 1] + 1)
                {
                    // Consecutive position, extend the range
                    length++;
                }
                else
                {
                    // Gap found, yield current range and start a new one
                    yield return (start, length);
                    start = positions[i];
                    length = 1;
                }
            }

            // Yield the last range
            yield return (start, length);
        }

        /// <summary>
        /// Clear all highlights and reset state
        /// </summary>
        public void Clear()
        {
            _markedPositionList.Clear();
        }

        #endregion
    }
}
