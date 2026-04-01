//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.Json.Serialization;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Serialisable snapshot of a single bookmark.
/// Stored in the .whproj alongside each project item so bookmarks
/// survive across sessions.
/// </summary>
public sealed class BookmarkDto
{
    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("length")]
    public long Length { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    /// <summary>
    /// Name of the bookmark group this entry belongs to.
    /// Matches the <c>GroupName</c> of a <c>BookmarkGroup</c> in <c>BookmarkGroupsPanel</c>.
    /// <see langword="null"/> means the default/ungrouped category.
    /// </summary>
    [JsonPropertyName("group")]
    public string? Group { get; set; }
}
