// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Schema/RtfSchemaEngine.cs
// Description:
//     Generic RTF loader/serializer driven by DocumentSchemaDefinition.
//     All prefix/suffix/wrap patterns come from the .whfmt documentSchema;
//     no hardcoded RTF tokens in C#.
// ==========================================================

using System.Text;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Schema;

/// <summary>
/// Schema-driven serializer for RTF documents.
/// <see cref="SerializeBlocks"/> is used by Phase 17 RtfDocumentSaver.
/// <see cref="LoadBlocks"/> provides basic token-based parsing for
/// schema-defined groups.
/// </summary>
public static class RtfSchemaEngine
{
    /// <summary>Escapes the three characters that must be escaped in RTF text: <c>\</c>, <c>{</c>, <c>}</c>.</summary>
    public static string EscapeText(string text) =>
        text.Replace(@"\", @"\\").Replace("{", @"\{").Replace("}", @"\}");

    // ── Loading ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses RTF bytes into <see cref="DocumentBlock"/>s using the supplied schema.
    /// </summary>
    public static List<DocumentBlock> LoadBlocks(byte[] rtfBytes, DocumentSchemaDefinition schema)
    {
        var result = new List<DocumentBlock>();
        var text   = Encoding.ASCII.GetString(rtfBytes);
        var lines  = SplitRtfParagraphs(text);

        foreach (var line in lines)
        {
            var kind   = MapLineToKind(line, schema);
            var plain  = StripRtfTokens(line);
            if (string.IsNullOrWhiteSpace(plain)) continue;

            result.Add(new DocumentBlock
            {
                Kind      = kind,
                Text      = plain,
                RawOffset = -1,
                RawLength = 0
            });
        }

        return result;
    }

    private static string MapLineToKind(string line, DocumentSchemaDefinition schema)
    {
        foreach (var rule in schema.BlockMappings)
        {
            if (!string.IsNullOrEmpty(rule.RtfGroup) && line.Contains(rule.RtfGroup))
                return rule.BlockKind;
        }
        return "paragraph";
    }

    private static IEnumerable<string> SplitRtfParagraphs(string rtf)
    {
        // Split on \par token
        return rtf.Split(["\r\n", "\n", "\\par"], StringSplitOptions.RemoveEmptyEntries)
                  .Where(s => s.TrimStart().StartsWith('\\') || s.Length > 0);
    }

    private static string StripRtfTokens(string line)
    {
        // Remove RTF control words and groups
        var sb = new StringBuilder(line.Length);
        var i  = 0;
        while (i < line.Length)
        {
            if (line[i] == '{' || line[i] == '}') { i++; continue; }
            if (line[i] == '\\')
            {
                i++;
                // Skip control word (letters + optional digits)
                while (i < line.Length && (char.IsLetter(line[i]) || char.IsDigit(line[i]) || line[i] == '-'))
                    i++;
                if (i < line.Length && line[i] == ' ') i++; // skip delimiter
                continue;
            }
            sb.Append(line[i++]);
        }
        return sb.ToString().Trim();
    }

    // ── Serialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes <see cref="DocumentBlock"/>s to an RTF string using the schema's
    /// <c>serializationRules</c> and <c>attributeSerializationRules</c>.
    /// </summary>
    public static string SerializeBlocks(
        IEnumerable<DocumentBlock> blocks, DocumentSchemaDefinition schema)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"{\rtf1\ansi\deff0");
        sb.AppendLine(@"{\fonttbl{\f0 Times New Roman;}}");
        sb.AppendLine(@"{\colortbl;}");
        sb.AppendLine(@"\f0\fs24");

        foreach (var block in blocks)
        {
            var blockRtf = SerializeBlock(block, schema);
            sb.Append(blockRtf);
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string SerializeBlock(DocumentBlock block, DocumentSchemaDefinition schema)
    {
        if (!schema.SerializationRules.TryGetValue(block.Kind, out var rule))
            return SerializeAsPlainParagraph(block.Text);

        var prefix = rule.Prefix;
        var suffix = rule.Suffix;
        var text   = SerializeInlineAttributes(block.Text, block.Attributes, schema);

        return $"{prefix}{text}{suffix}";
    }

    private static string SerializeInlineAttributes(
        string text, Dictionary<string, object> attributes,
        DocumentSchemaDefinition schema)
    {
        var result = text;
        foreach (var attr in attributes)
        {
            if (!schema.AttributeSerializationRules.TryGetValue(attr.Key, out var rule))
                continue;
            if (string.IsNullOrEmpty(rule.Wrap)) continue;

            string wrapped = rule.Wrap.Replace("{text}", result);
            if (wrapped.Contains("{value}"))
            {
                string raw = attr.Value?.ToString() ?? string.Empty;
                wrapped = wrapped.Replace("{value}", ApplySerializeTransform(raw, rule.Transform));
            }
            result = wrapped;
        }
        return result;
    }

    private static string ApplySerializeTransform(string value, string transform) =>
        transform switch
        {
            // RTF stores indent / margin in twips (1 pt = 20 twips).
            "pointsToTwips" when double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pt) =>
                ((int)Math.Round(pt * 20)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "pointsToHalfPoints" when double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hp) =>
                ((int)(hp * 2)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => value
        };

    private static string SerializeAsPlainParagraph(string text) =>
        $@"\pard\plain {text}\par" + "\n";
}
