// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Models/ResxEntry.cs
// Description:
//     Immutable domain record for a single <data> element
//     inside a .resx file.  Preserves all XML attributes
//     to enable lossless round-trip serialization.
// ==========================================================

namespace WpfHexEditor.Editor.ResxEditor.Models;

/// <summary>
/// Immutable snapshot of one &lt;data&gt; element from a .resx file.
/// </summary>
/// <param name="Name">The <c>name</c> attribute — the resource key.</param>
/// <param name="Value">Content of the nested &lt;value&gt; element.</param>
/// <param name="Comment">Content of the optional &lt;comment&gt; element.</param>
/// <param name="TypeName">The <c>type</c> attribute (may be null for plain strings).</param>
/// <param name="MimeType">The <c>mimetype</c> attribute (may be null).</param>
/// <param name="Space">The <c>xml:space</c> attribute (typically "preserve" for strings).</param>
public sealed record ResxEntry(
    string  Name,
    string  Value,
    string  Comment,
    string? TypeName,
    string? MimeType,
    string? Space)
{
    /// <summary>Inferred high-level type based on <see cref="TypeName"/> and <see cref="MimeType"/>.</summary>
    public ResxEntryType EntryType => InferType(TypeName, MimeType);

    private static ResxEntryType InferType(string? typeName, string? mimeType)
    {
        if (typeName is null && mimeType is null)
            return ResxEntryType.String;

        if (typeName is not null)
        {
            if (typeName.Contains("ResXFileRef",   StringComparison.OrdinalIgnoreCase)) return ResxEntryType.FileRef;
            if (typeName.Contains("Bitmap",        StringComparison.OrdinalIgnoreCase)) return ResxEntryType.Image;
            if (typeName.Contains("Icon",          StringComparison.OrdinalIgnoreCase)) return ResxEntryType.Image;
            if (typeName.Contains("System.Byte[]", StringComparison.OrdinalIgnoreCase)) return ResxEntryType.Binary;
            if (typeName.Contains("System.String", StringComparison.OrdinalIgnoreCase)) return ResxEntryType.String;
        }

        if (mimeType?.Contains("image", StringComparison.OrdinalIgnoreCase) == true)
            return ResxEntryType.Image;

        return typeName is null ? ResxEntryType.String : ResxEntryType.Other;
    }

    /// <summary>Returns a copy with the specified value changed.</summary>
    public ResxEntry WithValue(string newValue)   => this with { Value   = newValue };

    /// <summary>Returns a copy with the specified comment changed.</summary>
    public ResxEntry WithComment(string newComment) => this with { Comment = newComment };

    /// <summary>Returns a copy with the specified name changed.</summary>
    public ResxEntry WithName(string newName)     => this with { Name    = newName };
}
