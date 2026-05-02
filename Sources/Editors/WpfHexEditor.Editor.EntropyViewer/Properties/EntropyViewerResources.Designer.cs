///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.EntropyViewer
// File        : EntropyViewerResources.Designer.cs
// Description : Strongly-typed accessor for EntropyViewer-specific UI strings.
///////////////////////////////////////////////////////////////

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Editor.EntropyViewer.Properties;

internal static class EntropyViewerResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "WpfHexEditor.Editor.EntropyViewer.Properties.EntropyViewerResources",
            typeof(EntropyViewerResources).Assembly);

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    private static string GetString(string name) =>
        ResourceManager.GetString(name, _resourceCulture) ?? name;

    internal static string EntropyViewer_Analysing => GetString(nameof(EntropyViewer_Analysing));
}
