// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Options/ScreenRecorderOptions.cs
// Description: Singleton options model persisted to JSON in %APPDATA%.
//              Pattern mirrors AIAssistantOptions.
// ==========================================================

using System.IO;
using System.Text.Json;
using WpfHexEditor.Plugins.ScreenRecorder.Models;

namespace WpfHexEditor.Plugins.ScreenRecorder.Options;

public sealed class ScreenRecorderOptions
{
    private static readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "ScreenRecorder", "settings.json");

    private static ScreenRecorderOptions? _instance;
    public  static ScreenRecorderOptions   Instance => _instance ??= Load();

    // ── Capture ───────────────────────────────────────────────────────────────
    public string      HotkeyCapture    { get; set; } = "F9";
    public string      HotkeyStop       { get; set; } = "Shift+F9";
    public RecordingMode DefaultMode      { get; set; } = RecordingMode.Screenshot;
    public int         TimerInterval    { get; set; } = 500;

    // ── Overlay ───────────────────────────────────────────────────────────────
    public string OverlayColor   { get; set; } = "#FF2196F3";
    public double OverlayOpacity { get; set; } = 0.5;
    public bool   ShowHud        { get; set; } = true;

    // ── Export ────────────────────────────────────────────────────────────────
    public string DefaultOutputPath       { get; set; } = string.Empty;
    public double DefaultScale            { get; set; } = 1.0;
    public int    DefaultLoopCount        { get; set; } = 0;
    public int    RepeatLastFrameDelay    { get; set; } = 1000;

    // ── Session ───────────────────────────────────────────────────────────────
    public string DefaultSaveFolder { get; set; } = string.Empty;
    public bool   ConfirmDiscard    { get; set; } = true;
    public int    MaxFrames         { get; set; } = 9999;

    // ── ffmpeg ────────────────────────────────────────────────────────────────
    public string FfmpegPath { get; set; } = string.Empty;

    public static ScreenRecorderOptions Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<ScreenRecorderOptions>(json) ?? new();
            }
        }
        catch { /* first run or corrupt — use defaults */ }
        return new();
    }

    public static void Reload() => _instance = Load();

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { /* non-fatal */ }
    }
}
