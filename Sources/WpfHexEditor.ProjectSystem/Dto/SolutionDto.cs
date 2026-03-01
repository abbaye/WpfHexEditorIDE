//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.Json.Serialization;

namespace WpfHexEditor.ProjectSystem.Dto;

/// <summary>Serialisation root for a .whsln file.</summary>
internal sealed class SolutionDto
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("modified")]
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("projects")]
    public List<SolutionProjectRefDto> Projects { get; set; } = [];

    [JsonPropertyName("startupProject")]
    public string? StartupProject { get; set; }

    /// <summary>
    /// Serialised DockLayoutRootDto (as a raw JSON element so we can embed
    /// the Docking.Core serialiser output without a hard schema dependency).
    /// </summary>
    [JsonPropertyName("dockLayout")]
    public System.Text.Json.JsonElement? DockLayout { get; set; }
}

internal sealed class SolutionProjectRefDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Path relative to the .whsln directory.</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";
}
