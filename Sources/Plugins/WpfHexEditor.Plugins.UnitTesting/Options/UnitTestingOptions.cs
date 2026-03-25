// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: Options/UnitTestingOptions.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-24
// Description:
//     Persisted options for the Unit Testing panel.
//     Singleton pattern identical to AssemblyExplorerOptions / DataInspectorOptions.
//     Stored in %AppData%\WpfHexaEditor\Plugins\UnitTesting.json.
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Plugins.UnitTesting.Options;

/// <summary>Orientation of the detail pane relative to the results list.</summary>
public enum DetailLayout { TopBottom, LeftRight }

/// <summary>
/// Persisted options for the Unit Testing panel.
/// </summary>
public sealed class UnitTestingOptions
{
    private static UnitTestingOptions? _instance;

    /// <summary>Singleton instance (lazy-loaded from disk).</summary>
    public static UnitTestingOptions Instance => _instance ??= Load();

    // ── Settings ─────────────────────────────────────────────────────────────

    /// <summary>Automatically run tests after a successful build.</summary>
    public bool AutoRunOnBuild { get; set; } = true;

    /// <summary>Auto-select (and expand detail pane for) the first failed test after a run.</summary>
    public bool AutoExpandDetailOnFailure { get; set; } = true;

    /// <summary>Show the pass/fail/skip ratio bar at the bottom of the panel.</summary>
    public bool ShowRatioBar { get; set; } = true;

    /// <summary>Orientation of the detail pane relative to the results list.</summary>
    public DetailLayout DetailPaneLayout { get; set; } = DetailLayout.TopBottom;

    // ── Persistence ───────────────────────────────────────────────────────────

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexaEditor", "Plugins", "UnitTesting.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented              = true,
        DefaultIgnoreCondition     = JsonIgnoreCondition.Never,
        Converters                 = { new JsonStringEnumConverter() },
    };

    public static UnitTestingOptions Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<UnitTestingOptions>(json, SerializerOptions) ?? new();
            }
        }
        catch { /* fall through — return defaults */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, SerializerOptions));
        }
        catch { /* best-effort */ }
    }

    /// <summary>Forces the next access to reload from disk.</summary>
    public static void Invalidate() => _instance = Load();
}
