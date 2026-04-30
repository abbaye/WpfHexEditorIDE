// ==========================================================
// Project: WpfHexEditor.Plugins.PatternAnalysis
// File: Properties/PatternAnalysisResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for PatternAnalysis plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.PatternAnalysis.Properties;

internal static class PatternAnalysisResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.PatternAnalysis.Properties.PatternAnalysisResources",
                typeof(PatternAnalysisResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Looks up a localized string similar to "Pattern Analysis".</summary>
    internal static string PatternAnalysis_PluginName
        => ResourceManager.GetString("PatternAnalysis_PluginName", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Pattern Analysis".</summary>
    internal static string PatternAnalysis_PanelTitle
        => ResourceManager.GetString("PatternAnalysis_PanelTitle", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "_Pattern Analysis".</summary>
    internal static string PatternAnalysis_MenuItem
        => ResourceManager.GetString("PatternAnalysis_MenuItem", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "No data loaded".</summary>
    internal static string PatternAnalysis_NoDataLoaded
        => ResourceManager.GetString("PatternAnalysis_NoDataLoaded", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Very high entropy detected".</summary>
    internal static string PatternAnalysis_Entropy_VeryHigh
        => ResourceManager.GetString("PatternAnalysis_Entropy_VeryHigh", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Data may be encrypted or compressed (entropy &gt; 7.5 bits/byte)".</summary>
    internal static string PatternAnalysis_Entropy_VeryHigh_Desc
        => ResourceManager.GetString("PatternAnalysis_Entropy_VeryHigh_Desc", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Very low entropy detected".</summary>
    internal static string PatternAnalysis_Entropy_VeryLow
        => ResourceManager.GetString("PatternAnalysis_Entropy_VeryLow", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Data contains mostly repetitive or zero bytes (entropy &lt; 2.0 bits/byte)".</summary>
    internal static string PatternAnalysis_Entropy_VeryLow_Desc
        => ResourceManager.GetString("PatternAnalysis_Entropy_VeryLow_Desc", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Low randomness — data is repetitive or structured".</summary>
    internal static string PatternAnalysis_Entropy_Low
        => ResourceManager.GetString("PatternAnalysis_Entropy_Low", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Medium randomness — typical for mixed or compressed data".</summary>
    internal static string PatternAnalysis_Entropy_Medium
        => ResourceManager.GetString("PatternAnalysis_Entropy_Medium", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "High randomness — data may be encrypted or highly compressed".</summary>
    internal static string PatternAnalysis_Entropy_High
        => ResourceManager.GetString("PatternAnalysis_Entropy_High", _resourceCulture)!;

    /// <summary>Localized: "Extremely skewed distribution"</summary>
    internal static string PatternAnalysis_ExtremelySkewed
        => ResourceManager.GetString("PatternAnalysis_ExtremelySkewed", _resourceCulture)!;

    /// <summary>Localized: "No data to analyze. Select a range in the hex editor."</summary>
    internal static string PatternAnalysis_NoData
        => ResourceManager.GetString("PatternAnalysis_NoData", _resourceCulture)!;
}
