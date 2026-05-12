//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/WhfmtVariableParser.cs
// Description: Reads the "variables" property of a whfmt JSON document and
//              returns a list of typed VariableDefinition objects.
//              Handles both schemas in use across the catalog:
//                Schema A (dict):   "variables": { "magic": "", "size": 0 }
//                Schema B (typed):  "variables": [ { "name": "...", "type": "uint32", ... } ]
// Architecture notes (ADR-038 D7):
//              Schema A is auto-upgraded to Schema B in memory by inferring the
//              type from the literal initialValue (string→Ascii, int→Int32, etc.).
//              The original JSON is never rewritten — migration is at-load only.
//////////////////////////////////////////////

using System.Text.Json;

namespace WpfHexEditor.Core.Definitions.Models;

/// <summary>
/// Parses the <c>"variables"</c> property of a whfmt JSON document.
/// Supports both dict and typed-array schemas, normalizing them to a single
/// <see cref="VariableDefinition"/> list.
/// </summary>
public static class WhfmtVariableParser
{
    /// <summary>
    /// Parses variables from a full whfmt JSON document. Returns an empty list
    /// when the document has no <c>variables</c> property.
    /// </summary>
    public static IReadOnlyList<VariableDefinition> ParseDocument(string whfmtJson)
    {
        ArgumentNullException.ThrowIfNull(whfmtJson);
        using var doc = JsonDocument.Parse(whfmtJson, WhfmtJsonOptions.Jsonc);
        return ParseElement(doc.RootElement);
    }

    /// <summary>
    /// Parses variables from the already-loaded root <see cref="JsonElement"/> of a whfmt
    /// document. Returns an empty list when <c>variables</c> is absent.
    /// </summary>
    public static IReadOnlyList<VariableDefinition> ParseElement(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return [];
        if (!root.TryGetProperty("variables", out var vars)) return [];

        return vars.ValueKind switch
        {
            JsonValueKind.Array  => ParseTypedArray(vars),
            JsonValueKind.Object => ParseDictAndMigrate(vars),
            _ => [],
        };
    }

    /// <summary>
    /// Builds a fresh <see cref="WhfmtVariableStore"/>, registers every variable
    /// from <paramref name="whfmtJson"/>, and returns it ready for the expression engine.
    /// </summary>
    public static WhfmtVariableStore BuildStore(string whfmtJson)
    {
        var store = new WhfmtVariableStore();
        foreach (var def in ParseDocument(whfmtJson))
            store.Register(def);
        return store;
    }

    // -- Schema B: typed array -------------------------------------------------

    private static IReadOnlyList<VariableDefinition> ParseTypedArray(JsonElement arr)
    {
        var list = new List<VariableDefinition>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var name = GetString(item, "name");
            if (string.IsNullOrEmpty(name)) continue;

            var type    = WhfmtValueTypes.Parse(GetString(item, "type"));
            var offset  = GetInt(item, "offset", -1);
            var length  = GetInt(item, "length", 0);
            var endian  = ParseEndian(GetString(item, "endian"));
            var desc    = GetString(item, "description") ?? "";
            var initial = ExtractLiteral(item, "initialValue");

            list.Add(new VariableDefinition(name, type, offset, length, endian, desc, initial));
        }
        return list;
    }

    // -- Schema A: dict (auto-migrated to typed) -------------------------------

    private static IReadOnlyList<VariableDefinition> ParseDictAndMigrate(JsonElement obj)
    {
        var list = new List<VariableDefinition>();
        foreach (var prop in obj.EnumerateObject())
        {
            var name = prop.Name;
            // Two shapes inside a dict:
            //   "magic": ""               -> literal initial value
            //   "magic": { "type": "uint32", "offset": 0 }  -> nested object (rare, treated as typed entry)
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                var type    = WhfmtValueTypes.Parse(GetString(prop.Value, "type"));
                var offset  = GetInt(prop.Value, "offset", -1);
                var length  = GetInt(prop.Value, "length", 0);
                var endian  = ParseEndian(GetString(prop.Value, "endian"));
                var desc    = GetString(prop.Value, "description") ?? "";
                var initial = ExtractLiteral(prop.Value, "initialValue");
                list.Add(new VariableDefinition(name, type, offset, length, endian, desc, initial));
            }
            else
            {
                var initial = ExtractLiteralFromValue(prop.Value);
                var inferredType = InferType(prop.Value);
                list.Add(new VariableDefinition(name, inferredType, -1, 0, WhfmtEndian.Inherit, "", initial));
            }
        }
        return list;
    }

    private static WhfmtValueType InferType(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => WhfmtValueType.Ascii,
        JsonValueKind.Number => el.TryGetInt32(out _) ? WhfmtValueType.Int32
                              : el.TryGetInt64(out _) ? WhfmtValueType.Int64
                              : WhfmtValueType.Float64,
        JsonValueKind.True or JsonValueKind.False => WhfmtValueType.UInt8,
        _ => WhfmtValueType.Unknown,
    };

    // -- Helpers ---------------------------------------------------------------

    private static string? GetString(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static int GetInt(JsonElement el, string prop, int fallback)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
            ? i : fallback;

    private static WhfmtEndian ParseEndian(string? raw) => raw?.ToLowerInvariant() switch
    {
        "little" or "le" => WhfmtEndian.Little,
        "big"    or "be" => WhfmtEndian.Big,
        _                => WhfmtEndian.Inherit,
    };

    private static object? ExtractLiteral(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? ExtractLiteralFromValue(v) : null;

    private static object? ExtractLiteralFromValue(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString(),
        JsonValueKind.Number => v.TryGetInt64(out var l) ? l : v.GetDouble(),
        JsonValueKind.True   => true,
        JsonValueKind.False  => false,
        JsonValueKind.Null   => null,
        _ => null,
    };
}
