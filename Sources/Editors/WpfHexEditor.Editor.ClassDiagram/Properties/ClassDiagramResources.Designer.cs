// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Properties/ClassDiagramResources.Designer.cs
// Description: Strongly-typed resource accessor for ClassDiagram strings.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Editor.ClassDiagram.Properties;

internal static class ClassDiagramResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Editor.ClassDiagram.Properties.ClassDiagramResources",
                typeof(ClassDiagramResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    internal static string ClassDiagEd_Menu_AddMember
        => ResourceManager.GetString("ClassDiagEd_Menu_AddMember", _resourceCulture)!;

    internal static string ClassDiagEd_Menu_ChangeType
        => ResourceManager.GetString("ClassDiagEd_Menu_ChangeType", _resourceCulture)!;

    internal static string ClassDiagEd_Menu_AutoLayout
        => ResourceManager.GetString("ClassDiagEd_Menu_AutoLayout", _resourceCulture)!;
}
