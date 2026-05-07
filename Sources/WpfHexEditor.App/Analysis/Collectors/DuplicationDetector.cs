// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/DuplicationDetector.cs
// Description: Hash-based clone detection across syntax nodes.
//              Normalizes identifiers and literals before hashing so that
//              structurally identical blocks with different names are detected.
//              Stateless — safe for parallel use.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class DuplicationDetector
{
    /// <param name="trees">All syntax trees to scan.</param>
    /// <param name="minTokens">Minimum token count to consider a block a clone candidate.</param>
    internal static IReadOnlyList<DuplicationGroup> Detect(
        IEnumerable<SyntaxTree> trees, int minTokens = 50)
    {
        // Collect all statement-level blocks with ≥ minTokens tokens
        var buckets = new Dictionary<string, List<(string file, int start, int end, int tokens)>>();

        foreach (var tree in trees)
        {
            var root = tree.GetRoot();
            foreach (var block in root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BlockSyntax>())
            {
                var tokens = block.DescendantTokens().ToList();
                if (tokens.Count < minTokens) continue;

                var hash = ComputeNormalizedHash(tokens);
                if (!buckets.ContainsKey(hash))
                    buckets[hash] = [];

                var span  = block.GetLocation().GetLineSpan();
                buckets[hash].Add((
                    tree.FilePath,
                    span.StartLinePosition.Line + 1,
                    span.EndLinePosition.Line + 1,
                    tokens.Count));
            }
        }

        var groups = new List<DuplicationGroup>();
        foreach (var (_, occurrenceList) in buckets)
        {
            if (occurrenceList.Count < 2) continue;

            groups.Add(new DuplicationGroup
            {
                TokenCount  = occurrenceList[0].tokens,
                LineCount   = occurrenceList[0].end - occurrenceList[0].start + 1,
                Occurrences = occurrenceList
                    .Select(o => new DuplicationOccurrence
                    {
                        FilePath  = o.file,
                        StartLine = o.start,
                        EndLine   = o.end,
                    })
                    .ToList()
            });
        }

        return groups.OrderByDescending(g => g.LineCount).ToList();
    }

    // Normalize identifiers → "ID", literals → "LIT" before hashing.
    private static string ComputeNormalizedHash(IEnumerable<SyntaxToken> tokens)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var token in tokens)
        {
            var kind = token.Kind();
            if (kind == SyntaxKind.IdentifierToken)
                sb.Append("ID ");
            else if (token.IsKind(SyntaxKind.NumericLiteralToken)
                  || token.IsKind(SyntaxKind.StringLiteralToken)
                  || token.IsKind(SyntaxKind.CharacterLiteralToken))
                sb.Append("LIT ");
            else
                sb.Append(token.Text).Append(' ');
        }
        // Use a fast deterministic hash — not for security, just bucketing
        return sb.ToString().GetHashCode().ToString();
    }
}
