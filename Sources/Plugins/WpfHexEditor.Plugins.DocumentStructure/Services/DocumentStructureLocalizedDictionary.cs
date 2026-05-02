///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.DocumentStructure
// File        : DocumentStructureLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.DocumentStructure.Properties;

namespace WpfHexEditor.Plugins.DocumentStructure.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.DocumentStructure localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:documentSvc="clr-namespace:WpfHexEditor.Plugins.DocumentStructure.Services"
///   &lt;documentSvc:DocumentStructureLocalizedDictionary/&gt;
/// </summary>
public sealed class DocumentStructureLocalizedDictionary : LocalizedResourceDictionary
{
    public DocumentStructureLocalizedDictionary()
    {
        RegisterResourceManager(DocumentStructureResources.ResourceManager);
        LoadResources();
    }
}
