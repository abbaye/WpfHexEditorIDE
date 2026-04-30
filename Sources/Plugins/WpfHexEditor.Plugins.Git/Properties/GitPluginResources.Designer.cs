// ==========================================================
// Project: WpfHexEditor.Plugins.Git
// File: Properties/GitPluginResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for Git Integration plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.Git.Properties;

internal static class GitPluginResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.Git.Properties.GitPluginResources",
                typeof(GitPluginResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "Git Changes"</summary>
    internal static string Git_Changes
        => ResourceManager.GetString("Git_Changes", _resourceCulture)!;

    /// <summary>Localized: "Git History"</summary>
    internal static string Git_History
        => ResourceManager.GetString("Git_History", _resourceCulture)!;
}
