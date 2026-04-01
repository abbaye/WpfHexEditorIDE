// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.Folder
// File: GitIgnoreFilter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Lightweight .gitignore pattern parser and matcher.
//     Converts gitignore glob patterns to BCL Regex; handles *, **, ?,
//     negation (!pattern), anchored patterns (/pattern), and directory-only
//     markers (pattern/). No external NuGet dependencies.
//
// Architecture Notes:
//     Pattern: Strategy — plugged into FolderFileEnumerator
//     Each directory level can have its own filter instance (per .gitignore).
//     IsIgnored() receives a path relative to the directory containing
//     the .gitignore file.
// ==========================================================

using System.Text.RegularExpressions;

namespace WpfHexEditor.Plugins.SolutionLoader.Folder;

/// <summary>
/// Parses and applies a single <c>.gitignore</c> file's patterns.
/// </summary>
internal sealed class GitIgnoreFilter
{
    private readonly List<(Regex Pattern, bool Negate, bool DirectoryOnly)> _rules = [];

    // -----------------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads <c>.gitignore</c> from <paramref name="dir"/> (if present) and returns
    /// a filter. Returns an empty, always-passing filter when absent.
    /// </summary>
    public static GitIgnoreFilter LoadForDirectory(string dir)
    {
        var path = Path.Combine(dir, ".gitignore");
        var filter = new GitIgnoreFilter();
        if (!File.Exists(path)) return filter;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();

            // Skip blanks and comments.
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var negate       = line.StartsWith('!');
            if (negate) line = line[1..];

            var directoryOnly = line.EndsWith('/');
            if (directoryOnly) line = line.TrimEnd('/');

            if (line.Length == 0) continue;

            var regex = GlobToRegex(line);
            filter._rules.Add((regex, negate, directoryOnly));
        }

        return filter;
    }

    // -----------------------------------------------------------------------
    // Matching
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> if <paramref name="relativePath"/> should be ignored.
    /// <paramref name="isDirectory"/> controls directory-only rule matching.
    /// </summary>
    public bool IsIgnored(string relativePath, bool isDirectory = false)
    {
        // Normalize separators to forward slash for matching.
        var normalised = relativePath.Replace('\\', '/');
        var name       = Path.GetFileName(normalised);

        var ignored = false;

        foreach (var (pattern, negate, dirOnly) in _rules)
        {
            if (dirOnly && !isDirectory) continue;

            // Match against full relative path and also just the file name.
            var match = pattern.IsMatch(normalised) || pattern.IsMatch(name);
            if (!match) continue;

            ignored = !negate;
        }

        return ignored;
    }

    // -----------------------------------------------------------------------
    // Glob → Regex conversion
    // -----------------------------------------------------------------------

    private static Regex GlobToRegex(string glob)
    {
        // Anchored pattern (starts with /) means only match from root.
        var anchored = glob.StartsWith('/');
        if (anchored) glob = glob[1..];

        var sb = new System.Text.StringBuilder();
        sb.Append(anchored ? '^' : "(^|/)");

        var i = 0;
        while (i < glob.Length)
        {
            if (glob[i] == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                // ** matches any path segment sequence.
                sb.Append(".*");
                i += 2;
                if (i < glob.Length && glob[i] == '/') i++; // skip trailing /
            }
            else if (glob[i] == '*')
            {
                // * matches anything except /
                sb.Append("[^/]*");
                i++;
            }
            else if (glob[i] == '?')
            {
                // ? matches any single character except /
                sb.Append("[^/]");
                i++;
            }
            else
            {
                sb.Append(Regex.Escape(glob[i].ToString()));
                i++;
            }
        }

        sb.Append('$');

        return new Regex(sb.ToString(),
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
