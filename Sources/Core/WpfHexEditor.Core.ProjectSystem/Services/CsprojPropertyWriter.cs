// ==========================================================
// Project: WpfHexEditor.Core.ProjectSystem
// File: Services/CsprojPropertyWriter.cs
// Description:
//     Reads and writes project-level <PropertyGroup> elements in .csproj /
//     .vbproj files (TargetFramework, AssemblyName, RootNamespace, OutputType,
//     LangVersion, Nullable, etc.). Mirrors the pattern of
//     CsprojReferenceWriter and CsprojPackageWriter.
// Architecture: Static utility — pure functions, no state.
// ==========================================================

using System.Xml.Linq;

namespace WpfHexEditor.Core.ProjectSystem.Services;

/// <summary>
/// Read/write helpers for project-level MSBuild properties stored in the
/// first unconditional <c>&lt;PropertyGroup&gt;</c> of a project file.
/// </summary>
public static class CsprojPropertyWriter
{
    /// <summary>
    /// Returns the value of a project-level property, or <c>null</c> if absent.
    /// </summary>
    public static string? GetProjectProperty(string projectPath, string propertyName)
    {
        try
        {
            var doc = XDocument.Load(projectPath);
            var ns  = doc.Root?.Name.Namespace ?? XNamespace.None;
            return FindFirstUnconditionalPropertyGroup(doc, ns)?
                   .Element(ns + propertyName)?.Value;
        }
        catch { return null; }
    }

    /// <summary>
    /// Sets or removes a project-level property in the first unconditional
    /// <c>&lt;PropertyGroup&gt;</c>. A null/empty value removes the element.
    /// </summary>
    public static void SetProjectProperty(string projectPath, string propertyName, string? value)
    {
        try
        {
            var doc = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
            var ns  = doc.Root?.Name.Namespace ?? XNamespace.None;

            var pg = FindFirstUnconditionalPropertyGroup(doc, ns)
                     ?? CreatePropertyGroup(doc, ns);

            var el = pg.Element(ns + propertyName);
            if (string.IsNullOrEmpty(value))
            {
                el?.Remove();
            }
            else if (el is null)
            {
                pg.Add(new XElement(ns + propertyName, value));
            }
            else
            {
                el.Value = value;
            }

            doc.Save(projectPath);
        }
        catch { /* write errors are non-fatal */ }
    }

    private static XElement? FindFirstUnconditionalPropertyGroup(XDocument doc, XNamespace ns) =>
        doc.Root?
           .Elements(ns + "PropertyGroup")
           .FirstOrDefault(pg => pg.Attribute("Condition") is null);

    private static XElement CreatePropertyGroup(XDocument doc, XNamespace ns)
    {
        var pg = new XElement(ns + "PropertyGroup");
        doc.Root?.AddFirst(pg);
        return pg;
    }
}
