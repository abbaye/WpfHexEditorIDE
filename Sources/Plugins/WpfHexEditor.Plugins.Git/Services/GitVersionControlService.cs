// ==========================================================
// Project: WpfHexEditor.Plugins.Git
// File: Services/GitVersionControlService.cs
// Description:
//     IVersionControlService implementation using git CLI.
//     Composes GitDiffService (WpfHexEditor.Core.Diff) for repo detection.
//     Polls status every 5 seconds via DispatcherTimer.
// Architecture Notes:
//     Pattern: Composition — wraps GitDiffService, does not inherit it.
//     All StatusChanged events are fired on the UI thread.
//     Blame is cached per (filePath, HEAD commit hash).
//     Long ops (push/pull/fetch) publish GitOperationStarted/Completed events.
// ==========================================================

using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using WpfHexEditor.Core.Diff.Services;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Plugins.Git.Services;

/// <summary>
/// Git-backed implementation of <see cref="IVersionControlService"/>.
/// Requires git on PATH. Degrades gracefully when git is absent or no repo found.
/// </summary>
internal sealed class GitVersionControlService : IVersionControlService, IDisposable
{
    private const int TimeoutMs    = 8_000;
    private const int LongTimeoutMs = 60_000; // push/pull/fetch
    private const int PollInterval  = 5_000;

    private readonly GitDiffService  _diffService = new();
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher      _dispatcher;

    // Optional event bus — set by GitPlugin after construction
    internal Action<object>? PublishEvent { get; set; }

    // Blame cache: keyed by filePath (case-insensitive)
    private readonly Dictionary<string, (string HeadHash, IReadOnlyList<BlameEntry> Entries)>
        _blameCache = new(StringComparer.OrdinalIgnoreCase);

    private string? _repoRoot;
    private string? _branchName;
    private bool    _isDirty;

