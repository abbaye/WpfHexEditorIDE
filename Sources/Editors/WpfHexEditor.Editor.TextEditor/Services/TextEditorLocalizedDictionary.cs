///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.TextEditor
// File        : TextEditorLocalizedDictionary.cs
// Description : Self-contained localized ResourceDictionary for WpfHexEditor.Editor.TextEditor.
//               Registers two ResourceManagers:
//               1. CommonResources  — shared strings (Close, OK, Undo, Copy…)
//               2. TextEditorResources — TextEditor-specific strings (language label,
//                  status bar caret position, encoding…)
//               Injected into the TextEditor UserControl resources so that DynamicResource
//               lookups resolve correctly across the full visual tree.
//
// Usage (TextEditor.xaml Resources or App resources):
//   xmlns:svc="clr-namespace:WpfHexEditor.Editor.TextEditor.Services"
//   <svc:TextEditorLocalizedDictionary/>
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Properties;
using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.TextEditor.Properties;

namespace WpfHexEditor.Editor.TextEditor.Services;

/// <summary>
/// A <see cref="LocalizedResourceDictionary"/> that covers all string keys used
/// by the TextEditor control — common strings via <see cref="CommonResources"/> and
/// TextEditor-specific strings via <see cref="TextEditorResources"/>.
/// </summary>
public sealed class TextEditorLocalizedDictionary : LocalizedResourceDictionary
{
    /// <summary>
    /// Initialises the dictionary with both common strings and TextEditor-specific strings.
    /// </summary>
    public TextEditorLocalizedDictionary()
    {
        RegisterResourceManager(CommonResources.ResourceManager);
        RegisterResourceManager(TextEditorResources.ResourceManager);
        LoadResources();
    }
}
