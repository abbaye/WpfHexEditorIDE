// ==========================================================
// Project: WpfHexEditor.App
// File: Services/LspFirstRunService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Posts a first-run notification when bundled LSP servers are absent.
//     "Download now" action fetches OmniSharp + clangd inline via HttpClient
//     and updates the notification in-place with progress/result feedback.
//     No PowerShell execution required.
// ==========================================================

using System.IO;
using System.IO.Compression;
using System.Net.Http;
using WpfHexEditor.Editor.Core.Notifications;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Checks whether bundled LSP servers are present in the output directory and,
/// if not, posts a notification offering an inline download.
/// </summary>
internal sealed class LspFirstRunService : IDisposable
{
    private const string NotifId    = "lsp-first-run";
    private const string OmniSharpUrl =
        "https://github.com/OmniSharp/omnisharp-roslyn/releases/download/v1.39.12/omnisharp-win-x64-net6.0.zip";
    private const string ClangdUrl    =
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
        var omniExe = Path.Combine(AppContext.BaseDirectory, "tools", "lsp", "OmniSharp", "OmniSharp.exe");
        if (File.Exists(omniExe)) return;   // already installed — nothing to do

        _notifications.Post(new NotificationItem
        {
            Id       = NotifId,
            Title    = "LSP servers not installed",
            Message  = "OmniSharp (C#) and clangd (C/C++) add IntelliSense, live diagnostics, and rename. ~110 MB.",
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
        // Replace with in-progress entry (non-dismissible while downloading).
        _notifications.Post(new NotificationItem
        {
            Id           = NotifId,
            Title        = "Downloading LSP servers…",
            Message      = "OmniSharp (C#) + clangd (C/C++) — please do not close the IDE.",
            Severity     = NotificationSeverity.Info,
            IsDismissible = false,
        });

        try
        {
            var lspRoot = Path.Combine(AppContext.BaseDirectory, "tools", "lsp");

            await DownloadAndExtractAsync(
                OmniSharpUrl,
                Path.Combine(lspRoot, "OmniSharp"),
                "OmniSharp");

            await DownloadAndExtractAsync(
                ClangdUrl,
                Path.Combine(lspRoot, "clangd"),
                "clangd");

            _notifications.Post(new NotificationItem
            {
                Id       = NotifId,
                Title    = "LSP servers ready",
                Message  = "Open a .cs or .cpp file to activate IntelliSense and live diagnostics.",
                Severity = NotificationSeverity.Success,
            });
        }
        catch (Exception ex)
        {
            _notifications.Post(new NotificationItem
            {
                Id       = NotifId,
                Title    = "LSP download failed",
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
        // Update progress message in-place.
        _notifications.Post(new NotificationItem
        {
            Id           = NotifId,
            Title        = $"Downloading {label}…",
            Message      = "Please do not close the IDE.",
            Severity     = NotificationSeverity.Info,
            IsDismissible = false,
        });

        var zipPath = Path.Combine(Path.GetTempPath(), $"whe-lsp-{label}.zip");
        try
        {
            // Download to temp file.
            var bytes = await _http.GetByteArrayAsync(url).ConfigureAwait(false);
            await File.WriteAllBytesAsync(zipPath, bytes).ConfigureAwait(false);

            // Extract — flatten single top-level folder if present.
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

    public void Dispose() => _http.Dispose();
}
