//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Windows.Media;

namespace WpfHexaEditor.Models.Bookmarks
{
    /// <summary>
    /// Represents a bookmark category/group with color and description
    /// </summary>
    [Serializable]
    public class BookmarkGroup
    {
        /// <summary>
        /// Unique name of the category
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Display color for bookmarks in this category
        /// </summary>
        public Color Color { get; set; } = Colors.Blue;

        /// <summary>
        /// Description of this category
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Icon character (optional, for future UI enhancement)
        /// </summary>
        public string Icon { get; set; } = "\uE8A4"; // Default bookmark icon

        /// <summary>
        /// Whether this category is visible
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Sort order for display
        /// </summary>
        public int SortOrder { get; set; } = 0;

        public BookmarkGroup()
        {
        }

        public BookmarkGroup(string name, Color color)
        {
            Name = name;
            Color = color;
        }

        public override string ToString() => Name;

        public override bool Equals(object obj)
        {
            return obj is BookmarkGroup other && Name == other.Name;
        }

        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? 0;
        }
    }
}
