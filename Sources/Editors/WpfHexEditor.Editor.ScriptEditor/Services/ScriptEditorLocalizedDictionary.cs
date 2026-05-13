///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.ScriptEditor
// File        : Services/ScriptEditorLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.ScriptEditor.Properties;

namespace WpfHexEditor.Editor.ScriptEditor.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Editor.ScriptEditor localized strings
/// as static resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:scriptSvc="clr-namespace:WpfHexEditor.Editor.ScriptEditor.Services;assembly=WpfHexEditor.Editor.ScriptEditor"
///   &lt;scriptSvc:ScriptEditorLocalizedDictionary/&gt;
/// </summary>
public sealed class ScriptEditorLocalizedDictionary : LocalizedResourceDictionary
{
    public ScriptEditorLocalizedDictionary()
    {
        RegisterResourceManager(ScriptEditorResources.ResourceManager);
        LoadResources();
    }
}
