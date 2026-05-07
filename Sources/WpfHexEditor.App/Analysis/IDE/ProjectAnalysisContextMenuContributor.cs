// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/IDE/ProjectAnalysisContextMenuContributor.cs
// Description: Injects "Analyze Project…" on Project nodes
//              in the Solution Explorer context menu.
// ==========================================================

using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.Analysis.IDE;

internal sealed class ProjectAnalysisContextMenuContributor : ISolutionExplorerContextMenuContributor
{
    private readonly Func<string, Task> _runAnalysis;

    internal ProjectAnalysisContextMenuContributor(Func<string, Task> runAnalysis)
        => _runAnalysis = runAnalysis;

    public IReadOnlyList<SolutionContextMenuItem> GetContextMenuItems(string nodeKind, string? nodePath)
    {
        if (nodeKind != "Project" || string.IsNullOrEmpty(nodePath)) return [];

        return
        [
            SolutionContextMenuItem.Item(
                "Analyze Project…",
                new RelayCommand(() => _ = _runAnalysis(nodePath!)),
                iconGlyph: ""),
        ];
    }
}
