// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Services/ResxImportService.cs
// Description:
//     Imports entries from CSV, JSON, Android strings.xml,
//     and iOS .strings files into a list of ResxEntry records.
// ==========================================================

using System.Text.RegularExpressions;
using System.IO;
using System.Xml.Linq;
using WpfHexEditor.Editor.ResxEditor.Models;

namespace WpfHexEditor.Editor.ResxEditor.Services;

/// <summary>Supported import formats.</summary>
public enum ResxImportFormat { Csv, Json, AndroidStrings, IosStrings }

/// <summary>Imports localization data from external formats into RESX entries.</summary>
public static class ResxImportService
{
    public static async Task<IReadOnlyList<ResxEntry>> ImportAsync(
        string            inputPath,
        ResxImportFormat  format,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(inputPath, ct);
        return format switch
        {
            ResxImportFormat.Csv            => FromCsv(content),
            ResxImportFormat.Json           => FromJson(content),
            ResxImportFormat.AndroidStrings => FromAndroidStrings(content),
            ResxImportFormat.IosStrings     => FromIosStrings(content),
            _                               => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    // -- CSV ----------------------------------------------------------------

    private static IReadOnlyList<ResxEntry> FromCsv(string content)
    {
        var entries = new List<ResxEntry>();
        var lines   = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines.Skip(1)) // skip header
        {
            var cols = SplitCsvLine(line);
            if (cols.Length < 2) continue;
            var name    = cols[0].Trim();
            var value   = cols[1];
            var comment = cols.Length > 2 ? cols[2] : string.Empty;
            if (!string.IsNullOrEmpty(name))
                entries.Add(new ResxEntry(name, value, comment, null, null, "preserve"));
        }
        return entries;
    }

    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var inQuote = false;
        var current = new System.Text.StringBuilder();
        foreach (var c in line)
        {
            if (c == '"') { inQuote = !inQuote; continue; }
            if (c == ';' && !inQuote) { result.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        result.Add(current.ToString());
        return [.. result];
    }

    // -- JSON ---------------------------------------------------------------

    private static IReadOnlyList<ResxEntry> FromJson(string content)
    {
        var entries = new List<ResxEntry>();
        // Simple key/value JSON parser (no dependency on System.Text.Json)
        var pairs = Regex.Matches(content,
            """\"(?<key>[^"]+)\"\s*:\s*\"(?<val>(?:[^"\\]|\\.)*)\" """,
            RegexOptions.ExplicitCapture);

        foreach (Match m in pairs)
        {
            var key = m.Groups["key"].Value;
            var val = UnescapeJson(m.Groups["val"].Value);
            entries.Add(new ResxEntry(key, val, string.Empty, null, null, "preserve"));
        }
        return entries;
    }

    private static string UnescapeJson(string s)
        => s.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\r", "\r");

    // -- Android strings.xml -----------------------------------------------

    private static IReadOnlyList<ResxEntry> FromAndroidStrings(string content)
    {
        var entries = new List<ResxEntry>();
        try
        {
            var xdoc = XDocument.Parse(content);
            foreach (var el in xdoc.Root?.Elements("string") ?? [])
            {
                var name  = (string?)el.Attribute("name") ?? string.Empty;
                var value = el.Value.Replace("\\'", "'").Replace("\\\"", "\"");
                if (!string.IsNullOrEmpty(name))
                    entries.Add(new ResxEntry(name.Replace('_', '.'), value, string.Empty, null, null, "preserve"));
            }
        }
        catch { /* malformed XML — return empty */ }
        return entries;
    }

    // -- iOS .strings -------------------------------------------------------

    private static IReadOnlyList<ResxEntry> FromIosStrings(string content)
    {
        var entries = new List<ResxEntry>();
        var pairs   = Regex.Matches(content,
            """\"(?<key>(?:[^"\\]|\\.)*)\"\s*=\s*\"(?<val>(?:[^"\\]|\\.)*)\"\s*;""",
            RegexOptions.ExplicitCapture);

        foreach (Match m in pairs)
        {
            var key = UnescapeIos(m.Groups["key"].Value);
            var val = UnescapeIos(m.Groups["val"].Value);
            if (!string.IsNullOrEmpty(key))
                entries.Add(new ResxEntry(key, val, string.Empty, null, null, "preserve"));
        }
        return entries;
    }

    private static string UnescapeIos(string s)
        => s.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n");
}
