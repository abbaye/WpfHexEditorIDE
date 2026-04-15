// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Services/WhfmtAdHocFormatService.cs
// Description: Manages user-supplied adhoc .whfmt format files stored in the
//              AppData directory and optional additional search paths.
//              Provides add/remove operations and optional FileSystemWatcher
//              hot-reload support.
// Architecture: Domain service; no WPF dependencies. IDisposable for FSW cleanup.
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.Shell.Panels.Services;

/// <summary>
/// Manages user-supplied (adhoc) .whfmt format definition files.
/// Scans the user AppData directory and any additional search paths configured
/// in <see cref="WhfmtExplorerSettings"/>. Supports hot-reload via FileSystemWatcher.
/// </summary>
public sealed class WhfmtAdHocFormatService : IDisposable
{
    private static readonly string DefaultUserDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "WpfHexEditor", "FormatDefinitions");

    private readonly WhfmtExplorerSettings _settings;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    // Debounce: collapse rapid FSW events into a single notification
    private System.Threading.Timer? _debounceTimer;
    private const int DebounceMs = 500;

    public WhfmtAdHocFormatService(WhfmtExplorerSettings settings)
    {
        _settings = settings;
        EnsureUserDirExists();
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>Absolute path of the primary user format directory.</summary>
    public string UserFormatDirectory => DefaultUserDir;

    /// <summary>Returns true when the FileSystemWatcher is active.</summary>
    public bool IsWatching => _watcher is { EnableRaisingEvents: true };

    /// <summary>
    /// Fired when a format file is added, removed, or changed in any watched directory.
    /// Always raised on the thread that owns the FSW (background). Callers must marshal to UI thread.
    /// </summary>
    public event EventHandler? CatalogChanged;

    /// <summary>
    /// Returns all .whfmt file paths found across the user directory and any
    /// additional search paths defined in settings, excluding filenames in
    /// <see cref="WhfmtExplorerSettings.ExcludedFileNames"/>.
    /// </summary>
    public IReadOnlyList<string> GetUserFormatPaths()
    {
        var result = new List<string>();
        var allDirs = GetAllWatchedDirectories();

        foreach (var dir in allDirs)
        {
            if (!Directory.Exists(dir))
                continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.whfmt", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (!_settings.ExcludedFileNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    result.Add(file);
            }
        }

        return result;
    }

    /// <summary>
    /// Copies a .whfmt file from <paramref name="sourcePath"/> into the user format directory.
    /// Returns a failure result if the source file is invalid or a file with the same name already exists.
    /// </summary>
    public AdHocResult AddFormat(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return AdHocResult.Fail("Source path is empty.");

        if (!File.Exists(sourcePath))
            return AdHocResult.Fail($"File not found: {sourcePath}");

        if (!sourcePath.EndsWith(".whfmt", StringComparison.OrdinalIgnoreCase))
            return AdHocResult.Fail("Only .whfmt files are supported.");

        EnsureUserDirExists();

        var dest = Path.Combine(DefaultUserDir, Path.GetFileName(sourcePath));
        if (File.Exists(dest))
            return AdHocResult.Fail($"A format named '{Path.GetFileName(sourcePath)}' already exists in the user directory. Remove it first or rename the source file.");

        try
        {
            File.Copy(sourcePath, dest);
            return AdHocResult.Ok(dest);
        }
        catch (Exception ex)
        {
            return AdHocResult.Fail($"Copy failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a .whfmt file from the user format directory by file name (not full path).
    /// Returns a failure result if the file is not in the user directory.
    /// </summary>
    public AdHocResult RemoveFormat(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return AdHocResult.Fail("File name is empty.");

        var target = Path.Combine(DefaultUserDir, Path.GetFileName(fileName));
        if (!File.Exists(target))
            return AdHocResult.Fail($"'{fileName}' was not found in the user format directory.");

        try
        {
            File.Delete(target);
            return AdHocResult.Ok(target);
        }
        catch (Exception ex)
        {
            return AdHocResult.Fail($"Delete failed: {ex.Message}");
        }
    }

    /// <summary>Starts the FileSystemWatcher on all watched directories.</summary>
    public void StartWatching()
    {
        if (IsWatching) return;

        StopWatching();

        EnsureUserDirExists();

        // Primary directory watcher
        _watcher = new FileSystemWatcher(DefaultUserDir, "*.whfmt")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Changed += OnFileSystemEvent;
        _watcher.Renamed += OnFileSystemRenamed;
    }

    /// <summary>Stops and disposes the FileSystemWatcher.</summary>
    public void StopWatching()
    {
        if (_watcher == null) return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Created  -= OnFileSystemEvent;
        _watcher.Deleted  -= OnFileSystemEvent;
        _watcher.Changed  -= OnFileSystemEvent;
        _watcher.Renamed  -= OnFileSystemRenamed;
        _watcher.Dispose();
        _watcher = null;
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private IEnumerable<string> GetAllWatchedDirectories()
    {
        yield return DefaultUserDir;
        foreach (var extra in _settings.AdditionalSearchPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
            yield return extra;
    }

    private static void EnsureUserDirExists()
    {
        if (!Directory.Exists(DefaultUserDir))
            Directory.CreateDirectory(DefaultUserDir);
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e) => ScheduleDebounce();
    private void OnFileSystemRenamed(object sender, RenamedEventArgs e) => ScheduleDebounce();

    private void ScheduleDebounce()
    {
        _debounceTimer?.Change(DebounceMs, System.Threading.Timeout.Infinite);
        _debounceTimer ??= new System.Threading.Timer(
            _ => CatalogChanged?.Invoke(this, EventArgs.Empty),
            null, DebounceMs, System.Threading.Timeout.Infinite);
        _debounceTimer.Change(DebounceMs, System.Threading.Timeout.Infinite);
    }

    // ------------------------------------------------------------------
    // IDisposable
    // ------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatching();
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }
}

/// <summary>Result of an adhoc format add or remove operation.</summary>
public readonly record struct AdHocResult(bool Success, string? FilePath, string? Error)
{
    public static AdHocResult Ok(string filePath)   => new(true,  filePath, null);
    public static AdHocResult Fail(string error)    => new(false, null,     error);
}
