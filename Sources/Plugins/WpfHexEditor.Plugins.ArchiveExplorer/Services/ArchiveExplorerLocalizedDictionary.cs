///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.ArchiveExplorer
// File        : ArchiveExplorerLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.ArchiveExplorer.Properties;

namespace WpfHexEditor.Plugins.ArchiveExplorer.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.ArchiveExplorer localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:archiveeSvc="clr-namespace:WpfHexEditor.Plugins.ArchiveExplorer.Services"
///   &lt;archiveeSvc:ArchiveExplorerLocalizedDictionary/&gt;
/// </summary>
public sealed class ArchiveExplorerLocalizedDictionary : LocalizedResourceDictionary
{
    public ArchiveExplorerLocalizedDictionary()
    {
        RegisterResourceManager(ArchiveExplorerResources.ResourceManager);
        LoadResources();
    }
}
