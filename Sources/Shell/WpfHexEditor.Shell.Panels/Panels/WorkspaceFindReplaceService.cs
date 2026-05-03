// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WorkspaceFindReplaceService.cs
// Description:
//     Enumerates all text files in the current solution workspace,
//     searches for a pattern (literal or regex), and optionally
//     replaces matches. Results include file path, line, column,
//     and a preview snippet.
// Architecture: Stateless service — caller owns threading (Task.Run).
// ==========================================================

using System.IO;
using System.Text.RegularExpressions;
using WpfHexEditor.Core.ProjectSystem;
using WpfHexEditor.Core.ProjectSystem.Services;

namespace WpfHexEditor.Shell.Panels.Panels;

/// <summary>A single match returned by <see cref="WorkspaceFindReplaceService"/>.</summary>
internal sealed class WorkspaceSearchResult
{
    public string FilePath    { get; init; } = "";
    public int    Line        { get; init; }
    public int    Column      { get; init; }
    public string Preview     { get; init; } = "";
    public string MatchText   { get; init; } = "";
}

/// <summary>
/// Searches (and optionally replaces) across all text files in the active solution.
/// </summary>
internal sealed class WorkspaceFindReplaceService
{
    private static readonly string[] TextExtensions =
    [
        ".cs", ".vb", ".fs", ".fsx", ".fsi",
        ".xaml", ".xml", ".json", ".jsonc",
        ".md", ".txt", ".yaml", ".yml",
        ".toml", ".ini", ".config", ".csproj", ".vbproj",
        ".sln", ".slnx", ".props", ".targets", ".nuspec",
        ".html", ".htm", ".css", ".js", ".ts",
        ".h", ".cpp", ".c", ".py", ".rb", ".go", ".rs",
        ".sh", ".bat", ".ps1", ".psm1",
        ".whfmt", ".whlang",
    ];

    /// <summary>
    /// Searches all solution files for <paramref name="pattern"/>.
    /// </summary>
    /// <param name="pattern">Literal string or regex pattern.</param>
    /// <param name="isRegex">Whether <paramref name="pattern"/> is a regex.</param>
    /// <param name="matchCase">Case-sensitive match.</param>
    /// <param name="wholeWord">Match whole words only (wraps pattern in \b…\b).</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<IReadOnlyList<WorkspaceSearchResult>> SearchAsync(
        string pattern, bool isRegex, bool matchCase, bool wholeWord,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(pattern)) return [];

        var regex  = BuildRegex(pattern, isRegex, matchCase, wholeWord);
        var files  = EnumerateSolutionFiles();
        var results = new List<WorkspaceSearchResult>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            await SearchFileAsync(file, regex, results, ct).ConfigureAwait(false);
        }

        return results;
    }

    /// <summary>
    /// Replaces all matches of <paramref name="pattern"/> with <paramref name="replacement"/>
    /// across all solution files. Returns the list of changed files with match counts.
    /// </summary>
    public static async Task<IReadOnlyList<(string FilePath, int Count)>> ReplaceAllAsync(
        string pattern, string replacement, bool isRegex, bool matchCase, bool wholeWord,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(pattern)) return [];

        var regex   = BuildRegex(pattern, isRegex, matchCase, wholeWord);
        var files   = EnumerateSolutionFiles();
        var changed = new List<(string, int)>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            int count = await ReplaceInFileAsync(file, regex, replacement, ct).ConfigureAwait(false);
            if (count > 0) changed.Add((file, count));
        }

        return changed;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Regex BuildRegex(string pattern, bool isRegex, bool matchCase, bool wholeWord)
    {
        var opts = RegexOptions.Multiline;
        if (!matchCase) opts |= RegexOptions.IgnoreCase;

        string escaped = isRegex ? pattern : Regex.Escape(pattern);
        if (wholeWord) escaped = $@"\b{escaped}\b";

        return new Regex(escaped, opts, TimeSpan.FromSeconds(5));
    }

    private static IEnumerable<string> EnumerateSolutionFiles()
    {
        var solution = SolutionManager.Instance.CurrentSolution;
        if (solution is null) yield break;

        foreach (var project in solution.Projects)
            foreach (var item in project.Items)
            {
                var ext = Path.GetExtension(item.AbsolutePath);
                if (IsTextExtension(ext) && File.Exists(item.AbsolutePath))
                    yield return item.AbsolutePath;
            }
    }

    private static bool IsTextExtension(string ext) =>
        TextExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));

    private static async Task SearchFileAsync(
        string filePath, Regex regex, List<WorkspaceSearchResult> results,
        CancellationToken ct)
    {
        string[] lines;
        try { lines = await File.ReadAllLinesAsync(filePath, ct).ConfigureAwait(false); }
        catch { return; }

        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            ct.ThrowIfCancellationRequested();
            var line = lines[lineIdx];
            foreach (Match m in regex.Matches(line))
            {
                results.Add(new WorkspaceSearchResult
                {
                    FilePath  = filePath,
                    Line      = lineIdx + 1,
                    Column    = m.Index + 1,
                    MatchText = m.Value,
                    Preview   = line.Trim(),
                });
            }
        }
    }

    private static async Task<int> ReplaceInFileAsync(
        string filePath, Regex regex, string replacement, CancellationToken ct)
    {
        string text;
        try { text = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false); }
        catch { return 0; }

        int count = regex.Matches(text).Count;
        if (count == 0) return 0;

        string replaced = regex.Replace(text, replacement);
        try { await File.WriteAllTextAsync(filePath, replaced, ct).ConfigureAwait(false); }
        catch { return 0; }

        return count;
    }
}
