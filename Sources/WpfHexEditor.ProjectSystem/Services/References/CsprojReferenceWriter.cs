// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Services/References/CsprojReferenceWriter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-24
// Description:
//     Provides .csproj XML write-back for ProjectReference and Reference (assembly)
//     elements. Uses LINQ-to-XML (XDocument) to preserve all existing project content
//     while safely adding or removing references.
//
// Architecture Notes:
//     Pattern: Static utility class (pure functions, no state) — mirrors CsprojPackageWriter.
//     Callers are responsible for triggering project reload after a write operation.
// ==========================================================

using System.IO;
using System.Xml.Linq;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Services.References;

/// <summary>
/// Reads and writes <c>&lt;ProjectReference&gt;</c> and <c>&lt;Reference&gt;</c> elements
/// in SDK-style and legacy .csproj files without disturbing any other XML content.
/// </summary>
public static class CsprojReferenceWriter
{
    // ── Project references ────────────────────────────────────────────────────

    /// <summary>
    /// Adds a <c>&lt;ProjectReference Include="{relativePath}"/&gt;</c> element.
    /// If an entry for the same path already exists (case-insensitive) the call is a no-op.
    /// </summary>
    public static void AddProjectReference(string csprojPath, string referencedCsprojPath)
    {
        var doc      = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        var ns       = doc.Root?.Name.Namespace ?? XNamespace.None;
        var relative = MakeRelativePath(csprojPath, referencedCsprojPath);

        if (FindProjectReferenceElement(doc, ns, relative) is not null) return;

        var newRef    = new XElement(ns + "ProjectReference", new XAttribute("Include", relative));
        var itemGroup = FindOrCreateItemGroup(doc, ns, ns + "ProjectReference");
        itemGroup.Add(new XText("\n    "), newRef, new XText("\n  "));
        doc.Save(csprojPath, SaveOptions.None);
    }

    /// <summary>
    /// Removes the <c>&lt;ProjectReference&gt;</c> element for the given path.
    /// The parent <c>&lt;ItemGroup&gt;</c> is also removed if it becomes empty.
    /// </summary>
    public static void RemoveProjectReference(string csprojPath, string referencedCsprojPath)
    {
        var doc      = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        var ns       = doc.Root?.Name.Namespace ?? XNamespace.None;
        var relative = MakeRelativePath(csprojPath, referencedCsprojPath);

        var element = FindProjectReferenceElement(doc, ns, relative);
        if (element is null) return;

        RemoveElementAndEmptyParent(element);
        doc.Save(csprojPath, SaveOptions.None);
    }

    // ── Assembly references ───────────────────────────────────────────────────

    /// <summary>
    /// Adds a <c>&lt;Reference Include="{name}"&gt;&lt;HintPath&gt;{hintPath}&lt;/HintPath&gt;&lt;/Reference&gt;</c> element.
    /// If an entry for the same assembly name already exists the call is a no-op.
    /// </summary>
    public static void AddAssemblyReference(string csprojPath, string assemblyName, string? hintPath)
    {
        var doc = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        var ns  = doc.Root?.Name.Namespace ?? XNamespace.None;

        if (FindAssemblyReferenceElement(doc, ns, assemblyName) is not null) return;

        var newRef = new XElement(ns + "Reference", new XAttribute("Include", assemblyName));
        if (!string.IsNullOrEmpty(hintPath))
            newRef.Add(new XElement(ns + "HintPath", hintPath));

        var itemGroup = FindOrCreateItemGroup(doc, ns, ns + "Reference");
        itemGroup.Add(new XText("\n    "), newRef, new XText("\n  "));
        doc.Save(csprojPath, SaveOptions.None);
    }

    /// <summary>
    /// Removes the <c>&lt;Reference&gt;</c> element for the given assembly name.
    /// The parent <c>&lt;ItemGroup&gt;</c> is also removed if it becomes empty.
    /// </summary>
    public static void RemoveAssemblyReference(string csprojPath, string assemblyName)
    {
        var doc     = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        var ns      = doc.Root?.Name.Namespace ?? XNamespace.None;
        var element = FindAssemblyReferenceElement(doc, ns, assemblyName);
        if (element is null) return;

        RemoveElementAndEmptyParent(element);
        doc.Save(csprojPath, SaveOptions.None);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static XElement? FindProjectReferenceElement(XDocument doc, XNamespace ns, string relativePath)
        => doc.Descendants(ns + "ProjectReference")
              .FirstOrDefault(e => string.Equals(
                  e.Attribute("Include")?.Value, relativePath, StringComparison.OrdinalIgnoreCase));

    private static XElement? FindAssemblyReferenceElement(XDocument doc, XNamespace ns, string name)
        => doc.Descendants(ns + "Reference")
              .FirstOrDefault(e => string.Equals(
                  e.Attribute("Include")?.Value?.Split(',')[0].Trim(),
                  name, StringComparison.OrdinalIgnoreCase));

    private static XElement FindOrCreateItemGroup(XDocument doc, XNamespace ns, XName childName)
    {
        var existing = doc.Root?.Elements(ns + "ItemGroup")
                           .FirstOrDefault(g => g.Elements(childName).Any());
        if (existing is not null) return existing;

        var newGroup = new XElement(ns + "ItemGroup");
        doc.Root?.Add(new XText("\n  "), newGroup, new XText("\n"));
        return newGroup;
    }

    private static void RemoveElementAndEmptyParent(XElement element)
    {
        var parent = element.Parent;
        element.Remove();
        if (parent is not null && !parent.Elements().Any())
            parent.Remove();
    }

    private static string MakeRelativePath(string fromFile, string toFile)
    {
        var fromDir = Path.GetDirectoryName(fromFile) ?? string.Empty;
        return Path.GetRelativePath(fromDir, toFile).Replace('/', Path.DirectorySeparatorChar);
    }
}
