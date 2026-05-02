///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.TblEditor
// File        : TblEditorResources.Designer.cs
///////////////////////////////////////////////////////////////

using System.Resources;

namespace WpfHexEditor.Editor.TblEditor.Properties;

internal static class TblEditorResources
{
    private static ResourceManager? _resourceManager;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Editor.TblEditor.Properties.TblEditorResources",
                typeof(TblEditorResources).Assembly);
            return _resourceManager;
        }
    }
}
