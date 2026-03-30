// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Rtf/RtfDocumentLoader.cs
// Description:
//     IDocumentLoader for RTF files.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Rtf;

public sealed class RtfDocumentLoader : IDocumentLoader
{
    public string LoaderName => "RTF Document Loader";

    public IReadOnlyList<string> SupportedExtensions { get; } = ["rtf"];

    public bool CanLoad(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".rtf", StringComparison.OrdinalIgnoreCase);

    public async Task LoadAsync(
        string            filePath,
        Stream            stream,
        DocumentModel     target,
        CancellationToken ct = default)
    {
        byte[] rawBytes = await ReadAllBytesAsync(stream, ct);
        using var ms = new MemoryStream(rawBytes, writable: false);

        if (!IsRtfSignature(rawBytes))
            throw new InvalidDataException("Stream does not appear to be valid RTF (missing {\\rtf header).");

        ms.Position = 0;
        var tokenizer        = new RtfTokenizer(ms);
        var builder          = new BinaryMapBuilder();
        var structureBuilder = new RtfStructureBuilder();

        var (roots, metadata) = await Task.Run(
            () => structureBuilder.Build(tokenizer, builder, ct), ct);

        target.FilePath = filePath;
        target.Metadata = metadata with
        {
            MimeType      = "application/rtf",
            FormatVersion = DetectRtfVersion(rawBytes)
        };

        foreach (var block in roots)
            target.Blocks.Add(block);

        target.BinaryMap.MergeFrom(builder.Build());

        var alerts = new ForensicAnalyzer().Analyze(target, rawBytes);
        target.SetForensicAlerts(alerts);
    }

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
        ReadOnlySpan<byte> sig = "{\\rtf"u8;
        return raw.Length >= sig.Length && raw[..sig.Length].SequenceEqual(sig);
    }

    private static string DetectRtfVersion(ReadOnlySpan<byte> raw)
    {
        ReadOnlySpan<byte> marker = "\\rtf"u8;
        int idx = raw.IndexOf(marker);
        if (idx >= 0 && idx + marker.Length < raw.Length)
        {
            byte v = raw[idx + marker.Length];
            if (v >= '0' && v <= '9') return ((char)v).ToString();
        }
        return "1";
    }
}
