// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Properties/XamlDesignerResources.Designer.cs
// Description: Strongly-typed resource accessor for XamlDesigner strings.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Editor.XamlDesigner.Properties;

internal static class XamlDesignerResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Editor.XamlDesigner.Properties.XamlDesignerResources",
                typeof(XamlDesignerResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    internal static string XamlDesigEd_Menu_SelectParent
        => ResourceManager.GetString("XamlDesigEd_Menu_SelectParent", _resourceCulture)!;
}
