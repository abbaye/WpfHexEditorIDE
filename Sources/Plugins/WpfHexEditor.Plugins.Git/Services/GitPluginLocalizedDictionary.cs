///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.Git
// File        : GitPluginLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.Git.Properties;

namespace WpfHexEditor.Plugins.Git.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.Git localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:gitplugiSvc="clr-namespace:WpfHexEditor.Plugins.Git.Services"
///   &lt;gitplugiSvc:GitPluginLocalizedDictionary/&gt;
/// </summary>
public sealed class GitPluginLocalizedDictionary : LocalizedResourceDictionary
{
    public GitPluginLocalizedDictionary()
    {
        RegisterResourceManager(GitPluginResources.ResourceManager);
        LoadResources();
    }
}
