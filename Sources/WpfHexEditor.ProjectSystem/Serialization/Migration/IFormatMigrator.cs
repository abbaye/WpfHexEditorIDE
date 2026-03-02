//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.ProjectSystem.Dto;

namespace WpfHexEditor.ProjectSystem.Serialization.Migration;

/// <summary>
/// Upgrades a DTO from one format version to the next.
/// Each migrator handles exactly one version step (N → N+1).
/// </summary>
internal interface IFormatMigrator
{
    /// <summary>
    /// The format version this migrator reads from.
    /// </summary>
    int FromVersion { get; }

    /// <summary>
    /// The format version this migrator produces (always <see cref="FromVersion"/> + 1).
    /// </summary>
    int ToVersion { get; }

    /// <summary>
    /// Upgrades <paramref name="dto"/> in-place.
    /// </summary>
    void MigrateSolution(SolutionDto dto);

    /// <summary>
    /// Upgrades <paramref name="dto"/> in-place.
    /// </summary>
    void MigrateProject(ProjectDto dto);
}
