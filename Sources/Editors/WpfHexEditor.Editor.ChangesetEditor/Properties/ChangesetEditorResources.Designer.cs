///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.ChangesetEditor
// File        : ChangesetEditorResources.Designer.cs
///////////////////////////////////////////////////////////////

using System.Resources;

namespace WpfHexEditor.Editor.ChangesetEditor.Properties;

internal static class ChangesetEditorResources
{
    private static ResourceManager? _resourceManager;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Editor.ChangesetEditor.Properties.ChangesetEditorResources",
                typeof(ChangesetEditorResources).Assembly);
            return _resourceManager;
        }
    }
}
