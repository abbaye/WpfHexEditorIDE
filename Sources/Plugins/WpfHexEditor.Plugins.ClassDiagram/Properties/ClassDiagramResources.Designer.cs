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
}
