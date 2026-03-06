//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.ProjectSystem.Templates;

namespace WpfHexEditor.ProjectSystem.Dialogs;

/// <summary>
/// Dialog for adding a new item directly to an existing project.
/// Unlike <see cref="NewFileDialog"/>, there is no "Save Later" option —
/// the file is always created on disk inside the project directory.
/// <para>
/// After <see cref="Window.ShowDialog"/> returns <c>true</c>, read:
/// <list type="bullet">
///   <item><see cref="FileName"/> — chosen file name (no path)</item>
///   <item><see cref="SelectedTemplate"/> — the chosen <see cref="IFileTemplate"/></item>
///   <item><see cref="TargetFolderId"/> — virtual folder id, or <c>null</c> for project root</item>
/// </list>
/// </para>
/// </summary>
public partial class AddNewItemDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
{
    // ── Output properties ──────────────────────────────────────────────────
    public string         FileName         { get; private set; } = "";
    public IFileTemplate? SelectedTemplate { get; private set; }
    /// <summary>
    /// Id of the virtual folder to place the item in, or <c>null</c> for the project root.
    /// </summary>
    public string?        TargetFolderId   { get; private set; }

    // ── Constructor ────────────────────────────────────────────────────────
    /// <param name="project">Project that will receive the new item.</param>
    public AddNewItemDialog(IProject project)
    {
        InitializeComponent();

        PopulateTemplateList();
        PopulateFolderCombo(project);

        TemplateList.SelectedIndex = 0;
        NameBox.Focus();
    }

    // ── Initialisation helpers ─────────────────────────────────────────────

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

    private void PopulateFolderCombo(IProject project)
    {
        // Project root (always first)
        FolderCombo.Items.Add(new ComboBoxItem { Content = "(project root)", Tag = (string?)null });

        // Recursive enumeration of virtual folders
        foreach (var folder in project.RootFolders)
            AddFolderItem(folder, indent: 0);

        FolderCombo.SelectedIndex = 0;
    }

    private void AddFolderItem(IVirtualFolder folder, int indent)
    {
        FolderCombo.Items.Add(new ComboBoxItem
        {
            Content = new string(' ', indent * 2) + folder.Name,
            Tag     = folder.Id
        });

        foreach (var child in folder.Children)
            AddFolderItem(child, indent + 1);
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnTemplateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateList.SelectedItem is not ListBoxItem { Tag: IFileTemplate tpl }) return;

        if (NameBox.Text.Trim().Length == 0)
        {
            NameBox.Text = $"NewFile{tpl.DefaultExtension}";
            NameBox.SelectAll();
        }
        else if (IsDefaultFileName(NameBox.Text.Trim()))
        {
            var stem = Path.GetFileNameWithoutExtension(NameBox.Text.Trim());
            NameBox.Text = stem + tpl.DefaultExtension;
        }

        Refresh();
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        FileName         = NameBox.Text.Trim();
        SelectedTemplate = (TemplateList.SelectedItem as ListBoxItem)?.Tag as IFileTemplate;
        TargetFolderId   = (FolderCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        DialogResult     = true;
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private void Refresh()
    {
        var name  = NameBox.Text.Trim();
        var valid = name.Length > 0
                    && TemplateList.SelectedItem is not null
                    && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        OkButton.IsEnabled = valid;
    }

    private static bool IsDefaultFileName(string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        return stem.StartsWith("NewFile", StringComparison.OrdinalIgnoreCase)
            || stem.StartsWith("New",     StringComparison.OrdinalIgnoreCase);
    }
}
