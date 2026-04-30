///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.FormatInfo
// File        : FormatInfoLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.FormatInfo.Properties;

namespace WpfHexEditor.Plugins.FormatInfo.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.FormatInfo localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:formatinSvc="clr-namespace:WpfHexEditor.Plugins.FormatInfo.Services"
///   &lt;formatinSvc:FormatInfoLocalizedDictionary/&gt;
/// </summary>
public sealed class FormatInfoLocalizedDictionary : LocalizedResourceDictionary
{
    public FormatInfoLocalizedDictionary()
    {
        RegisterResourceManager(FormatInfoResources.ResourceManager);
        LoadResources();
    }
}
