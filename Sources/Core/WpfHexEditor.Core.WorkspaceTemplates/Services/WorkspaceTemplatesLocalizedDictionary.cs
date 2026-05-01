///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Core.WorkspaceTemplates
// File        : WorkspaceTemplatesLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Core.WorkspaceTemplates.Properties;

namespace WpfHexEditor.Core.WorkspaceTemplates.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Core.WorkspaceTemplates localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:wtSvc="clr-namespace:WpfHexEditor.Core.WorkspaceTemplates.Services;assembly=WpfHexEditor.Core.WorkspaceTemplates"
///   &lt;wtSvc:WorkspaceTemplatesLocalizedDictionary/&gt;
/// </summary>
public sealed class WorkspaceTemplatesLocalizedDictionary : LocalizedResourceDictionary
{
    public WorkspaceTemplatesLocalizedDictionary()
    {
        RegisterResourceManager(WorkspaceTemplatesResources.ResourceManager);
        LoadResources();
    }
}
