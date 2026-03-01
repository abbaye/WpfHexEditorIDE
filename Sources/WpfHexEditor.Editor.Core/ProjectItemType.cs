//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Classifies a file item inside a WpfHexEditor project.
/// </summary>
public enum ProjectItemType
{
    Unknown,

    /// <summary>
    /// Binary file to analyse (.bin, .rom, .exe, …).
    /// </summary>
    Binary,

    /// <summary>
    /// WpfHexEditor format-definition file (.whjson).
    /// </summary>
    FormatDefinition,

    /// <summary>
    /// IPS / BPS patch file.
    /// </summary>
    Patch,

    /// <summary>
    /// Character table file (.tbl, .tblx).
    /// </summary>
    Tbl,

    /// <summary>
    /// Generic JSON file (.json).
    /// </summary>
    Json,

    /// <summary>
    /// Plain text file.
    /// </summary>
    Text,

    /// <summary>
    /// Script or language-definition file edited by the text editor (.lua, .py, .asm, .whlang, …).
    /// </summary>
    Script,

    /// <summary>
    /// Image file (PNG, BMP, JPG, GIF, ICO, …) opened by the ImageViewer.
    /// </summary>
    Image,

    /// <summary>
    /// A saved file-comparison pair referencing two <see cref="Binary"/> items.
    /// Not a physical file on disk — represents a named comparison configuration.
    /// </summary>
    Comparison,
}
