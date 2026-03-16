//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Media;
using WpfHexEditor.App.Controls;
using WpfHexEditor.Options;

namespace WpfHexEditor.App;

/// <summary>
/// VS-style output logger that writes timestamped, color-coded messages to the <see cref="OutputPanel"/>.
/// Register the panel once, then call static methods from anywhere.
/// Log-level colours are user-configurable via Options → Environment → Output.
/// </summary>
internal static class OutputLogger
{
    private static OutputPanel? _panel;

    // --- Log-level brush palette ---------------------------------------
    // INFO  : null  = inherits theme foreground (white/light in dark themes)
    // Remaining brushes are rebuilt from AppSettings on Register() and on ColorsChanged.
    private static Brush _warnBrush    = FreezeRgb(220, 180,  50);
    private static Brush _errorBrush   = FreezeRgb(240,  80,  60);
    private static Brush _debugBrush   = FreezeRgb(130, 130, 130);
    private static Brush _successBrush = FreezeRgb( 78, 201, 176);

    private static SolidColorBrush FreezeRgb(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush FreezeHex(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
                hex = "FF" + hex;

            var color = Color.FromArgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                Convert.ToByte(hex[6..8], 16));

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch
        {
            // Fallback to a neutral gray if the hex string is malformed.
            return FreezeRgb(128, 128, 128);
        }
    }

    /// <summary>
    /// Rebuilds the four log-level brushes from the current <see cref="AppSettingsService"/> values.
    /// Called at registration time and whenever the user saves new colours in Options.
    /// </summary>
    private static void RebuildBrushes()
    {
        var opts = AppSettingsService.Instance.Current.OutputLogger;
        _warnBrush    = FreezeHex(opts.WarnColor);
        _errorBrush   = FreezeHex(opts.ErrorColor);
        _debugBrush   = FreezeHex(opts.DebugColor);
        _successBrush = FreezeHex(opts.SuccessColor);
    }

    /// <summary>
    /// Binds the logger to an <see cref="OutputPanel"/> instance.
    /// Also loads user-configured colours and subscribes to future colour changes.
    /// </summary>
    public static void Register(OutputPanel panel)
    {
        _panel = panel;
        RebuildBrushes();
        OutputLoggerSettings.ColorsChanged += RebuildBrushes;
    }

    // --- Source channel constants --------------------------------------

    public const string SourceGeneral      = "General";
    public const string SourcePluginSystem = "Plugin System";
    public const string SourceBuild        = "Build";
    public const string SourceDebug        = "Debug";

    // --- Public API — General channel ----------------------------------

    public static void Info(string message)  => Log("INFO ", message, null,          SourceGeneral);
    public static void Warn(string message)  => Log("WARN ", message, _warnBrush,   SourceGeneral);
    public static void Error(string message) => Log("ERROR", message, _errorBrush,  SourceGeneral);
    public static void Debug(string message) => Log("DEBUG", message, _debugBrush,  SourceDebug);

    // --- Public API — Plugin System channel ----------------------------

    public static void PluginInfo(string message)  => Log("INFO ", message, null,          SourcePluginSystem);
    public static void PluginWarn(string message)  => Log("WARN ", message, _warnBrush,   SourcePluginSystem);
    public static void PluginError(string message) => Log("ERROR", message, _errorBrush,  SourcePluginSystem);

    // --- Public API — Build channel ------------------------------------

    public static void BuildInfo(string message)    => Log("INFO ", message, null,           SourceBuild);
    public static void BuildWarn(string message)    => Log("WARN ", message, _warnBrush,    SourceBuild);
    public static void BuildError(string message)   => Log("ERROR", message, _errorBrush,   SourceBuild);
    public static void BuildSuccess(string message) => Log("OK   ", message, _successBrush, SourceBuild);

    /// <summary>
    /// Writes a separator line to visually group sections in the Build channel
    /// (e.g. project name, target name).
    /// </summary>
    public static void BuildSection(string title)
        => Append($"---- {title} ------------------------------------", null, SourceBuild);

    // -----------

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
