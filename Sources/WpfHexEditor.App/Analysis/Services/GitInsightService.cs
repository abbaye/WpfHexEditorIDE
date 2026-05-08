// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/GitInsightService.cs
// Description: Optional git-aware insights — change frequency for hotspots,
//              per-author LOC contribution, branch comparison.
//              All operations gracefully degrade when git is missing.
// Architecture Notes:
//     Stateless. Shells out to `git` (no LibGit2Sharp dependency).
// ==========================================================

using System.Diagnostics;
using System.IO;

namespace WpfHexEditor.App.Analysis.Services;

internal sealed class GitInsightService
{
    private readonly string _solutionDir;

    internal GitInsightService(string solutionDir) => _solutionDir = solutionDir;

    internal bool IsGitRepo()
    {
        if (string.IsNullOrEmpty(_solutionDir) || !Directory.Exists(_solutionDir)) return false;
        var dir = _solutionDir;
        for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            if (Directory.Exists(Path.Combine(dir, ".git"))) return true;
            dir = Path.GetDirectoryName(dir) ?? string.Empty;
        }
        return false;
    }

    /// <summary>For each file → number of distinct commits in the last N days.</summary>
    internal IReadOnlyDictionary<string, int> ChangeFrequency(int days = 30)
    {
        if (!IsGitRepo()) return new Dictionary<string, int>();

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var output = RunGit($"log --since=\"{days} days ago\" --name-only --pretty=format:");
        if (output is null) return result;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            var abs = Path.Combine(_solutionDir, trimmed);
            result[abs] = result.GetValueOrDefault(abs) + 1;
        }
        return result;
    }

    /// <summary>For each file → most-frequent author (top contributor).</summary>
    internal IReadOnlyDictionary<string, string> TopAuthors(IReadOnlyList<string> filePaths)
    {
        if (!IsGitRepo()) return new Dictionary<string, string>();

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in filePaths)
        {
            var output = RunGit($"log --pretty=format:%an -- \"{file}\"");
            if (output is null) continue;

            var top = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .GroupBy(x => x, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (top is not null) result[file] = top.Key;
        }
        return result;
    }

    private string? RunGit(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory       = _solutionDir,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return null;

            // Read both streams concurrently to avoid pipe-buffer deadlock
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            _ = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(10_000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* already exited */ }
                return null;
            }
            return proc.ExitCode == 0 ? stdoutTask.GetAwaiter().GetResult() : null;
        }
        catch { return null; }
    }
}
