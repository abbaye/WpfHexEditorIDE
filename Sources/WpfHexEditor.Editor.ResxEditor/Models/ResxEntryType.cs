// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Models/ResxEntryType.cs
// Description:
//     Discriminated union for the type of a .resx data entry,
//     inferred from the <data> element's type/mimetype attributes.
// ==========================================================

namespace WpfHexEditor.Editor.ResxEditor.Models;

/// <summary>High-level type category for a RESX data entry.</summary>
public enum ResxEntryType
{
    /// <summary>Plain text — no type attribute, or type="System.String".</summary>
    String,

    /// <summary>Embedded image — type contains "Bitmap", "Icon", or mimetype contains "image".</summary>
    Image,

    /// <summary>Raw byte array — type="System.Byte[]".</summary>
    Binary,

    /// <summary>File reference — type contains "ResXFileRef".</summary>
    FileRef,

    /// <summary>Any other typed entry (Color, Font, Point, etc.).</summary>
    Other
}
