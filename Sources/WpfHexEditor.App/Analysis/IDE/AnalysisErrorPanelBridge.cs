// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/IDE/AnalysisErrorPanelBridge.cs
// Description: Pushes analysis diagnostics to the IDE Error Panel and clears
//              stale WH0xxx entries on each re-run. Roslyn CS/IDE entries
//              are sent with source "Code Analysis" and filtered by source
//              so they don't collide with the LSP-produced diagnostics.
// ==========================================================

using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.SDK.Contracts.Services;
using SdkSeverity = WpfHexEditor.SDK.Contracts.Services.DiagnosticSeverity;

namespace WpfHexEditor.App.Analysis.IDE;

internal sealed class AnalysisErrorPanelBridge
{
    private const string SourceId = "Code Analysis";

    private readonly IErrorPanelService _errorPanel;

    internal AnalysisErrorPanelBridge(IErrorPanelService errorPanel)
        => _errorPanel = errorPanel;

    internal void ClearPrevious()
        => _errorPanel.ClearPluginDiagnostics(SourceId);

    internal void Push(IReadOnlyList<AnalysisDiagnostic> diagnostics)
    {
        foreach (var d in diagnostics)
        {
            _errorPanel.PostDiagnostic(
                Map(d.Severity),
                $"[{d.Id}] {d.Message}",
                source: string.IsNullOrEmpty(d.FilePath)
                    ? SourceId
                    : $"{d.FilePath}",
                line:   d.Line,
                column: d.Column);
        }
    }

    private static SdkSeverity Map(DiagnosticSeverity s) => s switch
    {
        DiagnosticSeverity.Error   => SdkSeverity.Error,
        DiagnosticSeverity.Warning => SdkSeverity.Warning,
        _                          => SdkSeverity.Info,
    };
}
