// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Services/SessionSerializer.cs
// Description: Reads and writes .whscr session archives.
//              Format: 8-byte WHSC magic header + ZIP stream.
//              ZIP contents: manifest.json, frames/meta.json, frames/0000.png...
// Architecture Notes:
//     Magic bytes are written/verified before the ZipArchive to allow whfmt detection.
//     PngBitmapEncoder runs on the UI thread for WPF threading safety.
//     Frames are lazy-decoded on load (full bitmap created on first Preview access).
// ==========================================================

using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Media.Imaging;
using WpfHexEditor.Plugins.ScreenRecorder.Models;

namespace WpfHexEditor.Plugins.ScreenRecorder.Services;

public static class SessionSerializer
{
    // "WHSC" + "R1" + 0x00 0x00
    private static readonly byte[] Magic = [0x57, 0x48, 0x53, 0x43, 0x52, 0x31, 0x00, 0x00];

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static async Task SaveAsync(CaptureSession session, string path, CancellationToken ct = default)
    {
        await using var file   = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.WriteAsync(Magic, ct);

        using var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true);
        await WriteManifestAsync(zip, session, ct);
        await WriteFrameMetaAsync(zip, session, ct);
        await WriteFramesAsync(zip, session, ct);
    }

    public static async Task<CaptureSession> LoadAsync(string path, CancellationToken ct = default)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        ValidateMagic(file);

        using var zip      = new ZipArchive(new SubStream(file, Magic.Length), ZipArchiveMode.Read);
        var manifest       = await ReadManifestAsync(zip, ct);
        var frameMetas     = await ReadFrameMetaAsync(zip, ct);
        var session        = BuildSession(manifest);

        foreach (var meta in frameMetas.OrderBy(m => m.Index))
        {
            ct.ThrowIfCancellationRequested();
            var bitmap  = await ReadFramePngAsync(zip, meta.Index, ct);
            var frame   = new CaptureFrame(meta.Index, bitmap, meta.Delay_ms, meta.Label, meta.Timestamp);
            session.AddFrame(frame);
        }

        return session;
    }

    // ── Write helpers ─────────────────────────────────────────────────────────

    private static async Task WriteManifestAsync(ZipArchive zip, CaptureSession session, CancellationToken ct)
    {
        var manifest = new SessionManifest
        {
            CreatedAt              = session.CreatedAt,
            FrameCount             = session.Frames.Count,
            GlobalDelay_ms         = session.GlobalDelay,
            LoopCount              = session.LoopCount,
            RepeatLastFrameDelay_ms = session.RepeatLastFrameDelay,
            OutputScale            = session.OutputScale,
            Mode                   = session.Mode.ToString(),
            CaptureRegion          = new SessionManifest.RegionDto
            {
                X      = session.Region.X,
                Y      = session.Region.Y,
                Width  = session.Region.Width,
                Height = session.Region.Height
            }
        };

        var entry = zip.CreateEntry("manifest.json", CompressionLevel.Fastest);
        await using var s = entry.Open();
        await JsonSerializer.SerializeAsync(s, manifest, JsonOpts, ct);
    }

    private static async Task WriteFrameMetaAsync(ZipArchive zip, CaptureSession session, CancellationToken ct)
    {
        var metas = session.Frames.Select(f => new FrameMeta
        {
            Index     = f.Index,
            Delay_ms  = f.Delay_ms,
            Label     = f.Label,
            Timestamp = f.Timestamp
        }).ToList();

        var entry = zip.CreateEntry("frames/meta.json", CompressionLevel.Fastest);
        await using var s = entry.Open();
        await JsonSerializer.SerializeAsync(s, metas, JsonOpts, ct);
    }

    private static async Task WriteFramesAsync(ZipArchive zip, CaptureSession session, CancellationToken ct)
    {
        foreach (var frame in session.Frames)
        {
            ct.ThrowIfCancellationRequested();
            var entryName = $"frames/{frame.Index:D4}.png";
            var entry     = zip.CreateEntry(entryName, CompressionLevel.NoCompression); // PNG already compressed
            await using var s = entry.Open();
            await EncodePngAsync(frame.Bitmap, s);
        }
    }

    // ── Read helpers ──────────────────────────────────────────────────────────

    private static void ValidateMagic(Stream stream)
    {
        var buf = new byte[Magic.Length];
        if (stream.Read(buf, 0, buf.Length) < buf.Length || !buf.AsSpan(0, 4).SequenceEqual(Magic.AsSpan(0, 4)))
            throw new InvalidDataException("Not a valid .whscr file (bad magic signature).");
    }

    private static async Task<SessionManifest> ReadManifestAsync(ZipArchive zip, CancellationToken ct)
    {
        var entry = zip.GetEntry("manifest.json")
            ?? throw new InvalidDataException(".whscr archive is missing manifest.json.");
        await using var s = entry.Open();
        return await JsonSerializer.DeserializeAsync<SessionManifest>(s, cancellationToken: ct)
            ?? throw new InvalidDataException("manifest.json deserialized as null.");
    }

    private static async Task<List<FrameMeta>> ReadFrameMetaAsync(ZipArchive zip, CancellationToken ct)
    {
        var entry = zip.GetEntry("frames/meta.json");
        if (entry is null) return [];
        await using var s = entry.Open();
        return await JsonSerializer.DeserializeAsync<List<FrameMeta>>(s, cancellationToken: ct) ?? [];
    }

    private static async Task<BitmapSource> ReadFramePngAsync(ZipArchive zip, int index, CancellationToken ct)
    {
        var entry = zip.GetEntry($"frames/{index:D4}.png")
            ?? throw new InvalidDataException($"Missing frame {index:D4}.png in archive.");

        await using var s   = entry.Open();
        using var       ms  = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        ms.Position = 0;

        // BitmapDecoder must run on a thread with access to WPF imaging
        return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return decoder.Frames[0];
        });
    }

    private static Task EncodePngAsync(BitmapSource bitmap, Stream target) =>
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(target);
        }).Task;

    private static CaptureSession BuildSession(SessionManifest m) => new()
    {
        GlobalDelay          = m.GlobalDelay_ms,
        LoopCount            = m.LoopCount,
        RepeatLastFrameDelay = m.RepeatLastFrameDelay_ms,
        OutputScale          = m.OutputScale,
        Mode                 = Enum.TryParse<RecordingMode>(m.Mode, out var mode) ? mode : RecordingMode.Screenshot,
        Region               = new CaptureRegion(m.CaptureRegion.X, m.CaptureRegion.Y,
                                                 m.CaptureRegion.Width, m.CaptureRegion.Height)
    };
}

// ── SubStream: skips the 8-byte magic header for ZipArchive ──────────────────

file sealed class SubStream(Stream inner, long offset) : Stream
{
    private long _position;

    public override bool CanRead  => inner.CanRead;
    public override bool CanSeek  => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length   => inner.Length - offset;

    public override long Position
    {
        get => _position;
        set { inner.Position = value + offset; _position = value; }
    }

    public override int Read(byte[] buffer, int bufOffset, int count)
    {
        inner.Position = _position + offset;
        var n = inner.Read(buffer, bufOffset, count);
        _position += n;
        return n;
    }

    public override long Seek(long off, SeekOrigin origin)
    {
        var abs = origin switch
        {
            SeekOrigin.Begin   => off,
            SeekOrigin.Current => _position + off,
            SeekOrigin.End     => Length + off,
            _                  => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        Position = abs;
        return _position;
    }

    public override void Flush()                          => inner.Flush();
    public override void SetLength(long value)            => throw new NotSupportedException();
    public override void Write(byte[] buf, int off, int c) => throw new NotSupportedException();
}
