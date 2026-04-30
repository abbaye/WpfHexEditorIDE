// ==========================================================
// Project: WpfHexEditor.Core.ProjectSystem
// File: Properties/ProjectSystemResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for ProjectSystem.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Core.ProjectSystem.Properties;

internal static class ProjectSystemResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Core.ProjectSystem.Properties.ProjectSystemResources",
                typeof(ProjectSystemResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    internal static string Validation_ProjectNameEmpty
        => ResourceManager.GetString("Validation_ProjectNameEmpty", _resourceCulture)!;

    internal static string Dialog_ValidationTitle
        => ResourceManager.GetString("Dialog_ValidationTitle", _resourceCulture)!;

    internal static string Error_RenameProjectFailed
        => ResourceManager.GetString("Error_RenameProjectFailed", _resourceCulture)!;

    internal static string Dialog_ErrorTitle
        => ResourceManager.GetString("Dialog_ErrorTitle", _resourceCulture)!;

    internal static string Error_CannotCreateOutputFolder
        => ResourceManager.GetString("Error_CannotCreateOutputFolder", _resourceCulture)!;

    internal static string Dialog_ConvertTblTitle
        => ResourceManager.GetString("Dialog_ConvertTblTitle", _resourceCulture)!;

    internal static string Error_OutputPathIdenticalToSource
        => ResourceManager.GetString("Error_OutputPathIdenticalToSource", _resourceCulture)!;

    internal static string Dialog_RemoveReferenceTitle
        => ResourceManager.GetString("Dialog_RemoveReferenceTitle", _resourceCulture)!;

    internal static string Message_NoUnusedReferences
        => ResourceManager.GetString("Message_NoUnusedReferences", _resourceCulture)!;

    internal static string Dialog_RemoveUnusedReferencesTitle
        => ResourceManager.GetString("Dialog_RemoveUnusedReferencesTitle", _resourceCulture)!;

    internal static string Confirm_RemoveUnusedReferences
        => ResourceManager.GetString("Confirm_RemoveUnusedReferences", _resourceCulture)!;

    internal static string Dialog_SelectFilesToAdd
        => ResourceManager.GetString("Dialog_SelectFilesToAdd", _resourceCulture)!;

    internal static string Filter_AllFiles
        => ResourceManager.GetString("Filter_AllFiles", _resourceCulture)!;

    internal static string Dialog_SelectProjectFolder
        => ResourceManager.GetString("Dialog_SelectProjectFolder", _resourceCulture)!;

    internal static string Dialog_SelectFileFolder
        => ResourceManager.GetString("Dialog_SelectFileFolder", _resourceCulture)!;

    internal static string Dialog_SelectSolutionFolder
        => ResourceManager.GetString("Dialog_SelectSolutionFolder", _resourceCulture)!;

    internal static string Dialog_SelectConvertOutputFolder
        => ResourceManager.GetString("Dialog_SelectConvertOutputFolder", _resourceCulture)!;

    internal static string Dialog_AddProjectReference
        => ResourceManager.GetString("Dialog_AddProjectReference", _resourceCulture)!;

    internal static string Filter_ProjectFiles
        => ResourceManager.GetString("Filter_ProjectFiles", _resourceCulture)!;

    internal static string Dialog_AddAssemblyReference
        => ResourceManager.GetString("Dialog_AddAssemblyReference", _resourceCulture)!;

    internal static string Filter_AssemblyFiles
        => ResourceManager.GetString("Filter_AssemblyFiles", _resourceCulture)!;

    internal static string Dialog_SelectOutputDirectory
        => ResourceManager.GetString("Dialog_SelectOutputDirectory", _resourceCulture)!;

    internal static string Dialog_SelectIcon
        => ResourceManager.GetString("Dialog_SelectIcon", _resourceCulture)!;

    internal static string Filter_IconFiles
        => ResourceManager.GetString("Filter_IconFiles", _resourceCulture)!;

    internal static string Dialog_SelectProjectToReference
        => ResourceManager.GetString("Dialog_SelectProjectToReference", _resourceCulture)!;

    internal static string Filter_CsharpProjects
        => ResourceManager.GetString("Filter_CsharpProjects", _resourceCulture)!;
}
