// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/InlineSuppressionReader.cs
// Description: Reads `// CodeAnalysis: suppress WH00xx [— reason]` markers
//              placed directly above a code construct. Filters out matching
//              diagnostics during analysis.
// Architecture Notes:
//     Stateless. Walks the already-parsed SyntaxTree's comment trivia —
//     no extra disk I/O.
// ==========================================================

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Services;

internal static class InlineSuppressionReader
{
    private static readonly Regex SuppressPattern =
        new(@"//\s*CodeAnalysis:\s*suppress\s+(WH\d{4})", RegexOptions.Compiled);

    private static readonly Regex SuppressFilePattern =
        new(@"//\s*CodeAnalysis:\s*suppress-file\s+(WH\d{4})", RegexOptions.Compiled);

    /// <summary>
    /// Map of (filePath → set of (ruleId, line)). Line = the line of the // comment.
    /// Line == 0 is reserved for file-scoped suppressions (all lines in that file).
    /// </summary>
    public sealed record SuppressionMap(Dictionary<string, HashSet<(string Rule, int Line)>> Entries);

    internal static SuppressionMap Read(IEnumerable<SyntaxTree> trees)
    {
        var map = new Dictionary<string, HashSet<(string, int)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tree in trees)
        {
            var set = new HashSet<(string, int)>();
            foreach (var trivia in tree.GetRoot().DescendantTrivia())
            {
                if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)) continue;
                var text = trivia.ToString();

                var fileMatch = SuppressFilePattern.Match(text);
                if (fileMatch.Success) { set.Add((fileMatch.Groups[1].Value, 0)); continue; }

                var match = SuppressPattern.Match(text);
                if (!match.Success) continue;

                int line = trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                set.Add((match.Groups[1].Value, line));
            }
            if (set.Count > 0) map[tree.FilePath] = set;
        }

        return new SuppressionMap(map);
    }

    internal static bool IsSuppressed(AnalysisDiagnostic d, SuppressionMap map)
    {
        if (!map.Entries.TryGetValue(d.FilePath, out var set)) return false;
        // File-scoped (line == 0) OR marker on the line above OR same line
        return set.Contains((d.Id, 0))
            || set.Contains((d.Id, d.Line - 1))
            || set.Contains((d.Id, d.Line));
    }
}
