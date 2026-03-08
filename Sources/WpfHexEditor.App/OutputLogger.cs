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

    // --- Log-level brush palette ---------------------------------------
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

    // --- Source channel constants --------------------------------------

    public const string SourceGeneral      = "General";
    public const string SourcePluginSystem = "Plugin System";
    public const string SourceBuild        = "Build";
    public const string SourceDebug        = "Debug";

    // --- Public API — General channel ----------------------------------

    public static void Info(string message)  => Log("INFO ", message, null,       SourceGeneral);
    public static void Warn(string message)  => Log("WARN ", message, WarnBrush,  SourceGeneral);
    public static void Error(string message) => Log("ERROR", message, ErrorBrush, SourceGeneral);
    public static void Debug(string message) => Log("DEBUG", message, DebugBrush, SourceDebug);

    // --- Public API — Plugin System channel ----------------------------

    public static void PluginInfo(string message)  => Log("INFO ", message, null,       SourcePluginSystem);
    public static void PluginWarn(string message)  => Log("WARN ", message, WarnBrush,  SourcePluginSystem);
    public static void PluginError(string message) => Log("ERROR", message, ErrorBrush, SourcePluginSystem);

    /// <summary>
    /// Writes a separator line to visually group output sections in the General channel.
    /// </summary>
    public static void Section(string title)
        => Append($"---- {title} ------------------------------------", null, SourceGeneral);

    /// <summary>
    /// Returns the last <paramref name="count"/> lines from the currently active source channel.
    /// Returns an empty list if the panel is not registered.
    /// </summary>
    public static IReadOnlyList<string> GetRecentLines(int count)
    {
        if (_panel is null) return [];
        return _panel.OutputBox.Dispatcher.Invoke(
            () => _panel.GetRecentLinesFromSource(_panel.ActiveSource, count));
    }

    /// <summary>
    /// Clears the currently visible source channel.
    /// </summary>
    public static void Clear()
    {
        if (_panel is null) return;
        _panel.OutputBox.Dispatcher.Invoke(() => _panel.ClearOutput());
    }

    // --- Internals -----------------------------------------------------

    private static void Log(string level, string message, Brush? color, string source)
        => Append($"[{DateTime.Now:HH:mm:ss}] {level}  {message}", color, source);

    private static void Append(string text, Brush? color, string source)
    {
        if (_panel is null) return;
        _panel.OutputBox.Dispatcher.Invoke(() => _panel.AppendLine(text, color, source));
    }
}
