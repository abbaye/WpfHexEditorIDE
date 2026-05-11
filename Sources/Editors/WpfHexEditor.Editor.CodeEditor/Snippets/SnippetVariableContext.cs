// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Snippets/SnippetVariableContext.cs
// Description:
//     Context object capturing the dynamic values available to a snippet
//     body at expansion time. Replaces tokens like ${FileName},
//     ${SelectedText}, ${CurrentLine}, ${Date}, ${Time}, ${ClipboardText},
//     ${CursorPosition}, ${IndentText}, ${LineNumber}, ${ProjectName} into
//     the expanded text.
// ==========================================================

using System.Text.RegularExpressions;

namespace WpfHexEditor.Editor.CodeEditor.Snippets;

/// <summary>
/// Snapshot of editor state passed to <see cref="SnippetVariableExpander"/>.
/// All fields default to empty / zero so callers can populate only what
/// they have available.
/// </summary>
public sealed class SnippetVariableContext
{
    public string  FilePath         { get; init; } = string.Empty;
    public string  SelectedText     { get; init; } = string.Empty;
    public string  CurrentLineText  { get; init; } = string.Empty;
    public int     CurrentLine      { get; init; }     // 0-based
    public int     CurrentColumn    { get; init; }     // 0-based
    public string  IndentText       { get; init; } = string.Empty;
    public string? ProjectName      { get; init; }
    public string? ClipboardText    { get; init; }
    public DateTime Timestamp       { get; init; } = DateTime.Now;

    /// <summary>Resolves the value of a variable by name (case-insensitive).</summary>
    public string Resolve(string variableName) => variableName.ToLowerInvariant() switch
    {
        "filename"        => System.IO.Path.GetFileName(FilePath),
        "filenamebase"    => System.IO.Path.GetFileNameWithoutExtension(FilePath),
        "filepath"        => FilePath,
        "fileext"         => System.IO.Path.GetExtension(FilePath),
        "filedir"         => System.IO.Path.GetDirectoryName(FilePath) ?? "",
        "selectedtext"    => SelectedText,
        "currentline"     => CurrentLineText,
        "linenumber"      => (CurrentLine + 1).ToString(),
        "cursorposition"  => $"({CurrentLine + 1},{CurrentColumn + 1})",
        "indent"          => IndentText,
        "projectname"     => ProjectName ?? "",
        "clipboardtext"   => ClipboardText ?? "",
        "date"            => Timestamp.ToString("yyyy-MM-dd"),
        "time"            => Timestamp.ToString("HH:mm:ss"),
        "year"            => Timestamp.Year.ToString(),
        "username"        => Environment.UserName,
        "machinename"     => Environment.MachineName,
        _                 => string.Empty,
    };
}

/// <summary>Expands <c>${VariableName}</c> tokens inside a snippet body.</summary>
public static class SnippetVariableExpander
{
    private static readonly Regex VariableRegex = new(@"\$\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

    /// <summary>
    /// Replaces every <c>${Name}</c> token in <paramref name="body"/> by the value
    /// returned by <paramref name="context"/>.<see cref="SnippetVariableContext.Resolve"/>.
    /// Unknown variables resolve to an empty string.
    /// </summary>
    public static string Expand(string body, SnippetVariableContext context)
    {
        if (string.IsNullOrEmpty(body) || !body.Contains("${", StringComparison.Ordinal))
            return body;

        return VariableRegex.Replace(body, m => context.Resolve(m.Groups[1].Value));
    }
}
