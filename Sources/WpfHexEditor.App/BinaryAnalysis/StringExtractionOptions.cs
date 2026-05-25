//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.App.BinaryAnalysis;

/// <summary>Persisted settings for the String Extraction panel.</summary>
internal sealed class StringExtractionOptions
{
    /// <summary>File path of the last TBL loaded. Null when cleared or never loaded.</summary>
    public string? LastTblFilePath { get; set; }

    // ── Persistence ───────────────────────────────────────────────────────────

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexaEditor", "BinaryAnalysis", "StringExtraction.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static StringExtractionOptions Load()
    {
        if (!File.Exists(FilePath)) return new StringExtractionOptions();
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<StringExtractionOptions>(json, JsonOpts)
                ?? new StringExtractionOptions();
        }
        catch { return new StringExtractionOptions(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* non-critical */ }
    }
}
