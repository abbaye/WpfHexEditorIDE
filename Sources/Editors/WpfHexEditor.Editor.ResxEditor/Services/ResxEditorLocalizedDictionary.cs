///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.ResxEditor
// File        : Services/ResxEditorLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.ResxEditor.Properties;

namespace WpfHexEditor.Editor.ResxEditor.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Editor.ResxEditor localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:reSvc="clr-namespace:WpfHexEditor.Editor.ResxEditor.Services;assembly=WpfHexEditor.Editor.ResxEditor"
///   &lt;reSvc:ResxEditorLocalizedDictionary/&gt;
/// </summary>
public sealed class ResxEditorLocalizedDictionary : LocalizedResourceDictionary
{
    public ResxEditorLocalizedDictionary()
    {
        RegisterResourceManager(ResxEditorResources.ResourceManager);
        LoadResources();
    }
}
