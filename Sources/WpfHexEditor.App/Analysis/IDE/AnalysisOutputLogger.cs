// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/IDE/AnalysisOutputLogger.cs
// Description: Pipes analysis progress and errors to the IDE Output panel.
// ==========================================================

using WpfHexEditor.App.Analysis.Services;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Analysis.IDE;

internal sealed class AnalysisOutputLogger : IProgress<AnalysisProgressUpdate>
{
    private readonly IOutputService _output;
    private readonly string         _verbosity;

    internal AnalysisOutputLogger(IOutputService output, string verbosity = "Normal")
    {
        _output    = output;
        _verbosity = verbosity;
    }

    public void Report(AnalysisProgressUpdate value)
    {
        if (_verbosity == "Silent") return;
        if (_verbosity == "Normal" && value.Percent % 20 != 0 && value.Percent != 100) return;

        _output.Write("Code Analysis", $"{value.Percent,3}%  {value.Phase}");
    }

    internal void LogError(string message)
        => _output.Error($"[Code Analysis] {message}");

    internal void LogStart(string scopeLabel)
        => _output.Info($"[Code Analysis] Starting analysis — {scopeLabel}");

    internal void LogFinished(int score, string grade, int fileCount)
        => _output.Info(
            $"[Code Analysis] Finished — Score: {score}/100 ({grade})  Files: {fileCount}");
}
