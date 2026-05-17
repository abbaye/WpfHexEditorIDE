// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Services/PngSequenceExportService.cs
// Description: Exports each frame as a numbered PNG file (0000.png … NNNN.png).
//              OutputScale is applied before encoding.
// ==========================================================

using System.IO;
using System.Threading;
using WpfHexEditor.Plugins.ScreenRecorder.Models;

namespace WpfHexEditor.Plugins.ScreenRecorder.Services;

public static class PngSequenceExportService
{
    public static async Task ExportAsync(
        IReadOnlyList<CaptureFrame> frames,
        ExportOptions               options,
        IProgress<int>?             progress = null,
        CancellationToken           ct       = default)
    {
        if (frames.Count == 0) throw new ArgumentException("No frames to export.", nameof(frames));

        Directory.CreateDirectory(options.OutputPath);

        // Bound concurrency to 4: encode on UI thread, write on thread pool, without OOM on large sessions.
        using var sem      = new SemaphoreSlim(4, 4);
        var       done     = 0;
        var       tasks    = new List<Task>(frames.Count);

        foreach (var frame in frames)
        {
            ct.ThrowIfCancellationRequested();
            await sem.WaitAsync(ct);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var scaled = options.OutputScale != 1.0
                        ? FrameCaptureEngine.ScaleBitmap(frame.Bitmap, options.OutputScale)
                        : frame.Bitmap;

                    var bytes    = await FrameCaptureEngine.EncodePngOnUiThreadAsync(scaled);
                    var filePath = Path.Combine(options.OutputPath, $"{frame.Index:D4}.png");
                    await File.WriteAllBytesAsync(filePath, bytes, ct);
                    progress?.Report(Interlocked.Increment(ref done) * 100 / frames.Count);
                }
                finally { sem.Release(); }
            }, ct));
        }

        await Task.WhenAll(tasks);
    }
}
