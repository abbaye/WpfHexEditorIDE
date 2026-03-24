// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Adapters/DebugAdapterLocator.cs
// Description:
//     Locates the netcoredbg or vsdbg debug adapter executable.
//     Search order: user override → PATH → DOTNET_ROOT shared → vsdbg.
// ==========================================================

namespace WpfHexEditor.Core.Debugger.Adapters;

/// <summary>
/// Discovers the .NET debug adapter (netcoredbg or vsdbg) on the current machine.
/// </summary>
public static class DebugAdapterLocator
{
    /// <summary>
    /// Tries to locate the debug adapter executable.
    /// Returns the full path or null when not found.
    /// </summary>
    /// <param name="overridePath">User-configured path (checked first).</param>
    public static string? Locate(string? overridePath = null)
    {
        // 1. User override
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return overridePath;

        // 2. netcoredbg in PATH
        var inPath = FindInPath("netcoredbg");
        if (inPath is not null) return inPath;

        // 3. netcoredbg next to dotnet root
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
                      ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");

        foreach (var candidate in EnumerateNetcoredbgCandidates(dotnetRoot))
            if (File.Exists(candidate)) return candidate;

        // 4. vsdbg (VS debugger, Windows only)
        var vsdbg = FindVsdbg();
        if (vsdbg is not null) return vsdbg;

        return null;
    }

    /// <summary>Returns the detected adapter type ("netcoredbg" / "vsdbg" / null).</summary>
    public static string? DetectAdapterType(string adapterPath) =>
        Path.GetFileNameWithoutExtension(adapterPath).ToLowerInvariant() switch
        {
            "netcoredbg" => "netcoredbg",
            "vsdbg"      => "vsdbg",
            _            => null
        };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? FindInPath(string exe)
    {
        var exeName = OperatingSystem.IsWindows() ? exe + ".exe" : exe;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var full = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private static IEnumerable<string> EnumerateNetcoredbgCandidates(string dotnetRoot)
    {
        var exeName = OperatingSystem.IsWindows() ? "netcoredbg.exe" : "netcoredbg";

        // Common community install locations
        yield return Path.Combine(dotnetRoot, "tools", exeName);
        yield return Path.Combine(dotnetRoot, exeName);

        // Samsung netcoredbg release in local AppData
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(appData, "netcoredbg", exeName);
    }

    private static string? FindVsdbg()
    {
        if (!OperatingSystem.IsWindows()) return null;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var vsdbg = Path.Combine(programFiles, "Microsoft Visual Studio", "Shared", "Common",
                                 "VSPerfCollectionTools", "vs2019", "vsdbg", "vsdbg.exe");
        return File.Exists(vsdbg) ? vsdbg : null;
    }
}
