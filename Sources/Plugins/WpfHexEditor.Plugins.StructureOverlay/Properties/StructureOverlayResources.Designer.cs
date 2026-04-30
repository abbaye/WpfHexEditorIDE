// ==========================================================
// Project: WpfHexEditor.Plugins.StructureOverlay
// File: Properties/StructureOverlayResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for StructureOverlay plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.StructureOverlay.Properties;

internal static class StructureOverlayResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.StructureOverlay.Properties.StructureOverlayResources",
                typeof(StructureOverlayResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Failed to load format definition.
    /// </summary>
    internal static string StructureOverlay_Error_LoadFailed
        => ResourceManager.GetString("StructureOverlay_Error_LoadFailed", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Remove all structure overlays?
    /// </summary>
    internal static string StructureOverlay_Confirm_RemoveAll
        => ResourceManager.GetString("StructureOverlay_Confirm_RemoveAll", _resourceCulture)!;
}
