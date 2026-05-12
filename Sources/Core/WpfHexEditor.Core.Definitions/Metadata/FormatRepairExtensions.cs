//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Metadata/FormatRepairExtensions.cs
// Description: Extension methods exposing the repair[] and checksums sections
//              of a .whfmt to the IDE. These were previously consumed only by
//              the whfmt.Validate CLI tool — P6 makes them visible to the
//              repair action panel in WpfHexEditor.App.
// Architecture notes (ADR-038 P6):
//              Model layer only — execution of a repair is delegated to a
//              host-provided IWhfmtRepairExecutor (declared but not implemented
//              here; P9 will plug whfmt.Validate's RepairCommand into this).
//////////////////////////////////////////////

using System.Text.Json;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Definitions.Models;

namespace WpfHexEditor.Core.Definitions.Metadata;

// ---------------------------------------------------------------------------
// Model types
// ---------------------------------------------------------------------------

/// <summary>A repair action declared in a format's <c>repair[]</c> block.</summary>
public sealed record RepairAction(
    string Name,
    string? Trigger,
    string Action,
    string? Target,
    string? Algorithm,
    string? Description);

/// <summary>A checksum declaration from a format's <c>checksums</c> object.</summary>
public sealed record ChecksumSpec(
    string Name,
    string Algorithm,
    int Offset,
    int Length,
    string? CoversExpression,
    string? Endian);

// ---------------------------------------------------------------------------
// Extension methods
// ---------------------------------------------------------------------------

/// <summary>
/// Repair + checksum extensions on <see cref="EmbeddedFormatEntry"/>.
/// Adds runtime visibility of fields previously only read by whfmt.Validate.
/// </summary>
public static class FormatRepairExtensions
{
    /// <summary>
    /// Returns the repair actions from the <c>repair[]</c> array.
    /// Returns an empty list when absent.
    /// </summary>
    public static IReadOnlyList<RepairAction> GetRepairs(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        var root = doc.RootElement;
        if (!root.TryGetProperty("repair", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];

        var list = new List<RepairAction>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var name   = Str(item, "name");
            var action = Str(item, "action");
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(action)) continue;

            list.Add(new RepairAction(
                Name:        name,
                Trigger:     StrN(item, "trigger"),
                Action:      action,
                Target:      StrN(item, "target"),
                Algorithm:   StrN(item, "algorithm"),
                Description: StrN(item, "description")));
        }
        return list;
    }

    /// <summary>
    /// Returns the checksum specs from the <c>checksums</c> object. Each entry's key is
    /// its name, and the value object describes how to compute / verify it.
    /// </summary>
    public static IReadOnlyList<ChecksumSpec> GetChecksums(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        var root = doc.RootElement;
        if (!root.TryGetProperty("checksums", out var checks) || checks.ValueKind != JsonValueKind.Object)
            return [];

        var list = new List<ChecksumSpec>();
        foreach (var prop in checks.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object) continue;
            var algo = Str(prop.Value, "algorithm");
            if (string.IsNullOrEmpty(algo)) continue;

            int offset = GetInt(prop.Value, "offset", 0);
            int length = GetInt(prop.Value, "length", 0);
            list.Add(new ChecksumSpec(
                Name:             prop.Name,
                Algorithm:        algo,
                Offset:           offset,
                Length:           length,
                CoversExpression: StrN(prop.Value, "covers"),
                Endian:           StrN(prop.Value, "endian")));
        }
        return list;
    }

    // ----- Helpers ------------------------------------------------------------

    private static string Str(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static string? StrN(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static int GetInt(JsonElement el, string prop, int fallback)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
            ? i : fallback;
}

// ---------------------------------------------------------------------------
// Host contract for actually executing a repair (implemented in P9 by whfmt.Validate)
// ---------------------------------------------------------------------------

/// <summary>
/// Host contract for applying a <see cref="RepairAction"/> to a file. Implementations
/// (e.g. whfmt.Validate's RepairCommand) provide the algorithm-specific logic
/// for "recompute_checksum", "fix_header", etc.
/// </summary>
public interface IWhfmtRepairExecutor
{
    /// <summary>
    /// Applies <paramref name="action"/> to the bytes at <paramref name="filePath"/>.
    /// Returns the result of the repair (success + message + byte changes).
    /// </summary>
    RepairResult Apply(string filePath, RepairAction action);
}

/// <summary>Result of a single repair application.</summary>
public sealed record RepairResult(bool Success, string Message, int BytesChanged);
