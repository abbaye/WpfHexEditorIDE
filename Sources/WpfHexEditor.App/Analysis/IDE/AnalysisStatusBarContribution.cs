// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/IDE/AnalysisStatusBarContribution.cs
// Description: Manages the Code Analysis badge in the IDE status bar.
//              Shows score + grade when idle, spinner text while running.
//              Click opens the report document.
// ==========================================================

using WpfHexEditor.PluginHost.Adapters;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App.Analysis.IDE;

internal sealed class AnalysisStatusBarContribution
{
    private const string UiId = "WpfHexEditor.Analysis.StatusBadge";

    private readonly IStatusBarAdapter _statusBar;

    internal AnalysisStatusBarContribution(IStatusBarAdapter statusBar)
        => _statusBar = statusBar;

    internal void ShowRunning()
    {
        _statusBar.AddStatusBarItem(UiId, new StatusBarItemDescriptor
        {
            Text      = "⟳ Analyzing…",
            Alignment = StatusBarAlignment.Right,
            Order     = 200,
            ToolTip   = "Code analysis in progress…",
        });
    }

    internal void ShowScore(int score, string grade)
    {
        var dot = score >= 80 ? "●" : score >= 60 ? "◑" : "○";
        _statusBar.AddStatusBarItem(UiId, new StatusBarItemDescriptor
        {
            Text      = $"{dot} {grade}  {score}",
            Alignment = StatusBarAlignment.Right,
            Order     = 200,
            ToolTip   = $"Code Quality Score: {score}/100 — click to open report",
        });
    }

    internal void Hide()
        => _statusBar.RemoveStatusBarItem(UiId);
}
