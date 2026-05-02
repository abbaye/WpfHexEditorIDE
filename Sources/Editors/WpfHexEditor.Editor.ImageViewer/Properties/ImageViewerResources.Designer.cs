///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.ImageViewer
// File        : ImageViewerResources.Designer.cs
// Description : Strongly-typed accessor for ImageViewer-specific UI strings.
///////////////////////////////////////////////////////////////

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Editor.ImageViewer.Properties;

internal static class ImageViewerResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "WpfHexEditor.Editor.ImageViewer.Properties.ImageViewerResources",
            typeof(ImageViewerResources).Assembly);

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    private static string GetString(string name) =>
        ResourceManager.GetString(name, _resourceCulture) ?? name;

    internal static string ImageViewer_SaveImageAs  => GetString(nameof(ImageViewer_SaveImageAs));
    internal static string ImageViewer_LoadingImage => GetString(nameof(ImageViewer_LoadingImage));
}
