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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CsprojPropertyWriter] read failed: {projectPath} — {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sets or removes a project-level property in the first unconditional
    /// <c>&lt;PropertyGroup&gt;</c>. A null/empty value removes the element.
    /// </summary>
    public static void SetProjectProperty(string projectPath, string propertyName, string? value)
        => SetProjectProperties(projectPath, new Dictionary<string, string?> { [propertyName] = value });

    /// <summary>
    /// Batch-sets multiple project-level properties in a single load/save cycle.
    /// Null/empty values remove the corresponding element.
    /// </summary>
    public static void SetProjectProperties(string projectPath, IReadOnlyDictionary<string, string?> properties)
    {
        if (properties is null || properties.Count == 0) return;
        try
        {
            var doc = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
            var ns  = doc.Root?.Name.Namespace ?? XNamespace.None;

            var pg = FindFirstUnconditionalPropertyGroup(doc, ns)
                     ?? CreatePropertyGroup(doc, ns);

            foreach (var (name, value) in properties)
                UpdatePropertyElement(pg, ns + name, value);

            doc.Save(projectPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CsprojPropertyWriter] write failed: {projectPath} — {ex.Message}");
        }
    }

    private static void UpdatePropertyElement(XElement parent, XName name, string? value)
    {
        var el = parent.Element(name);
        if (string.IsNullOrEmpty(value))  el?.Remove();
        else if (el is null)               parent.Add(new XElement(name, value));
        else                               el.Value = value;
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
