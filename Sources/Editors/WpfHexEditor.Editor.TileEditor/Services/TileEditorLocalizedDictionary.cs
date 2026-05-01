// GNU Affero General Public License v3.0  2026
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.TileEditor
// File        : Services/TileEditorLocalizedDictionary.cs

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.TileEditor.Properties;

namespace WpfHexEditor.Editor.TileEditor.Services;

public sealed class TileEditorLocalizedDictionary : LocalizedResourceDictionary
{
    public TileEditorLocalizedDictionary()
    {
        RegisterResourceManager(TileEditorResources.ResourceManager);
        LoadResources();
    }
}
