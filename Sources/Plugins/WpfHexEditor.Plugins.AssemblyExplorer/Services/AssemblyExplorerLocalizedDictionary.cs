///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.AssemblyExplorer
// File        : AssemblyExplorerLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.AssemblyExplorer.Properties;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.AssemblyExplorer localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:assemblySvc="clr-namespace:WpfHexEditor.Plugins.AssemblyExplorer.Services"
///   &lt;assemblySvc:AssemblyExplorerLocalizedDictionary/&gt;
/// </summary>
public sealed class AssemblyExplorerLocalizedDictionary : LocalizedResourceDictionary
{
    public AssemblyExplorerLocalizedDictionary()
    {
        RegisterResourceManager(AssemblyExplorerResources.ResourceManager);
        LoadResources();
    }
}
