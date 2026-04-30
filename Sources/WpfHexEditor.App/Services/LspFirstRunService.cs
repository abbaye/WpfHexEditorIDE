// ==========================================================
// Project: WpfHexEditor.App
// File: Services/LspFirstRunService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6, Claude Opus 4.6
// Created: 2026-03-29
// Description:
//     Posts a first-run notification when bundled LSP servers are absent.
//     "Download now" action fetches clangd inline via HttpClient.
//     C# and VB.NET use in-process Roslyn (no external server needed).
// ==========================================================

using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using WpfHexEditor.Editor.Core.Notifications;
using WpfHexEditor.App.Properties;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Checks whether bundled LSP servers are present in the output directory and,
/// if not, posts a notification offering an inline download.
/// C# and VB.NET use in-process Roslyn — only clangd requires download.
/// </summary>
internal sealed class LspFirstRunService : IDisposable
{
    private const string NotifId    = "lsp-first-run";
    private const string ClangdUrl  =
        "https://github.com/clangd/clangd/releases/download/18.1.3/clangd-windows-18.1.3.zip";

    private readonly INotificationService _notifications;
    private readonly HttpClient           _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    internal LspFirstRunService(INotificationService notifications)
        => _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks for bundled servers and posts the first-run notification if absent.
    /// Call once at IDE startup (after plugin system init).
    /// </summary>
    public void CheckAndNotify()
    {
        var clangdExe = Path.Combine(AppContext.BaseDirectory, "tools", "lsp", "clangd", "clangd.exe");
        if (File.Exists(clangdExe)) return;   // already installed — nothing to do

        _notifications.Post(new NotificationItem
        {
            Id       = NotifId,
            Title    = AppResources.App_Lsp_CppNotInstalled,
            Message  = "clangd adds IntelliSense, live diagnostics, and rename for C/C++. ~50 MB. (C#/VB.NET use built-in Roslyn.)",
            Severity = NotificationSeverity.Info,
            Actions  =
            [
                new NotificationAction("Download now",    () => DownloadAsync(),        IsDefault: true),
                new NotificationAction("Remind me later", () => DismissAsync()),
            ],
        });
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private Task DismissAsync()
    {
        _notifications.Dismiss(NotifId);
        return Task.CompletedTask;
    }

    private async Task DownloadAsync()
    {
        try
        {
            var lspRoot = Path.Combine(AppContext.BaseDirectory, "tools", "lsp");

            await DownloadAndExtractAsync(
                ClangdUrl,
                Path.Combine(lspRoot, "clangd"),
                "clangd");

            _notifications.Post(new NotificationItem
            {
                Id       = NotifId,
                Title    = AppResources.App_Lsp_ClangdReady,
                Message  = "Open a .cpp or .h file to activate C/C++ IntelliSense.",
                Severity = NotificationSeverity.Success,
            });
        }
        catch (Exception ex)
        {
            _notifications.Post(new NotificationItem
            {
                Id       = NotifId,
                Title    = AppResources.App_Lsp_ClangdFailed,
                Message  = ex.Message,
                Severity = NotificationSeverity.Error,
                Actions  =
                [
                    new NotificationAction("Retry", () => DownloadAsync(), IsDefault: true),
                    new NotificationAction("Dismiss", () => DismissAsync()),
                ],
            });
        }
    }

    private async Task DownloadAndExtractAsync(string url, string destDir, string label)
    {
        PostProgress(label, -1, 0, null);

        var zipPath = Path.Combine(Path.GetTempPath(), $"whe-lsp-{label}.zip");
        try
        {
            // ── Stream-based download with progress ──────────────────────
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var dest   = File.Create(zipPath);

            var buffer       = new byte[81920];
            long downloaded  = 0;
            double lastPct   = -1;
            var sw           = Stopwatch.StartNew();

            int read;
            while ((read = await source.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                downloaded += read;

                double pct = totalBytes.HasValue ? (double)downloaded / totalBytes.Value : -1;

                // Throttle: post only on ≥1% change or ≥500ms elapsed
                if (Math.Abs(pct - lastPct) >= 0.01 || sw.ElapsedMilliseconds >= 500)
                {
                    PostProgress(label, pct, downloaded, totalBytes);
                    lastPct = pct;
                    sw.Restart();
                }
            }

            // ── Extract ──────────────────────────────────────────────────
            dest.Close();

            _notifications.Post(new NotificationItem
            {
                Id             = NotifId,
                Title          = $"Extracting {label}…",
                Severity       = NotificationSeverity.Info,
                IsDismissible  = false,
                Progress       = -1,
                IsActiveDownload = true,
            });

            var tmpExtract = zipPath + "_extract";
            if (Directory.Exists(tmpExtract)) Directory.Delete(tmpExtract, recursive: true);

            ZipFile.ExtractToDirectory(zipPath, tmpExtract);

            if (Directory.Exists(destDir)) Directory.Delete(destDir, recursive: true);
            Directory.CreateDirectory(destDir);

            var children = Directory.GetDirectories(tmpExtract);
            var srcDir   = children.Length == 1 ? children[0] : tmpExtract;

            foreach (var file in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(srcDir, file);
                var target   = Path.Combine(destDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }

            Directory.Delete(tmpExtract, recursive: true);
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    private void PostProgress(string label, double progress, long downloaded, long? totalBytes)
    {
        string message = totalBytes.HasValue && progress >= 0
            ? $"{downloaded / (1024.0 * 1024):F1} / {totalBytes.Value / (1024.0 * 1024):F1} MB"
            : $"{downloaded / (1024.0 * 1024):F1} MB downloaded";

        _notifications.Post(new NotificationItem
        {
            Id               = NotifId,
            Title            = $"Downloading {label}…",
            Message          = message,
            Severity         = NotificationSeverity.Info,
            IsDismissible    = false,
            Progress         = progress,
            IsActiveDownload = true,
        });
    }

    public void Dispose() => _http.Dispose();
}
