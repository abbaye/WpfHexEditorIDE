// ==========================================================
// Project: WpfHexEditor.Core.DocumentStructure
// File: Providers/BinaryFormatStructureProvider.cs
// Created: 2026-04-05
// Description:
//     Structure provider for binary files using .whfmt format detection.
//     Converts BlockDefinition hierarchy from the detected FormatDefinition to
//     DocumentStructureNode tree. ByteOffset/ByteLength populated (no line numbers).
//
// Architecture Notes:
//     Priority 300. Only active when documentType=="hex".
//     Reads up to 4KB for format detection (signature bytes only).
//     Uses FormatDetectionService.SharedCatalog if populated, otherwise skips.
// ==========================================================

using System.IO;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Services;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

namespace WpfHexEditor.Core.DocumentStructure.Providers;

/// <summary>
/// Binary format structure provider using .whfmt definitions (Priority 300).
/// </summary>
public sealed class BinaryFormatStructureProvider : IDocumentStructureProvider
{
    public string DisplayName => "Binary Format";
    public int Priority => 300;

    public bool CanProvide(string? filePath, string? documentType, string? language)
        => !string.IsNullOrEmpty(documentType) &&
           documentType.Equals("hex", StringComparison.OrdinalIgnoreCase);

    public async Task<DocumentStructureResult?> GetStructureAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            var result = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                // Read a capped sample for detection (4KB for signature)
                const int SampleSize = 4096;
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var bufLen = (int)Math.Min(fs.Length, SampleSize);
                var buf = new byte[bufLen];
                _ = fs.Read(buf, 0, bufLen);

                var svc = new FormatDetectionService();
                // Use shared catalog if available
                var catalog = FormatDetectionService.SharedCatalog;
                if (catalog is not null)
                    foreach (var fmt in catalog)
                        svc.AddFormatDefinition(fmt);

                if (svc.GetFormatCount() == 0) return null;

                return svc.DetectFormat(buf, System.IO.Path.GetFileName(filePath));
            }, ct).ConfigureAwait(false);

            if (result is null || !result.Success || result.Format is null) return null;

            var nodes = ConvertBlocks(result.Format.Blocks);
            if (nodes.Count == 0) return null;

            return new DocumentStructureResult
            {
                Nodes = nodes,
                FilePath = filePath,
                Language = null,
            };
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private static IReadOnlyList<DocumentStructureNode> ConvertBlocks(IReadOnlyList<BlockDefinition>? blocks)
    {
        if (blocks is null || blocks.Count == 0) return [];

        var nodes = new List<DocumentStructureNode>();
        foreach (var block in blocks)
        {
            if (string.IsNullOrEmpty(block.Name)) continue;

            // Offset / length may be int, long, or string expressions
            long offset = block.Offset is int io ? io : block.Offset is long lo ? lo : -1;
            long length = block.Length is int il ? il : block.Length is long ll ? ll : 0;

            nodes.Add(new DocumentStructureNode
            {
                Name = block.Name,
                Kind = "block",
                Detail = block.Type,
                ByteOffset = offset,
                ByteLength = length,
            });
        }

        return nodes;
    }
}
