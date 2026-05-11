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

    // ADR-022 Phase 1B — RoundTripPreviewDialog
    internal static string ClassDiagEd_RoundTripPreview_Title
        => ResourceManager.GetString("ClassDiagEd_RoundTripPreview_Title", _resourceCulture)!;

    internal static string ClassDiagEd_RoundTripPreview_Before
        => ResourceManager.GetString("ClassDiagEd_RoundTripPreview_Before", _resourceCulture)!;

    internal static string ClassDiagEd_RoundTripPreview_After
        => ResourceManager.GetString("ClassDiagEd_RoundTripPreview_After", _resourceCulture)!;

    internal static string ClassDiagEd_RoundTripPreview_Apply
        => ResourceManager.GetString("ClassDiagEd_RoundTripPreview_Apply", _resourceCulture)!;

    internal static string ClassDiagEd_RoundTripPreview_Cancel
        => ResourceManager.GetString("ClassDiagEd_RoundTripPreview_Cancel", _resourceCulture)!;

    internal static string ClassDiagEd_RoundTripPreview_DontAskAgain
        => ResourceManager.GetString("ClassDiagEd_RoundTripPreview_DontAskAgain", _resourceCulture)!;

    internal static string ExportCode_Toolbar_MenuItem
        => ResourceManager.GetString("ExportCode_Toolbar_MenuItem", _resourceCulture)!;

    internal static string ExportCode_Status_NothingToExport
        => ResourceManager.GetString("ExportCode_Status_NothingToExport", _resourceCulture)!;

    internal static string ExportCode_Status_UnknownLanguage
        => ResourceManager.GetString("ExportCode_Status_UnknownLanguage", _resourceCulture)!;

    internal static string ExportCode_Status_Exported
        => ResourceManager.GetString("ExportCode_Status_Exported", _resourceCulture)!;

    internal static string ExportCode_Status_Failed
        => ResourceManager.GetString("ExportCode_Status_Failed", _resourceCulture)!;

    internal static string ExportCode_FileFilter
        => ResourceManager.GetString("ExportCode_FileFilter", _resourceCulture)!;
}
