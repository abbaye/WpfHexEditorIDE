// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Models/ResxDocument.cs
// Description:
//     In-memory representation of a parsed .resx file.
//     Wraps the underlying XDocument and exposes the ordered
//     list of ResxEntry records.  The XDocument is kept
//     so the serializer can preserve the <resheader> block
//     and any XML comments that precede data entries.
// ==========================================================

using System.Xml.Linq;

namespace WpfHexEditor.Editor.ResxEditor.Models;

/// <summary>
/// Parsed representation of a <c>.resx</c> / <c>.resw</c> file.
/// </summary>
public sealed class ResxDocument
{
    /// <summary>Absolute path of the file on disk (empty string for new, unsaved documents).</summary>
    public string FilePath { get; }

    /// <summary>Ordered list of resource entries.</summary>
    public IReadOnlyList<ResxEntry> Entries { get; }

    /// <summary>
    /// The underlying <see cref="XDocument"/> loaded with
    /// <see cref="System.Xml.Linq.LoadOptions.PreserveWhitespace"/>.
    /// Used by the serializer to round-trip the header and comments.
    /// </summary>
    public XDocument InternalDoc { get; }

    public ResxDocument(string filePath, IReadOnlyList<ResxEntry> entries, XDocument internalDoc)
    {
        FilePath    = filePath;
        Entries     = entries;
        InternalDoc = internalDoc;
    }

    /// <summary>Creates an empty document (new, unsaved resource file).</summary>
    public static ResxDocument CreateEmpty(string filePath = "")
    {
        var xdoc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("root",
                new XElement("resheader", new XAttribute("name", "resmimetype"),
                    new XElement("value", "text/microsoft-resx")),
                new XElement("resheader", new XAttribute("name", "version"),
                    new XElement("value", "2.0")),
                new XElement("resheader", new XAttribute("name", "reader"),
                    new XElement("value", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")),
                new XElement("resheader", new XAttribute("name", "writer"),
                    new XElement("value", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"))));
        return new ResxDocument(filePath, [], xdoc);
    }
}
