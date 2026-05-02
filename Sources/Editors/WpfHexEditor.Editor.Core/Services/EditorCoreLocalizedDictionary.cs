///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.Core
// File        : EditorCoreLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.Core.Properties;

namespace WpfHexEditor.Editor.Core.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Editor.Core localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:ecSvc="clr-namespace:WpfHexEditor.Editor.Core.Services;assembly=WpfHexEditor.Editor.Core"
///   &lt;ecSvc:EditorCoreLocalizedDictionary/&gt;
/// </summary>
public sealed class EditorCoreLocalizedDictionary : LocalizedResourceDictionary
{
    public EditorCoreLocalizedDictionary()
    {
        RegisterResourceManager(EditorCoreResources.ResourceManager);
        LoadResources();
    }
}
