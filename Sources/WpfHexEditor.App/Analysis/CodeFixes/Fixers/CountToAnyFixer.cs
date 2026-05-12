// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeFixes/Fixers/CountToAnyFixer.cs
// Description: Fixer for WH0070 — rewrites `xs.Count() > 0` (and equivalents)
//              into `xs.Any()`. Single-line, regex-based; refuses ambiguous
//              cases that would change semantics.
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Analysis.CodeFixes.Fixers;

internal sealed class CountToAnyFixer : IMechanicalFixer
{
    public string RuleId => "WH0070";

    private static readonly Regex CountGreaterThanZero =
        new(@"\.Count\(\)\s*>\s*0", RegexOptions.Compiled);

    private static readonly Regex CountNotEqualZero =
        new(@"\.Count\(\)\s*!=\s*0", RegexOptions.Compiled);

    private static readonly Regex CountGreaterEqualOne =
        new(@"\.Count\(\)\s*>=\s*1", RegexOptions.Compiled);

    public LspCodeAction? TryBuild(AnalysisDiagnostic d, IReadOnlyList<string> lines)
    {
        int idx = d.Line - 1;
        if (idx < 0 || idx >= lines.Count) return null;
        var line = lines[idx];

        string rewritten = CountGreaterThanZero.Replace(line, ".Any()");
        rewritten        = CountNotEqualZero.Replace(rewritten, ".Any()");
        rewritten        = CountGreaterEqualOne.Replace(rewritten, ".Any()");

        if (string.Equals(rewritten, line, StringComparison.Ordinal)) return null;

        var edit = new LspTextEdit
        {
            StartLine   = idx,
            StartColumn = 0,
            EndLine     = idx,
            EndColumn   = line.Length,
            NewText     = rewritten,
        };

        return FixerHelpers.SingleFileEdit(AppResources.CodeAnalysis_Fix_WH0070_Title, d.FilePath, edit);
    }
}
