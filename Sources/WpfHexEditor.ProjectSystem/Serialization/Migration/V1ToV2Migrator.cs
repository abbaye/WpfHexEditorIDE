//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.ProjectSystem.Dto;

namespace WpfHexEditor.ProjectSystem.Serialization.Migration;

/// <summary>
/// Migrates .whsln / .whproj files from format version 1 to version 2.
/// <list type="bullet">
///   <item><b>Solution:</b> captures the legacy <c>dockLayout</c> JSON so the caller
///     can migrate it to the sidecar <c>.whsln.user</c> file.</item>
///   <item><b>Project:</b> converts the single <c>targetItemId</c> reference into the
///     richer <c>linkedItems</c> array with role <c>"Tbl"</c>.</item>
/// </list>
/// </summary>
internal sealed class V1ToV2Migrator : IFormatMigrator
{
    public int FromVersion => 1;
    public int ToVersion   => 2;

    public void MigrateSolution(SolutionDto dto)
    {
        // Preserve the legacy dockLayout so SolutionSerializer can write it
        // to the .whsln.user sidecar on first save under v2.
        if (dto.DockLayout.HasValue &&
            dto.DockLayout.Value.ValueKind != System.Text.Json.JsonValueKind.Null)
        {
            dto.MigratedDockLayout = dto.DockLayout.Value.GetRawText();
        }
        dto.DockLayout = null;
    }

    public void MigrateProject(ProjectDto dto)
    {
        foreach (var item in dto.Items)
        {
            // Promote the single targetItemId to the new linkedItems list.
            if (item.TargetItemId is { Length: > 0 } tid &&
                (item.LinkedItems is null || item.LinkedItems.Count == 0))
            {
                item.LinkedItems = [new ItemLinkDto { ItemId = tid, Role = "Tbl" }];
            }
            item.TargetItemId = null;
        }
    }
}
