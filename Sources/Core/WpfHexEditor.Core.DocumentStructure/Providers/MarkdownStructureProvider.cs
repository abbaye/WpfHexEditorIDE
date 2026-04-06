// ==========================================================
// Project: WpfHexEditor.Core.DocumentStructure
// File: Providers/MarkdownStructureProvider.cs
// Created: 2026-04-05
// Description:
//     Structure provider for Markdown files. Parses # headings
//     into a hierarchical tree based on heading level.
//
// Architecture Notes:
//     Priority 300. Pure regex line scan — no external dependencies.
//     Heading hierarchy: # = depth 0, ## = depth 1, etc.
//     Fenced code blocks are skipped (not shown as nodes).
// ==========================================================

using System.IO;
using System.Text.RegularExpressions;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

namespace WpfHexEditor.Core.DocumentStructure.Providers;

/// <summary>
/// Markdown heading-based structure provider (Priority 300).
/// </summary>
public sealed partial class MarkdownStructureProvider : IDocumentStructureProvider
{
    public string DisplayName => "Markdown Headings";
    public int Priority => 300;

    private static readonly HashSet<string> s_extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdown", ".mkd"
    };

    public bool CanProvide(string? filePath, string? documentType, string? language)
    {
        if (!string.IsNullOrEmpty(language) && language.Equals("markdown", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.IsNullOrEmpty(filePath)) return false;
        return s_extensions.Contains(Path.GetExtension(filePath));
    }

    public async Task<DocumentStructureResult?> GetStructureAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return null;

        var lines = await File.ReadAllLinesAsync(filePath, ct).ConfigureAwait(false);
        var headings = new List<(int level, string text, int line)>();
        var inFencedBlock = false;

        for (var i = 0; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var line = lines[i];

            if (line.StartsWith("```") || line.StartsWith("~~~"))
            {
                inFencedBlock = !inFencedBlock;
                continue;
            }
            if (inFencedBlock) continue;

            var match = HeadingRegex().Match(line);
            if (match.Success)
            {
                var level = match.Groups[1].Value.Length; // 1-6
                var text = match.Groups[2].Value.Trim();
                headings.Add((level, text, i + 1)); // 1-based line
            }
        }

        if (headings.Count == 0) return null;

        var nodes = BuildHeadingHierarchy(headings);

        return new DocumentStructureResult
        {
            Nodes = nodes,
            FilePath = filePath,
            Language = "markdown",
        };
    }

    private static IReadOnlyList<DocumentStructureNode> BuildHeadingHierarchy(
        List<(int level, string text, int line)> headings)
    {
        var root = new List<DocumentStructureNode>();
        var stack = new Stack<(int level, List<DocumentStructureNode> children)>();
        stack.Push((0, root));

        foreach (var (level, text, line) in headings)
        {
            var node = new DocumentStructureNode
            {
                Name = text,
                Kind = "heading",
                Detail = new string('#', level),
                StartLine = line,
                Children = [],
            };

            // We need mutable children, so we use a different approach
            var mutableChildren = new List<DocumentStructureNode>();
            var mutableNode = new DocumentStructureNode
            {
                Name = text,
                Kind = "heading",
                Detail = $"H{level}",
                StartLine = line,
                Children = mutableChildren,
            };

            // Pop until we find a parent with lower level
            while (stack.Count > 1 && stack.Peek().level >= level)
                stack.Pop();

            stack.Peek().children.Add(mutableNode);
            stack.Push((level, mutableChildren));
        }

        return root;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingRegex();
}
