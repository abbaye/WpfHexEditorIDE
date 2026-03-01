//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.Json.Serialization;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Dto;

/// <summary>Serialisation root for a .whproj file.</summary>
internal sealed class ProjectDto
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("items")]
    public List<ProjectItemDto> Items { get; set; } = [];

    [JsonPropertyName("virtualFolders")]
    public List<VirtualFolderDto> VirtualFolders { get; set; } = [];
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

    /// <summary>Path relative to the .whproj file directory.</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("editorConfig")]
    public EditorConfigDto? EditorConfig { get; set; }

    [JsonPropertyName("bookmarks")]
    public List<BookmarkDto>? Bookmarks { get; set; }

    /// <summary>Base-64 encoded in-memory modifications not yet written to the file.</summary>
    [JsonPropertyName("unsavedModifications")]
    public string? UnsavedModifications { get; set; }

    [JsonPropertyName("targetItemId")]
    public string? TargetItemId { get; set; }
}

internal sealed class BookmarkDto
{
    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("length")]
    public long Length { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("color")]
    public string? Color { get; set; }
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
}
