// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeFixes/IRoslynFixer.cs
// Description: Per-rule fixer that operates on a parsed C# SyntaxTree instead
//              of regex on file content. Produces zero or one LspCodeAction
//              suitable for the existing ApplyWorkspaceEdit pipeline.
// Architecture Notes:
//     - Receives a SyntaxTree parsed from the file at the time of the menu
//       invocation — fresh on every Ctrl+. so stale-cache concerns from the
//       analysis run cannot misfire the edit
//     - Returns null when no safe transformation is possible (e.g. async void
//       on an event handler signature)
// ==========================================================

using Microsoft.CodeAnalysis;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Analysis.CodeFixes;

internal interface IRoslynFixer
{
    string RuleId { get; }

    LspCodeAction? TryBuild(AnalysisDiagnostic diagnostic, SyntaxTree tree);
}
