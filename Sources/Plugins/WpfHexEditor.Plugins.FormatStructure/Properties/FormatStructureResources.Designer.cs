// ==========================================================
// Project: WpfHexEditor.Plugins.FormatStructure
// File: Properties/FormatStructureResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for FormatStructure plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.FormatStructure.Properties;

internal static class FormatStructureResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.FormatStructure.Properties.FormatStructureResources",
                typeof(FormatStructureResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "0 fields"</summary>
    internal static string FormatStruct_ZeroFields
        => ResourceManager.GetString("FormatStruct_ZeroFields", _resourceCulture)!;

    /// <summary>Localized: "No format detected"</summary>
    internal static string FormatStruct_NoFormatDetected
        => ResourceManager.GetString("FormatStruct_NoFormatDetected", _resourceCulture)!;
}
