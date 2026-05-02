///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Core.ProjectSystem
// File        : ProjectSystemLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Core.ProjectSystem.Properties;

namespace WpfHexEditor.Core.ProjectSystem.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Core.ProjectSystem localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:psSvc="clr-namespace:WpfHexEditor.Core.ProjectSystem.Services"
///   &lt;psSvc:ProjectSystemLocalizedDictionary/&gt;
/// </summary>
public sealed class ProjectSystemLocalizedDictionary : LocalizedResourceDictionary
{
    public ProjectSystemLocalizedDictionary()
    {
        RegisterResourceManager(ProjectSystemResources.ResourceManager);
        LoadResources();
    }
}
