// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Services/GifExportService.cs
// Description: Exports a CaptureSession to an animated GIF without external dependencies.
// Architecture Notes:
//     GifBitmapEncoder produces GIF87a single-frame blobs. This service:
//       1. Encodes each frame to a MemoryStream via GifBitmapEncoder (UI thread).
//       2. Scans the raw bytes for the Image Descriptor marker (0x2C) to find the
//          frame payload — signature-based, not fixed offset, safe across .NET versions.
//       3. Injects a GCE (Graphic Control Extension) block immediately before 0x2C
//          to embed the per-frame delay and disposal method.
//       4. Prefixes the output with a GIF89a header (replaces GIF87a) and a
//          Netscape 2.0 loop block for animated GIF looping.
//       5. Appends GIF trailer byte 0x3B.
//     No third-party libraries required — only System.Windows.Media.Imaging.
// ==========================================================

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfHexEditor.Plugins.ScreenRecorder.Models;

namespace WpfHexEditor.Plugins.ScreenRecorder.Services;

public static class GifExportService
{
    // GIF89a magic (6 bytes)
    private static ReadOnlySpan<byte> Gif89aHeader => "GIF89a"u8;

    public static async Task ExportAsync(
        IReadOnlyList<CaptureFrame> frames,
        ExportOptions               options,
        IProgress<int>?             progress    = null,
        CancellationToken           ct          = default)
    {
        if (frames.Count == 0) throw new ArgumentException("No frames to export.", nameof(frames));

        await using var output = new FileStream(options.OutputPath, FileMode.Create, FileAccess.Write, FileShare.None);

        // Encode each frame to raw GIF87a bytes on the UI thread (BitmapSource requirement).
        var rawFrames = new List<byte[]>(frames.Count);
        foreach (var frame in frames)
        {
            ct.ThrowIfCancellationRequested();
            var scaled = options.OutputScale != 1.0
                ? FrameCaptureEngine.ScaleBitmap(frame.Bitmap, options.OutputScale)
                : frame.Bitmap;

            var raw = await EncodeFrameOnUiThreadAsync(scaled);
            rawFrames.Add(raw);
            progress?.Report(rawFrames.Count * 50 / frames.Count); // 0–50% for encoding
        }

        // Build the output GIF stream.
        WriteGif89aStream(output, rawFrames, frames, options);
        progress?.Report(100);
    }

    // ── Encoding ─────────────────────────────────────────────────────────────

    private static Task<byte[]> EncodeFrameOnUiThreadAsync(BitmapSource bitmap) =>
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // Quantize to Indexed8 for better 256-color palette.
            var quantized = new FormatConvertedBitmap(bitmap, PixelFormats.Indexed8, BitmapPalettes.Halftone256, 0);
            var encoder   = new GifBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(quantized));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }).Task;

    // ── GIF89a stream assembly ────────────────────────────────────────────────

    private static void WriteGif89aStream(
        Stream                      output,
        List<byte[]>                rawFrames,
        IReadOnlyList<CaptureFrame> frames,
        ExportOptions               options)
    {
        var first = rawFrames[0];

        // Write GIF89a header (6 bytes) — replaces GIF87a from encoder.
        output.Write(Gif89aHeader);

        // Write Logical Screen Descriptor (7 bytes) from the first frame (bytes 6–12).
        output.Write(first, 6, 7);

        // Write Global Color Table if present (flag at bit 7 of byte 10).
        var packed    = first[10];
        var hasGct    = (packed & 0x80) != 0;
        var gctSize   = hasGct ? 3 * (1 << ((packed & 0x07) + 1)) : 0;
        if (hasGct) output.Write(first, 13, gctSize);

        // Netscape 2.0 loop extension block.
        WriteNetscapeLoopBlock(output, options.LoopCount);

        // Write each frame with its GCE block.
        for (var i = 0; i < rawFrames.Count; i++)
        {
            var raw        = rawFrames[i];
            var delayMs    = i == rawFrames.Count - 1 ? options.RepeatLastFrameDelay : frames[i].Delay_ms;
            var imageStart = FindImageDescriptor(raw);
            if (imageStart < 0) continue;

            WriteGce(output, delayMs);
            output.Write(raw, imageStart, raw.Length - imageStart - 1); // -1 to skip original 0x3B trailer
        }

        // GIF trailer.
        output.WriteByte(0x3B);
    }

    // Scan for Image Descriptor marker (0x2C) — signature-based, never fixed offset.
    private static int FindImageDescriptor(byte[] raw)
    {
        for (var i = 1; i < raw.Length; i++)
            if (raw[i] == 0x2C) return i;
        return -1;
    }

    // GCE = Graphic Control Extension (8 bytes total).
    private static void WriteGce(Stream output, int delayMs)
    {
        var delay = (ushort)Math.Max(1, delayMs / 10); // GIF units: 1/100 s
        output.WriteByte(0x21); // Extension Introducer
        output.WriteByte(0xF9); // Graphic Control Label
        output.WriteByte(0x04); // Block size
        output.WriteByte(0x04); // Disposal: do not dispose; no user-input
        output.WriteByte((byte)(delay & 0xFF));
        output.WriteByte((byte)(delay >> 8));
        output.WriteByte(0x00); // Transparent color index (none)
        output.WriteByte(0x00); // Block terminator
    }

    // Netscape 2.0 Application Extension — enables looping.
    private static void WriteNetscapeLoopBlock(Stream output, int loopCount)
    {
        var loop = (ushort)loopCount; // 0 = infinite
        output.WriteByte(0x21); // Extension Introducer
        output.WriteByte(0xFF); // Application Extension Label
        output.WriteByte(0x0B); // Block size (11 bytes)
        output.Write("NETSCAPE2.0"u8);
        output.WriteByte(0x03); // Sub-block size
        output.WriteByte(0x01); // Sub-block ID
        output.WriteByte((byte)(loop & 0xFF));
        output.WriteByte((byte)(loop >> 8));
        output.WriteByte(0x00); // Block terminator
    }
}
