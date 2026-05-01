///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.EntropyViewer
// File        : EntropyViewerLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.EntropyViewer.Properties;

namespace WpfHexEditor.Editor.EntropyViewer.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Editor.EntropyViewer localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:evSvc="clr-namespace:WpfHexEditor.Editor.EntropyViewer.Services;assembly=WpfHexEditor.Editor.EntropyViewer"
///   &lt;evSvc:EntropyViewerLocalizedDictionary/&gt;
/// </summary>
public sealed class EntropyViewerLocalizedDictionary : LocalizedResourceDictionary
{
    public EntropyViewerLocalizedDictionary()
    {
        RegisterResourceManager(EntropyViewerResources.ResourceManager);
        LoadResources();
    }
}
