///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.DocumentEditor
// File        : Services/DocumentEditorLocalizedDictionary.cs
// Description : Self-contained localized ResourceDictionary for the Document Editor.
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Properties;
using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.DocumentEditor.Properties;

namespace WpfHexEditor.Editor.DocumentEditor.Services;

public sealed class DocumentEditorLocalizedDictionary : LocalizedResourceDictionary
{
    public DocumentEditorLocalizedDictionary()
    {
        RegisterResourceManager(CommonResources.ResourceManager);
        RegisterResourceManager(DocumentEditorResources.ResourceManager);
        LoadResources();
    }
}
