// ==========================================================
// Project: WpfHexEditor.Plugins.FileComparison
// File: Properties/FileComparisonResources.Designer.cs
// Description: Strongly-typed resource class for FileComparison plugin.
// Architecture: Standard ResourceManager pattern; satellite assemblies
//               provide fr-CA translations at runtime.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.FileComparison.Properties;

internal static class FileComparisonResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo?     _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.FileComparison.Properties.FileComparisonResources",
                typeof(FileComparisonResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Gets the localized panel title for the Diff Hub document tab.</summary>
    internal static string FileComparison_PanelTitle
        => ResourceManager.GetString("FileComparison_PanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized dialog title for selecting File 1.</summary>
    internal static string FileComparison_SelectFile1
        => ResourceManager.GetString("FileComparison_SelectFile1", _resourceCulture)!;

    /// <summary>Gets the localized dialog title for selecting File 2.</summary>
    internal static string FileComparison_SelectFile2
        => ResourceManager.GetString("FileComparison_SelectFile2", _resourceCulture)!;

    /// <summary>Gets the localized hint shown when no files have been selected yet.</summary>
    internal static string FileComparison_SelectFilesHint
        => ResourceManager.GetString("FileComparison_SelectFilesHint", _resourceCulture)!;

    /// <summary>Gets the localized message shown when only one file has been loaded.</summary>
    internal static string FileComparison_SelectBothFiles
        => ResourceManager.GetString("FileComparison_SelectBothFiles", _resourceCulture)!;

    /// <summary>Localized: "Select files to compare"</summary>
    internal static string FileComparison_SelectFiles
        => ResourceManager.GetString("FileComparison_SelectFiles", _resourceCulture)!;

    /// <summary>Localized: "Comparing…"</summary>
    internal static string FileComparison_Comparing
        => ResourceManager.GetString("FileComparison_Comparing", _resourceCulture)!;

    /// <summary>Localized: "Cancelled"</summary>
    internal static string FileComparison_Cancelled
        => ResourceManager.GetString("FileComparison_Cancelled", _resourceCulture)!;

    /// <summary>Localized: "Select two files and click Compare"</summary>
    internal static string FileComparison_SelectTwoFiles
        => ResourceManager.GetString("FileComparison_SelectTwoFiles", _resourceCulture)!;

    /// <summary>Localized: "Multiple Formats Detected"</summary>
    internal static string FileComparison_MultipleFormats
        => ResourceManager.GetString("FileComparison_MultipleFormats", _resourceCulture)!;

    /// <summary>Localized: "Select file to compare"</summary>
    internal static string FileComparison_SelectFile
        => ResourceManager.GetString("FileComparison_SelectFile", _resourceCulture)!;
}
