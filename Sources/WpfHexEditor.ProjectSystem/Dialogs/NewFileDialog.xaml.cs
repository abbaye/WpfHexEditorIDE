//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.ProjectSystem.Templates;

namespace WpfHexEditor.ProjectSystem.Dialogs;

/// <summary>
/// Dialog for creating a new file from a template.
/// After <see cref="System.Windows.Window.ShowDialog"/> returns <c>true</c>, read:
/// <list type="bullet">
///   <item><see cref="FileName"/> — chosen file name (no path)</item>
///   <item><see cref="FileDirectory"/> — chosen directory (empty when <see cref="SaveLater"/> is true)</item>
///   <item><see cref="FullPath"/> — combined full path (empty when <see cref="SaveLater"/> is true)</item>
///   <item><see cref="SelectedTemplate"/> — the chosen <see cref="IFileTemplate"/></item>
///   <item><see cref="TargetProject"/> — project to add to, or <c>null</c></item>
///   <item><see cref="SaveLater"/> — when true, caller should use <c>HexEditor.OpenNew()</c></item>
/// </list>
/// </summary>
public partial class NewFileDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
{
    // ── Output properties ──────────────────────────────────────────────
    public string         FileName         { get; private set; } = "";
    public string         FileDirectory    { get; private set; } = "";
    /// <summary>
    /// Non-empty only when <see cref="SaveLater"/> is <c>false</c>.
    /// </summary>
    public string         FullPath         => SaveLater ? "" : Path.Combine(FileDirectory, FileName);
    public IFileTemplate? SelectedTemplate { get; private set; }
    public IProject?      TargetProject    { get; private set; }
    /// <summary>
    /// When <c>true</c>, the host should open the document in-memory via <c>HexEditor.OpenNew()</c>;
    /// the save-file dialog will appear on the first Ctrl+S.
    /// </summary>
    public bool           SaveLater        { get; private set; }

    // ── Constructor ────────────────────────────────────────────────────
    /// <param name="defaultDirectory">Initial location shown in the Location box.</param>
    /// <param name="availableProjects">Projects to offer in the "Add to project" combo.</param>
    public NewFileDialog(
        string? defaultDirectory = null,
        IReadOnlyList<IProject>? availableProjects = null)
    {
        InitializeComponent();

        LocationBox.Text = defaultDirectory
            ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        PopulateTemplateList();
        PopulateProjectCombo(availableProjects);

        TemplateList.SelectedIndex = 0;
        NameBox.Focus();
    }

    // ── Initialisation helpers ─────────────────────────────────────────

    private void PopulateTemplateList()
    {
        foreach (var tpl in FileTemplateRegistry.Templates)
        {
            TemplateList.Items.Add(new ListBoxItem
            {
                Content = tpl.Name,
                ToolTip = tpl.Description,
                Tag     = tpl
            });
        }
    }

    private void PopulateProjectCombo(IReadOnlyList<IProject>? projects)
    {
        if (projects is null or { Count: 0 })
        {
            AddToProjectCheck.IsEnabled = false;
            return;
        }

        foreach (var p in projects)
            ProjectCombo.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p });

        ProjectCombo.SelectedIndex = 0;
    }

    // ── Event handlers ─────────────────────────────────────────────────

    private void OnTemplateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateList.SelectedItem is not ListBoxItem { Tag: IFileTemplate tpl }) return;

        // Auto-fill name only when it is empty or matches the previous default
        if (NameBox.Text.Trim().Length == 0)
        {
            var n = $"NewFile{tpl.DefaultExtension}";
            NameBox.Text = n;
            NameBox.SelectAll();
        }
        else if (IsDefaultFileName(NameBox.Text.Trim()))
        {
            // Replace extension only
            var stem = Path.GetFileNameWithoutExtension(NameBox.Text.Trim());
            NameBox.Text = stem + tpl.DefaultExtension;
        }

        Refresh();
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void OnSaveLaterChanged(object sender, RoutedEventArgs e)
    {
        var saveLater = SaveLaterCheck.IsChecked == true;
        LocationBox.IsEnabled  = !saveLater;
        BrowseButton.IsEnabled = !saveLater;
        Refresh();
    }

    private void OnAddToProjectChanged(object sender, RoutedEventArgs e)
    {
        ProjectCombo.IsEnabled = AddToProjectCheck.IsChecked == true;
        Refresh();
    }

    private void OnBrowseLocation(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title            = "Select folder for the new file",
            InitialDirectory = LocationBox.Text,
        };
        if (dlg.ShowDialog() == true)
            LocationBox.Text = dlg.FolderName;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        SaveLater        = SaveLaterCheck.IsChecked == true;
        FileName         = NameBox.Text.Trim();
        FileDirectory    = SaveLater ? "" : LocationBox.Text.Trim();
        SelectedTemplate = (TemplateList.SelectedItem as ListBoxItem)?.Tag as IFileTemplate;

        if (AddToProjectCheck.IsChecked == true
            && ProjectCombo.SelectedItem is ComboBoxItem { Tag: IProject proj })
            TargetProject = proj;

        DialogResult = true;
    }

    // ── Private helpers ────────────────────────────────────────────────

    private void Refresh()
    {
        var name     = NameBox.Text.Trim();
        var saveLater = SaveLaterCheck.IsChecked == true;
        var loc      = LocationBox.Text.Trim();

        var valid = name.Length > 0
                    && TemplateList.SelectedItem is not null
                    && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                    && (saveLater || loc.Length > 0);

        OkButton.IsEnabled = valid;
    }

    private static bool IsDefaultFileName(string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        return stem.StartsWith("NewFile", StringComparison.OrdinalIgnoreCase)
            || stem.StartsWith("New",     StringComparison.OrdinalIgnoreCase);
    }
}
