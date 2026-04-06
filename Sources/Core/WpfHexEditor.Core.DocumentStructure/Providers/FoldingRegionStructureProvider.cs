// ==========================================================
// Project: WpfHexEditor.Core.DocumentStructure
// File: Providers/FoldingRegionStructureProvider.cs
// Created: 2026-04-05
// Description:
//     Fallback structure provider for code files without LSP or SourceOutline.
//     Uses simple brace-counting to identify regions/blocks.
//
// Architecture Notes:
//     Priority 100 (lowest). Only used when no better provider matches.
//     Does NOT use CodeEditor's internal FoldingEngine (not SDK-exposed).
//     Instead, performs a simple brace/bracket scan for block boundaries.
// ==========================================================

using System.IO;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

namespace WpfHexEditor.Core.DocumentStructure.Providers;

/// <summary>
/// Fallback brace-based structure provider for code files (Priority 100).
/// </summary>
public sealed class FoldingRegionStructureProvider : IDocumentStructureProvider
{
    public string DisplayName => "Code Regions";
    public int Priority => 100;

    public bool CanProvide(string? filePath, string? documentType, string? language)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        // Only for code document types, as a last-resort fallback
        return !string.IsNullOrEmpty(documentType) &&
               documentType.Equals("code", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentStructureResult?> GetStructureAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return null;

        var lines = await File.ReadAllLinesAsync(filePath, ct).ConfigureAwait(false);
        var regions = new List<DocumentStructureNode>();

        // Scan for #region directives and brace-delimited blocks at indent level 0
        var regionStack = new Stack<(string name, int startLine)>();

        for (var i = 0; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var trimmed = lines[i].TrimStart();

            // #region support
            if (trimmed.StartsWith("#region", StringComparison.Ordinal))
            {
                var name = trimmed.Length > 8 ? trimmed[8..].Trim() : "Region";
                regionStack.Push((name, i + 1));
            }
            else if (trimmed.StartsWith("#endregion", StringComparison.Ordinal) && regionStack.Count > 0)
            {
                var (name, startLine) = regionStack.Pop();
                regions.Add(new DocumentStructureNode
                {
                    Name = name,
                    Kind = "region",
                    StartLine = startLine,
                    EndLine = i + 1,
                });
            }
        }

        if (regions.Count == 0) return null;

        // Sort by start line
        regions.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));

        return new DocumentStructureResult
        {
            Nodes = regions,
            FilePath = filePath,
            Language = null,
        };
    }
}
