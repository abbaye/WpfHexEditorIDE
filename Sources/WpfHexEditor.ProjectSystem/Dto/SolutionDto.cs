//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.Json.Serialization;
using WpfHexEditor.ProjectSystem.Serialization.Migration;

namespace WpfHexEditor.ProjectSystem.Dto;

/// <summary>
/// Serialisation root for a .whsln file.
/// </summary>
internal sealed class SolutionDto
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = MigrationPipeline.CurrentVersion;

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
    /// Legacy dock-layout field from v1. Never written by v2+.
    /// Kept for reading old files so the v1→v2 migrator can capture the value
    /// and write it to the .whsln.user sidecar.
    /// </summary>
    [JsonPropertyName("dockLayout")]
    public System.Text.Json.JsonElement? DockLayout { get; set; }

    /// <summary>
    /// Not persisted. Set by <see cref="V1ToV2Migrator"/> when migrating a v1 file
    /// so <see cref="SolutionSerializer"/> can write the layout to
    /// the .whsln.user sidecar on first save.
    /// </summary>
    [JsonIgnore]
    public string? MigratedDockLayout { get; set; }
}

internal sealed class SolutionProjectRefDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Path relative to the .whsln directory.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";
}
