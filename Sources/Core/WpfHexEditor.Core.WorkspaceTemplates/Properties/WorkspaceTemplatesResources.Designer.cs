///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Core.WorkspaceTemplates
// File        : WorkspaceTemplatesResources.Designer.cs
///////////////////////////////////////////////////////////////

using System.Resources;

namespace WpfHexEditor.Core.WorkspaceTemplates.Properties;

internal static class WorkspaceTemplatesResources
{
    private static ResourceManager? _resourceManager;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Core.WorkspaceTemplates.Properties.WorkspaceTemplatesResources",
                typeof(WorkspaceTemplatesResources).Assembly);
            return _resourceManager;
        }
    }
}
