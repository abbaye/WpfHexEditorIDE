// ==========================================================
// Project: WpfHexEditor.Plugins.FileStatistics
// File: Properties/FileStatisticsResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for FileStatistics plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.FileStatistics.Properties;

internal static class FileStatisticsResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.FileStatistics.Properties.FileStatisticsResources",
                typeof(FileStatisticsResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "File Statistics"</summary>
    internal static string FileStats_PluginName
        => ResourceManager.GetString("FileStats_PluginName", _resourceCulture)!;

    /// <summary>Localized: "High null byte ratio"</summary>
    internal static string FileStats_HighNullRatio
        => ResourceManager.GetString("FileStats_HighNullRatio", _resourceCulture)!;

    /// <summary>Localized: "Very high entropy"</summary>
    internal static string FileStats_VeryHighEntropy
        => ResourceManager.GetString("FileStats_VeryHighEntropy", _resourceCulture)!;

    /// <summary>Localized: "Data may be encrypted or compressed"</summary>
    internal static string FileStats_MayBeEncrypted
        => ResourceManager.GetString("FileStats_MayBeEncrypted", _resourceCulture)!;

    /// <summary>Localized: "No file loaded"</summary>
    internal static string FileStats_NoFileLoaded
        => ResourceManager.GetString("FileStats_NoFileLoaded", _resourceCulture)!;
}
