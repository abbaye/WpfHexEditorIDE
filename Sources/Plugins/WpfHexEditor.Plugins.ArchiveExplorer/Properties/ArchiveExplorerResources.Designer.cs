// ==========================================================
// Project: WpfHexEditor.Plugins.ArchiveExplorer
// File: Properties/ArchiveExplorerResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for Archive Explorer plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.ArchiveExplorer.Properties;

internal static class ArchiveExplorerResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.ArchiveExplorer.Properties.ArchiveExplorerResources",
                typeof(ArchiveExplorerResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "Archive Explorer"</summary>
    internal static string ArchiveExplorer_PluginName
        => ResourceManager.GetString("ArchiveExplorer_PluginName", _resourceCulture)!;

    /// <summary>Localized: "Open Archive"</summary>
    internal static string ArchiveExplorer_OpenArchive
        => ResourceManager.GetString("ArchiveExplorer_OpenArchive", _resourceCulture)!;

    /// <summary>Localized: "Select default extract folder"</summary>
    internal static string ArchiveExplorer_SelectExtractFolder
        => ResourceManager.GetString("ArchiveExplorer_SelectExtractFolder", _resourceCulture)!;
}
