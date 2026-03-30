// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoader.Rtf
// File: Parsers/RtfDocumentLoader.cs
// Description:
//     IDocumentLoader implementation for RTF files.
//     Orchestrates RtfTokenizer → RtfStructureBuilder → BinaryMapBuilder
//     to fully populate DocumentModel with offset-accurate blocks.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Plugins.DocumentLoader.Rtf.Parsers;

/// <summary>
/// Loads RTF files into a <see cref="DocumentModel"/> with full binary-map support.
/// </summary>
public sealed class RtfDocumentLoader : IDocumentLoader
{
    // ── IDocumentLoader ────────────────────────────────────────────────────

    public string LoaderName => "RTF Document Loader";

    public IReadOnlyList<string> SupportedExtensions { get; } =
        ["rtf"];

    public bool CanLoad(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return string.Equals(ext, ".rtf", StringComparison.OrdinalIgnoreCase);
    }

    public async Task LoadAsync(
        string           filePath,
        Stream           stream,
        DocumentModel    target,
        CancellationToken ct = default)
    {
        // ── 1. Read raw bytes for forensic analysis ──────────────────────
        byte[] rawBytes = await ReadAllBytesAsync(stream, ct);
        using var ms = new MemoryStream(rawBytes, writable: false);

        // ── 2. Quick RTF signature check ─────────────────────────────────
        if (!IsRtfSignature(rawBytes))
            throw new InvalidDataException("Stream does not appear to be valid RTF (missing {\\rtf header).");

        // ── 3. Tokenize + build block tree ────────────────────────────────
        ms.Position = 0;
        var tokenizer  = new RtfTokenizer(ms);
        var builder    = new BinaryMapBuilder();
        var structureBuilder = new RtfStructureBuilder();

        var (roots, metadata) = await Task.Run(
            () => structureBuilder.Build(tokenizer, builder, ct), ct);

        // ── 4. Populate model ─────────────────────────────────────────────
        target.FilePath = filePath;
        target.Metadata = metadata with
        {
            MimeType = "application/rtf",
            FormatVersion = DetectRtfVersion(rawBytes)
        };

        foreach (var block in roots)
            target.Blocks.Add(block);

        var binaryMap = builder.Build();
        target.BinaryMap.MergeFrom(binaryMap);

        // ── 5. Forensic analysis ──────────────────────────────────────────
        var analyzer = new ForensicAnalyzer();
        var alerts   = analyzer.Analyze(target, rawBytes);
        target.SetForensicAlerts(alerts);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)
    {
        if (stream is MemoryStream ms && ms.TryGetBuffer(out _))
            return ms.ToArray();

        using var buf = new MemoryStream();
        await stream.CopyToAsync(buf, ct);
        return buf.ToArray();
    }

    private static bool IsRtfSignature(ReadOnlySpan<byte> raw)
    {
        // RTF files begin with "{\rtf"
        ReadOnlySpan<byte> sig = "{\\rtf"u8;
        return raw.Length >= sig.Length && raw[..sig.Length].SequenceEqual(sig);
    }

    private static string DetectRtfVersion(ReadOnlySpan<byte> raw)
    {
        // Look for \rtfN control word to extract version digit
        ReadOnlySpan<byte> marker = "\\rtf"u8;
        int idx = raw.IndexOf(marker);
        if (idx >= 0 && idx + marker.Length < raw.Length)
        {
            byte versionByte = raw[idx + marker.Length];
            if (versionByte >= '0' && versionByte <= '9')
                return ((char)versionByte).ToString();
        }
        return "1";
    }
}
