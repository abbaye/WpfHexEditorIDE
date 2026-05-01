// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Properties/EditorCoreResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for Editor.Core.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Editor.Core.Properties;

internal static class EditorCoreResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Editor.Core.Properties.EditorCoreResources",
                typeof(EditorCoreResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }
}
