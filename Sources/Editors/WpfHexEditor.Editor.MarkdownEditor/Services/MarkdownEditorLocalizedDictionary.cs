///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.MarkdownEditor
// File        : Services/MarkdownEditorLocalizedDictionary.cs
// Description : Injects MarkdownEditorResources (toolbar labels/tooltips,
//               status bar labels, context menu items, and dialog titles)
//               into the WPF dynamic resource tree for runtime culture switching.
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.MarkdownEditor.Properties;

namespace WpfHexEditor.Editor.MarkdownEditor.Services;

public sealed class MarkdownEditorLocalizedDictionary : LocalizedResourceDictionary
{
    public MarkdownEditorLocalizedDictionary()
    {
        RegisterResourceManager(MarkdownEditorResources.ResourceManager);
    }
}
