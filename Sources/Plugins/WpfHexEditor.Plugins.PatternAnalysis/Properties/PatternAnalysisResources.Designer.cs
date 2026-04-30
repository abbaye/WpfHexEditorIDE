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
}
