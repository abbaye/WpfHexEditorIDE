// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Services/ResxValidationService.cs
// Description:
//     Validates an ordered list of RESX entries and returns a
//     list of DiagnosticEntry objects for the Error Panel.
//     Safe to call on the UI thread (no I/O except file-ref check).
// ==========================================================

using WpfHexEditor.Editor.Core;
using System.IO;
using WpfHexEditor.Editor.ResxEditor.Models;

namespace WpfHexEditor.Editor.ResxEditor.Services;

/// <summary>Validates RESX entries and returns IDE-compatible diagnostics.</summary>
public static class ResxValidationService
{
    /// <summary>
    /// Validates <paramref name="entries"/> and returns all discovered issues.
    /// <paramref name="filePath"/> is used in <see cref="DiagnosticEntry.FilePath"/>.
    /// </summary>
    public static List<DiagnosticEntry> Validate(
        IReadOnlyList<ResxEntry> entries,
        string filePath)
    {
        var diagnostics = new List<DiagnosticEntry>();
        var fileName    = Path.GetFileName(filePath);
        var seenKeys    = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var line  = i + 1; // 1-based row index used as logical "line"

            // RESX001 — Empty key name
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                diagnostics.Add(new DiagnosticEntry(
                    DiagnosticSeverity.Error, "RESX001",
                    "Entry has an empty key name.",
                    FileName: fileName, FilePath: filePath, Line: line));
                continue; // cannot deduplicate an empty key
            }

            // RESX002 — Duplicate key
            if (seenKeys.TryGetValue(entry.Name, out var firstLine))
            {
                diagnostics.Add(new DiagnosticEntry(
                    DiagnosticSeverity.Error, "RESX002",
                    $"Duplicate key '{entry.Name}' (also on row {firstLine}).",
                    FileName: fileName, FilePath: filePath, Line: line,
                    Tag: entry.Name));
            }
            else
            {
                seenKeys[entry.Name] = line;
            }

            // RESX003 — Empty value for String entry
            if (entry.EntryType == ResxEntryType.String && string.IsNullOrEmpty(entry.Value))
            {
                diagnostics.Add(new DiagnosticEntry(
                    DiagnosticSeverity.Warning, "RESX003",
                    $"String entry '{entry.Name}' has an empty value.",
                    FileName: fileName, FilePath: filePath, Line: line));
            }

            // RESX004 — Missing ResXFileRef target
            if (entry.EntryType == ResxEntryType.FileRef && !string.IsNullOrEmpty(entry.Value))
            {
                var refPath = ExtractFileRefPath(entry.Value, filePath);
                if (refPath is not null && !File.Exists(refPath))
                {
                    diagnostics.Add(new DiagnosticEntry(
                        DiagnosticSeverity.Warning, "RESX004",
                        $"ResXFileRef target not found: '{refPath}'.",
                        FileName: fileName, FilePath: filePath, Line: line));
                }
            }

            // RESX005 — Non-ASCII key name
            if (!IsAsciiIdentifier(entry.Name))
            {
                diagnostics.Add(new DiagnosticEntry(
                    DiagnosticSeverity.Message, "RESX005",
                    $"Key '{entry.Name}' contains non-ASCII characters.",
                    FileName: fileName, FilePath: filePath, Line: line));
            }

            // RESX006 — Very long key name
            if (entry.Name.Length > 128)
            {
                diagnostics.Add(new DiagnosticEntry(
                    DiagnosticSeverity.Message, "RESX006",
                    $"Key '{entry.Name[..20]}…' exceeds 128 characters ({entry.Name.Length}).",
                    FileName: fileName, FilePath: filePath, Line: line));
            }
        }

        return diagnostics;
    }

    // ------------------------------------------------------------------

    private static bool IsAsciiIdentifier(string s)
    {
        foreach (var c in s)
            if (c > 127) return false;
        return true;
    }

    /// <summary>Extracts the file path from a ResXFileRef value string.</summary>
    private static string? ExtractFileRefPath(string refValue, string baseFilePath)
    {
        // ResXFileRef value format: "RelPath;TypeName[;Encoding]"
        var semi = refValue.IndexOf(';');
        var rawPath = semi >= 0 ? refValue[..semi] : refValue;
        rawPath = rawPath.Trim();

        if (Path.IsPathRooted(rawPath)) return rawPath;

        var dir = Path.GetDirectoryName(baseFilePath);
        return dir is null ? null : Path.GetFullPath(Path.Combine(dir, rawPath));
    }
}
