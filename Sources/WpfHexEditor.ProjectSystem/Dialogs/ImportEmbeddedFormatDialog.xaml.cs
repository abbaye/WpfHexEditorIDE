//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
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
public partial class ImportEmbeddedFormatDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
{
    // ── Output properties ──────────────────────────────────────────────────
    public IReadOnlyList<EmbeddedFormatEntry> SelectedEntries { get; private set; } = [];
    /// <summary>
    /// Id of the virtual folder to place the items in, or <c>null</c> for the project root.
    /// </summary>
    public string? TargetFolderId { get; private set; }

    // ── Private state ──────────────────────────────────────────────────────
    private readonly IEmbeddedFormatCatalog _catalog;
    private IReadOnlyList<EmbeddedFormatEntry> _allEntries = [];
    private List<FormatRow> _filteredRows = [];

    /// <summary>Lazy full-JSON cache keyed by ResourceKey. Populated on first search.</summary>
    private readonly Dictionary<string, string> _jsonCache = [];

    private static readonly StringComparison OIC = StringComparison.OrdinalIgnoreCase;

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
        var selectedCat = (CategoryCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        var searchText  = SearchBox.Text?.Trim() ?? "";

        _filteredRows = _allEntries
            .Where(e =>
                (selectedCat is null || string.Equals(e.Category, selectedCat, OIC)) &&
                (searchText.Length == 0 ||
                 e.Name.Contains(searchText, OIC) ||
                 e.Description.Contains(searchText, OIC) ||
                 GetJsonText(e).Contains(searchText, OIC)))
            .Select(e => new FormatRow(e))
            .ToList();

        var view = new ListCollectionView(_filteredRows);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FormatRow.Category)));
        FormatList.ItemsSource = view;

        Refresh();
    }

    /// <summary>Returns full JSON text for the entry, loading lazily and caching.</summary>
    private string GetJsonText(EmbeddedFormatEntry entry)
    {
        if (!_jsonCache.TryGetValue(entry.ResourceKey, out var json))
            _jsonCache[entry.ResourceKey] = json = _catalog.GetJson(entry.ResourceKey);
        return json;
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnFilterChanged(object sender, EventArgs e)
        => ApplyFilter();

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var lastRow = FormatList.SelectedItems.Cast<FormatRow>().LastOrDefault();
        if (lastRow is not null)
        {
            // Header
            DescNameText.Text     = lastRow.Name;
            DescCategoryText.Text = lastRow.Category;
            DescText.Text         = lastRow.Entry.Description;

            // Version / Author
            DescVersionText.Text      = lastRow.Entry.Version;
            DescVersionRow.Visibility = string.IsNullOrEmpty(lastRow.Entry.Version)
                                        ? Visibility.Collapsed : Visibility.Visible;
            DescAuthorText.Text       = lastRow.Entry.Author;
            DescAuthorRow.Visibility  = string.IsNullOrEmpty(lastRow.Entry.Author)
                                        ? Visibility.Collapsed : Visibility.Visible;

            // Extension badges
            DescExtWrap.Children.Clear();
            foreach (var ext in lastRow.Entry.Extensions)
                DescExtWrap.Children.Add(MakeExtBadge(ext));

            // Quality bar
            DescQualityBar.Value  = lastRow.QualityScore;
            DescQualityText.Text  = $"{lastRow.QualityScore}%";
        }
        else
        {
            DescNameText.Text = DescCategoryText.Text = DescText.Text = "";
            DescVersionRow.Visibility = DescAuthorRow.Visibility = Visibility.Collapsed;
            DescVersionText.Text = DescAuthorText.Text = "";
            DescExtWrap.Children.Clear();
            DescQualityBar.Value = 0;
            DescQualityText.Text = "";
        }

        Refresh();
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        SelectedEntries = FormatList.SelectedItems
            .Cast<FormatRow>()
            .Select(r => r.Entry)
            .ToList();
        TargetFolderId = (FolderCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        DialogResult   = true;
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private void Refresh()
    {
        var count = FormatList.SelectedItems.Count;
        SelectionCountText.Text = count == 0
            ? "No format selected"
            : $"{count} format{(count == 1 ? "" : "s")} selected";
        ImportButton.IsEnabled = count > 0;
    }

    private static Border MakeExtBadge(string ext)
    {
        return new Border
        {
            Margin          = new Thickness(0, 0, 4, 4),
            Padding         = new Thickness(5, 2, 5, 2),
            CornerRadius    = new CornerRadius(3),
            Background      = (Brush)Application.Current.TryFindResource("ERR_FilterActiveBrush")
                              ?? new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
            Child           = new TextBlock
            {
                Text       = ext,
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize   = 11
            }
        };
    }

    // ── View-model row ─────────────────────────────────────────────────────

    private sealed class FormatRow(EmbeddedFormatEntry entry)
    {
        public EmbeddedFormatEntry Entry            => entry;
        public string              Name             => entry.Name;
        public string              Category         => entry.Category;
        public string              ExtensionsDisplay => string.Join(", ", entry.Extensions);
        public string              QualityDisplay   => $"{entry.QualityScore}%";
        public int                 QualityScore     => entry.QualityScore;
    }
}
