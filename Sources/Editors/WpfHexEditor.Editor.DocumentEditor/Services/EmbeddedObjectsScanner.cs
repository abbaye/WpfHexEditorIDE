// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Services/EmbeddedObjectsScanner.cs
// Description:
//     Walks a DocumentModel's block tree to enumerate embedded
//     artefacts (images, OLE objects, macros) for forensic review.
//     Independent of format: relies on block Kind + Attributes
//     populated by loaders (DocxXmlMapper, OdtXmlMapper,
//     RtfStructureBuilder).
// ==========================================================

using System.IO;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Services;

/// <summary>
/// Discovers embedded objects (images, OLE blobs, VBA macros) inside a
/// <see cref="DocumentModel"/> for the Embedded Objects review panel.
/// </summary>
public static class EmbeddedObjectsScanner
{
    // Format-specific macro entry paths — surfaced by the synthetic macro
    // entry. Selected via DocumentMetadata.MimeType so the panel doesn't
    // hard-code DOCX paths for ODF/RTF documents.
    private const string DocxMacroEntry = "word/vbaProject.bin";

    private static readonly string[] OoxmlMacroMimes =
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-word.document.macroEnabled.12",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
        "application/vnd.ms-word.template.macroEnabled.12",
    };

    /// <summary>
    /// Returns one entry per embedded artefact: images (with zipEntryName
    /// or in-memory binaryData), OLE objects, and a synthetic entry for
    /// VBA macros when <see cref="DocumentMetadata.HasMacros"/> is set.
    /// </summary>
    public static IReadOnlyList<EmbeddedObjectEntry> Scan(DocumentModel? model)
    {
        var list = new List<EmbeddedObjectEntry>();
        if (model is null) return list;

        Walk(model.Blocks, list);

        if (model.Metadata?.HasMacros == true)
        {
            string? macroPath = ResolveMacroEntryPath(model.Metadata);
            list.Add(new EmbeddedObjectEntry
            {
                Kind         = DocumentBlockKinds.Macro,
                Name         = macroPath is null ? "macros" : Path.GetFileName(macroPath),
                SizeBytes    = -1,
                ZipEntryName = macroPath,
            });
        }
        return list;
    }

    /// <summary>
    /// Resolves the well-known archive entry path that holds macro bytes for
    /// the document's source format. Returns null if the format doesn't have
    /// a single canonical macro entry (e.g. ODT macros live under Basic/).
    /// </summary>
    private static string? ResolveMacroEntryPath(DocumentMetadata meta)
    {
        if (string.IsNullOrEmpty(meta.MimeType)) return DocxMacroEntry;
        foreach (var m in OoxmlMacroMimes)
            if (meta.MimeType.Equals(m, StringComparison.OrdinalIgnoreCase))
                return DocxMacroEntry;
        return null;
    }

    private static void Walk(IEnumerable<DocumentBlock> blocks, List<EmbeddedObjectEntry> sink)
    {
        foreach (var b in blocks)
        {
            if (b.Kind == DocumentBlockKinds.Image || b.Kind == DocumentBlockKinds.ObjectEmbed)
                sink.Add(BuildEntry(b));
            if (b.Children.Count > 0) Walk(b.Children, sink);
        }
    }

    private static EmbeddedObjectEntry BuildEntry(DocumentBlock b)
    {
        var entry = new EmbeddedObjectEntry
        {
            Kind  = b.Kind == DocumentBlockKinds.ObjectEmbed ? "OLE" : DocumentBlockKinds.Image,
            Name  = ExtractName(b),
            Block = b
        };
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.ZipEntryName, out var ze) && ze is string zes)
            entry.ZipEntryName = zes;
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.BinaryData, out var bd) && bd is byte[] bytes)
        {
            entry.InlineData = bytes;
            entry.SizeBytes  = bytes.Length;
        }
        else if (b.Attributes.TryGetValue(DocumentBlockAttributes.BinarySize, out var bs))
            entry.SizeBytes = bs is int isz ? isz : -1;
        else
            entry.SizeBytes = b.RawLength;
        return entry;
    }

    private static string ExtractName(DocumentBlock b)
    {
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.ZipEntryName, out var ze) && ze is string s && !string.IsNullOrEmpty(s))
            return Path.GetFileName(s);
        if (!string.IsNullOrEmpty(b.Text)) return b.Text;
        return b.Kind;
    }
}

/// <summary>A single embedded artefact discovered by <see cref="EmbeddedObjectsScanner"/>.</summary>
public sealed class EmbeddedObjectEntry
{
    public string  Kind         { get; set; } = string.Empty;
    public string  Name         { get; set; } = string.Empty;
    public int     SizeBytes    { get; set; }
    public string? ZipEntryName { get; set; }
    public byte[]? InlineData   { get; set; }
    public DocumentBlock? Block { get; set; }

    /// <summary>
    /// SHA-256 hex digest of the embedded bytes. Populated lazily by the
    /// dialog after extraction — empty until <see cref="ComputeHash"/> runs.
    /// </summary>
    public string Sha256 { get; private set; } = string.Empty;

    /// <summary>Computes <see cref="Sha256"/> over the given bytes. Idempotent.</summary>
    public void ComputeHash(byte[] bytes)
    {
        if (!string.IsNullOrEmpty(Sha256) || bytes is null || bytes.Length == 0) return;
        Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>Display string for the Source column: zip path or raw byte offset.</summary>
    public string Source => ZipEntryName
        ?? (Block is not null ? $"@0x{Block.RawOffset:X}" : string.Empty);

    public string SizeText => SizeBytes < 0
        ? "—"
        : SizeBytes < 1024
            ? FormattableString.Invariant($"{SizeBytes} B")
            : SizeBytes < 1024 * 1024
                ? FormattableString.Invariant($"{SizeBytes / 1024.0:F1} KB")
                : FormattableString.Invariant($"{SizeBytes / 1024.0 / 1024.0:F2} MB");
}
