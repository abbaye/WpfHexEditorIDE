///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.CustomParserTemplate
// File        : CustomParserTemplateLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.CustomParserTemplate.Properties;

namespace WpfHexEditor.Plugins.CustomParserTemplate.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.CustomParserTemplate localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:custompaSvc="clr-namespace:WpfHexEditor.Plugins.CustomParserTemplate.Services"
///   &lt;custompaSvc:CustomParserTemplateLocalizedDictionary/&gt;
/// </summary>
public sealed class CustomParserTemplateLocalizedDictionary : LocalizedResourceDictionary
{
    public CustomParserTemplateLocalizedDictionary()
    {
        RegisterResourceManager(CustomParserTemplateResources.ResourceManager);
        LoadResources();
    }
}
