///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.PluginDev
// File        : PluginDevLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.PluginDev.Properties;

namespace WpfHexEditor.PluginDev.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.PluginDev localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:pdSvc="clr-namespace:WpfHexEditor.PluginDev.Services;assembly=WpfHexEditor.PluginDev"
///   &lt;pdSvc:PluginDevLocalizedDictionary/&gt;
/// </summary>
public sealed class PluginDevLocalizedDictionary : LocalizedResourceDictionary
{
    public PluginDevLocalizedDictionary()
    {
        RegisterResourceManager(PluginDevResources.ResourceManager);
        LoadResources();
    }
}
