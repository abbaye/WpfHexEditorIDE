//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.Json.Serialization;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.ProjectSystem.Serialization.Migration;

namespace WpfHexEditor.ProjectSystem.Dto;

/// <summary>
/// Serialisation root for a .whproj file.
/// </summary>
internal sealed class ProjectDto
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = MigrationPipeline.CurrentVersion;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("items")]
    public List<ProjectItemDto> Items { get; set; } = [];

    [JsonPropertyName("virtualFolders")]
    public List<VirtualFolderDto> VirtualFolders { get; set; } = [];

    /// <summary>
    /// Id of the default TBL item for this project, or null if none.
    /// </summary>
    [JsonPropertyName("defaultTblItemId")]
    public string? DefaultTblItemId { get; set; }

    /// <summary>
    /// VS-style project type id set by the template at creation time.
    /// </summary>
    [JsonPropertyName("projectType")]
    public string? ProjectType { get; set; }
}

internal sealed class ProjectItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProjectItemType Type { get; set; } = ProjectItemType.Binary;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Path relative to the .whproj file directory.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("editorConfig")]
    public EditorConfigDto? EditorConfig { get; set; }

    /// <summary>
    /// Bookmarks saved for this item. Uses the public <see cref="BookmarkDto"/> from Editor.Core.
    /// </summary>
    [JsonPropertyName("bookmarks")]
    public List<BookmarkDto>? Bookmarks { get; set; }

    /// <summary>
    /// Base-64 encoded in-memory modifications not yet written to the file.
    /// </summary>
    [JsonPropertyName("unsavedModifications")]
    public string? UnsavedModifications { get; set; }

    /// <summary>
    /// Legacy single-target reference (v1). Kept for v1→v2 migration; always null on write.
    /// </summary>
    [JsonPropertyName("targetItemId")]
    public string? TargetItemId { get; set; }

    /// <summary>
    /// Typed links to other project items (replaces the legacy <c>targetItemId</c>).
    /// </summary>
    [JsonPropertyName("linkedItems")]
    public List<ItemLinkDto>? LinkedItems { get; set; }
}

/// <summary>
/// A typed link from one project item to another.
/// </summary>
internal sealed class ItemLinkDto
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = "";

    /// <summary>
    /// Free-form role. Well-known values: <c>"Tbl"</c>, <c>"TblAlternate"</c>,
    /// <c>"Patch"</c>, <c>"FormatDefinition"</c>, <c>"Reference"</c>.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
}

internal sealed class VirtualFolderDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("itemIds")]
    public List<string> ItemIds { get; set; } = [];

    [JsonPropertyName("children")]
    public List<VirtualFolderDto> Children { get; set; } = [];

    /// <summary>
    /// Relative path (from .whproj dir) of the physical directory, or null if purely virtual.
    /// </summary>
    [JsonPropertyName("physicalPath")]
    public string? PhysicalRelativePath { get; set; }
}
