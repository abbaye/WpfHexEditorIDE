// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/IDE/FileAnalysisContextMenuContributor.cs
// Description: Injects "Analyze File…" on .cs File nodes
//              in the Solution Explorer context menu.
// ==========================================================

using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.Analysis.IDE;

internal sealed class FileAnalysisContextMenuContributor : ISolutionExplorerContextMenuContributor
{
    private readonly Func<string, Task> _runAnalysis;

    internal FileAnalysisContextMenuContributor(Func<string, Task> runAnalysis)
        => _runAnalysis = runAnalysis;

    public IReadOnlyList<SolutionContextMenuItem> GetContextMenuItems(string nodeKind, string? nodePath)
    {
        if (nodeKind != "File" || string.IsNullOrEmpty(nodePath)) return [];
        if (!nodePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return [];

        return
        [
            SolutionContextMenuItem.Item(
                "Analyze File…",
                new RelayCommand(() => _ = _runAnalysis(nodePath!)),
                iconGlyph: ""),
        ];
    }
}
