// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: EditorConfigService.cs
// Author: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Reads .editorconfig files walking up the directory tree from the
//     currently open file.  Parses the most commonly supported properties
//     and exposes them as an EditorConfigSettings record.
//
// Architecture Notes:
//     Adapter Pattern — converts raw .editorconfig key/value pairs into a
//     typed EditorConfigSettings record consumed by CodeEditor and TextEditor.
//     Files higher in the tree are overridden by files closer to the document.
//     Search stops at a section that contains `root = true`.
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Typed representation of .editorconfig properties relevant to editors.
/// Null property values mean "not specified — use the editor's own default".
/// </summary>
public sealed record EditorConfigSettings
{
    /// <summary>indent_style: "tab" or "space".</summary>
    public bool? UseSpaces { get; init; }

    /// <summary>indent_size / tab_width: number of spaces per indent level.</summary>
    public int? IndentSize { get; init; }

    /// <summary>end_of_line: "lf", "crlf", or "cr".</summary>
    public string? EndOfLine { get; init; }

    /// <summary>charset: e.g. "utf-8", "utf-8-bom".</summary>
    public string? Charset { get; init; }

    /// <summary>trim_trailing_whitespace: true / false.</summary>
    public bool? TrimTrailingWhitespace { get; init; }

    /// <summary>insert_final_newline: true / false.</summary>
    public bool? InsertFinalNewline { get; init; }

    /// <summary>max_line_length: integer or "off".</summary>
    public int? MaxLineLength { get; init; }

    /// <summary>Returns a settings instance where all properties are null (no overrides).</summary>
    public static EditorConfigSettings Empty { get; } = new();
}

