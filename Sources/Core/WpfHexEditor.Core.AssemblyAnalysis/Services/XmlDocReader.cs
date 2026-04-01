// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/XmlDocReader.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Parses the companion .NET XML documentation file that ships beside
//     many .dll assemblies (e.g. System.Runtime.xml, MyLib.xml).
//     Provides fast O(1) lookup of <summary> text by standard doc-comment ID.
//     BCL-only: uses System.Xml.Linq — no NuGet required.
//
// Architecture Notes:
//     Pattern: Factory (TryLoad) + Read-only value object.
//     The dictionary is built once from the XML file; all lookups are O(1).
//     TryLoad returns null gracefully when no companion file is found or
//     the XML is malformed — callers treat null as "no documentation".
//     Doc-comment IDs follow the ECMA-334 §D.1 specification:
//       T:Namespace.TypeName
//       M:Namespace.TypeName.MethodName
//       F:Namespace.TypeName.FieldName
//       P:Namespace.TypeName.PropertyName
//       E:Namespace.TypeName.EventName
// ==========================================================

using System.IO;
using System.Xml.Linq;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Reads and caches XML documentation summaries from the companion .xml file
/// that ships alongside .NET assemblies.
/// </summary>
public sealed class XmlDocReader
{
    private readonly Dictionary<string, string> _summaries;

    private XmlDocReader(Dictionary<string, string> summaries)
        => _summaries = summaries;

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to load the XML documentation file that corresponds to
    /// <paramref name="assemblyFilePath"/> (same directory, same name, .xml extension).
    /// Returns null when the file does not exist or cannot be parsed.
    /// </summary>
    public static XmlDocReader? TryLoad(string assemblyFilePath)
    {
        if (string.IsNullOrEmpty(assemblyFilePath)) return null;

        var xmlPath = Path.ChangeExtension(assemblyFilePath, ".xml");
        if (!File.Exists(xmlPath)) return null;

        try
        {
            var doc = XDocument.Load(xmlPath, LoadOptions.None);
            var summaries = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var member in doc.Descendants("member"))
            {
                var name = (string?)member.Attribute("name");
                if (string.IsNullOrEmpty(name)) continue;

                var summary = member.Element("summary")?.Value;
                if (string.IsNullOrEmpty(summary)) continue;

                summaries[name] = NormalizeWhitespace(summary);
            }

            return new XmlDocReader(summaries);
        }
        catch
        {
            // Malformed XML or I/O error — treat as "no documentation".
            return null;
        }
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the summary text for the given doc-comment ID, or null if not found.
    /// </summary>
    /// <param name="docId">e.g. "T:System.Collections.List`1" or "M:System.Math.Abs"</param>
    public string? GetSummary(string docId)
        => _summaries.TryGetValue(docId, out var s) ? s : null;

    // ── Doc-ID builders (called by AssemblyAnalysisEngine) ───────────────────

    /// <summary>Builds the standard doc-comment ID for a type.</summary>
    public static string TypeDocId(string ns, string name)
        => string.IsNullOrEmpty(ns) ? $"T:{name}" : $"T:{ns}.{name}";

    /// <summary>Builds the standard doc-comment ID for a method (no overload suffix).</summary>
    public static string MethodDocId(string typeFullName, string memberName)
        => $"M:{typeFullName}.{memberName}";

    /// <summary>Builds the standard doc-comment ID for a field.</summary>
    public static string FieldDocId(string typeFullName, string memberName)
        => $"F:{typeFullName}.{memberName}";

    /// <summary>Builds the standard doc-comment ID for a property.</summary>
    public static string PropertyDocId(string typeFullName, string memberName)
        => $"P:{typeFullName}.{memberName}";

    /// <summary>Builds the standard doc-comment ID for an event.</summary>
    public static string EventDocId(string typeFullName, string memberName)
        => $"E:{typeFullName}.{memberName}";

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Collapses multi-line XML summary text (which often has leading whitespace on each line)
    /// into a single space-separated sentence.
    /// </summary>
    private static string NormalizeWhitespace(string raw)
    {
        var lines = raw.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", lines.Select(l => l.Trim()).Where(l => l.Length > 0));
    }
}
