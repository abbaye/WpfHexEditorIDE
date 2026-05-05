// ==========================================================
// Project: WpfHexEditor.Core.ProjectSystem
// File: Utilities/ProjectFileXmlUtils.cs
// Description:
//     Shared XML and path helpers used by project-file readers/writers
//     (CsprojReferenceWriter, VBItemGroupPropertyProvider, etc.).
// Architecture: Static utility — pure functions, no state.
// ==========================================================

using System.IO;
using System.Xml.Linq;

namespace WpfHexEditor.Core.ProjectSystem.Utilities;

internal static class ProjectFileXmlUtils
{
    /// <summary>Returns the path of <paramref name="absoluteFilePath"/> relative to the directory of <paramref name="projectFilePath"/>, using the OS separator.</summary>
    internal static string GetRelativePath(string projectFilePath, string absoluteFilePath)
    {
        var projDir = Path.GetDirectoryName(projectFilePath) ?? "";
        return Path.GetRelativePath(projDir, absoluteFilePath)
                   .Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>Normalises a path for case-insensitive comparison (forward-slash → backslash, no leading slash).</summary>
    internal static string NormPath(string? p) =>
        p?.Replace('/', '\\').TrimStart('\\') ?? "";

    /// <summary>
    /// Sets or removes a child element on <paramref name="parent"/>.
    /// Removes the element when <paramref name="value"/> is null or empty; creates or updates it otherwise.
    /// </summary>
    internal static void SetOrRemove(XElement parent, XName name, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            parent.Element(name)?.Remove();
        }
        else
        {
            var el = parent.Element(name);
            if (el is null) parent.Add(new XElement(name, value));
            else el.Value = value;
        }
    }

    /// <summary>Finds the first descendant item element whose <c>Include</c> attribute matches <paramref name="relative"/> (case-insensitive, normalised).</summary>
    internal static XElement? FindItemByInclude(XDocument doc, XNamespace ns, string relative) =>
        doc.Descendants(ns + "ItemGroup")
           .SelectMany(g => g.Elements())
           .FirstOrDefault(e => string.Equals(
               NormPath(e.Attribute("Include")?.Value),
               NormPath(relative),
               StringComparison.OrdinalIgnoreCase));
}
