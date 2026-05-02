///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.PluginInstaller
// File        : InstallerResources.Designer.cs
///////////////////////////////////////////////////////////////

using System.Resources;

namespace WpfHexEditor.PluginInstaller.Properties;

internal static class InstallerResources
{
    private static ResourceManager? _resourceManager;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.PluginInstaller.Properties.InstallerResources",
                typeof(InstallerResources).Assembly);
            return _resourceManager;
        }
    }
}
