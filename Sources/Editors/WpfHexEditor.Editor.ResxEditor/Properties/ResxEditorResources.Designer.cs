///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.ResxEditor
// File        : Properties/ResxEditorResources.Designer.cs
///////////////////////////////////////////////////////////////

using System.Resources;

namespace WpfHexEditor.Editor.ResxEditor.Properties;

internal static class ResxEditorResources
{
    private static ResourceManager? _resourceManager;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Editor.ResxEditor.Properties.ResxEditorResources",
                typeof(ResxEditorResources).Assembly);
            return _resourceManager;
        }
    }
}
