// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentStructure
// File: Properties/DocumentStructureResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for Document Structure plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.DocumentStructure.Properties;

internal static class DocumentStructureResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.DocumentStructure.Properties.DocumentStructureResources",
                typeof(DocumentStructureResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "Document Structure"</summary>
    internal static string DocStructure_PluginName
        => ResourceManager.GetString("DocStructure_PluginName", _resourceCulture)!;
}
