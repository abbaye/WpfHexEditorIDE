
//////////////////////////////////////////////
// Apache 2.0  - 2026
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
        => OutputLogger.Info($"[{category}] {message}");

    public void Clear() => OutputLogger.Clear();

    public IReadOnlyList<string> GetRecentLines(int count)
        => OutputLogger.GetRecentLines(count);
}
