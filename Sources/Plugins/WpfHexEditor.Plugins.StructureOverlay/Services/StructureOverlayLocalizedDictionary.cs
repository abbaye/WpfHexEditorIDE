///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.StructureOverlay
// File        : StructureOverlayLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.StructureOverlay.Properties;

namespace WpfHexEditor.Plugins.StructureOverlay.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.StructureOverlay localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:structurSvc="clr-namespace:WpfHexEditor.Plugins.StructureOverlay.Services"
///   &lt;structurSvc:StructureOverlayLocalizedDictionary/&gt;
/// </summary>
public sealed class StructureOverlayLocalizedDictionary : LocalizedResourceDictionary
{
    public StructureOverlayLocalizedDictionary()
    {
        RegisterResourceManager(StructureOverlayResources.ResourceManager);
        LoadResources();
    }
}
