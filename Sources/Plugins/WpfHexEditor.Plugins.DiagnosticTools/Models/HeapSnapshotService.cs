// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Models/HeapSnapshotService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Triggers a GC heap dump (gcdump) via DiagnosticsClient.WriteDump().
//     Writes the dump to %TEMP%\WpfHexEditor\<pid>_<timestamp>.gcdump.
//
// Architecture Notes:
//     Async fire-and-forget: caller awaits Task<string?> (returns dump path).
//     Heap dumps may take several seconds on large heaps — progress callback
//     is forwarded to the VM status line.
// ==========================================================

using System.IO;
using Microsoft.Diagnostics.NETCore.Client;

namespace WpfHexEditor.Plugins.DiagnosticTools.Models;

/// <summary>
/// Captures a GC heap snapshot for the target process using EventPipe.
/// Returns the path of the written <c>.gcdump</c> file, or <c>null</c> on failure.
/// </summary>
internal static class HeapSnapshotService
{
    public static async Task<string?> CaptureAsync(
        int                     pid,
        Action<string>          statusCallback,
        CancellationToken       ct = default)
    {
        try
        {
            var outDir = Path.Combine(Path.GetTempPath(), "WpfHexEditor");
            Directory.CreateDirectory(outDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dumpPath  = Path.Combine(outDir, $"{pid}_{timestamp}.gcdump");

            statusCallback($"Capturing heap snapshot → {Path.GetFileName(dumpPath)} …");

            var client = new DiagnosticsClient(pid);

            await Task.Run(() =>
            {
                client.WriteDump(
                    DumpType.WithHeap,
                    dumpPath,
                    logDumpGeneration: false);
            }, ct).ConfigureAwait(false);

            statusCallback($"Heap snapshot saved: {dumpPath}");
            return dumpPath;
        }
        catch (OperationCanceledException)
        {
            statusCallback("Heap snapshot cancelled.");
            return null;
        }
        catch (Exception ex)
        {
            statusCallback($"Heap snapshot failed: {ex.Message}");
            return null;
        }
    }
}
