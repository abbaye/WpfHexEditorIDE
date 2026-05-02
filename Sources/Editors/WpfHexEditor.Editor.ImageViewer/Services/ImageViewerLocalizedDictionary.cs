///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.ImageViewer
// File        : ImageViewerLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.ImageViewer.Properties;

namespace WpfHexEditor.Editor.ImageViewer.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Editor.ImageViewer localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:imagevieSvc="clr-namespace:WpfHexEditor.Editor.ImageViewer.Services"
///   &lt;imagevieSvc:ImageViewerLocalizedDictionary/&gt;
/// </summary>
public sealed class ImageViewerLocalizedDictionary : LocalizedResourceDictionary
{
    public ImageViewerLocalizedDictionary()
    {
        RegisterResourceManager(ImageViewerResources.ResourceManager);
        LoadResources();
    }
}
