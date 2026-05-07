// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/ViewModels/IssueViewModel.cs
// Description: ViewModel wrapping a single AnalysisDiagnostic for display.
// ==========================================================

using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.UI.ViewModels;

public sealed class IssueViewModel
{
    public string Id          { get; }
    public string Message     { get; }
    public string FilePath    { get; }
    public string FileName    { get; }
    public int    Line        { get; }
    public string ProjectName { get; }
    public string Source      { get; }
    public DiagnosticSeverity Severity { get; }

    public string SeverityIcon => Severity switch
    {
        DiagnosticSeverity.Error   => "●",
        DiagnosticSeverity.Warning => "◑",
        _                          => "○",
    };

    public IssueViewModel(AnalysisDiagnostic d)
    {
        Id          = d.Id;
        Message     = d.Message;
        FilePath    = d.FilePath;
        FileName    = System.IO.Path.GetFileName(d.FilePath);
        Line        = d.Line;
        ProjectName = d.ProjectName;
        Source      = d.RuleSource;
        Severity    = d.Severity;
    }
}
