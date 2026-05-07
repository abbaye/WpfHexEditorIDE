// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/IDE/AnalysisToolbarContribution.cs
// Description: Adds "Run Code Analysis" to the Tools menu.
//              Uses IMenuAdapter so the item participates in the
//              standard menu organizer infrastructure.
// ==========================================================

using WpfHexEditor.PluginHost.Adapters;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App.Analysis.IDE;

internal sealed class AnalysisToolbarContribution
{
    private const string RunUiId    = "WpfHexEditor.Analysis.MenuRun";
    private const string ReportUiId = "WpfHexEditor.Analysis.MenuOpenReport";

    private readonly IMenuAdapter _menu;

    internal AnalysisToolbarContribution(IMenuAdapter menu)
        => _menu = menu;

    internal void Register(Func<Task> runSolution, Func<Task> openReport)
    {
        _menu.AddMenuItem(RunUiId, new MenuItemDescriptor
        {
            Header     = "_Run Code Analysis",
            ParentPath = "Tools",
            IconGlyph  = "",
            Group      = "Analysis",
            Category   = "Analysis",
            ToolTip    = "Run a full code analysis on the current solution",
            Command    = new RelayCommand(() => _ = runSolution()),
        });

        _menu.AddMenuItem(ReportUiId, new MenuItemDescriptor
        {
            Header     = "_Open Analysis Report",
            ParentPath = "Tools",
            IconGlyph  = "",
            Group      = "Analysis",
            Category   = "Analysis",
            ToolTip    = "Open the last code analysis report",
            Command    = new RelayCommand(() => _ = openReport()),
        });
    }

    internal void Unregister()
    {
        _menu.RemoveMenuItem(RunUiId);
        _menu.RemoveMenuItem(ReportUiId);
    }
}
