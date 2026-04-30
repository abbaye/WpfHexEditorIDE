///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.PluginHost
// File        : PluginHostLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.PluginHost.Properties;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.PluginHost localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:phSvc="clr-namespace:WpfHexEditor.PluginHost.Services;assembly=WpfHexEditor.PluginHost"
///   &lt;phSvc:PluginHostLocalizedDictionary/&gt;
/// </summary>
public sealed class PluginHostLocalizedDictionary : LocalizedResourceDictionary
{
    public PluginHostLocalizedDictionary()
    {
        RegisterResourceManager(PluginHostResources.ResourceManager);
        LoadResources();
    }
}
