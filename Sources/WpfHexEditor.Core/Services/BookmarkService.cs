//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Models.Bookmarks;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service responsible for bookmark management operations
    /// </summary>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var service = new BookmarkService();
    ///
    /// // Add bookmarks with descriptions
    /// service.AddBookmark(position: 100, description: "Header start");
    /// service.AddBookmark(position: 500, description: "Data section");
    /// service.AddBookmark(position: 1000, description: "Footer");
    ///
    /// // Check if bookmark exists
    /// if (service.HasBookmarkAt(100))
    ///     Console.WriteLine("Bookmark found at position 100");
    ///
    /// // Navigate between bookmarks
    /// var current = 250;
    /// var next = service.GetNextBookmark(current);
    /// if (next != null)
    ///     Console.WriteLine($"Next bookmark at {next.BytePositionInStream}: {next.Description}");
    ///
    /// var previous = service.GetPreviousBookmark(current);
    /// if (previous != null)
    ///     Console.WriteLine($"Previous bookmark at {previous.BytePositionInStream}");
    ///
    /// // Get all bookmarks
    /// foreach (var bookmark in service.GetAllBookmarks())
    ///     Console.WriteLine($"Position: {bookmark.BytePositionInStream}, Description: {bookmark.Description}");
    ///
    /// // Update bookmark description
    /// service.UpdateBookmarkDescription(100, "Updated header description");
    ///
    /// // Remove specific bookmark
    /// service.RemoveBookmark(500);
    ///
    /// // Get statistics
    /// int count = service.GetBookmarkCount();
    /// Console.WriteLine($"Total bookmarks: {count}");
    /// </code>
    /// </example>
    public class BookmarkService
    {
        #region Private Fields

        /// <summary>
        /// Internal list of bookmarks
        /// </summary>
        private readonly List<BookMark> _bookmarks = new();

        /// <summary>
        /// Category colors for enhanced bookmarks
        /// </summary>
        private readonly Dictionary<string, Color> _categoryColors = new();

        /// <summary>
        /// Registered bookmark groups
        /// </summary>
        private readonly List<BookmarkGroup> _groups = new();

        #endregion

        #region Add Operations

        /// <summary>
        /// Add a bookmark at the specified position
        /// </summary>
        /// <param name="position">Position in stream</param>
        /// <param name="description">Optional description</param>
        /// <param name="marker">Scroll marker type</param>
        /// <returns>True if bookmark was added, false if position already has a bookmark</returns>
        public bool AddBookmark(long position, string description = "", ScrollMarker marker = ScrollMarker.Bookmark)
        {
            if (position < 0)
                return false;

            // Check if bookmark already exists at this position with same marker
            if (_bookmarks.Any(b => b.BytePositionInStream == position && b.Marker == marker))
                return false;

            var bookmark = new BookMark(description, position, marker);
            _bookmarks.Add(bookmark);
            return true;
        }

        /// <summary>
        /// Add a bookmark object directly
        /// </summary>
        /// <param name="bookmark">Bookmark to add</param>
        /// <returns>True if bookmark was added</returns>
        public bool AddBookmark(BookMark bookmark)
        {
            if (bookmark == null || bookmark.BytePositionInStream < 0)
                return false;

            // Check if bookmark already exists
            if (_bookmarks.Any(b => b.BytePositionInStream == bookmark.BytePositionInStream &&
                                    b.Marker == bookmark.Marker))
                return false;

            _bookmarks.Add(bookmark);
            return true;
        }

        #endregion

        #region Remove Operations

        /// <summary>
        /// Remove bookmark at specified position
        /// </summary>
        /// <param name="position">Position in stream</param>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>Number of bookmarks removed</returns>
        public int RemoveBookmark(long position, ScrollMarker? marker = null)
        {
            if (position < 0)
                return 0;

            var count = 0;
            if (marker.HasValue)
            {
                // Remove only bookmarks with specified marker
                count = _bookmarks.RemoveAll(b => b.BytePositionInStream == position && b.Marker == marker.Value);
            }
            else
            {
                // Remove all bookmarks at position
                count = _bookmarks.RemoveAll(b => b.BytePositionInStream == position);
            }

            return count;
        }

        /// <summary>
        /// Remove all bookmarks with specified marker type
        /// </summary>
        /// <param name="marker">Marker type to remove</param>
        /// <returns>Number of bookmarks removed</returns>
        public int RemoveAllBookmarks(ScrollMarker marker)
        {
            return _bookmarks.RemoveAll(b => b.Marker == marker);
        }

        /// <summary>
        /// Clear all bookmarks
        /// </summary>
        /// <returns>Number of bookmarks cleared</returns>
        public int ClearAll()
        {
            var count = _bookmarks.Count;
            _bookmarks.Clear();
            return count;
        }

        #endregion

        #region Query Operations

        /// <summary>
        /// Get all bookmarks
        /// </summary>
        /// <returns>Enumerable of all bookmarks</returns>
        public IEnumerable<BookMark> GetAllBookmarks()
        {
            return _bookmarks.ToList();
        }

        /// <summary>
        /// Get bookmarks filtered by marker type
        /// </summary>
        /// <param name="marker">Marker type filter</param>
        /// <returns>Enumerable of filtered bookmarks</returns>
        public IEnumerable<BookMark> GetBookmarksByMarker(ScrollMarker marker)
        {
            return _bookmarks.Where(b => b.Marker == marker).ToList();
        }

        /// <summary>
        /// Get bookmark at specific position
        /// </summary>
        /// <param name="position">Position in stream</param>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>Bookmark at position, or null if not found</returns>
        public BookMark GetBookmarkAt(long position, ScrollMarker? marker = null)
        {
            if (position < 0)
                return null;

            if (marker.HasValue)
                return _bookmarks.FirstOrDefault(b => b.BytePositionInStream == position && b.Marker == marker.Value);

            return _bookmarks.FirstOrDefault(b => b.BytePositionInStream == position);
        }

        /// <summary>
        /// Check if bookmark exists at position
        /// </summary>
        /// <param name="position">Position in stream</param>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>True if bookmark exists</returns>
        public bool HasBookmarkAt(long position, ScrollMarker? marker = null)
        {
            if (position < 0)
                return false;

            if (marker.HasValue)
                return _bookmarks.Any(b => b.BytePositionInStream == position && b.Marker == marker.Value);

            return _bookmarks.Any(b => b.BytePositionInStream == position);
        }

        /// <summary>
        /// Get total number of bookmarks
        /// </summary>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>Count of bookmarks</returns>
        public int GetBookmarkCount(ScrollMarker? marker = null)
        {
            if (marker.HasValue)
                return _bookmarks.Count(b => b.Marker == marker.Value);

            return _bookmarks.Count;
        }

        /// <summary>
        /// Check if any bookmarks exist
        /// </summary>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>True if bookmarks exist</returns>
        public bool HasBookmarks(ScrollMarker? marker = null)
        {
            if (marker.HasValue)
                return _bookmarks.Any(b => b.Marker == marker.Value);

            return _bookmarks.Count > 0;
        }

        /// <summary>
        /// Get next bookmark after specified position
        /// </summary>
        /// <param name="position">Current position</param>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>Next bookmark, or null if none found</returns>
        public BookMark GetNextBookmark(long position, ScrollMarker? marker = null)
        {
            IEnumerable<BookMark> bookmarks = marker.HasValue
                ? _bookmarks.Where(b => b.Marker == marker.Value)
                : _bookmarks;

            return bookmarks
                .Where(b => b.BytePositionInStream > position)
                .OrderBy(b => b.BytePositionInStream)
                .FirstOrDefault();
        }

        /// <summary>
        /// Get previous bookmark before specified position
        /// </summary>
        /// <param name="position">Current position</param>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>Previous bookmark, or null if none found</returns>
        public BookMark GetPreviousBookmark(long position, ScrollMarker? marker = null)
        {
            IEnumerable<BookMark> bookmarks = marker.HasValue
                ? _bookmarks.Where(b => b.Marker == marker.Value)
                : _bookmarks;

            return bookmarks
                .Where(b => b.BytePositionInStream < position)
                .OrderByDescending(b => b.BytePositionInStream)
                .FirstOrDefault();
        }

        #endregion

        #region Update Operations

        /// <summary>
        /// Update bookmark description at position
        /// </summary>
        /// <param name="position">Position in stream</param>
        /// <param name="description">New description</param>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>True if bookmark was updated</returns>
        public bool UpdateBookmarkDescription(long position, string description, ScrollMarker? marker = null)
        {
            var bookmark = GetBookmarkAt(position, marker);
            if (bookmark == null)
                return false;

            bookmark.Description = description ?? string.Empty;
            return true;
        }

        #endregion

        #region Enhanced Bookmark Operations

        /// <summary>
        /// Add enhanced bookmark with metadata (category, annotation, tags)
        /// </summary>
        /// <param name="position">Position in stream</param>
        /// <param name="description">Description</param>
        /// <param name="category">Category name</param>
        /// <param name="annotation">Optional annotation</param>
        /// <param name="tags">Optional tags</param>
        /// <param name="marker">Scroll marker type</param>
        /// <returns>True if bookmark was added</returns>
        public bool AddBookmarkWithMetadata(long position, string description = "", string category = "Default",
            string annotation = "", List<string> tags = null, ScrollMarker marker = ScrollMarker.Bookmark)
        {
            if (position < 0)
                return false;

            // Check if bookmark already exists at this position with same marker
            if (_bookmarks.Any(b => b.BytePositionInStream == position && b.Marker == marker))
                return false;

            var bookmark = new EnhancedBookmark(position, description, category, annotation, tags)
            {
                Marker = marker
            };
            _bookmarks.Add(bookmark);
            return true;
        }

        /// <summary>
        /// Update bookmark category
        /// </summary>
        /// <param name="position">Position in stream</param>
        /// <param name="category">New category</param>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>True if bookmark was updated</returns>
        public bool UpdateBookmarkCategory(long position, string category, ScrollMarker? marker = null)
        {
            var bookmark = GetBookmarkAt(position, marker);
            if (bookmark is EnhancedBookmark enhanced)
            {
                enhanced.Category = category;
                enhanced.ModifiedDate = DateTime.Now;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Update bookmark annotation
        /// </summary>
        /// <param name="position">Position in stream</param>
        /// <param name="annotation">New annotation</param>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>True if bookmark was updated</returns>
        public bool UpdateBookmarkAnnotation(long position, string annotation, ScrollMarker? marker = null)
        {
            var bookmark = GetBookmarkAt(position, marker);
            if (bookmark is EnhancedBookmark enhanced)
            {
                enhanced.Annotation = annotation;
                enhanced.ModifiedDate = DateTime.Now;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Add tag to bookmark
        /// </summary>
        /// <param name="position">Position in stream</param>
        /// <param name="tag">Tag to add</param>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>True if tag was added</returns>
        public bool AddTagToBookmark(long position, string tag, ScrollMarker? marker = null)
        {
            var bookmark = GetBookmarkAt(position, marker);
            if (bookmark is EnhancedBookmark enhanced)
            {
                return enhanced.AddTag(tag);
            }
            return false;
        }

        /// <summary>
        /// Remove tag from bookmark
        /// </summary>
        /// <param name="position">Position in stream</param>
        /// <param name="tag">Tag to remove</param>
        /// <param name="marker">Optional marker type filter</param>
        /// <returns>True if tag was removed</returns>
        public bool RemoveTagFromBookmark(long position, string tag, ScrollMarker? marker = null)
        {
            var bookmark = GetBookmarkAt(position, marker);
            if (bookmark is EnhancedBookmark enhanced)
            {
                return enhanced.RemoveTag(tag);
            }
            return false;
        }

        /// <summary>
        /// Get all bookmarks in a specific category
        /// </summary>
        /// <param name="category">Category name</param>
        /// <returns>Enumerable of enhanced bookmarks in category</returns>
        public IEnumerable<EnhancedBookmark> GetBookmarksByGroup(string category)
        {
            return _bookmarks
                .OfType<EnhancedBookmark>()
                .Where(b => b.Category == category)
                .ToList();
        }

        /// <summary>
        /// Get all enhanced bookmarks
        /// </summary>
        /// <returns>Enumerable of enhanced bookmarks</returns>
        public IEnumerable<EnhancedBookmark> GetAllEnhancedBookmarks()
        {
            return _bookmarks.OfType<EnhancedBookmark>().ToList();
        }

        /// <summary>
        /// Get bookmarks with specific tag
        /// </summary>
        /// <param name="tag">Tag to search for</param>
        /// <param name="caseSensitive">Case sensitive search</param>
        /// <returns>Enumerable of enhanced bookmarks with tag</returns>
        public IEnumerable<EnhancedBookmark> GetBookmarksByTag(string tag, bool caseSensitive = false)
        {
            return _bookmarks
                .OfType<EnhancedBookmark>()
                .Where(b => b.HasTag(tag, caseSensitive))
                .ToList();
        }

        /// <summary>
        /// Get next bookmark in specific category
        /// </summary>
        /// <param name="position">Current position</param>
        /// <param name="category">Category name</param>
        /// <returns>Next bookmark in category, or null if none found</returns>
        public EnhancedBookmark GetNextBookmarkInCategory(long position, string category)
        {
            return _bookmarks
                .OfType<EnhancedBookmark>()
                .Where(b => b.Category == category && b.BytePositionInStream > position)
                .OrderBy(b => b.BytePositionInStream)
                .FirstOrDefault();
        }

        /// <summary>
        /// Get previous bookmark in specific category
        /// </summary>
        /// <param name="position">Current position</param>
        /// <param name="category">Category name</param>
        /// <returns>Previous bookmark in category, or null if none found</returns>
        public EnhancedBookmark GetPreviousBookmarkInCategory(long position, string category)
        {
            return _bookmarks
                .OfType<EnhancedBookmark>()
                .Where(b => b.Category == category && b.BytePositionInStream < position)
                .OrderByDescending(b => b.BytePositionInStream)
                .FirstOrDefault();
        }

        #endregion

        #region Category/Group Management

        /// <summary>
        /// Register a bookmark group/category
        /// </summary>
        /// <param name="group">Group to register</param>
        /// <returns>True if group was added</returns>
        public bool RegisterGroup(BookmarkGroup group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.Name))
                return false;

            // Check if group already exists
            if (_groups.Any(g => g.Name == group.Name))
                return false;

            _groups.Add(group);
            _categoryColors[group.Name] = group.Color;
            return true;
        }

        /// <summary>
        /// Unregister a bookmark group
        /// </summary>
        /// <param name="name">Group name</param>
        /// <returns>True if group was removed</returns>
        public bool UnregisterGroup(string name)
        {
            var removed = _groups.RemoveAll(g => g.Name == name) > 0;
            if (removed)
                _categoryColors.Remove(name);
            return removed;
        }

        /// <summary>
        /// Get all registered groups
        /// </summary>
        /// <returns>Enumerable of all groups</returns>
        public IEnumerable<BookmarkGroup> GetAllGroups()
        {
            return _groups.ToList();
        }

        /// <summary>
        /// Get group by name
        /// </summary>
        /// <param name="name">Group name</param>
        /// <returns>Group, or null if not found</returns>
        public BookmarkGroup GetGroup(string name)
        {
            return _groups.FirstOrDefault(g => g.Name == name);
        }

        /// <summary>
        /// Update group color
        /// </summary>
        /// <param name="name">Group name</param>
        /// <param name="color">New color</param>
        /// <returns>True if group was updated</returns>
        public bool UpdateGroupColor(string name, Color color)
        {
            var group = GetGroup(name);
            if (group != null)
            {
                group.Color = color;
                _categoryColors[name] = color;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get category colors dictionary for rendering
        /// </summary>
        /// <returns>Dictionary of category names to colors</returns>
        public Dictionary<string, Color> GetCategoryColors()
        {
            return new Dictionary<string, Color>(_categoryColors);
        }

        /// <summary>
        /// Get all unique categories from existing bookmarks
        /// </summary>
        /// <returns>List of category names</returns>
        public List<string> GetAllCategories()
        {
            return _bookmarks
                .OfType<EnhancedBookmark>()
                .Select(b => b.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        #endregion
    }
}
