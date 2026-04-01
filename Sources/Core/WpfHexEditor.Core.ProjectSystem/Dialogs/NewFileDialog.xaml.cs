//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.ProjectSystem.Templates;

namespace WpfHexEditor.Core.ProjectSystem.Dialogs;

/// <summary>
/// VS-style New File dialog: left category sidebar, center template grid/list with
/// search + sort + view-toggle toolbar, right description panel, bottom name/location bar.
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
    // ── Output properties ───────────────────────────────────────────────────

    public string         FileName         { get; private set; } = "";
    public string         FileDirectory    { get; private set; } = "";
    /// <summary>Non-empty only when <see cref="SaveLater"/> is <c>false</c>.</summary>
    public string         FullPath         => SaveLater ? "" : Path.Combine(FileDirectory, FileName);
    public IFileTemplate? SelectedTemplate { get; private set; }
    public IProject?      TargetProject    { get; private set; }
    /// <summary>
    /// Id of the virtual folder in <see cref="TargetProject"/> to place the item in.
    /// <c>null</c> means the project root (or not launched from Solution Explorer).
    /// </summary>
    public string?        TargetFolderId   { get; private set; }
    /// <summary>
    /// When <c>true</c> the host opens the document in-memory via HexEditor.OpenNew();
    /// the save-file dialog appears on the first Ctrl+S.
    /// </summary>
    public bool           SaveLater        { get; private set; }

    // ── Internal state ──────────────────────────────────────────────────────

    private readonly List<IFileTemplate> _allTemplates;
    private string   _selectedCategory = "All";
    private string   _searchText       = "";
    private string   _sortBy           = "Name";
    private bool     _viewModeGrid     = true;
    private bool     _suppressSync;
    private IProject? _preSelectedProject;

    // ── Constructor ─────────────────────────────────────────────────────────

    /// <param name="defaultDirectory">Initial location shown in the Location box.</param>
    /// <param name="availableProjects">Projects to offer in the "Add to project" combo.</param>
    /// <param name="preSelectedProject">
    /// When set (launched from Solution Explorer), the dialog pre-checks "Add to project",
    /// pre-selects this project, and shows the folder picker.
    /// </param>
    /// <param name="preSelectedFolder">
    /// Virtual folder id to pre-select in the folder combo.
    /// Ignored when <paramref name="preSelectedProject"/> is <c>null</c>.
    /// </param>
    public NewFileDialog(
        string?                  defaultDirectory   = null,
        IReadOnlyList<IProject>? availableProjects  = null,
        IProject?                preSelectedProject = null,
        string?                  preSelectedFolder  = null)
    {
        InitializeComponent();

        LocationBox.Text = defaultDirectory
            ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        _preSelectedProject = preSelectedProject;
        _allTemplates = FileTemplateRegistry.Templates.ToList();
        PopulateCategorySidebar();
        PopulateProjectCombo(availableProjects, preSelectedProject);

        if (preSelectedProject is not null)
        {
            AddToProjectCheck.IsChecked = true;
            ProjectCombo.IsEnabled      = true;
            PopulateFolderCombo(preSelectedProject, preSelectedFolder);
            FolderLabel.Visibility      = Visibility.Visible;
            FolderCombo.Visibility      = Visibility.Visible;
        }

        CategoryList.SelectedIndex = 0;   // "All"

        // Default to list view — more readable for large template sets.
        _viewModeGrid               = false;
        ViewListBtn.IsChecked       = true;
        TemplateGridView.Visibility = Visibility.Collapsed;
        TemplateListView.Visibility = Visibility.Visible;

        ApplyFilter();
        NameBox.Focus();
    }

    // ── Initialisation helpers ──────────────────────────────────────────────

    private void PopulateCategorySidebar()
    {
        // Add plain strings — WPF creates ListBoxItem containers and applies ItemContainerStyle.
        // Adding ListBoxItem directly would bypass ItemContainerStyle (IsItemItsOwnContainer).
        CategoryList.Items.Add("All");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cat in _allTemplates.SelectMany(t => t.Categories))
            if (seen.Add(cat))
                CategoryList.Items.Add(cat);
    }

    private void PopulateProjectCombo(IReadOnlyList<IProject>? projects, IProject? preSelected)
    {
        if (projects is null or { Count: 0 })
        {
            AddToProjectCheck.IsEnabled = false;
            return;
        }
        int preSelectedIndex = 0;
        for (int i = 0; i < projects.Count; i++)
        {
            var p = projects[i];
            ProjectCombo.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p });
            if (preSelected is not null && p.Id == preSelected.Id)
                preSelectedIndex = i;
        }
        ProjectCombo.SelectedIndex = preSelectedIndex;
    }

    private void PopulateFolderCombo(IProject project, string? preSelectedFolderId)
    {
        FolderCombo.Items.Clear();
        FolderCombo.Items.Add(new ComboBoxItem { Content = "(project root)", Tag = (string?)null });

        foreach (var folder in project.RootFolders)
            AddFolderItem(folder, indent: 0, preSelectedFolderId);

        FolderCombo.SelectedIndex = 0;
    }

    private void AddFolderItem(IVirtualFolder folder, int indent, string? preSelectedId)
    {
        var item = new ComboBoxItem
        {
            Content = new string(' ', indent * 2) + folder.Name,
            Tag     = folder.Id,
        };
        FolderCombo.Items.Add(item);
        if (folder.Id == preSelectedId)
            FolderCombo.SelectedItem = item;

        foreach (var child in folder.Children)
            AddFolderItem(child, indent + 1, preSelectedId);
    }

    // ── Filter / sort / view ────────────────────────────────────────────────

    private void ApplyFilter()
    {
        if (_allTemplates is null) return;   // called by InitializeComponent before ctor assigns the list

        var filtered = _allTemplates.AsEnumerable();

        if (_selectedCategory != "All")
            filtered = filtered.Where(t => t.Categories.Contains(_selectedCategory));

        if (_searchText.Length > 0)
            filtered = filtered.Where(t =>
                t.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        filtered = _sortBy switch
        {
            "Category"  => filtered.OrderBy(t => t.Category).ThenBy(t => t.Name),
            "Extension" => filtered.OrderBy(t => t.DefaultExtension),
            _           => filtered.OrderBy(t => t.Name),
        };

        var list = filtered.ToList();
        TemplateGridView.ItemsSource = list;
        TemplateListView.ItemsSource = list;

        var active = _viewModeGrid ? (ListView)TemplateGridView : TemplateListView;
        if (list.Count > 0 && active.SelectedIndex < 0)
            active.SelectedIndex = 0;
    }

    // ── Event handlers — toolbar ────────────────────────────────────────────

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryList.SelectedItem is not string cat) return;
        _selectedCategory = cat;
        ApplyFilter();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        ApplyFilter();
    }

    private void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo.SelectedItem is ComboBoxItem { Content: string s })
            _sortBy = s;
        ApplyFilter();
    }

    private void OnViewGrid(object sender, RoutedEventArgs e)
    {
        if (ViewListBtn is null) return;   // fired by InitializeComponent before sibling is ready
        _viewModeGrid            = true;
        ViewListBtn.IsChecked    = false;
        TemplateGridView.Visibility = Visibility.Visible;
        TemplateListView.Visibility = Visibility.Collapsed;
        SyncSelection(TemplateListView, TemplateGridView);
    }

    private void OnViewList(object sender, RoutedEventArgs e)
    {
        if (ViewGridBtn is null) return;   // symmetric guard
        _viewModeGrid            = false;
        ViewGridBtn.IsChecked    = false;
        TemplateGridView.Visibility = Visibility.Collapsed;
        TemplateListView.Visibility = Visibility.Visible;
        SyncSelection(TemplateGridView, TemplateListView);
    }

    private void SyncSelection(ListView from, ListView to)
    {
        if (_suppressSync) return;
        _suppressSync = true;
        try
        {
            to.SelectedItem  = from.SelectedItem;
            to.ScrollIntoView(to.SelectedItem);
        }
        finally { _suppressSync = false; }
    }

    // ── Event handlers — template list ─────────────────────────────────────

    private void OnTemplateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSync) return;
        if (sender is not ListView lv) return;
        if (lv.SelectedItem is not IFileTemplate tpl) return;

        UpdateDescriptionPanel(tpl);

        // Sync the other list view silently
        var other = lv == TemplateGridView ? TemplateListView : TemplateGridView;
        _suppressSync = true;
        try   { other.SelectedItem = tpl; }
        finally { _suppressSync = false; }

        // Auto-fill name
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

    private void UpdateDescriptionPanel(IFileTemplate tpl)
    {
        DescIconText.Text     = tpl.IconGlyph;
        DescNameText.Text     = tpl.Name;
        DescExtText.Text      = tpl.DefaultExtension;
        DescText.Text         = tpl.Description;
        DescCategoryText.Text = $"Category: {tpl.Category}";
    }

    // ── Event handlers — bottom bar ─────────────────────────────────────────

    private void OnInputChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void OnSaveLaterChanged(object sender, RoutedEventArgs e)
    {
        var saveLater          = SaveLaterCheck.IsChecked == true;
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
        SaveLater     = SaveLaterCheck.IsChecked == true;
        FileName      = NameBox.Text.Trim();
        FileDirectory = SaveLater ? "" : LocationBox.Text.Trim();

        var activeList = _viewModeGrid ? TemplateGridView : TemplateListView;
        SelectedTemplate = activeList.SelectedItem as IFileTemplate;

        if (AddToProjectCheck.IsChecked == true
            && ProjectCombo.SelectedItem is ComboBoxItem { Tag: IProject proj })
            TargetProject = proj;

        if (FolderCombo.Visibility == Visibility.Visible
            && FolderCombo.SelectedItem is ComboBoxItem { Tag: string fid })
            TargetFolderId = fid;

        DialogResult = true;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void Refresh()
    {
        var name      = NameBox.Text.Trim();
        var saveLater = SaveLaterCheck.IsChecked == true;
        var loc       = LocationBox.Text.Trim();

        var activeList = _viewModeGrid ? TemplateGridView : TemplateListView;
        var valid      = name.Length > 0
                         && activeList.SelectedItem is not null
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
