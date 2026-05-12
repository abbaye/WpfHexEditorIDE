// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeFixes/Fixers/ConfigureAwaitFixer.cs
// Description: Fixer for WH0062 — adds `.ConfigureAwait(false)` to an `await`
//              expression that is missing it. Safe regex-based rewrite: only
//              applies when the line contains a single `await` ending in `;`.
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Analysis.CodeFixes.Fixers;

internal sealed class ConfigureAwaitFixer : IMechanicalFixer
{
    public string RuleId => "WH0062";

    private static readonly Regex AwaitStatement =
        new(@"^(?<lead>\s*)(?<expr>(?:.*?\bawait\b.+?))(?<tail>;\s*(//.*)?)$", RegexOptions.Compiled);

    public LspCodeAction? TryBuild(AnalysisDiagnostic d, IReadOnlyList<string> lines)
    {
        int idx = d.Line - 1;
        if (idx < 0 || idx >= lines.Count) return null;
        var line = lines[idx];

        // Already has ConfigureAwait — nothing to do
        if (line.Contains("ConfigureAwait", StringComparison.Ordinal)) return null;

        var m = AwaitStatement.Match(line);
        if (!m.Success) return null;

        string rewritten = $"{m.Groups["lead"].Value}{m.Groups["expr"].Value}.ConfigureAwait(false){m.Groups["tail"].Value}";

        var edit = new LspTextEdit
        {
            StartLine   = idx,
            StartColumn = 0,
            EndLine     = idx,
            EndColumn   = line.Length,
            NewText     = rewritten,
        };

        return FixerHelpers.SingleFileEdit(AppResources.CodeAnalysis_Fix_WH0062_Title, d.FilePath, edit);
    }
}
