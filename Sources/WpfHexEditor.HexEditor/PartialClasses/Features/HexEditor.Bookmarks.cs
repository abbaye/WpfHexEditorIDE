//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Bookmarks Management
    /// Contains methods for managing bookmarks in the hex editor
    /// </summary>
    public partial class HexEditor
    {
        #region Public Methods - Bookmarks

        /// <summary>
        /// Add a bookmark at the specified position
        /// </summary>
        /// <param name="position">Position to bookmark (virtual)</param>
        public void SetBookmark(long position)
        {
            if (position < 0 || position >= VirtualLength) return;
            if (!_bookmarks.Contains(position))
            {
                _bookmarks.Add(position);
                _bookmarks.Sort(); // Keep sorted for easy navigation
            }
        }

        /// <summary>
        /// Remove a bookmark at the specified position
        /// </summary>
        /// <param name="position">Position to remove bookmark from (virtual)</param>
        public void RemoveBookmark(long position)
        {
            _bookmarks.Remove(position);
        }

        /// <summary>
        /// Clear all bookmarks
        /// </summary>
        public void ClearAllBookmarks()
        {
            _bookmarks.Clear();
        }

        /// <summary>
        /// Get all bookmarks
        /// </summary>
        /// <returns>Array of bookmark positions</returns>
        public long[] GetBookmarks()
        {
            return _bookmarks.ToArray();
        }

        /// <summary>
        /// Check if a position is bookmarked
        /// </summary>
        /// <param name="position">Position to check (virtual)</param>
        /// <returns>True if position is bookmarked</returns>
        public bool IsBookmarked(long position)
        {
            return _bookmarks.Contains(position);
        }

        /// <summary>
        /// Get the next bookmark after the specified position
        /// </summary>
        /// <param name="position">Current position (virtual)</param>
        /// <returns>Position of next bookmark, or -1 if none found</returns>
        public long GetNextBookmark(long position)
        {
            foreach (var bookmark in _bookmarks)
            {
                if (bookmark > position)
                    return bookmark;
            }
            return -1;
        }

        /// <summary>
        /// Get the previous bookmark before the specified position
        /// </summary>
        /// <param name="position">Current position (virtual)</param>
        /// <returns>Position of previous bookmark, or -1 if none found</returns>
        public long GetPreviousBookmark(long position)
        {
            for (int i = _bookmarks.Count - 1; i >= 0; i--)
            {
                if (_bookmarks[i] < position)
                    return _bookmarks[i];
            }
            return -1;
        }

        #endregion
    }
}
