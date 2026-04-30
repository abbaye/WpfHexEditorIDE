// ==========================================================
// Project: WpfHexEditor.Plugins.CustomParserTemplate
// File: Properties/CustomParserTemplateResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for CustomParserTemplate plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.CustomParserTemplate.Properties;

internal static class CustomParserTemplateResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.CustomParserTemplate.Properties.CustomParserTemplateResources",
                typeof(CustomParserTemplateResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Load Error.
    /// </summary>
    internal static string CustomParser_Dialog_LoadError
        => ResourceManager.GetString("CustomParser_Dialog_LoadError", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Confirm Delete.
    /// </summary>
    internal static string CustomParser_Dialog_ConfirmDelete
        => ResourceManager.GetString("CustomParser_Dialog_ConfirmDelete", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Delete Error.
    /// </summary>
    internal static string CustomParser_Dialog_DeleteError
        => ResourceManager.GetString("CustomParser_Dialog_DeleteError", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Save Template.
    /// </summary>
    internal static string CustomParser_Dialog_SaveTemplate
        => ResourceManager.GetString("CustomParser_Dialog_SaveTemplate", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Save Error.
    /// </summary>
    internal static string CustomParser_Dialog_SaveError
        => ResourceManager.GetString("CustomParser_Dialog_SaveError", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Add Block.
    /// </summary>
    internal static string CustomParser_Dialog_AddBlock
        => ResourceManager.GetString("CustomParser_Dialog_AddBlock", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Apply Template.
    /// </summary>
    internal static string CustomParser_Dialog_ApplyTemplate
        => ResourceManager.GetString("CustomParser_Dialog_ApplyTemplate", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Export.
    /// </summary>
    internal static string CustomParser_Dialog_Export
        => ResourceManager.GetString("CustomParser_Dialog_Export", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Export Success.
    /// </summary>
    internal static string CustomParser_Dialog_ExportSuccess
        => ResourceManager.GetString("CustomParser_Dialog_ExportSuccess", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Export Error.
    /// </summary>
    internal static string CustomParser_Dialog_ExportError
        => ResourceManager.GetString("CustomParser_Dialog_ExportError", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Import Success.
    /// </summary>
    internal static string CustomParser_Dialog_ImportSuccess
        => ResourceManager.GetString("CustomParser_Dialog_ImportSuccess", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Import Error.
    /// </summary>
    internal static string CustomParser_Dialog_ImportError
        => ResourceManager.GetString("CustomParser_Dialog_ImportError", _resourceCulture)!;

    /// <summary>
    ///   Recherche une chaîne localisée semblable à Import Template.
    /// </summary>
    internal static string CustomParser_Dialog_ImportTemplate
        => ResourceManager.GetString("CustomParser_Dialog_ImportTemplate", _resourceCulture)!;
}
