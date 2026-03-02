//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Media;
using WpfHexEditor.App.Controls;

namespace WpfHexEditor.App;

/// <summary>
/// VS-style output logger that writes timestamped, color-coded messages to the <see cref="OutputPanel"/>.
/// Register the panel once, then call static methods from anywhere.
/// </summary>
internal static class OutputLogger
{
    private static OutputPanel? _panel;

    // ─── Log-level brush palette ───────────────────────────────────────
    // INFO  : null  = inherits theme foreground (white/light in dark themes)
    private static readonly Brush WarnBrush  = Freeze(Color.FromRgb(220, 180,  50)); // gold
    private static readonly Brush ErrorBrush = Freeze(Color.FromRgb(240,  80,  60)); // red-orange
    private static readonly Brush DebugBrush = Freeze(Color.FromRgb(130, 130, 130)); // gray

    private static SolidColorBrush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    /// <summary>
    /// Binds the logger to an <see cref="OutputPanel"/> instance.
    /// </summary>
    public static void Register(OutputPanel panel)
    {
        _panel = panel;
    }

    // ─── Public API ────────────────────────────────────────────────────

    public static void Info(string message)  => Log("INFO ", message, null);
    public static void Warn(string message)  => Log("WARN ", message, WarnBrush);
    public static void Error(string message) => Log("ERROR", message, ErrorBrush);
    public static void Debug(string message) => Log("DEBUG", message, DebugBrush);

    /// <summary>
    /// Writes a separator line to visually group output sections.
    /// </summary>
    public static void Section(string title)
        => Append($"──── {title} ────────────────────────────────────", null);

    /// <summary>
    /// Clears all output.
    /// </summary>
    public static void Clear()
    {
        if (_panel is null) return;
        _panel.OutputBox.Dispatcher.Invoke(() => _panel.ClearOutput());
    }

    // ─── Internals ─────────────────────────────────────────────────────

    private static void Log(string level, string message, Brush? color)
        => Append($"[{DateTime.Now:HH:mm:ss}] {level}  {message}", color);

    private static void Append(string text, Brush? color)
    {
        if (_panel is null) return;
        _panel.OutputBox.Dispatcher.Invoke(() => _panel.AppendLine(text, color));
    }
}
