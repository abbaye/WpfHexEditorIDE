///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.AIAssistant
// File        : AIAssistantLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.AIAssistant.Properties;

namespace WpfHexEditor.Plugins.AIAssistant.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.AIAssistant localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:aiassistSvc="clr-namespace:WpfHexEditor.Plugins.AIAssistant.Services"
///   &lt;aiassistSvc:AIAssistantLocalizedDictionary/&gt;
/// </summary>
public sealed class AIAssistantLocalizedDictionary : LocalizedResourceDictionary
{
    public AIAssistantLocalizedDictionary()
    {
        RegisterResourceManager(AIAssistantResources.ResourceManager);
        LoadResources();
    }
}
