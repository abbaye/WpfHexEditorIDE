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
    /// <summary>
    /// Maps legacy PascalCase root field name → v3 canonical camelCase.
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

    /// <summary>Pre-quoted legacy keys for the fast-path substring scan.</summary>
    private static readonly string[] s_legacyQuotedKeys =
        s_rootRenames.Keys.Concat(s_detectionRenames.Keys)
            .Select(k => '"' + k + '"').ToArray();

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
    /// producing the new JSON. Honors the same "don't overwrite an existing
    /// camelCase value" policy as <see cref="Migrate"/> — collisions are reported
    /// as drops rather than renames.
    /// </summary>
    public static IReadOnlyList<string> DryRun(string whfmtJson)
    {
        if (!HasAnyLegacyField(whfmtJson)) return Array.Empty<string>();

        var root = ParseToNode(whfmtJson);
        if (root is not JsonObject obj) return Array.Empty<string>();

        var report = new List<string>();
        AppendPlan(obj, s_rootRenames, "root", report);
        if (obj["detection"] is JsonObject det)
            AppendPlan(det, s_detectionRenames, "detection", report);
        return report;
    }

    // ---- helpers ----------------------------------------------------------

    private static JsonNode? ParseToNode(string json)
    {
        // JsonNode.Parse(string) does not consume JsonDocumentOptions, so we
        // route through JsonDocument (which strips comments + trailing commas)
        // and re-emit strict JSON into a stream that JsonNode reads directly.
        using var doc = JsonDocument.Parse(json, WhfmtJsonOptions.Jsonc);
        using var ms  = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
            doc.RootElement.WriteTo(writer);
        ms.Position = 0;
        return JsonNode.Parse(ms);
    }

    private static bool RenameKeys(JsonObject obj, IReadOnlyDictionary<string, string> renames)
    {
        bool changed = false;
        foreach (var (from, to) in renames)
        {
            if (!obj.ContainsKey(from)) continue;
            if (obj.ContainsKey(to))
            {
                // Existing camelCase value is authoritative; drop the legacy duplicate.
                obj.Remove(from);
                changed = true;
                continue;
            }
            var node = obj[from];
            obj.Remove(from);   // detaches node from parent — safe to re-attach
            obj[to] = node;
            changed = true;
        }
        return changed;
    }

    private static void AppendPlan(
        JsonObject obj,
        IReadOnlyDictionary<string, string> renames,
        string scope,
        List<string> report)
    {
        foreach (var (from, to) in renames)
        {
            if (!obj.ContainsKey(from)) continue;
            report.Add(obj.ContainsKey(to)
                ? $"{scope}: {from} dropped (camelCase '{to}' already present)"
                : $"{scope}: {from} → {to}");
        }
    }

    private static bool HasAnyLegacyField(string json)
    {
        foreach (var quoted in s_legacyQuotedKeys)
            if (json.Contains(quoted, StringComparison.Ordinal)) return true;
        return false;
    }
}
