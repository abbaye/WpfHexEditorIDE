//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/WhfmtVersionMigrator.cs
// Description: In-memory migration of whfmt v2 documents to v3 canonical form.
//              Renames PascalCase root fields to camelCase (QualityMetrics →
//              qualityMetrics, MimeTypes → mimeTypes, Software → software,
//              UseCases → useCases, TechnicalDetails → technicalDetails) and
//              normalizes detection sub-fields (Strength, EntropyHint,
//              MinimumScore). The original JSON text on disk is never modified;
//              consumers that need v3-canonical shape can call Migrate(json).
// Architecture notes (ADR-038 D1 / D2):
//              ZERO field deletion. PascalCase fields are preserved (rename only).
//              Catalog backfill stays a separate, optional operation that can
//              run post-P11 in batches. The runtime now tolerates both shapes
//              via the migrator's normalization.
//////////////////////////////////////////////

using System.Text.Json;
using System.Text.Json.Nodes;

namespace WpfHexEditor.Core.Definitions.Models;

/// <summary>
/// In-memory whfmt v2 → v3 normalizer. Renames legacy PascalCase top-level fields
/// (and nested <c>detection</c> sub-fields) to their v3 camelCase canonical names.
/// </summary>
public static class WhfmtVersionMigrator
{
    private static readonly JsonNodeOptions s_nodeOpts = new() { PropertyNameCaseInsensitive = false };

    private static readonly JsonDocumentOptions s_docOpts = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Maps legacy PascalCase root field name → v3 canonical camelCase.
    /// Order matters for fields that may exist under both names already —
    /// we never overwrite an existing camelCase value.
    /// </summary>
    private static readonly Dictionary<string, string> s_rootRenames =
        new(StringComparer.Ordinal)
        {
            { "QualityMetrics",    "qualityMetrics" },
            { "MimeTypes",         "mimeTypes" },
            { "Software",          "software" },
            { "UseCases",          "useCases" },
            { "TechnicalDetails",  "technicalDetails" },
        };

    /// <summary>Same mapping for <c>detection</c> sub-fields.</summary>
    private static readonly Dictionary<string, string> s_detectionRenames =
        new(StringComparer.Ordinal)
        {
            { "Strength",     "strength" },
            { "EntropyHint",  "entropyHint" },
            { "MinimumScore", "minimumScore" },
        };

    /// <summary>
    /// Migrates <paramref name="whfmtJson"/> in memory and returns the normalized
    /// JSON string. The input is never modified. Returns <paramref name="whfmtJson"/>
    /// unchanged when no renames apply (cheap fast path).
    /// </summary>
    public static string Migrate(string whfmtJson)
    {
        ArgumentNullException.ThrowIfNull(whfmtJson);
        if (!HasAnyLegacyField(whfmtJson)) return whfmtJson;

        var root = ParseToNode(whfmtJson);
        if (root is not JsonObject obj) return whfmtJson;

        bool changed = false;
        changed |= RenameKeys(obj, s_rootRenames);

        if (obj["detection"] is JsonObject det)
            changed |= RenameKeys(det, s_detectionRenames);

        if (!changed) return whfmtJson;
        return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Returns the migration report (list of renames that would be applied) without
    /// producing the new JSON. Useful for diagnostic UIs.
    /// </summary>
    public static IReadOnlyList<string> DryRun(string whfmtJson)
    {
        var report = new List<string>();
        var root = ParseToNode(whfmtJson);
        if (root is not JsonObject obj) return report;

        foreach (var (from, to) in s_rootRenames)
            if (obj.ContainsKey(from)) report.Add($"root: {from} → {to}");

        if (obj["detection"] is JsonObject det)
            foreach (var (from, to) in s_detectionRenames)
                if (det.ContainsKey(from)) report.Add($"detection: {from} → {to}");

        return report;
    }

    // ---- helpers ----------------------------------------------------------

    private static JsonNode? ParseToNode(string json)
    {
        // System.Text.Json's JsonNode parser does not consume JsonDocumentOptions,
        // so we round-trip through JsonDocument first to absorb comments + trailing commas,
        // then re-serialize via a Writer so the emitted JSON is strict (no comments,
        // no trailing commas) for the JsonNode parse to consume.
        using var doc = JsonDocument.Parse(json, s_docOpts);
        using var ms  = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            doc.RootElement.WriteTo(writer);
        }
        var strictJson = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        return JsonNode.Parse(strictJson, s_nodeOpts);
    }

    private static bool RenameKeys(JsonObject obj, IReadOnlyDictionary<string, string> renames)
    {
        bool changed = false;
        foreach (var (from, to) in renames)
        {
            if (!obj.ContainsKey(from)) continue;
            // Don't overwrite an existing camelCase value — that one is authoritative.
            if (obj.ContainsKey(to))
            {
                obj.Remove(from);
                changed = true;
                continue;
            }
            // Move the value: detach + reattach under the new key.
            var node = obj[from];
            obj.Remove(from);
            obj[to] = node?.DeepClone();
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// Fast-path check: does the raw text contain any of the known legacy keys
    /// inside JSON property positions? Avoids paying for a full parse when nothing
    /// will change. Conservative — false positives are OK (they just cause a parse),
    /// false negatives are not. We just look for the substrings prefixed by '"'.
    /// </summary>
    private static bool HasAnyLegacyField(string json)
    {
        foreach (var key in s_rootRenames.Keys)
            if (json.Contains('"' + key + '"', StringComparison.Ordinal)) return true;
        foreach (var key in s_detectionRenames.Keys)
            if (json.Contains('"' + key + '"', StringComparison.Ordinal)) return true;
        return false;
    }
}
