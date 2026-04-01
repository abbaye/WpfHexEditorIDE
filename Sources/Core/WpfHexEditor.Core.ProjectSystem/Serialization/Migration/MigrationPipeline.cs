//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Core.ProjectSystem.Dto;

namespace WpfHexEditor.Core.ProjectSystem.Serialization.Migration;

/// <summary>
/// Runs a chain of <see cref="IFormatMigrator"/> instances to upgrade
/// a DTO from its persisted version to <see cref="CurrentVersion"/>.
/// <para>
/// To add a new format version: implement <see cref="IFormatMigrator"/> for vN→vN+1,
/// register it in <see cref="_migrators"/>, and bump <see cref="CurrentVersion"/>.
/// </para>
/// </summary>
internal static class MigrationPipeline
{
    /// <summary>
    /// The format version written by this build of the application.
    /// </summary>
    public const int CurrentVersion = 2;

    private static readonly IFormatMigrator[] _migrators =
    [
        new V1ToV2Migrator(),
        // future: new V2ToV3Migrator(),
    ];

    // -- Public API ----------------------------------------------------------

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="fileVersion"/> is older than
    /// <see cref="CurrentVersion"/> and migration is required before saving.
    /// </summary>
    public static bool NeedsMigration(int fileVersion) => fileVersion < CurrentVersion;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="fileVersion"/> is newer than
    /// the application supports. Such files can still be opened (unknown fields are
    /// silently ignored by System.Text.Json) but should not be written back.
    /// </summary>
    public static bool IsNewerThanSupported(int fileVersion) => fileVersion > CurrentVersion;

    /// <summary>
    /// Upgrades <paramref name="dto"/> in-place from its current version to
    /// <see cref="CurrentVersion"/>. Updates <c>dto.Version</c> on each step.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// Thrown when a required migrator is not registered (gap in the chain).
    /// </exception>
    public static void UpgradeSolution(SolutionDto dto)
    {
        while (dto.Version < CurrentVersion)
        {
            var migrator = FindMigrator(dto.Version)
                ?? throw new InvalidDataException(
                    $"No migrator registered for .whsln version {dto.Version} → {dto.Version + 1}.");

            migrator.MigrateSolution(dto);
            dto.Version = migrator.ToVersion;
        }
    }

    /// <summary>
    /// Upgrades <paramref name="dto"/> in-place from its current version to
    /// <see cref="CurrentVersion"/>. Updates <c>dto.Version</c> on each step.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// Thrown when a required migrator is not registered (gap in the chain).
    /// </exception>
    public static void UpgradeProject(ProjectDto dto)
    {
        while (dto.Version < CurrentVersion)
        {
            var migrator = FindMigrator(dto.Version)
                ?? throw new InvalidDataException(
                    $"No migrator registered for .whproj version {dto.Version} → {dto.Version + 1}.");

            migrator.MigrateProject(dto);
            dto.Version = migrator.ToVersion;
        }
    }

    /// <summary>
    /// Creates a versioned backup of <paramref name="filePath"/> before an upgrade.
    /// The backup is named <c>{file}.v{fromVersion}.bak</c> and overwrites any previous
    /// backup for the same version.
    /// </summary>
    public static void CreateBackup(string filePath, int fromVersion)
    {
        var backupPath = $"{filePath}.v{fromVersion}.bak";
        File.Copy(filePath, backupPath, overwrite: true);
    }

    // -- Helpers -------------------------------------------------------------

    private static IFormatMigrator? FindMigrator(int fromVersion)
        => Array.Find(_migrators, m => m.FromVersion == fromVersion);
}
