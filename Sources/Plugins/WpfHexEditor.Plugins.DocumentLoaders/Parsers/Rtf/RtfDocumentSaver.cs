// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Rtf/RtfDocumentSaver.cs
// Description:
//     IDocumentSaver for RTF files.
//     Uses RtfSchemaEngine.SerializeBlocks() driven by RTF.whfmt documentSchema.
//     No hardcoded RTF tokens in C#.
// ==========================================================

using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Schema;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Rtf;

public sealed class RtfDocumentSaver : IDocumentSaver
{
    public string SaverName => "RTF Document Saver";

    public IReadOnlyList<string> SupportedExtensions { get; } = [".rtf"];

    public bool CanSave(string filePath) =>
        Path.GetExtension(filePath).Equals(".rtf", StringComparison.OrdinalIgnoreCase);

    public async Task SaveAsync(DocumentModel model, Stream output, CancellationToken ct = default)
    {
        var schema  = LoadSchema("RTF.whfmt");
        string rtf  = schema is not null
            ? RtfSchemaEngine.SerializeBlocks(model.Blocks, schema)
            : FallbackSerialize(model);

        await using var writer = new StreamWriter(output, leaveOpen: true);
        await writer.WriteAsync(rtf);
    }

    private static DocumentSchemaDefinition? LoadSchema(string fileName)
    {
        var catalog = EmbeddedFormatCatalog.Instance;
        var key = catalog.GetAll()
            .Select(e => e.ResourceKey)
            .FirstOrDefault(k => k is not null &&
                k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (key is null) return null;
        try { return DocumentSchemaReader.ReadFromJson(catalog.GetJson(key), fileName); }
        catch { return null; }
    }

    private static string FallbackSerialize(DocumentModel model)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(@"{\rtf1\ansi\deff0");
        sb.AppendLine(@"{\fonttbl{\f0 Times New Roman;}}");
        sb.AppendLine(@"\f0\fs24");
        foreach (var block in model.Blocks)
            sb.AppendLine($@"\pard\plain {EscapeRtf(block.Text)}\par");
        sb.Append('}');
        return sb.ToString();
    }

    private static string EscapeRtf(string text)
    {
        return text.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}");
    }
}
