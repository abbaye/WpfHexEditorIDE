// ==========================================================
// Project: whfmt.Backfill
// File: Inferrers/NameNormalizer.cs
// Description: camelCase / PascalCase → snake_case conversion for diff/fuzz logical field names.
// ==========================================================

using System.Text;

namespace WhfmtBackfill.Inferrers;

/// <summary>Converts block <c>storeAs</c> identifiers (camelCase) to snake_case used by diff/fuzz blocks.</summary>
public static class NameNormalizer
{
    /// <summary>Convert "imageWidth" → "image_width", "IHDR_CRC" → "ihdr_crc", "Width" → "width".</summary>
    public static string ToSnakeCase(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var sb = new StringBuilder(id.Length + 4);
        for (int i = 0; i < id.Length; i++)
        {
            char c = id[i];
            if (char.IsUpper(c))
            {
                bool boundary = i > 0
                    && sb.Length > 0 && sb[^1] != '_'
                    && (char.IsLower(id[i - 1]) || (i + 1 < id.Length && char.IsLower(id[i + 1])));
                if (boundary) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (c == ' ' || c == '-' || c == '.')
            {
                if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Trim('_');
    }

    /// <summary>True for fields conventionally treated as noise in semantic diff (timestamps, padding, free text).</summary>
    public static bool IsLikelyIgnored(string snake)
    {
        if (string.IsNullOrEmpty(snake)) return false;
        return snake.Contains("time") || snake.Contains("date") || snake.Contains("stamp") ||
               snake.Contains("padding") || snake.Contains("reserved") || snake.Contains("unused") ||
               snake.Contains("comment") || snake.Contains("creator") || snake.Contains("producer") ||
               snake.Contains("modified") || snake.EndsWith("_at");
    }

    /// <summary>True for fields whose name suggests a numeric size/length/count/offset (used for boundary fuzzing).</summary>
    public static bool IsNumericMagnitude(string snake)
    {
        if (string.IsNullOrEmpty(snake)) return false;
        return snake.Contains("size") || snake.Contains("length") || snake.Contains("count") ||
               snake.Contains("offset") || snake.Contains("width") || snake.Contains("height");
    }

    /// <summary>True for value types representing a numeric integer.</summary>
    public static bool IsNumericValueType(string vt) => vt switch
    {
        "uint8" or "uint16" or "uint32" or "uint64" or
        "int8"  or "int16"  or "int32"  or "int64" => true,
        _ => false,
    };
}
