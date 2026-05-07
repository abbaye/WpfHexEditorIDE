// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/IDE/AnalysisCommandsRegistrar.cs
// Description: Registers analysis.run / analysis.openReport /
//              analysis.clearSnapshot in the IDE CommandRegistry
//              so they are discoverable via Command Palette.
// ==========================================================

using WpfHexEditor.Core.Commands;
using SdkCmd = WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.App.Analysis.IDE;

internal sealed class AnalysisCommandsRegistrar
{
    private readonly Func<Task>  _runSolution;
    private readonly Func<Task>  _openReport;
    private readonly Action      _clearSnapshot;

    internal AnalysisCommandsRegistrar(
        Func<Task> runSolution,
        Func<Task> openReport,
        Action     clearSnapshot)
    {
        _runSolution   = runSolution;
        _openReport    = openReport;
        _clearSnapshot = clearSnapshot;
    }

    internal void Register(ICommandRegistry registry)
    {
        registry.Register(new CommandDefinition(
            "analysis.run", "Run Code Analysis", "Analysis",
            null, "", new SdkCmd.RelayCommand(() => _ = _runSolution())));

        registry.Register(new CommandDefinition(
            "analysis.openReport", "Open Code Analysis Report", "Analysis",
            null, "", new SdkCmd.RelayCommand(() => _ = _openReport())));

        registry.Register(new CommandDefinition(
            "analysis.clearSnapshot", "Clear Code Analysis Snapshot", "Analysis",
            null, null, new SdkCmd.RelayCommand(_clearSnapshot)));
    }
}
