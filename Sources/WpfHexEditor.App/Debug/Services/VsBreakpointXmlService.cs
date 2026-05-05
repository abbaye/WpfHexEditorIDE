// ==========================================================
// Project: WpfHexEditor.App
// File: Debug/Services/VsBreakpointXmlService.cs
// Description:
//     Import/export breakpoints in Visual Studio XML format.
//     VS format: <BreakpointCollection><Breakpoint File="" Line="" .../></BreakpointCollection>
//     Central storage remains .whide/breakpoints.json — this is a convenience tool.
// Architecture:
//     Static utility, no dependencies beyond System.Xml.Linq and SDK records.
// ==========================================================

using System.IO;
using System.Xml.Linq;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Debug.Services;

/// <summary>Imported breakpoint record (subset of <see cref="DebugBreakpointInfo"/>).</summary>
public sealed record ImportedBreakpoint(string FilePath, int Line, string? Condition, bool IsEnabled);

/// <summary>
/// Reads and writes breakpoints in the Visual Studio XML export format.
/// </summary>
internal static class VsBreakpointXmlService
{
    // ── Export ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes breakpoints to a VS-compatible XML file.
    /// </summary>
    public static void ExportToXml(string xmlPath, IEnumerable<DebugBreakpointInfo> breakpoints)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("BreakpointCollection",
                breakpoints.Select(bp => new XElement("Breakpoint",
                    new XAttribute("File", bp.FilePath),
                    new XAttribute("Line", bp.Line),
                    new XAttribute("Condition", bp.Condition ?? string.Empty),
                    new XAttribute("IsEnabled", bp.IsEnabled)))));

        var dir = Path.GetDirectoryName(xmlPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        doc.Save(xmlPath);
    }

    // ── Import ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a VS breakpoint XML file. Skips malformed entries gracefully.
    /// </summary>
    public static IReadOnlyList<ImportedBreakpoint> ImportFromXml(string xmlPath)
    {
        if (!File.Exists(xmlPath)) return [];

        try
        {
            var doc = XDocument.Load(xmlPath);
            var root = doc.Root;
            if (root is null) return [];

            var results = new List<ImportedBreakpoint>();
            foreach (var el in root.Elements("Breakpoint"))
            {
                var file = (string?)el.Attribute("File");
                var lineStr = (string?)el.Attribute("Line");

                if (string.IsNullOrEmpty(file) || !int.TryParse(lineStr, out int line) || line < 1)
                    continue;

                var condition = (string?)el.Attribute("Condition");
                if (string.IsNullOrEmpty(condition)) condition = null;

                var enabledStr = (string?)el.Attribute("IsEnabled");
                bool isEnabled = !string.Equals(enabledStr, "false", StringComparison.OrdinalIgnoreCase);

                results.Add(new ImportedBreakpoint(file, line, condition, isEnabled));
            }

            return results;
        }
        catch
        {
            return [];
        }
    }

    // ── Auto-detect ──────────────────────────────────────────────────────────

    /// <summary>
    /// Looks for a VS-exported breakpoint XML in the <c>.vs/</c> folder hierarchy.
    /// Returns the path if found, <c>null</c> otherwise.
    /// Does NOT parse the binary <c>.suo</c> — only looks for user-exported XML files.
    /// </summary>
    public static string? FindVsBreakpointXml(string solutionDir)
    {
        if (string.IsNullOrEmpty(solutionDir) || !Directory.Exists(solutionDir))
            return null;

        var vsDir = Path.Combine(solutionDir, ".vs");
        if (!Directory.Exists(vsDir)) return null;

        // Search for breakpoint*.xml files in .vs/ subdirectories.
        try
        {
            foreach (var xml in Directory.EnumerateFiles(vsDir, "breakpoint*.xml", SearchOption.AllDirectories))
                return xml;
        }
        catch { /* access denied, etc. */ }

        return null;
    }
}
