///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.ChangesetEditor
// File        : ChangesetEditorLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.ChangesetEditor.Properties;

namespace WpfHexEditor.Editor.ChangesetEditor.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Editor.ChangesetEditor localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:ceSvc="clr-namespace:WpfHexEditor.Editor.ChangesetEditor.Services;assembly=WpfHexEditor.Editor.ChangesetEditor"
///   &lt;ceSvc:ChangesetEditorLocalizedDictionary/&gt;
/// </summary>
public sealed class ChangesetEditorLocalizedDictionary : LocalizedResourceDictionary
{
    public ChangesetEditorLocalizedDictionary()
    {
        RegisterResourceManager(ChangesetEditorResources.ResourceManager);
        LoadResources();
    }
}
