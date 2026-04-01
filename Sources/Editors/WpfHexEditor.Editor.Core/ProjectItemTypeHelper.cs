//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
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
            "whfmt"                         => ProjectItemType.FormatDefinition,
            "json"                          => ProjectItemType.Json,
            "tbl" or "tblx"                 => ProjectItemType.Tbl,
            "ips" or "bps" or "ups" or "xdelta" => ProjectItemType.Patch,
            "txt" or "md" or "log"          => ProjectItemType.Text,
            "png" or "bmp" or "jpg" or "jpeg" or "gif"
                or "ico" or "tiff" or "tif" or "webp"
                or "dds" or "tga"           => ProjectItemType.Image,
            "chr" or "til" or "gfx"         => ProjectItemType.Tile,
            "wav" or "mp3" or "ogg" or "flac"
                or "xm" or "mod" or "it" or "s3m" or "aiff" => ProjectItemType.Audio,
            "asm" or "s" or "lua" or "py" or "rb" or "pl"
                or "sh" or "bat" or "ps1" or "whlang"
                or "scr" or "msg" or "evt" or "script" or "dec" => ProjectItemType.Script,
            _                               => ProjectItemType.Binary,
        };
    }
}
