// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/ViewModels/FileMetricsViewModel.cs
// Description: ViewModel wrapping FileMetrics with sort/display helpers.
// ==========================================================

using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.UI.ViewModels;

public sealed class FileMetricsViewModel
{
    public string FileName    { get; }
    public string FilePath    { get; }
    public string ProjectName { get; }
    public int    TotalLines  { get; }
    public int    CodeLines   { get; }
    public int    TypeCount   { get; }
    public int    MethodCount { get; }
    public int    MaxCC       { get; }
    public int    Score       { get; }

    public string ScoreIcon => Score >= 80 ? "●" : Score >= 60 ? "◑" : "○";

    public FileMetricsViewModel(FileMetrics f)
    {
        FileName    = f.FileName;
        FilePath    = f.FilePath;
        ProjectName = f.ProjectName;
        TotalLines  = f.TotalLines;
        CodeLines   = f.CodeLines;
        TypeCount   = f.TypeCount;
        MethodCount = f.MethodCount;
        MaxCC       = f.MaxCyclomaticComplexity;
        Score       = f.Score;
    }
}
