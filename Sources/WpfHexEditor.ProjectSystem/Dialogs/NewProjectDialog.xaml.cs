//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.ProjectSystem.Templates;

namespace WpfHexEditor.ProjectSystem.Dialogs;

/// <summary>
/// Dialog for creating a new project (.whproj) inside the active solution.
/// Displays a VS-style template picker (category filter + template list).
/// After <see cref="Window.ShowDialog"/> returns <see langword="true"/>, read
/// <see cref="ProjectName"/>, <see cref="ProjectDirectory"/> and <see cref="SelectedTemplate"/>.
/// </summary>
public partial class NewProjectDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
{
    // ── Outputs ──────────────────────────────────────────────────────────

    /// <summary>
    /// Name entered by the user for the new project.
    /// </summary>
    public string ProjectName      { get; private set; } = "";

    /// <summary>
    /// Parent directory chosen by the user (project folder = <c>ProjectDirectory/ProjectName</c>).
    /// </summary>
    public string ProjectDirectory { get; private set; } = "";

    /// <summary>
    /// Template selected by the user, or <see langword="null"/> if none was chosen.
    /// </summary>
    public IProjectTemplate? SelectedTemplate { get; private set; }

    // ── Categories ───────────────────────────────────────────────────────

    private static readonly string[] Categories =
        ["All", "General", "Analysis", "ReverseEngineering", "Development", "RomHacking"];

    // ── Constructor ──────────────────────────────────────────────────────

    /// <param name="suggestedDirectory">Pre-filled location (e.g. the solution directory).</param>
    public NewProjectDialog(string? suggestedDirectory = null)
    {
        InitializeComponent();

        LocationBox.Text = string.IsNullOrEmpty(suggestedDirectory)
                           ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                           : suggestedDirectory;

        // Populate category list
        foreach (var cat in Categories)
            CategoryList.Items.Add(cat);

        CategoryList.SelectedIndex = 0;  // "All"
        NameBox.Focus();
    }

    // ── Event handlers ───────────────────────────────────────────────────

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
        => RefreshTemplateList();

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
        => RefreshTemplateList();

    private void RefreshTemplateList()
    {
        var cat    = CategoryList.SelectedItem as string;
        var search = SearchBox?.Text?.Trim() ?? "";

        var templates = (cat is null || cat == "All")
            ? ProjectTemplateRegistry.Templates
            : ProjectTemplateRegistry.GetByCategory(cat);

        if (search.Length > 0)
            templates = templates
                .Where(t => t.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                         || t.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        TemplateList.Items.Clear();
        foreach (var t in templates)
            TemplateList.Items.Add(t);

        if (TemplateList.Items.Count > 0)
            TemplateList.SelectedIndex = 0;
        else
        {
            SelectedTemplate   = null;
            DescriptionText.Text = "";
        }
    }

    private void OnTemplateSelected(object sender, SelectionChangedEventArgs e)
    {
        SelectedTemplate     = TemplateList.SelectedItem as IProjectTemplate;
        DescriptionText.Text = SelectedTemplate?.Description ?? "";
        Refresh();
    }

    private void OnNameOrLocationChanged(object sender, TextChangedEventArgs e)
        => Refresh();

    private void Refresh()
    {
        var name  = NameBox.Text.Trim();
        var loc   = LocationBox.Text.Trim();
        var valid = name.Length > 0 && loc.Length > 0
                    && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        OkButton.IsEnabled = valid;
        PreviewText.Text   = valid
            ? Path.Combine(loc, name, name + ".whproj")
            : string.Empty;
    }

    private void OnBrowseLocation(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title            = "Select parent folder for the new project",
            InitialDirectory = LocationBox.Text,
        };
        if (dlg.ShowDialog() == true)
            LocationBox.Text = dlg.FolderName;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        ProjectName      = NameBox.Text.Trim();
        ProjectDirectory = LocationBox.Text.Trim();
        DialogResult     = true;
    }
}