    public GitVersionControlService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;

        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(PollInterval)
        };
        _timer.Tick += async (_, _) => await PollStatusAsync();
    }

    // ── IVersionControlService ────────────────────────────────────────────────

    public bool    IsRepo     => _repoRoot is not null;
    public string? BranchName => _branchName;
    public bool    IsDirty    => _isDirty;

    public event EventHandler? StatusChanged;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await Task.Run(() => RefreshInternal(), ct);
        _dispatcher.Invoke(() => StatusChanged?.Invoke(this, EventArgs.Empty));
    }

    public async Task<IReadOnlyList<BlameEntry>> GetBlameAsync(
        string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return [];

        var headHash = await Task.Run(() => GetHeadHash(filePath), ct);

        if (_blameCache.TryGetValue(filePath, out var cached) && cached.HeadHash == headHash)
            return cached.Entries;

        var entries = await Task.Run(() => ParseBlame(filePath), ct);
        _blameCache[filePath] = (headHash ?? string.Empty, entries);
        return entries;
    }

    public async Task<IReadOnlyList<GitChangeEntry>> GetChangedFilesAsync(
        CancellationToken ct = default)
    {
        if (_repoRoot is null) return [];
        return await Task.Run(() => ParseStatus(_repoRoot), ct);
    }

    public async Task<string> GetDiffAsync(string filePath, CancellationToken ct = default)
    {
        if (_repoRoot is null) return string.Empty;
        return await Task.Run(() => RunGit(_repoRoot, $"diff HEAD -- \"{filePath}\""), ct);
    }

    public async Task<IReadOnlyList<DiffHunk>> GetDiffHunksAsync(
        string filePath, CancellationToken ct = default)
    {
        var raw = await GetDiffAsync(filePath, ct);
        return ParseHunks(raw);
    }

    // ── Staging ───────────────────────────────────────────────────────────────

    public async Task StageAsync(string filePath, CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        await Task.Run(() => RunGit(_repoRoot, $"add -- \"{filePath}\""), ct);
        await RefreshAsync(ct);
    }

    public async Task UnstageAsync(string filePath, CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        await Task.Run(() => RunGit(_repoRoot, $"restore --staged -- \"{filePath}\""), ct);
        await RefreshAsync(ct);
    }

    public async Task DiscardAsync(string filePath, CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        await Task.Run(() => RunGit(_repoRoot, $"checkout -- \"{filePath}\""), ct);
        await RefreshAsync(ct);
    }

    // ── Commit ────────────────────────────────────────────────────────────────

    public async Task CommitAsync(string message, bool amend = false, CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        var amendFlag = amend ? "--amend" : string.Empty;
        // Use -m with escaped message (replace " with ')
        var safeMsg = message.Replace("\"", "'");
        await Task.Run(() => RunGit(_repoRoot, $"commit {amendFlag} -m \"{safeMsg}\""), ct);
        _blameCache.Clear(); // HEAD moved
        await RefreshAsync(ct);
        // Refresh ahead/behind after commit
        _ = RefreshAheadBehindAsync(ct);
    }

    // ── Remote ────────────────────────────────────────────────────────────────

    public async Task PushAsync(bool force = false, CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        var forceFlag = force ? "--force-with-lease" : string.Empty;
        await RunLongGitAsync("Push", _repoRoot, $"push {forceFlag}", ct);
        await RefreshAheadBehindAsync(ct);
    }

    public async Task PullAsync(CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        await RunLongGitAsync("Pull", _repoRoot, "pull --rebase", ct);
        _blameCache.Clear();
        await RefreshAsync(ct);
        await RefreshAheadBehindAsync(ct);
    }

    public async Task FetchAsync(CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        await RunLongGitAsync("Fetch", _repoRoot, "fetch --prune", ct);
        await RefreshAheadBehindAsync(ct);
    }

    public async Task<AheadBehind> GetAheadBehindAsync(CancellationToken ct = default)
    {
        if (_repoRoot is null) return new AheadBehind(0, 0);
        return await Task.Run(() =>
        {
            // HEAD..@{u} = commits on remote not in local = behind
            // @{u}..HEAD = commits in local not on remote = ahead
            var behindRaw = RunGit(_repoRoot, "rev-list --count HEAD..@{u}").Trim();
            var aheadRaw  = RunGit(_repoRoot, "rev-list --count @{u}..HEAD").Trim();
            int.TryParse(aheadRaw,  out var ahead);
            int.TryParse(behindRaw, out var behind);
            return new AheadBehind(ahead, behind);
        }, ct);
    }

    // ── Branches ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<BranchInfo>> GetBranchesAsync(CancellationToken ct = default)
    {
        if (_repoRoot is null) return [];
        return await Task.Run(() => ParseBranches(_repoRoot), ct);
    }

    public async Task SwitchBranchAsync(string name, CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        await Task.Run(() => RunGit(_repoRoot, $"checkout \"{name}\""), ct);
        await RefreshAsync(ct);
    }

    public async Task CreateBranchAsync(string name, bool checkout = true, CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        var cmd = checkout ? $"checkout -b \"{name}\"" : $"branch \"{name}\"";
        await Task.Run(() => RunGit(_repoRoot, cmd), ct);
        await RefreshAsync(ct);
    }

    public async Task DeleteBranchAsync(string name, bool force = false, CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        var flag = force ? "-D" : "-d";
        await Task.Run(() => RunGit(_repoRoot, $"branch {flag} \"{name}\""), ct);
        await RefreshAsync(ct);
    }

    // ── Stash ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<StashEntry>> GetStashListAsync(CancellationToken ct = default)
    {
        if (_repoRoot is null) return [];
        return await Task.Run(() => ParseStashList(_repoRoot), ct);
    }

    public async Task StashAsync(string? message = null, bool includeUntracked = true,
        CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        var u    = includeUntracked ? "-u " : string.Empty;
        var m    = message is not null ? $"-m \"{message.Replace("\"", "'")}\" " : string.Empty;
        await Task.Run(() => RunGit(_repoRoot, $"stash push {u}{m}".TrimEnd()), ct);
        await RefreshAsync(ct);
    }

    public async Task StashPopAsync(int index = 0, CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        await Task.Run(() => RunGit(_repoRoot, $"stash pop stash@{{{index}}}"), ct);
        _blameCache.Clear();
        await RefreshAsync(ct);
    }

    public async Task StashDropAsync(int index, CancellationToken ct = default)
    {
        if (_repoRoot is null) return;
        await Task.Run(() => RunGit(_repoRoot, $"stash drop stash@{{{index}}}"), ct);
    }

    // ── History ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CommitInfo>> GetLogAsync(
        int maxCount = 100, string? filePath = null, CancellationToken ct = default)
    {
        if (_repoRoot is null) return [];
        var fileArg = filePath is not null ? $"-- \"{filePath}\"" : string.Empty;
        return await Task.Run(() => ParseLog(_repoRoot, maxCount, fileArg), ct);
    }

    public async Task<string> ShowCommitAsync(string hash, CancellationToken ct = default)
    {
        if (_repoRoot is null) return string.Empty;
        return await Task.Run(
            () => RunGit(_repoRoot, $"show --stat -p {hash}"),
            ct);
    }

    // ── Polling ───────────────────────────────────────────────────────────────

    public void StartPolling() => _timer.Start();
    public void StopPolling()  => _timer.Stop();

    public void SetActiveFile(string? filePath)
    {
        _repoRoot = filePath is not null ? _diffService.GetRepoRoot(filePath) : null;
        _ = RefreshAsync();
        if (_repoRoot is not null) _ = RefreshAheadBehindAsync();
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private async Task PollStatusAsync()
    {
        if (_repoRoot is null) return;
        var (branch, dirty) = await Task.Run(() => ReadBranchAndDirty(_repoRoot));
        if (branch == _branchName && dirty == _isDirty) return;

        _branchName = branch;
        _isDirty    = dirty;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshInternal()
    {
        if (_repoRoot is null) { _branchName = null; _isDirty = false; return; }
        (_branchName, _isDirty) = ReadBranchAndDirty(_repoRoot);
    }

    private (string? branch, bool dirty) ReadBranchAndDirty(string repoRoot)
    {
        var branch = RunGit(repoRoot, "branch --show-current").Trim();
        var status = RunGit(repoRoot, "status --porcelain");
        return (branch.Length > 0 ? branch : null, status.Trim().Length > 0);
    }

    private async Task RefreshAheadBehindAsync(CancellationToken ct = default)
    {
        var ab = await GetAheadBehindAsync(ct);
        PublishEvent?.Invoke(new GitAheadBehindChangedEvent(ab.Ahead, ab.Behind));
    }

    private async Task RunLongGitAsync(string opName, string workDir, string args,
        CancellationToken ct)
    {
        PublishEvent?.Invoke(new GitOperationStartedEvent(opName));
        string? error = null;
        try
        {
            var output = await Task.Run(
                () => RunGitWithTimeout(workDir, args, LongTimeoutMs), ct);
            // Check for error indicators in output
            if (output.StartsWith("error:", StringComparison.OrdinalIgnoreCase) ||
                output.StartsWith("fatal:", StringComparison.OrdinalIgnoreCase))
                error = output.Trim();
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        PublishEvent?.Invoke(new GitOperationCompletedEvent(opName, error is null, error));
    }

    private string? GetHeadHash(string filePath)
    {
        var root = _diffService.GetRepoRoot(filePath);
        if (root is null) return null;
        return RunGit(root, "rev-parse HEAD").Trim();
    }

    private IReadOnlyList<BlameEntry> ParseBlame(string filePath)
    {
        var root = _diffService.GetRepoRoot(filePath);
        if (root is null) return [];

        var raw = RunGit(root, $"blame --porcelain \"{filePath}\"");
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var result  = new List<BlameEntry>();
        var commits = new Dictionary<string, (string author, DateTime date, string msg)>(StringComparer.Ordinal);
        var lines   = raw.Split('\n');

        string? currentHash   = null;
        int     currentLine   = 0;
        string  currentAuthor = string.Empty;
        DateTime currentDate  = default;
        string  currentMsg    = string.Empty;

        foreach (var line in lines)
        {
            if (line.Length >= 40 && line[39] == ' ' && IsHex(line, 40))
            {
                currentHash = line[..40];
                var parts = line.Split(' ');
                if (parts.Length >= 3 && int.TryParse(parts[2], out var ln))
                    currentLine = ln;

                if (commits.TryGetValue(currentHash, out var cached))
                    (currentAuthor, currentDate, currentMsg) = cached;
            }
            else if (line.StartsWith("author ", StringComparison.Ordinal))
            {
                currentAuthor = line[7..];
            }
            else if (line.StartsWith("author-time ", StringComparison.Ordinal)
                     && long.TryParse(line[12..], out var ts))
            {
                currentDate = DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime;
            }
            else if (line.StartsWith("summary ", StringComparison.Ordinal))
            {
                currentMsg = line[8..];
                if (currentHash is not null)
                    commits[currentHash] = (currentAuthor, currentDate, currentMsg);
            }
            else if (line.StartsWith("\t", StringComparison.Ordinal) && currentHash is not null)
            {
                result.Add(new BlameEntry(currentLine, currentHash, currentAuthor, currentDate, currentMsg));
            }
        }

        return result;
    }

    private IReadOnlyList<GitChangeEntry> ParseStatus(string repoRoot)
    {
        var raw = RunGit(repoRoot, "status --porcelain");
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var result = new List<GitChangeEntry>();
        foreach (var line in raw.Split('\n'))
        {
            if (line.Length < 4) continue;
            var xy   = line[..2];
            var path = line[3..].Trim().Trim('"');

            var kind = xy switch
            {
                " M" or "MM" => GitChangeKind.Modified,
                "M " or "A " => GitChangeKind.Staged,
                " A"         => GitChangeKind.Added,
                " D" or "D " => GitChangeKind.Deleted,
                "R " or " R" => GitChangeKind.Renamed,
                "??"         => GitChangeKind.Untracked,
                "UU" or "AA" or "DD" => GitChangeKind.Conflicted,
                _            => GitChangeKind.Modified
            };
            result.Add(new GitChangeEntry(path, kind));
        }
        return result;
    }

    private IReadOnlyList<BranchInfo> ParseBranches(string repoRoot)
    {
        // --format: refname:short | HEAD marker (* or space) | upstream:short
        var raw = RunGit(repoRoot, "branch -a --format=%(refname:short)|%(HEAD)|%(upstream:short)");
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var result = new List<BranchInfo>();
        foreach (var line in raw.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('|');
            if (parts.Length < 2) continue;
            var name       = parts[0].Trim();
            var isCurrent  = parts[1].Trim() == "*";
            var upstream   = parts.Length >= 3 ? parts[2].Trim() : null;
            var isRemote   = name.StartsWith("remotes/", StringComparison.Ordinal);
            if (isRemote) name = name["remotes/".Length..];
            result.Add(new BranchInfo(name, isCurrent, isRemote,
                upstream?.Length > 0 ? upstream : null));
        }
        return result;
    }

    private IReadOnlyList<StashEntry> ParseStashList(string repoRoot)
    {
        // Format: stash@{0}|message|date (ISO 8601)
        var raw = RunGit(repoRoot, "stash list --format=%gd|%gs|%ai");
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var result = new List<StashEntry>();
        foreach (var line in raw.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('|');
            if (parts.Length < 2) continue;
            // stash@{N} → extract index
            var refPart = parts[0].Trim(); // "stash@{0}"
            var index = 0;
            var ob = refPart.IndexOf('{');
            var cb = refPart.IndexOf('}');
            if (ob >= 0 && cb > ob)
                int.TryParse(refPart[(ob + 1)..cb], out index);

            var message = parts[1].Trim();
            DateTime date = default;
            if (parts.Length >= 3) DateTime.TryParse(parts[2].Trim(), out date);
            result.Add(new StashEntry(index, message, date));
        }
        return result;
    }

    private IReadOnlyList<CommitInfo> ParseLog(string repoRoot, int maxCount, string fileArg)
    {
        // Separator-based format to handle multi-word fields
        const string Sep = "||GIT_SEP||";
        var format = $"%H{Sep}%h{Sep}%s{Sep}%an{Sep}%ai{Sep}%D";
        var raw = RunGit(repoRoot,
            $"log --max-count={maxCount} --format={format} {fileArg}");
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var result = new List<CommitInfo>();
        foreach (var line in raw.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(Sep, StringSplitOptions.None);
            if (parts.Length < 5) continue;
            DateTime.TryParse(parts[4].Trim(), out var date);
            result.Add(new CommitInfo(
                Hash        : parts[0].Trim(),
                ShortHash   : parts[1].Trim(),
                Message     : parts[2].Trim(),
                AuthorName  : parts[3].Trim(),
                Date        : date,
                ChangedFiles: [])); // populated lazily on demand
        }
        return result;
    }

    private static IReadOnlyList<DiffHunk> ParseHunks(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var result = new List<DiffHunk>();
        var lines  = raw.Split('\n');
        int i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (!line.StartsWith("@@", StringComparison.Ordinal)) { i++; continue; }

            // @@ -oldStart,oldCount +newStart,newCount @@ header
            var end = line.IndexOf("@@", 2, StringComparison.Ordinal);
            var header = end >= 0 ? line[(end + 2)..].Trim() : string.Empty;
            var rangeStr = end >= 0 ? line[2..end].Trim() : line[2..].Trim();
            int oldStart = 0, oldCount = 1, newStart = 0, newCount = 1;
            var rangeParts = rangeStr.Split(' ');
            ParseHunkRange(rangeParts.FirstOrDefault(p => p.StartsWith('-')), ref oldStart, ref oldCount);
            ParseHunkRange(rangeParts.FirstOrDefault(p => p.StartsWith('+')), ref newStart, ref newCount);

            var hunkLines = new List<string>();
            i++;
            while (i < lines.Length &&
                   !lines[i].StartsWith("@@", StringComparison.Ordinal) &&
                   !lines[i].StartsWith("diff ", StringComparison.Ordinal))
            {
                hunkLines.Add(lines[i]);
                i++;
            }
            result.Add(new DiffHunk(oldStart, oldCount, newStart, newCount, header, [.. hunkLines]));
        }
        return result;
    }

    private static void ParseHunkRange(string? part, ref int start, ref int count)
    {
        if (part is null) return;
        var s = part.TrimStart('-', '+');
        var comma = s.IndexOf(',');
        if (comma < 0) { int.TryParse(s, out start); return; }
        int.TryParse(s[..comma], out start);
        int.TryParse(s[(comma + 1)..], out count);
    }

    private string RunGit(string workingDir, string args) =>
        RunGitWithTimeout(workingDir, args, TimeoutMs);

    private string RunGitWithTimeout(string workingDir, string args, int timeoutMs)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory       = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                }
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(timeoutMs);
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsHex(string s, int len)
    {
        for (var i = 0; i < Math.Min(len, s.Length); i++)
            if (!Uri.IsHexDigit(s[i])) return false;
        return true;
    }

    public void Dispose()
    {
        _timer.Stop();
        _blameCache.Clear();
    }
}
