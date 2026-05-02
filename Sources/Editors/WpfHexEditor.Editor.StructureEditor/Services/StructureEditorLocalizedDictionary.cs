///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.StructureEditor
// File        : StructureEditorLocalizedDictionary.cs
// Description : Self-contained LocalizedResourceDictionary for StructureEditor.
///////////////////////////////////////////////////////////////
using WpfHexEditor.Core.Localization.Properties;
using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.StructureEditor.Properties;

namespace WpfHexEditor.Editor.StructureEditor.Services;

public sealed class StructureEditorLocalizedDictionary : LocalizedResourceDictionary
{
    public StructureEditorLocalizedDictionary()
    {
        RegisterResourceManager(CommonResources.ResourceManager);
        RegisterResourceManager(StructureEditorResources.ResourceManager);
        LoadResources();
    }
}
