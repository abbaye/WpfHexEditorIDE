///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.CodeEditor
// File        : CodeEditorLocalizedDictionary.cs
// Description : Self-contained LocalizedResourceDictionary for CodeEditor.
//               Merges CommonResources (shared strings) with the module-specific
//               CodeEditorResources into a single WPF ResourceDictionary that
//               updates in place on runtime culture changes.
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Properties;
using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.CodeEditor.Properties;

namespace WpfHexEditor.Editor.CodeEditor.Services;

public sealed class CodeEditorLocalizedDictionary : LocalizedResourceDictionary
{
    public CodeEditorLocalizedDictionary()
    {
        RegisterResourceManager(CommonResources.ResourceManager);
        RegisterResourceManager(CodeEditorResources.ResourceManager);
        LoadResources();
    }
}
