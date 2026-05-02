///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.LSPTools
// File        : LspToolsLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.LSPTools.Properties;

namespace WpfHexEditor.Plugins.LSPTools.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.LSPTools localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:lsptoolsSvc="clr-namespace:WpfHexEditor.Plugins.LSPTools.Services"
///   &lt;lsptoolsSvc:LspToolsLocalizedDictionary/&gt;
/// </summary>
public sealed class LspToolsLocalizedDictionary : LocalizedResourceDictionary
{
    public LspToolsLocalizedDictionary()
    {
        RegisterResourceManager(LspToolsResources.ResourceManager);
        LoadResources();
    }
}
