///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.DiffViewer
// File        : DiffViewerLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.DiffViewer.Properties;

namespace WpfHexEditor.Editor.DiffViewer.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Editor.DiffViewer localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:dvSvc="clr-namespace:WpfHexEditor.Editor.DiffViewer.Services;assembly=WpfHexEditor.Editor.DiffViewer"
///   &lt;dvSvc:DiffViewerLocalizedDictionary/&gt;
/// </summary>
public sealed class DiffViewerLocalizedDictionary : LocalizedResourceDictionary
{
    public DiffViewerLocalizedDictionary()
    {
        RegisterResourceManager(DiffViewerResources.ResourceManager);
        LoadResources();
    }
}
