///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.DisassemblyViewer
// File        : DisassemblyViewerLocalizedDictionary.cs
// Description : Self-contained LocalizedResourceDictionary for DisassemblyViewer.
///////////////////////////////////////////////////////////////
using WpfHexEditor.Core.Localization.Properties;
using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.DisassemblyViewer.Properties;

namespace WpfHexEditor.Editor.DisassemblyViewer.Services;

public sealed class DisassemblyViewerLocalizedDictionary : LocalizedResourceDictionary
{
    public DisassemblyViewerLocalizedDictionary()
    {
        RegisterResourceManager(CommonResources.ResourceManager);
        RegisterResourceManager(DisassemblyViewerResources.ResourceManager);
        LoadResources();
    }
}
