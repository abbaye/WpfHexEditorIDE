// ==========================================================
// Project: WpfHexEditor.Plugins.StringExtractor
// File: Properties/StringExtractorResources.Designer.cs
// Description: Strongly-typed resource class for String Extractor plugin.
// Architecture: Standard ResourceManager pattern; satellite assemblies
//               provide localized translations at runtime.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.StringExtractor.Properties;

internal static class StringExtractorResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo?     _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.StringExtractor.Properties.StringExtractorResources",
                typeof(StringExtractorResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    internal static string StringExtractor_PluginName
        => ResourceManager.GetString("StringExtractor_PluginName", _resourceCulture)!;

    internal static string StringExtractor_PanelTitle
        => ResourceManager.GetString("StringExtractor_PanelTitle", _resourceCulture)!;

    internal static string StringExtractor_MenuItem
        => ResourceManager.GetString("StringExtractor_MenuItem", _resourceCulture)!;

    internal static string StringExtractor_ColOffset
        => ResourceManager.GetString("StringExtractor_ColOffset", _resourceCulture)!;

    internal static string StringExtractor_ColLength
        => ResourceManager.GetString("StringExtractor_ColLength", _resourceCulture)!;

    internal static string StringExtractor_ColEncoding
        => ResourceManager.GetString("StringExtractor_ColEncoding", _resourceCulture)!;

    internal static string StringExtractor_ColValue
        => ResourceManager.GetString("StringExtractor_ColValue", _resourceCulture)!;

    internal static string StringExtractor_MinLength
        => ResourceManager.GetString("StringExtractor_MinLength", _resourceCulture)!;

    internal static string StringExtractor_FilterPlaceholder
        => ResourceManager.GetString("StringExtractor_FilterPlaceholder", _resourceCulture)!;

    internal static string StringExtractor_ExtractButton
        => ResourceManager.GetString("StringExtractor_ExtractButton", _resourceCulture)!;

    internal static string StringExtractor_ExportButton
        => ResourceManager.GetString("StringExtractor_ExportButton", _resourceCulture)!;

    internal static string StringExtractor_CopyButton
        => ResourceManager.GetString("StringExtractor_CopyButton", _resourceCulture)!;

    internal static string StringExtractor_NoFile
        => ResourceManager.GetString("StringExtractor_NoFile", _resourceCulture)!;

    internal static string StringExtractor_NoResults
        => ResourceManager.GetString("StringExtractor_NoResults", _resourceCulture)!;

    internal static string StringExtractor_Extracting
        => ResourceManager.GetString("StringExtractor_Extracting", _resourceCulture)!;

    internal static string StringExtractor_ResultCount
        => ResourceManager.GetString("StringExtractor_ResultCount", _resourceCulture)!;

    internal static string StringExtractor_FilteredCount
        => ResourceManager.GetString("StringExtractor_FilteredCount", _resourceCulture)!;

    internal static string StringExtractor_Enc_ASCII
        => ResourceManager.GetString("StringExtractor_Enc_ASCII", _resourceCulture)!;

    internal static string StringExtractor_Enc_UTF8
        => ResourceManager.GetString("StringExtractor_Enc_UTF8", _resourceCulture)!;

    internal static string StringExtractor_Enc_UTF16LE
        => ResourceManager.GetString("StringExtractor_Enc_UTF16LE", _resourceCulture)!;

    internal static string StringExtractor_Enc_UTF16BE
        => ResourceManager.GetString("StringExtractor_Enc_UTF16BE", _resourceCulture)!;

    internal static string StringExtractor_ExportTitle
        => ResourceManager.GetString("StringExtractor_ExportTitle", _resourceCulture)!;

    internal static string StringExtractor_ExportFilter
        => ResourceManager.GetString("StringExtractor_ExportFilter", _resourceCulture)!;

    internal static string StringExtractor_ExportSuccess
        => ResourceManager.GetString("StringExtractor_ExportSuccess", _resourceCulture)!;

    internal static string StringExtractor_NavigateTo
        => ResourceManager.GetString("StringExtractor_NavigateTo", _resourceCulture)!;

    internal static string StringExtractor_CopyValue
        => ResourceManager.GetString("StringExtractor_CopyValue", _resourceCulture)!;

    internal static string StringExtractor_CopyOffset
        => ResourceManager.GetString("StringExtractor_CopyOffset", _resourceCulture)!;

    internal static string StringExtractor_Tooltip_Extract
        => ResourceManager.GetString("StringExtractor_Tooltip_Extract", _resourceCulture)!;

    internal static string StringExtractor_Tooltip_Export
        => ResourceManager.GetString("StringExtractor_Tooltip_Export", _resourceCulture)!;

    internal static string StringExtractor_Tooltip_MinLength
        => ResourceManager.GetString("StringExtractor_Tooltip_MinLength", _resourceCulture)!;
}
