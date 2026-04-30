///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.XamlDesigner
// File        : XamlDesignerLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.XamlDesigner.Properties;

namespace WpfHexEditor.Plugins.XamlDesigner.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.XamlDesigner localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:xamldesiSvc="clr-namespace:WpfHexEditor.Plugins.XamlDesigner.Services"
///   &lt;xamldesiSvc:XamlDesignerLocalizedDictionary/&gt;
/// </summary>
public sealed class XamlDesignerLocalizedDictionary : LocalizedResourceDictionary
{
    public XamlDesignerLocalizedDictionary()
    {
        RegisterResourceManager(XamlDesignerResources.ResourceManager);
        LoadResources();
    }
}
