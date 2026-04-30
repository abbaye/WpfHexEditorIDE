// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: Properties/XamlDesignerResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for XamlDesigner plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.XamlDesigner.Properties;

internal static class XamlDesignerResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.XamlDesigner.Properties.XamlDesignerResources",
                typeof(XamlDesignerResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>
    ///   Looks up a localized string similar to XAML Outline.
    /// </summary>
    internal static string XamlDesigner_Panel_Outline
        => ResourceManager.GetString("XamlDesigner_Panel_Outline", _resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to XAML Properties.
    /// </summary>
    internal static string XamlDesigner_Panel_Properties
        => ResourceManager.GetString("XamlDesigner_Panel_Properties", _resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to XAML Toolbox.
    /// </summary>
    internal static string XamlDesigner_Panel_Toolbox
        => ResourceManager.GetString("XamlDesigner_Panel_Toolbox", _resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Resource Browser.
    /// </summary>
    internal static string XamlDesigner_Panel_ResourceBrowser
        => ResourceManager.GetString("XamlDesigner_Panel_ResourceBrowser", _resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Design Data.
    /// </summary>
    internal static string XamlDesigner_Panel_DesignData
        => ResourceManager.GetString("XamlDesigner_Panel_DesignData", _resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Animation Timeline.
    /// </summary>
    internal static string XamlDesigner_Panel_AnimationTimeline
        => ResourceManager.GetString("XamlDesigner_Panel_AnimationTimeline", _resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Design History.
    /// </summary>
    internal static string XamlDesigner_Panel_DesignHistory
        => ResourceManager.GetString("XamlDesigner_Panel_DesignHistory", _resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Binding Inspector.
    /// </summary>
    internal static string XamlDesigner_Panel_BindingInspector
        => ResourceManager.GetString("XamlDesigner_Panel_BindingInspector", _resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Live Visual Tree.
    /// </summary>
    internal static string XamlDesigner_Panel_LiveVisualTree
        => ResourceManager.GetString("XamlDesigner_Panel_LiveVisualTree", _resourceCulture)!;
}
