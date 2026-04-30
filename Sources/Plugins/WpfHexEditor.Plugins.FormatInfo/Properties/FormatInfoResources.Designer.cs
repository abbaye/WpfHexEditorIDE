// ==========================================================
// Project: WpfHexEditor.Plugins.FormatInfo
// File: Properties/FormatInfoResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for Format Info plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.FormatInfo.Properties;

internal static class FormatInfoResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.FormatInfo.Properties.FormatInfoResources",
                typeof(FormatInfoResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "Format Info"</summary>
    internal static string FormatInfo_PluginName
        => ResourceManager.GetString("FormatInfo_PluginName", _resourceCulture)!;
}
