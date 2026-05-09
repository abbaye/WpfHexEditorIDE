// ==========================================================
// Project: whfmt.Backfill
// File: Emit/JsonBlockEmitter.cs
// Description: Renders inferred diff/repair/fuzz models as pretty-printed JSON fragments.
// Architecture: Uses System.Text.Json with indented output; produces "key": <value> fragments.
// ==========================================================

using System.Text;
using System.Text.Json;
using WhfmtBackfill.Inferrers;

namespace WhfmtBackfill.Emit;

/// <summary>Emits JSON fragments for inferred whfmt blocks.</summary>
public static class JsonBlockEmitter
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented        = true,
        Encoder              = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Emit the diff block as JSON: <c>"diff": { ... }</c>. Indents content with the given prefix.</summary>
    public static string EmitDiff(InferredDiff diff, string indent = "  ")
    {
        var obj = new
        {
            keyFields    = diff.KeyFields,
            ignoreFields = diff.IgnoreFields,
            note         = diff.Note,
        };
        return EmitProperty("diff", obj, indent);
    }

    /// <summary>Emit the repair block as a JSON array fragment.</summary>
    public static string EmitRepair(IReadOnlyList<RepairRule> rules, string indent = "  ")
    {
        var serializable = rules.Select(r => BuildRepairDto(r)).ToList();
        return EmitProperty("repair", serializable, indent);
    }

    /// <summary>Emit the fuzz block.</summary>
    public static string EmitFuzz(InferredFuzz fuzz, string indent = "  ")
    {
        var obj = new
        {
            preserveChecksums   = fuzz.PreserveChecksums,
            maxMutationsPerFile = fuzz.MaxMutationsPerFile,
            strategies          = fuzz.Strategies.Select(s => BuildStrategyDto(s)).ToList(),
            note                = fuzz.Note,
        };
        return EmitProperty("fuzz", obj, indent);
    }

    private static object BuildRepairDto(RepairRule r)
    {
        var dict = new Dictionary<string, object?>
        {
            ["name"]        = r.Name,
            ["trigger"]     = r.Trigger,
            ["action"]      = r.Action,
        };
        if (r.Target     is not null) dict["target"]     = r.Target;
        if (r.Value      is not null) dict["value"]      = r.Value;
        if (r.Length     is not null) dict["length"]     = r.Length;
        if (r.Algorithm  is not null) dict["algorithm"]  = r.Algorithm;
        if (r.DataOffset is not null) dict["dataOffset"] = r.DataOffset;
        if (r.DataLength is not null) dict["dataLength"] = r.DataLength;
        dict["description"] = r.Description;
        return dict;
    }

    private static object BuildStrategyDto(FuzzStrategy s)
    {
        var dict = new Dictionary<string, object?>
        {
            ["field"]    = s.Field,
            ["mutation"] = s.Mutation,
        };
        if (s.Rate is not null) dict["rate"] = s.Rate;
        dict["description"] = s.Description;
        return dict;
    }

    private static string EmitProperty(string key, object value, string indent)
    {
        string inner = JsonSerializer.Serialize(value, IndentedOptions);
        // Indent each line of inner with `indent`
        var sb = new StringBuilder();
        sb.Append(indent).Append('"').Append(key).Append("\": ");
        var lines = inner.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (i == 0) sb.Append(line);
            else        sb.Append('\n').Append(indent).Append(line);
        }
        return sb.ToString();
    }
}
