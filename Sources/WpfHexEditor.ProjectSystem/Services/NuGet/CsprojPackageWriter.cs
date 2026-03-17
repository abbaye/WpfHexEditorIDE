// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Services/NuGet/CsprojPackageWriter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Provides .csproj XML write-back for NuGet PackageReference elements.
//     Uses LINQ-to-XML (XDocument) to preserve all existing project content
//     while safely adding, updating, or removing package references.
//
// Architecture Notes:
//     Pattern: Static utility class (pure functions, no state)
//     All methods operate on the file path; callers are responsible for
//     triggering project reload after a write operation.
// ==========================================================

using System.IO;
using System.Xml.Linq;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Services.NuGet;

/// <summary>
/// Reads and writes <c>&lt;PackageReference&gt;</c> elements in SDK-style
/// and legacy .csproj files without disturbing any other XML content.
/// </summary>
public static class CsprojPackageWriter
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds or updates a <c>&lt;PackageReference Include="{id}" Version="{version}"/&gt;</c>
    /// element in <paramref name="csprojPath"/>.
    /// If an entry with the same Id already exists (case-insensitive) its Version is updated.
    /// Otherwise the entry is appended to the first <c>&lt;ItemGroup&gt;</c> that contains
    /// PackageReferences, or to a new <c>&lt;ItemGroup&gt;</c> before the closing tag.
    /// </summary>
    public static void AddOrUpdatePackageReference(string csprojPath, string packageId, string version)
    {
        var doc = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        var ns  = doc.Root?.Name.Namespace ?? XNamespace.None;

        var existing = FindPackageReferenceElement(doc, ns, packageId);
        if (existing is not null)
        {
            existing.SetAttributeValue("Version", version);
        }
        else
        {
            var newRef    = new XElement(ns + "PackageReference",
                                new XAttribute("Include", packageId),
                                new XAttribute("Version", version));
            var itemGroup = FindOrCreatePackageItemGroup(doc, ns);
            itemGroup.Add(new XText("\n    "), newRef, new XText("\n  "));
        }

        doc.Save(csprojPath, SaveOptions.None);
    }

    /// <summary>
    /// Removes the <c>&lt;PackageReference&gt;</c> element for <paramref name="packageId"/>
    /// from <paramref name="csprojPath"/>. If the parent <c>&lt;ItemGroup&gt;</c> becomes
    /// empty after removal it is also removed.
    /// </summary>
    public static void RemovePackageReference(string csprojPath, string packageId)
    {
        var doc = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        var ns  = doc.Root?.Name.Namespace ?? XNamespace.None;

        var element = FindPackageReferenceElement(doc, ns, packageId);
        if (element is null) return;

        var parent = element.Parent;
        element.Remove();

        // Remove the parent ItemGroup if it has no element children left.
        if (parent is not null && !parent.Elements().Any())
            parent.Remove();

        doc.Save(csprojPath, SaveOptions.None);
    }

    /// <summary>
    /// Returns all current <c>&lt;PackageReference&gt;</c> entries in <paramref name="csprojPath"/>
    /// as <see cref="PackageReferenceInfo"/> records.
    /// Returns an empty list if the file cannot be parsed.
    /// </summary>
    public static IReadOnlyList<PackageReferenceInfo> GetPackageReferences(string csprojPath)
    {
        if (!File.Exists(csprojPath)) return [];

        try
        {
            var doc = XDocument.Load(csprojPath);
            var ns  = doc.Root?.Name.Namespace ?? XNamespace.None;

            return doc.Descendants(ns + "PackageReference")
                      .Select(e => new PackageReferenceInfo(
                          e.Attribute("Include")?.Value ?? string.Empty,
                          e.Attribute("Version")?.Value))
                      .Where(r => !string.IsNullOrWhiteSpace(r.Id))
                      .ToList();
        }
        catch
        {
            return [];
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static XElement? FindPackageReferenceElement(XDocument doc, XNamespace ns, string packageId)
        => doc.Descendants(ns + "PackageReference")
              .FirstOrDefault(e => string.Equals(
                  e.Attribute("Include")?.Value,
                  packageId,
                  StringComparison.OrdinalIgnoreCase));

    private static XElement FindOrCreatePackageItemGroup(XDocument doc, XNamespace ns)
    {
        // Prefer an existing ItemGroup that already contains PackageReferences.
        var existing = doc.Root?.Elements(ns + "ItemGroup")
                           .FirstOrDefault(g => g.Elements(ns + "PackageReference").Any());
        if (existing is not null) return existing;

        // No PackageReference group found — create a new one at the end of the Project element.
        var newGroup = new XElement(ns + "ItemGroup");
        doc.Root?.Add(new XText("\n  "), newGroup, new XText("\n"));
        return newGroup;
    }
}
