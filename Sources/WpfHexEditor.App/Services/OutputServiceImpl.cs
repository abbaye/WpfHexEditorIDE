
//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Adapts the OutputPanel to the IOutputService SDK contract used by plugins.
/// </summary>
public sealed class OutputServiceImpl : IOutputService
{
    // OutputPanel registration is handled by OutputLogger.Register() inside OutputPanel's constructor.
    // OutputServiceImpl routes plugin log calls through the static OutputLogger.

    public void Info(string message)    => OutputLogger.Info(message);
    public void Warning(string message) => OutputLogger.Warn(message);
    public void Error(string message)   => OutputLogger.Error(message);
    public void Debug(string message)   => OutputLogger.Debug(message);

    public void Write(string category, string message)
    {
        switch (category)
        {
            case OutputLogger.SourcePluginSystem:
                // Route to the Plugin System channel with severity heuristics.
                if (message.Contains(" error ", StringComparison.OrdinalIgnoreCase)
                    || message.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                    OutputLogger.PluginError(message);
                else if (message.Contains("failed", StringComparison.OrdinalIgnoreCase)
                         || message.Contains(" warning ", StringComparison.OrdinalIgnoreCase)
                         || message.StartsWith("Warning", StringComparison.OrdinalIgnoreCase))
                    OutputLogger.PluginWarn(message);
                else
                    OutputLogger.PluginInfo(message);
                break;

            case OutputLogger.SourceBuild:
                // Route to the dedicated Build channel with severity heuristics.
                // MSBuild error/warning lines are identified by common prefixes.
                if (message.Contains(" error ", StringComparison.OrdinalIgnoreCase)
                    || message.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                    OutputLogger.BuildError(message);
                else if (message.Contains(" warning ", StringComparison.OrdinalIgnoreCase)
                         || message.StartsWith("Warning", StringComparison.OrdinalIgnoreCase))
                    OutputLogger.BuildWarn(message);
                else if (message.StartsWith("===") && message.Contains("succeeded",
                             StringComparison.OrdinalIgnoreCase))
                    OutputLogger.BuildSuccess(message);
                else
                    OutputLogger.BuildInfo(message);
                break;

            case OutputLogger.SourceUnitTesting:
                if (message.StartsWith("[Error]", StringComparison.OrdinalIgnoreCase))
                    OutputLogger.TestError(message);
                else
                    OutputLogger.TestRaw(message);
                break;

            default:
                OutputLogger.Info($"[{category}] {message}");
                break;
        }
    }

    public void Clear() => OutputLogger.Clear();

    public IReadOnlyList<string> GetRecentLines(int count)
        => OutputLogger.GetRecentLines(count);
}
