// Project      : WpfHexEditorControl
// File         : Services/GitDiffService.cs
// Description  : Wraps `git show` and `git log/branch` via System.Diagnostics.Process.
// Architecture : Infrastructure service — pure I/O, no WPF.

using System.Diagnostics;

namespace WpfHexEditor.Core.Diff.Services;

/// <summary>Metadata about a git commit for display in pickers.</summary>
public sealed record GitCommitInfo(string Hash, string ShortHash, string Message, string Author, DateTimeOffset Date);

/// <summary>
/// Git integration service for the Compare Files feature.
/// All methods require git to be on PATH.
/// </summary>
public sealed class GitDiffService
{
    private const int TimeoutMs = 10_000;

    // -----------------------------------------------------------------------
    // Repository detection
    // -----------------------------------------------------------------------

    /// <summary>Returns true when <paramref name="filePath"/> is inside a git repository.</summary>
    public bool IsGitRepository(string filePath) => GetRepoRoot(filePath) is not null;

    /// <summary>
    /// Walks up from <paramref name="filePath"/> looking for a <c>.git</c> directory.
    /// Returns the repository root, or <c>null</c> if none found.
    /// </summary>
    public string? GetRepoRoot(string filePath)
    {
        var dir = File.Exists(filePath)
            ? Path.GetDirectoryName(filePath)
            : (Directory.Exists(filePath) ? filePath : null);

        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Branch / commit enumeration
    // -----------------------------------------------------------------------

    /// <summary>Returns all local and remote branches, sorted alphabetically.</summary>
    public async Task<IReadOnlyList<string>> GetBranchesAsync(string repoRoot, CancellationToken ct = default)
    {
        var raw = await RunGitAsync(repoRoot,
            "branch -a --format=%(refname:short)", ct).ConfigureAwait(false);
        return raw.Where(s => !string.IsNullOrWhiteSpace(s))
                  .Select(s => s.Trim())
                  .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                  .ToList();
    }

    /// <summary>Returns the <paramref name="count"/> most recent commits.</summary>
    public async Task<IReadOnlyList<GitCommitInfo>> GetRecentCommitsAsync(
        string repoRoot, int count = 30, CancellationToken ct = default)
    {
        var raw = await RunGitAsync(repoRoot,
            $"log --pretty=format:%H|%h|%s|%an|%aI -{count}", ct).ConfigureAwait(false);

        return raw.Where(s => !string.IsNullOrWhiteSpace(s))
                  .Select(ParseCommit)
                  .Where(c => c is not null)
                  .Cast<GitCommitInfo>()
                  .ToList();
    }

    // -----------------------------------------------------------------------
    // Version extraction
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracts <paramref name="gitRef"/>:<paramref name="filePath"/> to a temporary file
    /// using <c>git show</c>.
    /// Returns the temp file path, or <c>null</c> on failure / timeout.
    /// The caller is responsible for deleting the temp file.
    /// </summary>
    public async Task<string?> ExtractRefVersionAsync(
        string repoRoot, string gitRef, string filePath, CancellationToken ct = default)
    {
        var relativePath = Path.GetRelativePath(repoRoot, filePath).Replace('\\', '/');
        var arg          = $"show {gitRef}:{relativePath}";

        var psi = BuildPsi(repoRoot, arg);
        psi.RedirectStandardOutput = true;
        psi.StandardOutputEncoding = System.Text.Encoding.UTF8;

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutMs);

            var content = await process.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            if (process.ExitCode != 0 || string.IsNullOrEmpty(content))
                return null;

            var tempDir  = Path.Combine(Path.GetTempPath(), "WpfHexEditor");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir,
                $"diff_{Path.GetFileNameWithoutExtension(filePath)}_{gitRef.Replace('/', '_')}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{Path.GetExtension(filePath)}");

            await File.WriteAllTextAsync(tempFile, content, ct).ConfigureAwait(false);
            return tempFile;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return null;
        }
        catch
        {
            return null;
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static async Task<IReadOnlyList<string>> RunGitAsync(
        string repoRoot, string arguments, CancellationToken ct)
    {
        var psi = BuildPsi(repoRoot, arguments);
        psi.RedirectStandardOutput = true;
        psi.StandardOutputEncoding = System.Text.Encoding.UTF8;

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutMs);
            var output = await process.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            return process.ExitCode == 0
                ? output.Split('\n')
                : [];
        }
        catch
        {
            return [];
        }
    }

    private static ProcessStartInfo BuildPsi(string workDir, string arguments) => new()
    {
        FileName               = "git",
        Arguments              = arguments,
        WorkingDirectory       = workDir,
        UseShellExecute        = false,
        CreateNoWindow         = true,
        RedirectStandardError  = true
    };

    private static GitCommitInfo? ParseCommit(string line)
    {
        var parts = line.Split('|');
        if (parts.Length < 5) return null;
        if (!DateTimeOffset.TryParse(parts[4], out var date)) date = DateTimeOffset.MinValue;
        return new GitCommitInfo(
            Hash:      parts[0].Trim(),
            ShortHash: parts[1].Trim(),
            Message:   parts[2].Trim(),
            Author:    parts[3].Trim(),
            Date:      date);
    }
}
