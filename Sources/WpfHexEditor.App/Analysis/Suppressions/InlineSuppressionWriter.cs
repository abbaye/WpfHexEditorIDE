// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Suppressions/InlineSuppressionWriter.cs
// Description: Writes `// CodeAnalysis: suppress WHxxxx` markers above a code
//              construct, or `// CodeAnalysis: suppress-file WHxxxx` at the
//              top of a file. Idempotent — never duplicates an existing marker.
// Architecture Notes:
//     Pair of InlineSuppressionReader. Pure I/O — no Roslyn dependency.
// ==========================================================

using System.IO;

namespace WpfHexEditor.App.Analysis.Suppressions;

internal static class InlineSuppressionWriter
{
    private const string TokenInSource = "CodeAnalysis: suppress ";
    private const string TokenInFile   = "CodeAnalysis: suppress-file ";

    /// <summary>Insert `// CodeAnalysis: suppress WHxxxx` above the given line.</summary>
    internal static bool WriteInSource(string filePath, int line, string ruleId)
    {
        if (!IsValid(filePath, ruleId) || line < 1) return false;
        try
        {
            var lines = File.ReadAllLines(filePath).ToList();
            if (line > lines.Count) return false;

            int idx = line - 1;
            if (HasMarker(lines, idx, TokenInSource, ruleId)) return true;

            string indent = LeadingWhitespace(lines[idx]);
            lines.Insert(idx, $"{indent}// {TokenInSource}{ruleId}");
            File.WriteAllLines(filePath, lines);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Insert `// CodeAnalysis: suppress-file WHxxxx` at the top of the file.</summary>
    internal static bool WriteInFile(string filePath, string ruleId)
    {
        if (!IsValid(filePath, ruleId)) return false;
        try
        {
            var lines = File.ReadAllLines(filePath).ToList();
            if (HasMarker(lines, 0, TokenInFile, ruleId)) return true;
            lines.Insert(0, $"// {TokenInFile}{ruleId}");
            File.WriteAllLines(filePath, lines);
            return true;
        }
        catch { return false; }
    }

    private static bool IsValid(string filePath, string ruleId)
        => File.Exists(filePath) && !string.IsNullOrEmpty(ruleId);

    private static bool HasMarker(List<string> lines, int idx, string token, string ruleId)
    {
        // Scan the line above OR the line itself for an existing marker
        for (int i = Math.Max(0, idx - 1); i <= Math.Min(lines.Count - 1, idx); i++)
        {
            var t = lines[i];
            if (t.Contains(token, StringComparison.Ordinal)
             && t.Contains(ruleId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string LeadingWhitespace(string line)
        => new(line.TakeWhile(char.IsWhiteSpace).ToArray());
}
