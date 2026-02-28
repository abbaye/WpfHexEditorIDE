//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
//////////////////////////////////////////////

using System.Windows.Controls;

namespace WpfHexEditor.Sample.Docking;

/// <summary>
/// VS-style output logger that writes timestamped messages to the Output panel.
/// Register the output <see cref="TextBox"/> once, then call static methods from anywhere.
/// </summary>
internal static class OutputLogger
{
    private static TextBox? _output;

    /// <summary>
    /// Binds the logger to the Output panel's TextBox.
    /// </summary>
    public static void Register(TextBox outputTextBox)
    {
        _output = outputTextBox;
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
        if (_output is null) return;
        _output.Dispatcher.Invoke(() => _output.Clear());
    }

    // ─── Internals ─────────────────────────────────────────────────────

    private static void Log(string level, string message)
    {
        Append($"[{DateTime.Now:HH:mm:ss}] {level}  {message}{Environment.NewLine}");
    }

    private static void Append(string text)
    {
        if (_output is null) return;

        _output.Dispatcher.Invoke(() =>
        {
            _output.AppendText(text);
            _output.ScrollToEnd();
        });
    }
}
