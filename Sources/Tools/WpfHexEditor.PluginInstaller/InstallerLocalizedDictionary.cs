///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.PluginInstaller
// File        : InstallerLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.PluginInstaller.Properties;

namespace WpfHexEditor.PluginInstaller;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.PluginInstaller localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:instSvc="clr-namespace:WpfHexEditor.PluginInstaller;assembly=WpfHexEditor.PluginInstaller"
///   &lt;instSvc:InstallerLocalizedDictionary/&gt;
/// </summary>
public sealed class InstallerLocalizedDictionary : LocalizedResourceDictionary
{
    public InstallerLocalizedDictionary()
    {
        RegisterResourceManager(InstallerResources.ResourceManager);
        LoadResources();
    }
}
