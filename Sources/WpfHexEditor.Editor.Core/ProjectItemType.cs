//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>Classifies a file item inside a WpfHexEditor project.</summary>
public enum ProjectItemType
{
    Unknown,

    /// <summary>Binary file to analyse (.bin, .rom, .exe, …).</summary>
    Binary,

    /// <summary>WpfHexEditor format-definition file (.whjson).</summary>
    FormatDefinition,

    /// <summary>IPS / BPS patch file.</summary>
    Patch,

    /// <summary>Character table file (.tbl, .tblx).</summary>
    Tbl,

    /// <summary>Generic JSON file (.json).</summary>
    Json,

    /// <summary>Plain text file.</summary>
    Text,
}
