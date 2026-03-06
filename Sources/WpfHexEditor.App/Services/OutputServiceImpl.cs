
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Panels.IDE.Panels;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Adapts the OutputPanel to the IOutputService SDK contract used by plugins.
/// </summary>
public sealed class OutputServiceImpl : IOutputService
{
    private OutputPanel? _outputPanel;

    /// <summary>Called by MainWindow once the OutputPanel is created.</summary>
    public void SetOutputPanel(OutputPanel panel) => _outputPanel = panel;

    public void Info(string message) => _outputPanel?.Info(message);
    public void Warning(string message) => _outputPanel?.Info($"[WARN] {message}");
    public void Error(string message) => _outputPanel?.Error(message);
    public void Debug(string message) => _outputPanel?.Debug(message);

    public void Write(string category, string message)
        => _outputPanel?.Info($"[{category}] {message}");

    public void Clear() => _outputPanel?.Clear();
}
