// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Properties/ClassDiagramResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for ClassDiagram plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.ClassDiagram.Properties;

internal static class ClassDiagramResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.ClassDiagram.Properties.ClassDiagramResources",
                typeof(ClassDiagramResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Class Diagram.
    /// </summary>
    internal static string ClassDiagram_PluginName
        => ResourceManager.GetString("ClassDiagram_PluginName", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Class Diagram.
    /// </summary>
    internal static string ClassDiagram_OptionsCategory
        => ResourceManager.GetString("ClassDiagram_OptionsCategory", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Class Outline.
    /// </summary>
    internal static string ClassDiagram_OutlinePanelTitle
        => ResourceManager.GetString("ClassDiagram_OutlinePanelTitle", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Class Properties.
    /// </summary>
    internal static string ClassDiagram_PropertiesPanelTitle
        => ResourceManager.GetString("ClassDiagram_PropertiesPanelTitle", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Diagram Toolbox.
    /// </summary>
    internal static string ClassDiagram_ToolboxPanelTitle
        => ResourceManager.GetString("ClassDiagram_ToolboxPanelTitle", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Relationships.
    /// </summary>
    internal static string ClassDiagram_RelationshipsPanelTitle
        => ResourceManager.GetString("ClassDiagram_RelationshipsPanelTitle", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Diagram History.
    /// </summary>
    internal static string ClassDiagram_HistoryPanelTitle
        => ResourceManager.GetString("ClassDiagram_HistoryPanelTitle", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Diagram Search.
    /// </summary>
    internal static string ClassDiagram_SearchPanelTitle
        => ResourceManager.GetString("ClassDiagram_SearchPanelTitle", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Metrics Dashboard.
    /// </summary>
    internal static string ClassDiagram_MetricsPanelTitle
        => ResourceManager.GetString("ClassDiagram_MetricsPanelTitle", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Reset.
    /// </summary>
    internal static string ClassDiagram_Reset
        => ResourceManager.GetString("ClassDiagram_Reset", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Class _Outline.
    /// </summary>
    internal static string ClassDiagram_Menu_OutlineHeader
        => ResourceManager.GetString("ClassDiagram_Menu_OutlineHeader", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Generate Class Diagram for Solution.
    /// </summary>
    internal static string ClassDiagram_GenerateSolutionDiagram
        => ResourceManager.GetString("ClassDiagram_GenerateSolutionDiagram", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Export Metrics CSV.
    /// </summary>
    internal static string ClassDiagram_ExportMetricsCSV
        => ResourceManager.GetString("ClassDiagram_ExportMetricsCSV", _resourceCulture)!;
}
