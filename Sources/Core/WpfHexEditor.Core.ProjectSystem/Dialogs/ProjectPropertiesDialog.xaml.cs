// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Dialogs/ProjectPropertiesDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Updated: 2026-03-16
// Description:
//     VS-Like tabbed Project Properties dialog.
//     Tabs: Application | Build | Items | References
//     Read-only view for WH projects; exposes VS-specific metadata
//     (TargetFramework, AssemblyName, etc.) when available.
//
// Architecture Notes:
//     Pattern: Presenter — populates controls from IProject + optional
//     VsProject cast for extended metadata. Save commits name change
//     via ISolutionManager if the project is a WH project.
// ==========================================================

using System.IO;
using System.Windows;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.ProjectSystem.Properties;
using WpfHexEditor.Core.ProjectSystem.Services;

namespace WpfHexEditor.Core.ProjectSystem.Dialogs;

/// <summary>
/// VS-Like tabbed properties dialog for a <see cref="IProject"/>.
/// </summary>
public partial class ProjectPropertiesDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
{
    private readonly IProject _project;

    public ProjectPropertiesDialog(IProject project)
    {
        InitializeComponent();
        _project = project;
        Populate(project);
    }

    // -----------------------------------------------------------------------
    // Population
    // -----------------------------------------------------------------------

    private void Populate(IProject project)
    {
        ProjectTitleText.Text = project.Name + " — Properties";

        // -- Application tab --
        TbName.Text           = project.Name;
        FilePathText.Text     = project.ProjectFilePath;
        FilePathTooltip.Text  = project.ProjectFilePath;
        DirectoryText.Text    = Path.GetDirectoryName(project.ProjectFilePath) ?? "";
        ProjectTypeText.Text  = project.ProjectType ?? "WpfHexEditor Project";
        ItemCountText.Text    = $"{project.Items.Count} item(s)";

        // Build tab defaults.
        BuildConfigText.Text   = "Debug";
        BuildPlatformText.Text = "Any CPU";
        OutputPathText.Text    = @"bin\Debug\net8.0-windows\";
        OptimiseText.Text      = "No";

        // Items tab.
        ItemsListView.ItemsSource = project.Items;

        // Extended metadata — available only for VS projects loaded via VsSolutionLoader.
        // Using reflection-free interface check via namespace convention.
        PopulateVsMetadata(project);
    }

    private void PopulateVsMetadata(IProject project)
    {
        // Try to read VS-specific properties via well-known property names
        // using dynamic dispatch so we don't need a direct project reference to
        // WpfHexEditor.Plugins.SolutionLoader.VS (which would create a circular dep).
        var type = project.GetType();

        string Get(string name)
        {
            try { return type.GetProperty(name)?.GetValue(project) as string ?? ""; }
            catch { return ""; }
        }

        IEnumerable<string> GetList(string name)
        {
            try
            {
                return type.GetProperty(name)?.GetValue(project) as IEnumerable<string>
                       ?? [];
            }
            catch { return []; }
        }

        var targetFx = Get("TargetFramework");
        if (!string.IsNullOrEmpty(targetFx))
        {
            TargetFrameworkText.Text = targetFx;
            AssemblyNameText.Text    = Get("AssemblyName") is { Length: > 0 } a ? a : project.Name;
            OutputTypeText.Text      = Get("OutputType") is { Length: > 0 } o ? o : "Library";

            var refs = GetList("ProjectReferences")
                           .Select(r => new ReferenceEntry(Path.GetFileNameWithoutExtension(r), "Project"))
                           .Concat(GetList("PackageReferences").Select(p => new ReferenceEntry(p, "NuGet")))
                           .ToList();
            ReferencesListView.ItemsSource = refs;
        }
        else
        {
            TargetFrameworkText.Text = "net8.0-windows";
            AssemblyNameText.Text    = project.Name;
            OutputTypeText.Text      = "Library";
            ReferencesTab.Visibility = Visibility.Collapsed;
        }
    }

    // -----------------------------------------------------------------------
    // Save
    // -----------------------------------------------------------------------

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var newName = TbName.Text.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            MessageBox.Show(ProjectSystemResources.Validation_ProjectNameEmpty, ProjectSystemResources.Dialog_ValidationTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.Equals(newName, _project.Name, StringComparison.Ordinal))
        {
            try
            {
                await SolutionManager.Instance.RenameProjectAsync(_project, newName);
                ProjectTitleText.Text = newName + " — Properties";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ProjectSystemResources.Error_RenameProjectFailed}\n{ex.Message}", ProjectSystemResources.Dialog_ErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        DialogResult = true;
    }

    // -----------------------------------------------------------------------
    // Nested helpers
    // -----------------------------------------------------------------------

    /// <summary>Flat DTO for the References ListView.</summary>
    private sealed record ReferenceEntry(string Name, string RefType);
}
