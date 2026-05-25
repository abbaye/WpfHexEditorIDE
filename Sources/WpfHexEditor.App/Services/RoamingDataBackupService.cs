// ==========================================================
// Project: WpfHexEditor.App
// File: Services/RoamingDataBackupService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-05-20
// Description:
//     Backup and restore of the four managed roaming files.
//     Backup layout: %AppData%\WpfHexEditor\Backup\YYYYMMDD-HHmmss\
//     Plugins\ directory is never touched.
// ==========================================================

using System.IO;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Manages timestamped backups of the four roaming data files.
/// Never touches the Plugins\ directory.
/// </summary>
internal static class RoamingDataBackupService
{
    /// <summary>Roaming files managed by this service (relative to <see cref="RoamingRoot"/>).</summary>
    private static readonly string[] ManagedRelativePaths =
    [
        "settings.json",
        "plugin-isolation-overrides.json",
        Path.Combine("App", "layout.json"),
        Path.Combine("App", "session.json"),
    ];

    public static string RoamingRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor");

    public static string BackupRoot { get; } = Path.Combine(RoamingRoot, "Backup");

    /// <summary>
    /// Copies all existing managed files into a new timestamped backup folder.
    /// Returns the backup folder path, or null if no files existed to copy.
    /// </summary>
    public static string? CreateBackup()
    {
        var stamp     = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupDir = Path.Combine(BackupRoot, stamp);

        var (copied, _) = CopyManagedFiles(RoamingRoot, backupDir);
        if (copied == 0) return null;

        OutputLogger.Info($"[RoamingBackup] Backup created: {backupDir}");
        return backupDir;
    }

    /// <summary>
    /// Returns the most recent backup folder, or null if none exists.
    /// Folders are named YYYYMMDD-HHmmss so lexicographic descending equals chronological descending.
    /// </summary>
    public static string? GetLatestBackup()
    {
        if (!Directory.Exists(BackupRoot)) return null;

        return Directory.GetDirectories(BackupRoot)
            .OrderByDescending(d => d)
            .FirstOrDefault();
    }

    /// <summary>
    /// Restores all files from <paramref name="backupFolder"/> (or the latest backup when null)
    /// into their original locations. Returns (restoredCount, errorMessages).
    /// </summary>
    public static (int Restored, List<string> Errors) RestoreLatestBackup(string? backupFolder = null)
    {
        var latest = backupFolder ?? GetLatestBackup();
        if (latest is null) return (0, ["No backup found."]);

        var (copied, errors) = CopyManagedFiles(latest, RoamingRoot);
        OutputLogger.Info($"[RoamingBackup] Restored {copied} file(s) from '{latest}'.");
        return (copied, errors);
    }

    /// <summary>
    /// Deletes all managed files from disk.
    /// Returns (deletedFileNames, errorMessages).
    /// </summary>
    public static (List<string> Deleted, List<string> Errors) DeleteManagedFiles()
    {
        var deleted = new List<string>();
        var errors  = new List<string>();

        foreach (var relative in ManagedRelativePaths)
        {
            var path = Path.Combine(RoamingRoot, relative);
            try
            {
                File.Delete(path);
                deleted.Add(Path.GetFileName(path));
            }
            catch (FileNotFoundException) { /* already gone — nothing to delete */ }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                OutputLogger.Error($"[RoamingBackup] Failed to delete '{relative}': {ex.Message}");
            }
        }

        return (deleted, errors);
    }

    // Copies ManagedRelativePaths that exist under sourceRoot into destRoot.
    // Returns (count copied, errors). Destination subdirectories are created as needed.
    private static (int Copied, List<string> Errors) CopyManagedFiles(string sourceRoot, string destRoot)
    {
        var copied = 0;
        var errors = new List<string>();

        // Pre-create the App\ subdirectory once rather than per-file.
        Directory.CreateDirectory(Path.Combine(destRoot, "App"));

        foreach (var relative in ManagedRelativePaths)
        {
            var src = Path.Combine(sourceRoot, relative);
            if (!File.Exists(src)) continue;

            var dst = Path.Combine(destRoot, relative);
            try
            {
                File.Copy(src, dst, overwrite: true);
                copied++;
            }
            catch (Exception ex)
            {
                errors.Add($"{relative}: {ex.Message}");
                OutputLogger.Error($"[RoamingBackup] Failed to copy '{relative}': {ex.Message}");
            }
        }

        return (copied, errors);
    }
}
