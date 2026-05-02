///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.ResxLocalization
// File        : ResxLocalizationLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.ResxLocalization.Properties;

namespace WpfHexEditor.Plugins.ResxLocalization.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.ResxLocalization localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:resxlocaSvc="clr-namespace:WpfHexEditor.Plugins.ResxLocalization.Services"
///   &lt;resxlocaSvc:ResxLocalizationLocalizedDictionary/&gt;
/// </summary>
public sealed class ResxLocalizationLocalizedDictionary : LocalizedResourceDictionary
{
    public ResxLocalizationLocalizedDictionary()
    {
        RegisterResourceManager(ResxLocalizationResources.ResourceManager);
        LoadResources();
    }
}