/// <summary>
/// Resolves .editorconfig settings for a file path by walking up its directory tree.
/// </summary>
public static class EditorConfigService
{
    // Matches a section header such as [*.cs] or [*.{cs,json}].
    private static readonly Regex s_sectionRegex =
        new(@"^\[([^\]]*)\]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches a key = value pair (comments stripped, whitespace trimmed).
    private static readonly Regex s_kvRegex =
        new(@"^([^=;#]+?)\s*=\s*(.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Resolves the effective .editorconfig settings for <paramref name="filePath"/>
    /// by walking up the directory tree and merging all matching sections.
    /// </summary>
    /// <param name="filePath">Absolute path to the file being edited.</param>
    /// <returns>
    /// An <see cref="EditorConfigSettings"/> with the highest-priority values found,
    /// or <see cref="EditorConfigSettings.Empty"/> when no .editorconfig is found.
    /// </returns>
    public static EditorConfigSettings Resolve(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return EditorConfigSettings.Empty;

        string? dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        string  fileName = Path.GetFileName(filePath);

        // Collect all applicable [section] → property maps, from the file's directory
        // up to the root (or up to the first `root = true` marker).
        var layers = new List<Dictionary<string, string>>();

        while (dir is not null)
        {
            string candidate = Path.Combine(dir, ".editorconfig");
            if (File.Exists(candidate))
            {
                bool isRoot = ParseFile(candidate, fileName, out var props);
                layers.Add(props);

                if (isRoot) break; // do not look further up the tree
            }

            dir = Path.GetDirectoryName(dir);
        }

        if (layers.Count == 0)
            return EditorConfigSettings.Empty;

        // Merge: the file closest to the document (index 0) wins.
        // Properties set in lower-priority files only apply if not already set.
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = layers.Count - 1; i >= 0; i--)
            foreach (var kv in layers[i])
                merged[kv.Key] = kv.Value;

        return BuildSettings(merged);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a single .editorconfig file.
    /// Returns true when the file contains a top-level <c>root = true</c> declaration.
    /// <paramref name="properties"/> is populated with all key/value pairs whose
    /// section glob matches <paramref name="fileName"/>.
    /// </summary>
    private static bool ParseFile(
        string editorConfigPath,
        string fileName,
        out Dictionary<string, string> properties)
    {
        properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool isRoot      = false;
        bool inSection   = false;
        bool sectionMatch = false;

        try
        {
            foreach (string raw in File.ReadLines(editorConfigPath))
            {
                string line = StripComment(raw).Trim();
                if (line.Length == 0) continue;

                // Top-level root = true (before any section header)
                if (!inSection && TryParseKV(line, out var rootKey, out var rootVal)
                    && string.Equals(rootKey, "root", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(rootVal, "true", StringComparison.OrdinalIgnoreCase))
                {
                    isRoot = true;
                    continue;
                }

                // Section header
                var sectionMatch2 = s_sectionRegex.Match(line);
                if (sectionMatch2.Success)
                {
                    inSection    = true;
                    sectionMatch = GlobMatches(sectionMatch2.Groups[1].Value, fileName);
                    continue;
                }

                // Key/value inside a matching section
                if (sectionMatch && TryParseKV(line, out var key, out var val))
                    properties[key] = val;
            }
        }
        catch (IOException) { /* file locked / deleted — ignore */ }

        return isRoot;
    }

    private static bool TryParseKV(string line, out string key, out string value)
    {
        var m = s_kvRegex.Match(line);
        if (!m.Success) { key = value = string.Empty; return false; }
        key   = m.Groups[1].Value.Trim();
        value = m.Groups[2].Value.Trim();
        return true;
    }

    /// <summary>
    /// Converts a merged key/value map into a typed <see cref="EditorConfigSettings"/>.
    /// </summary>
    private static EditorConfigSettings BuildSettings(Dictionary<string, string> props)
    {
        bool?   useSpaces             = null;
        int?    indentSize            = null;
        string? endOfLine             = null;
        string? charset               = null;
        bool?   trimTrailingWs        = null;
        bool?   insertFinalNewline    = null;
        int?    maxLineLength         = null;

        if (props.TryGetValue("indent_style", out var style))
            useSpaces = string.Equals(style, "space", StringComparison.OrdinalIgnoreCase);

        // indent_size takes precedence over tab_width when both are present.
        if (props.TryGetValue("indent_size", out var is_) && int.TryParse(is_, out int isv) && isv > 0)
            indentSize = isv;
        else if (props.TryGetValue("tab_width", out var tw) && int.TryParse(tw, out int twv) && twv > 0)
            indentSize = twv;

        if (props.TryGetValue("end_of_line", out var eol))
            endOfLine = eol.ToLowerInvariant();

        if (props.TryGetValue("charset", out var cs))
            charset = cs.ToLowerInvariant();

        if (props.TryGetValue("trim_trailing_whitespace", out var ttw))
            trimTrailingWs = string.Equals(ttw, "true", StringComparison.OrdinalIgnoreCase);

        if (props.TryGetValue("insert_final_newline", out var ifn))
            insertFinalNewline = string.Equals(ifn, "true", StringComparison.OrdinalIgnoreCase);

        if (props.TryGetValue("max_line_length", out var mll)
            && int.TryParse(mll, out int mllv) && mllv > 0)
            maxLineLength = mllv;

        return new EditorConfigSettings
        {
            UseSpaces             = useSpaces,
            IndentSize            = indentSize,
            EndOfLine             = endOfLine,
            Charset               = charset,
            TrimTrailingWhitespace = trimTrailingWs,
            InsertFinalNewline    = insertFinalNewline,
            MaxLineLength         = maxLineLength,
        };
    }

    /// <summary>
    /// Matches a .editorconfig glob pattern against a file name.
    /// Supports: * (any chars except /), ** (any chars), ? (any single char),
    /// {a,b} (alternatives), [abc] (character class), [!abc] (negated class).
    /// </summary>
    internal static bool GlobMatches(string pattern, string fileName)
    {
        // Convert glob → regex
        var regex = GlobToRegex(pattern);
        return Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase);
    }

    private static string GlobToRegex(string glob)
    {
        var sb = new System.Text.StringBuilder("^");
        int i  = 0;
        while (i < glob.Length)
        {
            char c = glob[i];
            switch (c)
            {
                case '*':
                    if (i + 1 < glob.Length && glob[i + 1] == '*') { sb.Append(".*"); i += 2; }
                    else { sb.Append("[^/]*"); i++; }
                    break;
                case '?':  sb.Append("[^/]"); i++; break;
                case '{':
                    sb.Append('(');
                    i++;
                    while (i < glob.Length && glob[i] != '}')
                    {
                        if (glob[i] == ',') sb.Append('|');
                        else sb.Append(Regex.Escape(glob[i].ToString()));
                        i++;
                    }
                    sb.Append(')');
                    if (i < glob.Length) i++; // skip '}'
                    break;
                case '[':
                    sb.Append('[');
                    i++;
                    if (i < glob.Length && glob[i] == '!') { sb.Append('^'); i++; }
                    while (i < glob.Length && glob[i] != ']') { sb.Append(glob[i]); i++; }
                    sb.Append(']');
                    if (i < glob.Length) i++; // skip ']'
                    break;
                default:
                    sb.Append(Regex.Escape(c.ToString()));
                    i++;
                    break;
            }
        }
        sb.Append('$');
        return sb.ToString();
    }

    private static string StripComment(string line)
    {
        int idx = line.IndexOfAny(new[] { ';', '#' });
        return idx >= 0 ? line[..idx] : line;
    }
}
