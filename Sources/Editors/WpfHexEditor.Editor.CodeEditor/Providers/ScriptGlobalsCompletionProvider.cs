// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Providers/ScriptGlobalsCompletionProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     ILocalCompletionProvider implementation that reads ScriptGlobals from
//     the LanguageDefinition (populated from the "scriptGlobals" whfmt block)
//     and returns context-aware completions:
//       - After "GlobalName."  → members of that global
//       - Otherwise            → all top-level globals
//
// Architecture Notes:
//     Stateless — context detection is done entirely from SmartCompleteContext.
//     Auto-registered by CodeEditorSplitHost when SetLanguage sets a language
//     with ScriptGlobals.Count > 0; no manual wiring required by callers.
// ==========================================================

using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Providers;

/// <summary>
/// Provides SmartComplete suggestions for script globals and their members,
/// driven entirely by the <c>scriptGlobals</c> block in a <c>.whfmt</c> file.
/// </summary>
public sealed class ScriptGlobalsCompletionProvider : ILocalCompletionProvider
{
    public string LanguageId => "csharp-script";

    /// <inheritdoc/>
    public IReadOnlyList<SmartCompleteSuggestion> GetCompletions(
        SmartCompleteContext context,
        LanguageDefinition?  language)
    {
        if (language is null || language.ScriptGlobals.Count == 0)
            return [];

        var (objectName, isDotAccess) = FindPrecedingToken(
            context.CurrentLine ?? string.Empty,
            context.CursorPosition.Column);

        if (isDotAccess)
        {
            var global = language.ScriptGlobals
                .FirstOrDefault(g => string.Equals(g.Name, objectName, StringComparison.Ordinal));
            return global is not null
                ? global.Members.Select(MemberSuggestion).ToList()
                : (IReadOnlyList<SmartCompleteSuggestion>)[];
        }

        return language.ScriptGlobals.Select(GlobalSuggestion).ToList();
    }

    // Returns the identifier immediately before '.' at the given column,
    // or ("", false) when no dot-access pattern is detected.
    private static (string objectName, bool isDotAccess) FindPrecedingToken(string line, int col)
    {
        if (col <= 0 || col > line.Length) return (string.Empty, false);

        var before = line[..col];
        if (!before.EndsWith('.'))          return (string.Empty, false);

        // Walk backwards over the identifier preceding the dot
        var chars = before[..^1];
        var start = chars.Length - 1;
        while (start >= 0 && (char.IsLetterOrDigit(chars[start]) || chars[start] == '_'))
            start--;

        var name = chars[(start + 1)..];
        return name.Length > 0
            ? (name, true)
            : (string.Empty, false);
    }

    private static SmartCompleteSuggestion GlobalSuggestion(ScriptGlobalEntry g) =>
        new(g.Name, g.Documentation)
        {
            InsertText   = g.Name,
            TypeHint     = g.Type,
            Icon         = "\uE8D0",   // Segoe MDL2 Variable
            SortPriority = 50,         // above default 100 → shown first
            Type         = SuggestionType.Property,
        };

    private static SmartCompleteSuggestion MemberSuggestion(ScriptMemberEntry m)
    {
        var isMethod = string.Equals(m.Kind, "method", StringComparison.OrdinalIgnoreCase);
        return new SmartCompleteSuggestion(m.Name, m.Documentation)
        {
            InsertText   = isMethod ? m.Name + "()" : m.Name,
            TypeHint     = m.Type,
            Icon         = isMethod ? "\uE8B8" : "\uE8D0",  // Method vs Variable
            SortPriority = 50,
            CursorOffset = isMethod ? -1 : 0,               // position inside ()
            Type         = isMethod ? SuggestionType.Function : SuggestionType.Property,
        };
    }
}
