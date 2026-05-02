// ==========================================================
// Project: WpfHexEditor.Plugins.ResxLocalization
// File: Properties/ResxLocalizationResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for RESX Localization plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.ResxLocalization.Properties;

internal static class ResxLocalizationResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.ResxLocalization.Properties.ResxLocalizationResources",
                typeof(ResxLocalizationResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "Locale Browser"</summary>
    internal static string ResxLoc_LocaleBrowser
        => ResourceManager.GetString("ResxLoc_LocaleBrowser", _resourceCulture)!;

    /// <summary>Localized: "Missing Translations"</summary>
    internal static string ResxLoc_MissingTranslations
        => ResourceManager.GetString("ResxLoc_MissingTranslations", _resourceCulture)!;

    /// <summary>Localized: "No locale data loaded"</summary>
    internal static string ResxLoc_NoLocaleData
        => ResourceManager.GetString("ResxLoc_NoLocaleData", _resourceCulture)!;
}
