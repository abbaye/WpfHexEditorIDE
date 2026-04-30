// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Properties/AssemblyExplorerResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for AssemblyExplorer plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Properties;

internal static class AssemblyExplorerResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.AssemblyExplorer.Properties.AssemblyExplorerResources",
                typeof(AssemblyExplorerResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Assembly Explorer.
    /// </summary>
    internal static string AsmExplorer_PluginName
        => ResourceManager.GetString("AsmExplorer_PluginName", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Assembly Explorer.
    /// </summary>
    internal static string AsmExplorer_PanelTitle
        => ResourceManager.GetString("AsmExplorer_PanelTitle", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Assembly Search.
    /// </summary>
    internal static string AsmExplorer_SearchPanelTitle
        => ResourceManager.GetString("AsmExplorer_SearchPanelTitle", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Assembly Diff.
    /// </summary>
    internal static string AsmExplorer_DiffPanelTitle
        => ResourceManager.GetString("AsmExplorer_DiffPanelTitle", _resourceCulture)!;

    internal static string AsmExplorer_ExportCancelled
        => ResourceManager.GetString("AsmExplorer_ExportCancelled", _resourceCulture)!;

    internal static string AsmExplorer_NoAssemblyLoaded
        => ResourceManager.GetString("AsmExplorer_NoAssemblyLoaded", _resourceCulture)!;

    internal static string AsmExplorer_AnalysisCancelled
        => ResourceManager.GetString("AsmExplorer_AnalysisCancelled", _resourceCulture)!;

    internal static string AsmExplorer_Searching
        => ResourceManager.GetString("AsmExplorer_Searching", _resourceCulture)!;

    internal static string AsmExplorer_Comparing
        => ResourceManager.GetString("AsmExplorer_Comparing", _resourceCulture)!;

    internal static string AsmExplorer_NoPdb
        => ResourceManager.GetString("AsmExplorer_NoPdb", _resourceCulture)!;

    internal static string AsmExplorer_SelectMethod
        => ResourceManager.GetString("AsmExplorer_SelectMethod", _resourceCulture)!;
}
