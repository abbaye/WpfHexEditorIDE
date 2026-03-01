//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Utility methods for <see cref="ProjectItemType"/>.
/// </summary>
public static class ProjectItemTypeHelper
{
    /// <summary>
    /// Returns the most appropriate <see cref="ProjectItemType"/> for a given
    /// file extension (case-insensitive, with or without leading dot).
    /// </summary>
    public static ProjectItemType FromExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension)) return ProjectItemType.Binary;

        return extension.ToLowerInvariant().TrimStart('.') switch
        {
            "whjson"                        => ProjectItemType.FormatDefinition,
            "json"                          => ProjectItemType.Json,
            "tbl" or "tblx"                 => ProjectItemType.Tbl,
            "ips" or "bps" or "ups" or "xdelta" => ProjectItemType.Patch,
            "txt" or "md" or "log"          => ProjectItemType.Text,
            "png" or "bmp" or "jpg" or "jpeg" or "gif"
                or "ico" or "tiff" or "tif" or "webp"
                or "dds" or "tga"           => ProjectItemType.Image,
            _                               => ProjectItemType.Binary,
        };
    }
}
