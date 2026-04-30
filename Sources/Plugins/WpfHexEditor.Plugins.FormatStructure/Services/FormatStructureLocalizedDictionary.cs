///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.FormatStructure
// File        : FormatStructureLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.FormatStructure.Properties;

namespace WpfHexEditor.Plugins.FormatStructure.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.FormatStructure localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:formatstSvc="clr-namespace:WpfHexEditor.Plugins.FormatStructure.Services"
///   &lt;formatstSvc:FormatStructureLocalizedDictionary/&gt;
/// </summary>
public sealed class FormatStructureLocalizedDictionary : LocalizedResourceDictionary
{
    public FormatStructureLocalizedDictionary()
    {
        RegisterResourceManager(FormatStructureResources.ResourceManager);
        LoadResources();
    }
}
