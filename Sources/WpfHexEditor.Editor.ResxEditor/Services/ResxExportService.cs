// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Services/ResxExportService.cs
// Description:
//     Exports RESX entries to CSV, JSON, XLIFF 1.2,
//     Android strings.xml, and iOS .strings formats.
// ==========================================================

using System.Text;
using System.IO;
using System.Xml.Linq;
using WpfHexEditor.Editor.ResxEditor.Models;

namespace WpfHexEditor.Editor.ResxEditor.Services;

/// <summary>Supported export formats.</summary>
public enum ResxExportFormat { Csv, Json, Xliff, AndroidStrings, IosStrings }

/// <summary>Exports RESX string entries to various localization formats.</summary>
public static class ResxExportService
{
    public static async Task ExportAsync(
        IReadOnlyList<ResxEntry> entries,
        ResxExportFormat         format,
        string                   outputPath,
        string                   sourceLanguage = "en",
        CancellationToken        ct = default)
    {
        var content = format switch
        {
            ResxExportFormat.Csv            => ToCsv(entries),
            ResxExportFormat.Json           => ToJson(entries),
            ResxExportFormat.Xliff          => ToXliff(entries, outputPath, sourceLanguage),
            ResxExportFormat.AndroidStrings => ToAndroidStrings(entries),
            ResxExportFormat.IosStrings     => ToIosStrings(entries),
            _                               => throw new ArgumentOutOfRangeException(nameof(format))
        };

        await File.WriteAllTextAsync(outputPath, content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct);
    }

    // -- CSV ----------------------------------------------------------------

    private static string ToCsv(IReadOnlyList<ResxEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name;Value;Comment");
        foreach (var e in entries.Where(e => e.EntryType == ResxEntryType.String))
            sb.AppendLine($"{EscapeCsv(e.Name)};{EscapeCsv(e.Value)};{EscapeCsv(e.Comment)}");
        return sb.ToString();
    }

    private static string EscapeCsv(string s)
    {
        if (s.Contains(';') || s.Contains('"') || s.Contains('\n'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }

    // -- JSON ---------------------------------------------------------------

    private static string ToJson(IReadOnlyList<ResxEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        var strings = entries.Where(e => e.EntryType == ResxEntryType.String).ToList();
        for (int i = 0; i < strings.Count; i++)
        {
            var e     = strings[i];
            var comma = i < strings.Count - 1 ? "," : string.Empty;
            sb.AppendLine($"  {JsonString(e.Name)}: {JsonString(e.Value)}{comma}");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string JsonString(string s)
        => $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r")}\"";

    // -- XLIFF 1.2 ----------------------------------------------------------

    private static string ToXliff(IReadOnlyList<ResxEntry> entries, string outputPath, string srcLang)
    {
        var fileName = Path.GetFileName(outputPath);
        var doc = new XDocument(
            new XElement("xliff",
                new XAttribute("version", "1.2"),
                new XAttribute("xmlns", "urn:oasis:names:tc:xliff:document:1.2"),
                new XElement("file",
                    new XAttribute("original", fileName),
                    new XAttribute("source-language", srcLang),
                    new XAttribute("datatype", "resx"),
                    new XElement("body",
                        entries.Where(e => e.EntryType == ResxEntryType.String)
                               .Select(e => new XElement("trans-unit",
                                   new XAttribute("id", e.Name),
                                   new XElement("source", e.Value),
                                   string.IsNullOrEmpty(e.Comment) ? null
                                       : new XElement("note", e.Comment)))))));
        return doc.ToString(SaveOptions.OmitDuplicateNamespaces);
    }

    // -- Android strings.xml -----------------------------------------------

    private static string ToAndroidStrings(IReadOnlyList<ResxEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<resources>");
        foreach (var e in entries.Where(e => e.EntryType == ResxEntryType.String))
        {
            var key = e.Name.Replace('.', '_');
            sb.AppendLine($"    <string name=\"{key}\">{AndroidEscape(e.Value)}</string>");
        }
        sb.AppendLine("</resources>");
        return sb.ToString();
    }

    private static string AndroidEscape(string s)
        => s.Replace("'", "\\'").Replace("\"", "\\\"").Replace("&", "&amp;").Replace("<", "&lt;");

    // -- iOS .strings -------------------------------------------------------

    private static string ToIosStrings(IReadOnlyList<ResxEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (var e in entries.Where(e => e.EntryType == ResxEntryType.String))
        {
            if (!string.IsNullOrEmpty(e.Comment))
                sb.AppendLine($"/* {e.Comment} */");
            sb.AppendLine($"\"{IosEscape(e.Name)}\" = \"{IosEscape(e.Value)}\";");
        }
        return sb.ToString();
    }

    private static string IosEscape(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
}
