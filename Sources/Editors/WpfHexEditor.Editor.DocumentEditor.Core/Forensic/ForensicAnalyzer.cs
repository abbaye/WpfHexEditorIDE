// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Forensic/ForensicAnalyzer.cs
// Description:
//     Analyses a populated DocumentModel and emits ForensicAlerts.
//     Checks performed:
//       • OffsetGap    — gaps between consecutive block ranges
//       • OffsetOverlap — overlapping block ranges
//       • InvalidEncoding — UTF-8/ANSI runs with invalid byte sequences
//       • MacroPresent — DOCX HasMacros flag
//       • SuspiciousMetadata — future-dated or anomalous author/date
//       • ParseError   — already present in model (passthrough)
// ==========================================================

using System.Text;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Forensic;

/// <summary>
/// Produces <see cref="ForensicAlert"/> instances by analysing a
/// fully-populated <see cref="DocumentModel"/>.
/// </summary>
public sealed class ForensicAnalyzer
{
    private const int MinGapBytes = 4; // gaps smaller than this are ignored (padding)

    /// <summary>
    /// Analyses <paramref name="model"/> and returns all detected alerts.
    /// </summary>
    /// <param name="model">Fully populated model (BinaryMap must be sealed).</param>
    /// <param name="rawBytes">Raw source bytes for encoding validation.</param>
    public IReadOnlyList<ForensicAlert> Analyze(DocumentModel model, ReadOnlySpan<byte> rawBytes)
    {
        var alerts = new List<ForensicAlert>();

        AnalyzeOffsetGapsAndOverlaps(model.BinaryMap, alerts);
        AnalyzeMacros(model.Metadata, alerts);
        AnalyzeSuspiciousMetadata(model.Metadata, alerts);
        if (rawBytes.Length > 0)
            AnalyzeEncoding(model, rawBytes, alerts);

        // Passthrough existing ParseError alerts added by the loader.
        foreach (var existing in model.ForensicAlerts)
            if (existing.Kind == ForensicAlertKind.ParseError)
                alerts.Add(existing);

        return alerts;
    }

    // ──────────────────────────────── Offset gaps / overlaps ─────────────────

    private static void AnalyzeOffsetGapsAndOverlaps(BinaryMap.BinaryMap map,
                                                      List<ForensicAlert> alerts)
    {
        var entries = map.Entries;
        if (entries.Count < 2) return;

        for (int i = 0; i < entries.Count - 1; i++)
        {
            var curr = entries[i];
            var next = entries[i + 1];

            long gap = next.Offset - curr.End;
            if (gap > MinGapBytes)
            {
                alerts.Add(new ForensicAlert
                {
                    Kind = ForensicAlertKind.OffsetGap,
                    Severity = ForensicSeverity.Warning,
                    Description = $"Gap of {gap} bytes between blocks at 0x{curr.End:X8} and 0x{next.Offset:X8}.",
                    Block = curr.Block,
                    Offset = curr.End,
                    Suggestion = "Inspect hidden data between these offsets in the Hex pane."
                });
            }
            else if (gap < 0)
            {
                alerts.Add(new ForensicAlert
                {
                    Kind = ForensicAlertKind.OffsetOverlap,
                    Severity = ForensicSeverity.Error,
                    Description = $"Blocks overlap by {-gap} bytes at 0x{next.Offset:X8}.",
                    Block = next.Block,
                    Offset = next.Offset,
                    Suggestion = "File may be corrupt. Compare raw bytes with expected structure."
                });
            }
        }
    }

    // ──────────────────────────────── Macros ─────────────────────────────────

    private static void AnalyzeMacros(DocumentMetadata meta, List<ForensicAlert> alerts)
    {
        if (!meta.HasMacros) return;
        alerts.Add(new ForensicAlert
        {
            Kind = ForensicAlertKind.MacroPresent,
            Severity = ForensicSeverity.Warning,
            Description = "Document contains macro storage (VBA or similar).",
            Suggestion = "Review macro content before executing. Use forensic mode to inspect the vbaProject.bin entry."
        });
    }

    // ──────────────────────────────── Metadata anomalies ─────────────────────

    private static void AnalyzeSuspiciousMetadata(DocumentMetadata meta, List<ForensicAlert> alerts)
    {
        var now = DateTime.UtcNow;

        if (meta.CreatedUtc.HasValue && meta.CreatedUtc.Value > now.AddDays(1))
        {
            alerts.Add(new ForensicAlert
            {
                Kind = ForensicAlertKind.SuspiciousMetadata,
                Severity = ForensicSeverity.Warning,
                Description = $"Creation date is in the future: {meta.CreatedUtc.Value:yyyy-MM-dd HH:mm} UTC.",
                Suggestion = "Metadata may have been tampered with or clock was misconfigured."
            });
        }

        if (meta.ModifiedUtc.HasValue && meta.CreatedUtc.HasValue &&
            meta.ModifiedUtc.Value < meta.CreatedUtc.Value)
        {
            alerts.Add(new ForensicAlert
            {
                Kind = ForensicAlertKind.SuspiciousMetadata,
                Severity = ForensicSeverity.Warning,
                Description = "Modified date is earlier than creation date.",
                Suggestion = "Metadata may have been altered."
            });
        }
    }

    // ──────────────────────────────── Encoding ────────────────────────────────

    private static void AnalyzeEncoding(DocumentModel model, ReadOnlySpan<byte> raw,
                                         List<ForensicAlert> alerts)
    {
        // Only check RTF (plain-byte stream). ZIP-based formats (DOCX/ODT) use UTF-8 XML.
        if (!model.FilePath.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase)) return;

        // Validate that the header is valid ASCII (RTF is 7-bit ASCII + escape sequences).
        int invalidAt = -1;
        for (int i = 0; i < Math.Min(raw.Length, 4096); i++)
        {
            if (raw[i] > 0x7E && raw[i] != 0x0D && raw[i] != 0x0A)
            {
                invalidAt = i;
                break;
            }
        }

        if (invalidAt >= 0)
        {
            alerts.Add(new ForensicAlert
            {
                Kind = ForensicAlertKind.InvalidEncoding,
                Severity = ForensicSeverity.Warning,
                Description = $"Non-ASCII byte 0x{raw[invalidAt]:X2} at offset 0x{invalidAt:X8} in RTF stream.",
                Offset = invalidAt,
                Suggestion = "May indicate binary embedded content or encoding mismatch."
            });
        }
    }
}
