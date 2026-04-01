//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using WpfHexEditor.Core.Models.Bookmarks;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service for advanced bookmark search operations
    /// Provides search across annotations, tags, categories, and date ranges
    /// </summary>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var searchService = new BookmarkSearchService();
    /// var bookmarkService = new BookmarkService();
    ///
    /// // Add some enhanced bookmarks
    /// bookmarkService.AddBookmarkWithMetadata(100, "Header", "Important", "File header section", new List&lt;string&gt; { "metadata", "header" });
    /// bookmarkService.AddBookmarkWithMetadata(500, "Data", "Normal", "Main data section", new List&lt;string&gt; { "data" });
    ///
    /// // Search by annotation
    /// var results = searchService.SearchByAnnotation(bookmarkService.GetAllEnhancedBookmarks(), "header");
    /// foreach (var bookmark in results)
    ///     Console.WriteLine($"Found: {bookmark.Description} at {bookmark.BytePositionInStream}");
    ///
    /// // Search by tag
    /// var tagged = searchService.SearchByTag(bookmarkService.GetAllEnhancedBookmarks(), "metadata");
    ///
    /// // Search by category
    /// var important = searchService.SearchByCategory(bookmarkService.GetAllEnhancedBookmarks(), "Important");
    ///
    /// // Complex search with multiple criteria
    /// var complexResults = searchService.Search(bookmarkService.GetAllEnhancedBookmarks(), new BookmarkSearchCriteria
    /// {
    ///     Query = "header",
    ///     SearchInDescription = true,
    ///     SearchInAnnotation = true,
    ///     SearchInTags = true,
    ///     Categories = new List&lt;string&gt; { "Important" },
    ///     CaseSensitive = false
    /// });
    /// </code>
    /// </example>
    public class BookmarkSearchService
    {
        /// <summary>
        /// Search bookmarks by annotation text
        /// </summary>
        /// <param name="bookmarks">Bookmarks to search</param>
        /// <param name="query">Search query</param>
        /// <param name="caseSensitive">Case sensitive search</param>
        /// <returns>List of matching bookmarks</returns>
        public List<EnhancedBookmark> SearchByAnnotation(IEnumerable<EnhancedBookmark> bookmarks, string query, bool caseSensitive = false)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<EnhancedBookmark>();

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            return bookmarks
                .Where(b => !string.IsNullOrEmpty(b.Annotation) && b.Annotation.IndexOf(query, comparison) >= 0)
                .ToList();
        }

        /// <summary>
        /// Search bookmarks by tag
        /// </summary>
        /// <param name="bookmarks">Bookmarks to search</param>
        /// <param name="tag">Tag to search for</param>
        /// <param name="caseSensitive">Case sensitive search</param>
        /// <returns>List of matching bookmarks</returns>
        public List<EnhancedBookmark> SearchByTag(IEnumerable<EnhancedBookmark> bookmarks, string tag, bool caseSensitive = false)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return new List<EnhancedBookmark>();

            return bookmarks
                .Where(b => b.HasTag(tag, caseSensitive))
                .ToList();
        }

        /// <summary>
        /// Search bookmarks by category
        /// </summary>
        /// <param name="bookmarks">Bookmarks to search</param>
        /// <param name="category">Category name</param>
        /// <param name="caseSensitive">Case sensitive search</param>
        /// <returns>List of matching bookmarks</returns>
        public List<EnhancedBookmark> SearchByCategory(IEnumerable<EnhancedBookmark> bookmarks, string category, bool caseSensitive = false)
        {
            if (string.IsNullOrWhiteSpace(category))
                return new List<EnhancedBookmark>();

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            return bookmarks
                .Where(b => b.Category.Equals(category, comparison))
                .ToList();
        }

        /// <summary>
        /// Search bookmarks by date range
        /// </summary>
        /// <param name="bookmarks">Bookmarks to search</param>
        /// <param name="startDate">Start date (inclusive)</param>
        /// <param name="endDate">End date (inclusive)</param>
        /// <param name="useCreatedDate">If true, search by CreatedDate; if false, search by ModifiedDate</param>
        /// <returns>List of matching bookmarks</returns>
        public List<EnhancedBookmark> SearchByDateRange(IEnumerable<EnhancedBookmark> bookmarks, DateTime startDate, DateTime endDate, bool useCreatedDate = true)
        {
            return bookmarks
                .Where(b =>
                {
                    var date = useCreatedDate ? b.CreatedDate : b.ModifiedDate;
                    return date >= startDate && date <= endDate;
                })
                .ToList();
        }

        /// <summary>
        /// Search bookmarks by creator
        /// </summary>
        /// <param name="bookmarks">Bookmarks to search</param>
        /// <param name="createdBy">Creator name</param>
        /// <param name="caseSensitive">Case sensitive search</param>
        /// <returns>List of matching bookmarks</returns>
        public List<EnhancedBookmark> SearchByCreator(IEnumerable<EnhancedBookmark> bookmarks, string createdBy, bool caseSensitive = false)
        {
            if (string.IsNullOrWhiteSpace(createdBy))
                return new List<EnhancedBookmark>();

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            return bookmarks
                .Where(b => b.CreatedBy != null && b.CreatedBy.Equals(createdBy, comparison))
                .ToList();
        }

        /// <summary>
        /// Search bookmarks by priority
        /// </summary>
        /// <param name="bookmarks">Bookmarks to search</param>
        /// <param name="minPriority">Minimum priority (inclusive)</param>
        /// <param name="maxPriority">Maximum priority (inclusive)</param>
        /// <returns>List of matching bookmarks</returns>
        public List<EnhancedBookmark> SearchByPriority(IEnumerable<EnhancedBookmark> bookmarks, int minPriority = 0, int maxPriority = 5)
        {
            return bookmarks
                .Where(b => b.Priority >= minPriority && b.Priority <= maxPriority)
                .ToList();
        }

        /// <summary>
        /// Search bookmarks using general query matching
        /// Searches in description, annotation, tags, and category
        /// </summary>
        /// <param name="bookmarks">Bookmarks to search</param>
        /// <param name="query">Search query</param>
        /// <param name="caseSensitive">Case sensitive search</param>
        /// <param name="searchInTags">Include tags in search</param>
        /// <param name="searchInAnnotation">Include annotation in search</param>
        /// <returns>List of matching bookmarks</returns>
        public List<EnhancedBookmark> SearchByQuery(IEnumerable<EnhancedBookmark> bookmarks, string query,
            bool caseSensitive = false, bool searchInTags = true, bool searchInAnnotation = true)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<EnhancedBookmark>();

            return bookmarks
                .Where(b => b.MatchesQuery(query, caseSensitive, searchInTags, searchInAnnotation))
                .ToList();
        }

        /// <summary>
        /// Advanced search with multiple criteria
        /// </summary>
        /// <param name="bookmarks">Bookmarks to search</param>
        /// <param name="criteria">Search criteria</param>
        /// <returns>List of matching bookmarks</returns>
        public List<EnhancedBookmark> Search(IEnumerable<EnhancedBookmark> bookmarks, BookmarkSearchCriteria criteria)
        {
            if (criteria == null)
                return bookmarks.ToList();

            var results = bookmarks.AsEnumerable();

            // Apply query filter
            if (!string.IsNullOrWhiteSpace(criteria.Query))
            {
                results = results.Where(b => b.MatchesQuery(criteria.Query, criteria.CaseSensitive,
                    criteria.SearchInTags, criteria.SearchInAnnotation));
            }

            // Apply description filter
            if (criteria.SearchInDescription && !string.IsNullOrWhiteSpace(criteria.Query))
            {
                var comparison = criteria.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                results = results.Where(b => !string.IsNullOrEmpty(b.Description) &&
                    b.Description.IndexOf(criteria.Query, comparison) >= 0);
            }

            // Apply category filter
            if (criteria.Categories != null && criteria.Categories.Count > 0)
            {
                var comparison = criteria.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                results = results.Where(b => criteria.Categories.Any(c => b.Category.Equals(c, comparison)));
            }

            // Apply tags filter (must have ALL specified tags)
            if (criteria.RequiredTags != null && criteria.RequiredTags.Count > 0)
            {
                results = results.Where(b => criteria.RequiredTags.All(tag => b.HasTag(tag, criteria.CaseSensitive)));
            }

            // Apply date range filter
            if (criteria.StartDate.HasValue && criteria.EndDate.HasValue)
            {
                results = results.Where(b =>
                {
                    var date = criteria.UseCreatedDate ? b.CreatedDate : b.ModifiedDate;
                    return date >= criteria.StartDate.Value && date <= criteria.EndDate.Value;
                });
            }

            // Apply priority filter
            if (criteria.MinPriority.HasValue || criteria.MaxPriority.HasValue)
            {
                var min = criteria.MinPriority ?? 0;
                var max = criteria.MaxPriority ?? 5;
                results = results.Where(b => b.Priority >= min && b.Priority <= max);
            }

            // Apply creator filter
            if (!string.IsNullOrWhiteSpace(criteria.CreatedBy))
            {
                var comparison = criteria.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                results = results.Where(b => b.CreatedBy != null && b.CreatedBy.Equals(criteria.CreatedBy, comparison));
            }

            // Apply read-only filter
            if (criteria.IncludeReadOnly.HasValue)
            {
                results = results.Where(b => b.IsReadOnly == criteria.IncludeReadOnly.Value);
            }

            return results.ToList();
        }

        /// <summary>
        /// Get bookmarks sorted by various criteria
        /// </summary>
        /// <param name="bookmarks">Bookmarks to sort</param>
        /// <param name="sortBy">Sort criteria</param>
        /// <param name="descending">Sort in descending order</param>
        /// <returns>Sorted list of bookmarks</returns>
        public List<EnhancedBookmark> SortBookmarks(IEnumerable<EnhancedBookmark> bookmarks,
            BookmarkSortCriteria sortBy, bool descending = false)
        {
            IOrderedEnumerable<EnhancedBookmark> sorted = null;

            switch (sortBy)
            {
                case BookmarkSortCriteria.Position:
                    sorted = descending
                        ? bookmarks.OrderByDescending(b => b.BytePositionInStream)
                        : bookmarks.OrderBy(b => b.BytePositionInStream);
                    break;

                case BookmarkSortCriteria.Description:
                    sorted = descending
                        ? bookmarks.OrderByDescending(b => b.Description)
                        : bookmarks.OrderBy(b => b.Description);
                    break;

                case BookmarkSortCriteria.Category:
                    sorted = descending
                        ? bookmarks.OrderByDescending(b => b.Category)
                        : bookmarks.OrderBy(b => b.Category);
                    break;

                case BookmarkSortCriteria.CreatedDate:
                    sorted = descending
                        ? bookmarks.OrderByDescending(b => b.CreatedDate)
                        : bookmarks.OrderBy(b => b.CreatedDate);
                    break;

                case BookmarkSortCriteria.ModifiedDate:
                    sorted = descending
                        ? bookmarks.OrderByDescending(b => b.ModifiedDate)
                        : bookmarks.OrderBy(b => b.ModifiedDate);
                    break;

                case BookmarkSortCriteria.Priority:
                    sorted = descending
                        ? bookmarks.OrderByDescending(b => b.Priority)
                        : bookmarks.OrderBy(b => b.Priority);
                    break;

                default:
                    sorted = bookmarks.OrderBy(b => b.BytePositionInStream);
                    break;
            }

            return sorted.ToList();
        }
    }

    /// <summary>
    /// Search criteria for advanced bookmark search
    /// </summary>
    public class BookmarkSearchCriteria
    {
        /// <summary>
        /// General search query
        /// </summary>
        public string Query { get; set; }

        /// <summary>
        /// Search in description field
        /// </summary>
        public bool SearchInDescription { get; set; } = true;

        /// <summary>
        /// Search in annotation field
        /// </summary>
        public bool SearchInAnnotation { get; set; } = true;

        /// <summary>
        /// Search in tags
        /// </summary>
        public bool SearchInTags { get; set; } = true;

        /// <summary>
        /// Case sensitive search
        /// </summary>
        public bool CaseSensitive { get; set; } = false;

        /// <summary>
        /// Filter by categories (OR logic - matches any)
        /// </summary>
        public List<string> Categories { get; set; }

        /// <summary>
        /// Required tags (AND logic - must have all)
        /// </summary>
        public List<string> RequiredTags { get; set; }

        /// <summary>
        /// Start date for date range filter
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// End date for date range filter
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Use CreatedDate for date filter (if false, uses ModifiedDate)
        /// </summary>
        public bool UseCreatedDate { get; set; } = true;

        /// <summary>
        /// Minimum priority (0-5)
        /// </summary>
        public int? MinPriority { get; set; }

        /// <summary>
        /// Maximum priority (0-5)
        /// </summary>
        public int? MaxPriority { get; set; }

        /// <summary>
        /// Filter by creator
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// Include read-only bookmarks filter
        /// </summary>
        public bool? IncludeReadOnly { get; set; }
    }

    /// <summary>
    /// Sort criteria for bookmark sorting
    /// </summary>
    public enum BookmarkSortCriteria
    {
        /// <summary>
        /// Sort by position in stream
        /// </summary>
        Position,

        /// <summary>
        /// Sort by description
        /// </summary>
        Description,

        /// <summary>
        /// Sort by category
        /// </summary>
        Category,

        /// <summary>
        /// Sort by creation date
        /// </summary>
        CreatedDate,

        /// <summary>
        /// Sort by modification date
        /// </summary>
        ModifiedDate,

        /// <summary>
        /// Sort by priority
        /// </summary>
        Priority
    }
}
