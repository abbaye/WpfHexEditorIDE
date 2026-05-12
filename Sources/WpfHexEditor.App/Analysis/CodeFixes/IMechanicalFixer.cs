// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeFixes/IMechanicalFixer.cs
// Description: Contract for a per-rule mechanical fix that produces an LSP-
//              compatible edit. Implementations read the source file directly
//              and never call Roslyn — keeps the popup latency well under 100ms.
// ==========================================================

using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Analysis.CodeFixes;

internal interface IMechanicalFixer
{
    /// <summary>The RuleId this fixer reacts to (e.g. "WH0062").</summary>
    string RuleId { get; }

    /// <summary>
    /// Build a code action for `diagnostic` or return null if no safe fix is possible.
    /// `sourceLines` is the file content split by newline (caller-cached so multiple
    /// fixers on the same line don't re-read the file).
    /// </summary>
    LspCodeAction? TryBuild(AnalysisDiagnostic diagnostic, IReadOnlyList<string> sourceLines);
}
