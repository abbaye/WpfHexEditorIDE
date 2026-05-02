// ==========================================================
// Project: WpfHexEditor.Plugins.LSPTools
// File: Properties/LspToolsResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for LSP Tools plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.LSPTools.Properties;

internal static class LspToolsResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.LSPTools.Properties.LspToolsResources",
                typeof(LspToolsResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "Call Hierarchy"</summary>
    internal static string LspTools_CallHierarchy
        => ResourceManager.GetString("LspTools_CallHierarchy", _resourceCulture)!;

    /// <summary>Localized: "Type Hierarchy"</summary>
    internal static string LspTools_TypeHierarchy
        => ResourceManager.GetString("LspTools_TypeHierarchy", _resourceCulture)!;
}
