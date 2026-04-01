//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Windows.Media;
using WpfHexEditor.Core;

namespace WpfHexEditor.Core.Models.Bookmarks
{
    /// <summary>
    /// Enhanced bookmark with metadata, categories, annotations, and tags
    /// Extends the base BookMark class with additional functionality
    /// </summary>
    [Serializable]
    public class EnhancedBookmark : BookMark
    {
        /// <summary>
        /// Category/group name for this bookmark
        /// </summary>
        public string Category { get; set; } = "Default";

        /// <summary>
        /// Custom color for this bookmark (overrides category color if set)
        /// </summary>
        public Color? CustomColor { get; set; } = null;

        /// <summary>
        /// Multi-line annotation/notes for this bookmark
        /// </summary>
        public string Annotation { get; set; } = string.Empty;

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Last modified timestamp
        /// </summary>
        public DateTime ModifiedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Tags for searching and filtering
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// User who created this bookmark (for collaboration features)
        /// </summary>
        public string CreatedBy { get; set; } = Environment.UserName;

        /// <summary>
        /// Whether this bookmark is read-only
        /// </summary>
        public bool IsReadOnly { get; set; } = false;

        /// <summary>
        /// Priority level (0-5, where 5 is highest)
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Gets the effective color (custom color if set, otherwise category color)
        /// </summary>
        public Color GetEffectiveColor(Dictionary<string, Color> categoryColors = null)
        {
            if (CustomColor.HasValue)
                return CustomColor.Value;

            if (categoryColors != null && categoryColors.ContainsKey(Category))
                return categoryColors[Category];

            // Default to marker-based color or blue
            return Marker switch
            {
                ScrollMarker.Bookmark => Colors.Blue,
                ScrollMarker.TblBookmark => Colors.Purple,
                _ => Colors.Blue
            };
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public EnhancedBookmark() : base()
        {
        }

        /// <summary>
        /// Constructor with position and description
        /// </summary>
        public EnhancedBookmark(long position, string description, string category = "Default")
            : base(description, position)
        {
            Category = category;
        }

        /// <summary>
        /// Constructor with all metadata
        /// </summary>
        public EnhancedBookmark(long position, string description, string category, string annotation, List<string> tags = null)
            : base(description, position)
        {
            Category = category;
            Annotation = annotation;
            if (tags != null)
                Tags = new List<string>(tags);
        }

        /// <summary>
        /// Clone this bookmark
        /// </summary>
        public EnhancedBookmark Clone()
        {
            return new EnhancedBookmark
            {
                BytePositionInStream = this.BytePositionInStream,
                Description = this.Description,
                Marker = this.Marker,
                Category = this.Category,
                CustomColor = this.CustomColor,
                Annotation = this.Annotation,
                CreatedDate = this.CreatedDate,
                ModifiedDate = DateTime.Now,
                Tags = new List<string>(this.Tags),
                CreatedBy = this.CreatedBy,
                IsReadOnly = this.IsReadOnly,
                Priority = this.Priority
            };
        }

        /// <summary>
        /// Check if bookmark matches search query
        /// </summary>
        public bool MatchesQuery(string query, bool caseSensitive = false, bool searchTags = true, bool searchAnnotation = true)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            // Search in description
            if (!string.IsNullOrEmpty(Description) && Description.IndexOf(query, comparison) >= 0)
                return true;

            // Search in annotation
            if (searchAnnotation && !string.IsNullOrEmpty(Annotation) && Annotation.IndexOf(query, comparison) >= 0)
                return true;

            // Search in tags
            if (searchTags)
            {
                foreach (var tag in Tags)
                {
                    if (!string.IsNullOrEmpty(tag) && tag.IndexOf(query, comparison) >= 0)
                        return true;
                }
            }

            // Search in category
            if (!string.IsNullOrEmpty(Category) && Category.IndexOf(query, comparison) >= 0)
                return true;

            return false;
        }

        /// <summary>
        /// Check if bookmark has a specific tag
        /// </summary>
        public bool HasTag(string tag, bool caseSensitive = false)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            foreach (var t in Tags)
            {
                if (t.Equals(tag, comparison))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Add a tag if not already present
        /// </summary>
        public bool AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || HasTag(tag))
                return false;

            Tags.Add(tag.Trim());
            ModifiedDate = DateTime.Now;
            return true;
        }

        /// <summary>
        /// Remove a tag
        /// </summary>
        public bool RemoveTag(string tag)
        {
            var removed = Tags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
                ModifiedDate = DateTime.Now;
            return removed;
        }

        public override string ToString()
        {
            var baseString = base.ToString(); // Position + Description
            if (!string.IsNullOrEmpty(Category) && Category != "Default")
                return $"[{Category}] {baseString}";
            return baseString;
        }
    }
}
