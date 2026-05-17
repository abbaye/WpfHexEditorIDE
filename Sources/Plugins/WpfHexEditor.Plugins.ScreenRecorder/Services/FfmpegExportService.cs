// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Services/FfmpegExportService.cs
// Description: Exports a CaptureSession to MP4 via ffmpeg.
// Architecture Notes:
//     Writes frames to a temp PNG sequence, shells ffmpeg with -framerate + -i pattern,
//     then cleans up the temp dir. IsAvailable probes ffmpeg PATH (or custom path
//     from ScreenRecorderOptions) on first call; result is cached.
//     Non-activating: does not block the UI thread; progress is reported via IProgress<int>.
// ==========================================================

using System.Diagnostics;
using System.IO;
using WpfHexEditor.Plugins.ScreenRecorder.Models;
using WpfHexEditor.Plugins.ScreenRecorder.Options;

namespace WpfHexEditor.Plugins.ScreenRecorder.Services;

public static class FfmpegExportService
{
    private static bool?   _isAvailable;
    private static string? _resolvedFfmpegPath;

    public static bool IsAvailable => _isAvailable ??= CheckAvailable();

    public static void RefreshAvailability() { _isAvailable = null; _resolvedFfmpegPath = null; }

    public static async Task ExportAsync(
        IReadOnlyList<CaptureFrame> frames,
        ExportOptions               options,
        int                         fps      = 10,
        IProgress<int>?             progress = null,
        CancellationToken           ct       = default)
    {
        if (!IsAvailable) throw new InvalidOperationException("ffmpeg is not available on PATH.");
        if (frames.Count == 0) throw new ArgumentException("No frames to export.", nameof(frames));

        var tempDir = Path.Combine(Path.GetTempPath(), $"whscr_mp4_{Guid.NewGuid():N}");
        try
        {
            // Step 1: export PNG sequence to temp dir (0–80%).
            var pngOptions = options with { OutputPath = tempDir };
            await PngSequenceExportService.ExportAsync(frames, pngOptions,
                new Progress<int>(p => progress?.Report(p * 80 / 100)), ct);

            // Step 2: run ffmpeg (80–100%).
            ct.ThrowIfCancellationRequested();
            await RunFfmpegAsync(_resolvedFfmpegPath ?? "ffmpeg", tempDir, options.OutputPath, fps, ct);
            progress?.Report(100);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool CheckAvailable()
    {
        var configured = ScreenRecorderOptions.Instance.FfmpegPath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            _resolvedFfmpegPath = configured;
            return true;
        }

        // Probe via process — ffmpeg exits 0 when given -version.
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            });
            p?.WaitForExit(3000);
            if (p?.ExitCode == 0) { _resolvedFfmpegPath = "ffmpeg"; return true; }
            return false;
        }
        catch { return false; }
    }

    private static async Task RunFfmpegAsync(string ffmpeg, string framesDir, string outputPath, int fps, CancellationToken ct)
    {
        var args = $"-y -framerate {fps} -i \"{Path.Combine(framesDir, "%04d.png")}\" " +
                   $"-c:v libx264 -pix_fmt yuv420p -movflags +faststart \"{outputPath}\"";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(ffmpeg, args)
            {
                UseShellExecute       = false,
                RedirectStandardError = true,
                CreateNoWindow        = true
            }
        };

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start ffmpeg process at '{ffmpeg}'.");

        // Read stderr to prevent deadlock; content is only used for error reporting.
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}.\n{stderr}");
    }
}
