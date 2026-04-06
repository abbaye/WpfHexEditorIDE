// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: Templates/SlnxSolutionHelper.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-05
// Description:
//     Static helpers for creating and modifying .slnx solution files.
//     Used by DotNetProjectTemplate when the user prefers the XML format.
//
// Architecture Notes:
//     Uses System.Xml.Linq (XDocument) for clean XML manipulation.
// ==========================================================

using System.Xml.Linq;

namespace WpfHexEditor.Plugins.SolutionLoader.VS.Templates;

/// <summary>
/// Creates and modifies Visual Studio <c>.slnx</c> (XML-based) solution files.
/// </summary>
internal static class SlnxSolutionHelper
{
    /// <summary>
    /// Builds a minimal .slnx XML string containing a single project.
    /// </summary>
    public static string BuildSlnx(string projectName, string projectFileExtension)
    {
        var doc = new XDocument(
            new XElement("Solution",
                new XElement("Project",
                    new XAttribute("Path", $"{projectName}\\{projectName}{projectFileExtension}")),
                new XElement("Properties",
                    new XElement("Property",
                        new XAttribute("Name", "ActiveConfiguration"),
                        new XAttribute("Value", "Debug|Any CPU")))));

        return doc.ToString();
    }

    /// <summary>
    /// Adds a project reference to an existing .slnx file.
    /// </summary>
    public static async Task PatchSlnxAsync(
        string slnxPath, string projectRelPath, CancellationToken ct = default)
    {
        var doc = XDocument.Parse(await File.ReadAllTextAsync(slnxPath, ct));
        var root = doc.Root!;

        // Add before <Properties> if it exists, otherwise at the end.
        var properties = root.Element("Properties");
        var projectElement = new XElement("Project",
            new XAttribute("Path", projectRelPath.Replace('/', '\\')));

        if (properties is not null)
            properties.AddBeforeSelf(projectElement);
        else
            root.Add(projectElement);

        await File.WriteAllTextAsync(slnxPath, doc.ToString(), ct);
    }

    /// <summary>
    /// Removes a project from an existing .slnx file by its relative path.
    /// </summary>
    public static async Task RemoveProjectFromSlnxAsync(
        string slnxPath, string projectRelPath, CancellationToken ct = default)
    {
        var doc = XDocument.Parse(await File.ReadAllTextAsync(slnxPath, ct));
        var normalizedTarget = projectRelPath.Replace('/', '\\').TrimStart('\\');

        // Remove matching <Project> elements at any depth.
        var toRemove = doc.Descendants("Project")
            .Where(e =>
            {
                var path = e.Attribute("Path")?.Value?.Replace('/', '\\').TrimStart('\\');
                return path is not null &&
                       path.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        foreach (var element in toRemove)
            element.Remove();

        await File.WriteAllTextAsync(slnxPath, doc.ToString(), ct);
    }
}
