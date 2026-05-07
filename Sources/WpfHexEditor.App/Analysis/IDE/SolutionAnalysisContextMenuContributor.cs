// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/IDE/SolutionAnalysisContextMenuContributor.cs
// Description: Injects "Run Code Analysis" on Solution nodes
//              in the Solution Explorer context menu.
// ==========================================================

using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.Analysis.IDE;

internal sealed class SolutionAnalysisContextMenuContributor : ISolutionExplorerContextMenuContributor
{
    private readonly Func<string, Task> _runAnalysis;

    internal SolutionAnalysisContextMenuContributor(Func<string, Task> runAnalysis)
        => _runAnalysis = runAnalysis;

    public IReadOnlyList<SolutionContextMenuItem> GetContextMenuItems(string nodeKind, string? nodePath)
    {
        if (nodeKind != "Solution" || string.IsNullOrEmpty(nodePath)) return [];

        return
        [
            SolutionContextMenuItem.Separator(),
            SolutionContextMenuItem.Item(
                "Run Code Analysis…",
                new RelayCommand(() => _ = _runAnalysis(nodePath!)),
                iconGlyph: ""),
        ];
    }
}
