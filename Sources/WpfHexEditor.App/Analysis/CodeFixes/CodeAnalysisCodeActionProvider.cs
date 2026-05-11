// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeFixes/CodeAnalysisCodeActionProvider.cs
// Description: ICodeActionProvider that turns the latest CodeAnalysisReport
//              into inline Code Actions (Ctrl+. / lightbulb).
//              Looks up diagnostics for the caret line, then asks every
//              registered mechanical fixer for a quickfix; also emits an
//              "Add inline suppress marker" action for every diagnostic.
// Architecture Notes:
//     Stateless w.r.t. the editor — only consumes a DiagnosticIndex set by
//     the CodeAnalysisModule whenever a fresh report is published.
//     Reads files via File.ReadAllLines on the worker thread (provider is
//     called off the UI thread); result is cached per (path, mtime).
// ==========================================================

using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using WpfHexEditor.App.Analysis.CodeFixes.Fixers;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Editor.CodeEditor.Providers;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Analysis.CodeFixes;

internal sealed class CodeAnalysisCodeActionProvider : ICodeActionProvider
{
    private readonly IMechanicalFixer[] _fixers =
    [
        new ConfigureAwaitFixer(),
        new CountToAnyFixer(),
        new RemoveTodoMarkerFixer(),
    ];

    // Phase 11 — Roslyn-based fixers operate on parsed SyntaxTree, safer than regex.
    private readonly IRoslynFixer[] _roslynFixers =
    [
        new AsyncVoidToTaskFixer(),
        new UnusedLocalRemoveFixer(),
        new UnusedPrivateRemoveFixer(),
    ];

    private DiagnosticIndex? _index;

    /// <summary>Update the index with the diagnostics from the latest report.</summary>
    internal void SetDiagnostics(IReadOnlyList<AnalysisDiagnostic> diagnostics)
        => _index = new DiagnosticIndex(diagnostics);

    public Task<IReadOnlyList<LspCodeAction>> GetCodeActionsAsync(
        string filePath, int line, int column, CancellationToken ct)
    {
        var index = _index;
        if (index is null) return Task.FromResult<IReadOnlyList<LspCodeAction>>([]);

        // line is 0-based at the editor surface; AnalysisDiagnostic.Line is 1-based.
        int oneBased = line + 1;
        var diagnostics = index.At(filePath, oneBased, tolerance: 1).ToList();
        if (diagnostics.Count == 0) return Task.FromResult<IReadOnlyList<LspCodeAction>>([]);

        return Task.FromResult<IReadOnlyList<LspCodeAction>>(BuildActions(filePath, diagnostics));
    }

    private IReadOnlyList<LspCodeAction> BuildActions(string filePath, List<AnalysisDiagnostic> diagnostics)
    {
        IReadOnlyList<string>? lines = null;
        SyntaxTree?            tree  = null;
        try
        {
            if (File.Exists(filePath))
            {
                var text = File.ReadAllText(filePath);
                lines    = text.Split('\n');
                if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    tree = CSharpSyntaxTree.ParseText(text, path: filePath);
            }
        }
        catch { /* best-effort; provider must never throw */ }

        var actions = new List<LspCodeAction>();
        foreach (var d in diagnostics)
        {
            if (lines is not null)
            {
                foreach (var fixer in _fixers)
                {
                    if (fixer.RuleId != d.Id) continue;
                    var built = fixer.TryBuild(d, lines);
                    if (built is not null) actions.Add(built);
                }
            }
            if (tree is not null)
            {
                foreach (var roslyn in _roslynFixers)
                {
                    if (roslyn.RuleId != d.Id) continue;
                    var built = roslyn.TryBuild(d, tree);
                    if (built is not null) actions.Add(built);
                }
            }
            actions.Add(BuildSuppressAction(d));
        }
        return actions;
    }

    /// <summary>"Suppress WH0xxx here" — always available, inserts a comment marker above the line.</summary>
    private static LspCodeAction BuildSuppressAction(AnalysisDiagnostic d)
    {
        int idx = Math.Max(0, d.Line - 1);
        string marker = $"// CodeAnalysis: suppress {d.Id}\n";

        var edit = new LspTextEdit
        {
            StartLine   = idx,
            StartColumn = 0,
            EndLine     = idx,
            EndColumn   = 0,
            NewText     = marker,
        };

        return new LspCodeAction
        {
            Title = string.Format(AppResources.CodeAnalysis_Fix_Suppress_Title, d.Id),
            Kind  = "quickfix",
            Edit  = new LspWorkspaceEdit
            {
                Changes = new Dictionary<string, IReadOnlyList<LspTextEdit>>(StringComparer.OrdinalIgnoreCase)
                {
                    [d.FilePath] = new[] { edit },
                },
            },
        };
    }
}
