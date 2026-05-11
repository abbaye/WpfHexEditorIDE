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
    public string Resolve(string variableName) => Resolvers.TryGetValue(variableName, out var fn)
        ? fn(this)
        : string.Empty;

    private static readonly Dictionary<string, Func<SnippetVariableContext, string>> Resolvers
        = new(StringComparer.OrdinalIgnoreCase)
        {
            ["FileName"]       = c => System.IO.Path.GetFileName(c.FilePath),
            ["FileNameBase"]   = c => System.IO.Path.GetFileNameWithoutExtension(c.FilePath),
            ["FilePath"]       = c => c.FilePath,
            ["FileExt"]        = c => System.IO.Path.GetExtension(c.FilePath),
            ["FileDir"]        = c => System.IO.Path.GetDirectoryName(c.FilePath) ?? "",
            ["SelectedText"]   = c => c.SelectedText,
            ["CurrentLine"]    = c => c.CurrentLineText,
            ["LineNumber"]     = c => (c.CurrentLine + 1).ToString(),
            ["CursorPosition"] = c => $"({c.CurrentLine + 1},{c.CurrentColumn + 1})",
            ["Indent"]         = c => c.IndentText,
            ["ProjectName"]    = c => c.ProjectName ?? "",
            ["ClipboardText"]  = c => c.ClipboardText ?? "",
            ["Date"]           = c => c.Timestamp.ToString("yyyy-MM-dd"),
            ["Time"]           = c => c.Timestamp.ToString("HH:mm:ss"),
            ["Year"]           = c => c.Timestamp.Year.ToString(),
            ["UserName"]       = _ => Environment.UserName,
            ["MachineName"]    = _ => Environment.MachineName,
        };

    /// <summary>The set of variable names recognised by <see cref="Resolve"/>.</summary>
    public static IReadOnlyCollection<string> KnownVariables => Resolvers.Keys;
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
