// ==========================================================
// Project: WpfHexEditor.Plugins.DataInspector
// File: Properties/DataInspectorResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for DataInspector plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.DataInspector.Properties;

internal static class DataInspectorResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.DataInspector.Properties.DataInspectorResources",
                typeof(DataInspectorResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Looks up a localized string similar to "Data Inspector".</summary>
    internal static string DataInspector_PluginName
        => ResourceManager.GetString("DataInspector_PluginName", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Data Analysis".</summary>
    internal static string DataInspector_OptionsCategory
        => ResourceManager.GetString("DataInspector_OptionsCategory", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Data Inspector".</summary>
    internal static string DataInspector_PanelTitle
        => ResourceManager.GetString("DataInspector_PanelTitle", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "_Data Inspector".</summary>
    internal static string DataInspector_MenuItem
        => ResourceManager.GetString("DataInspector_MenuItem", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "No data to display".</summary>
    internal static string BarChart_NoData
        => ResourceManager.GetString("BarChart_NoData", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Total".</summary>
    internal static string BarChart_Total
        => ResourceManager.GetString("BarChart_Total", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Max".</summary>
    internal static string BarChart_Max
        => ResourceManager.GetString("BarChart_Max", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Entropy".</summary>
    internal static string BarChart_Entropy
        => ResourceManager.GetString("BarChart_Entropy", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "bits/byte".</summary>
    internal static string BarChart_BitsPerByte
        => ResourceManager.GetString("BarChart_BitsPerByte", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "bytes".</summary>
    internal static string BarChart_Bytes
        => ResourceManager.GetString("BarChart_Bytes", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "No bytes selected — select bytes in the hex editor.".</summary>
    internal static string DataInspector_EmptyState
        => ResourceManager.GetString("DataInspector_EmptyState", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Active view".</summary>
    internal static string DataInspector_Scope_ActiveView
        => ResourceManager.GetString("DataInspector_Scope_ActiveView", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Whole file".</summary>
    internal static string DataInspector_Scope_WholeFile
        => ResourceManager.GetString("DataInspector_Scope_WholeFile", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Selection".</summary>
    internal static string DataInspector_Scope_Selection
        => ResourceManager.GetString("DataInspector_Scope_Selection", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Frequency".</summary>
    internal static string DataInspector_Mode_Frequency
        => ResourceManager.GetString("DataInspector_Mode_Frequency", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Entropy".</summary>
    internal static string DataInspector_Mode_Entropy
        => ResourceManager.GetString("DataInspector_Mode_Entropy", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "No data".</summary>
    internal static string DataInspector_Footer_NoData
        => ResourceManager.GetString("DataInspector_Footer_NoData", _resourceCulture)!;
}
