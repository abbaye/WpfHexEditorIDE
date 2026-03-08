// ==========================================================
// Project: WpfHexEditor.Plugins.DataInspector
// File: DataInspectorOptions.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Persistent settings for the Data Inspector plugin.
//     Serialized to %AppData%\WpfHexaEditor\Plugins\DataInspector.json.
// ==========================================================

using System.IO;
using System.Text.Json;

namespace WpfHexEditor.Plugins.DataInspector.Options;

/// <summary>Position of the frequency/entropy chart relative to the interpretations list.</summary>
public enum ChartPosition { Left, Right, Top, Bottom }

public sealed class DataInspectorOptions
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexaEditor", "Plugins", "DataInspector.json");

    private static DataInspectorOptions? _instance;
    public static DataInspectorOptions Instance => _instance ??= Load();

    // -- Option properties ----------------------------------------------------

    /// <summary>Whether the inspector auto-refreshes when the hex selection changes.</summary>
    public bool AutoRefresh { get; set; } = true;

    /// <summary>Maximum number of bytes read for string interpretation.</summary>
    public int MaxStringBytes { get; set; } = 64;

    /// <summary>Whether little-endian byte order is used by default.</summary>
    public bool DefaultLittleEndian { get; set; } = true;

    /// <summary>Whether to show the ByteChart panel alongside the inspector.</summary>
    public bool ShowByteChart { get; set; } = true;

    /// <summary>Position of the frequency/entropy chart relative to the interpretations list.</summary>
    public ChartPosition ChartPosition { get; set; } = ChartPosition.Left;

    // -- Persistence ----------------------------------------------------------

    public static DataInspectorOptions Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<DataInspectorOptions>(json) ?? new();
            }
        }
        catch { /* fall through to defaults */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* save is best-effort */ }
    }
}
