//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Dialogs;

/// <summary>
/// Dialog that lets the user pick one or more embedded format definitions
/// to import (copy on disk) into a project.
/// <para>
/// After <see cref="Window.ShowDialog"/> returns <c>true</c>, read:
/// <list type="bullet">
///   <item><see cref="SelectedEntries"/> — the entries to import</item>
///   <item><see cref="TargetFolderId"/> — virtual folder id, or <c>null</c> for project root</item>
/// </list>
/// </para>
/// </summary>
public partial class ImportEmbeddedFormatDialog : Window
{
    // ── Output properties ──────────────────────────────────────────────────
    public IReadOnlyList<EmbeddedFormatEntry> SelectedEntries { get; private set; } = [];
    /// <summary>Id of the virtual folder to place the items in, or <c>null</c> for the project root.</summary>
    public string? TargetFolderId { get; private set; }

    // ── Private state ──────────────────────────────────────────────────────
    private readonly IEmbeddedFormatCatalog _catalog;
    private IReadOnlyList<EmbeddedFormatEntry> _allEntries = [];
    private List<FormatRow> _filteredRows = [];

    // ── Constructor ────────────────────────────────────────────────────────
    /// <param name="catalog">Catalog of embedded format definitions.</param>
    /// <param name="project">Project that will receive the imported items (used for folder picker).</param>
    public ImportEmbeddedFormatDialog(IEmbeddedFormatCatalog catalog, IProject project)
    {
        _catalog = catalog;
        InitializeComponent();

        LoadEntries();
        PopulateCategoryCombo();
        PopulateFolderCombo(project);
        ApplyFilter();
    }

    // ── Initialisation ─────────────────────────────────────────────────────

    private void LoadEntries()
        => _allEntries = _catalog.GetAll();

    private void PopulateCategoryCombo()
    {
        CategoryCombo.Items.Add(new ComboBoxItem { Content = "(All)", Tag = (string?)null });
        foreach (var cat in _catalog.GetCategories())
            CategoryCombo.Items.Add(new ComboBoxItem { Content = cat, Tag = cat });
        CategoryCombo.SelectedIndex = 0;
    }

    private void PopulateFolderCombo(IProject project)
    {
        FolderCombo.Items.Add(new ComboBoxItem { Content = "(project root)", Tag = (string?)null });
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

    // ── Filter ─────────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        var selectedCat  = (CategoryCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        var searchText   = SearchBox.Text?.Trim() ?? "";

        _filteredRows = _allEntries
            .Where(e =>
                (selectedCat is null || string.Equals(e.Category, selectedCat, StringComparison.OrdinalIgnoreCase)) &&
                (searchText.Length == 0 ||
                 e.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                 e.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
            .Select(e => new FormatRow(e))
            .ToList();

        FormatList.ItemsSource = _filteredRows;
        Refresh();
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnFilterChanged(object sender, EventArgs e)
        => ApplyFilter();

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Update description panel from last selected item
        var lastRow = FormatList.SelectedItems.Cast<FormatRow>().LastOrDefault();
        if (lastRow is not null)
        {
            DescNameText.Text     = lastRow.Name;
            DescCategoryText.Text = lastRow.Category;
            DescText.Text         = lastRow.Entry.Description;
        }
        else
        {
            DescNameText.Text = DescCategoryText.Text = DescText.Text = "";
        }
        Refresh();
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        SelectedEntries = FormatList.SelectedItems
            .Cast<FormatRow>()
            .Select(r => r.Entry)
            .ToList();
        TargetFolderId  = (FolderCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        DialogResult    = true;
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private void Refresh()
    {
        var count = FormatList.SelectedItems.Count;
        SelectionCountText.Text = count == 0
            ? "No format selected"
            : $"{count} format{(count == 1 ? "" : "s")} selected";
        ImportButton.IsEnabled  = count > 0;
    }

    // ── View-model row ─────────────────────────────────────────────────────

    private sealed class FormatRow(EmbeddedFormatEntry entry)
    {
        public EmbeddedFormatEntry Entry            => entry;
        public string              Name             => entry.Name;
        public string              Category         => entry.Category;
        public string              ExtensionsDisplay => string.Join(", ", entry.Extensions);
        public int                 QualityScore     => entry.QualityScore;
    }
}
