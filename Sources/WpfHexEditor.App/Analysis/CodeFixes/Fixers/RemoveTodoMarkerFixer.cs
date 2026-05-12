// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeFixes/Fixers/RemoveTodoMarkerFixer.cs
// Description: Fixer for WH0032 — removes a `// TODO`/`// FIXME`/`// HACK`
//              marker line entirely (only when the comment is the whole line).
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Analysis.CodeFixes.Fixers;

internal sealed class RemoveTodoMarkerFixer : IMechanicalFixer
{
    public string RuleId => "WH0032";

    private static readonly Regex CommentOnlyTodo =
        new(@"^\s*//\s*(TODO|FIXME|HACK|XXX)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public LspCodeAction? TryBuild(AnalysisDiagnostic d, IReadOnlyList<string> lines)
    {
        int idx = d.Line - 1;
        if (idx < 0 || idx >= lines.Count) return null;
        if (!CommentOnlyTodo.IsMatch(lines[idx])) return null;

        // Delete the whole line (including its terminator) by replacing [idx..idx+1) with ""
        int endLine   = idx + 1 < lines.Count ? idx + 1 : idx;
        int endColumn = idx + 1 < lines.Count ? 0       : lines[idx].Length;

        var edit = new LspTextEdit
        {
            StartLine   = idx,
            StartColumn = 0,
            EndLine     = endLine,
            EndColumn   = endColumn,
            NewText     = string.Empty,
        };

        return FixerHelpers.SingleFileEdit(AppResources.CodeAnalysis_Fix_WH0032_Title, d.FilePath, edit);
    }
}
