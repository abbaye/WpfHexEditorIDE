//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
//////////////////////////////////////////////

using WpfHexEditor.App.Controls;

namespace WpfHexEditor.App;

/// <summary>
/// VS-style output logger that writes timestamped messages to the <see cref="OutputPanel"/>.
/// Register the panel once, then call static methods from anywhere.
/// </summary>
internal static class OutputLogger
{
    private static OutputPanel? _panel;

    /// <summary>
    /// Binds the logger to an <see cref="OutputPanel"/> instance.
    /// </summary>
    public static void Register(OutputPanel panel)
    {
        _panel = panel;
    }

    // ─── Public API ────────────────────────────────────────────────────

    public static void Info(string message) => Log("INFO ", message);
    public static void Warn(string message) => Log("WARN ", message);
    public static void Error(string message) => Log("ERROR", message);
    public static void Debug(string message) => Log("DEBUG", message);

    /// <summary>
    /// Writes a separator line to visually group output sections.
    /// </summary>
    public static void Section(string title)
    {
        Append($"──── {title} ────────────────────────────────────{Environment.NewLine}");
    }

    /// <summary>
    /// Clears all output.
    /// </summary>
    public static void Clear()
    {
        if (_panel is null) return;
        _panel.TextBox.Dispatcher.Invoke(() => _panel.TextBox.Clear());
    }

    // ─── Internals ─────────────────────────────────────────────────────

    private static void Log(string level, string message)
    {
        Append($"[{DateTime.Now:HH:mm:ss}] {level}  {message}{Environment.NewLine}");
    }

    private static void Append(string text)
    {
        if (_panel is null) return;

        _panel.TextBox.Dispatcher.Invoke(() =>
        {
            _panel.TextBox.AppendText(text);
            _panel.ScrollToEndIfEnabled();
        });
    }
}
