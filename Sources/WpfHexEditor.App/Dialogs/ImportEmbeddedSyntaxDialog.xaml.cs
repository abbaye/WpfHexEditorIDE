//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Views;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// Dialog that lets the user pick one or more embedded syntax definitions
/// (from <c>.whfmt</c> <c>syntaxDefinition</c> blocks) to export/import into a project.
/// Preview uses <c>CodeEditor</c> in read-only mode with built-in JSON syntax highlighting.
/// <para>
/// After <see cref="Window.ShowDialog"/> returns <c>true</c>, read:
/// <list type="bullet">
///   <item><see cref="SelectedEntries"/> — the format entries to import</item>
///   <item><see cref="TargetFolderId"/> — virtual folder id, or <c>null</c> for project root</item>
/// </list>
/// </para>
/// </summary>
public partial class ImportEmbeddedSyntaxDialog : ThemedDialog
{
    // -- Output properties --------------------------------------------------
    public IReadOnlyList<EmbeddedFormatEntry> SelectedEntries { get; private set; } = [];
    /// <summary>Id of the virtual folder, or <c>null</c> for the project root.</summary>
    public string? TargetFolderId { get; private set; }

    // -- Private state ------------------------------------------------------
    private readonly EmbeddedFormatCatalog _catalog;
    private          List<CategoryNode>    _categories   = [];
    private readonly HashSet<SyntaxRow>    _checked      = [];
    private          SyntaxRow?            _lastSelected;

    private static readonly StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    // -- Constructor --------------------------------------------------------
    /// <param name="catalog">Catalog of embedded format definitions (source of syntaxDefinition blocks).</param>
    /// <param name="project">Project that will receive the imported items.</param>
    public ImportEmbeddedSyntaxDialog(EmbeddedFormatCatalog catalog, IProject project, string? initialFolderId = null)
    {
        _catalog = catalog;
        InitializeComponent();

        BuildCategories();
        BuildTree(filter: "");
        PopulateFolderCombo(project);
        PreSelectFolder(initialFolderId);
        RefreshStatus();
    }

    private void PreSelectFolder(string? folderId)
    {
        if (folderId is null) return;
        foreach (ComboBoxItem item in FolderCombo.Items)
        {
            if (item.Tag as string == folderId)
            {
                FolderCombo.SelectedItem = item;
                return;
            }
        }
    }

    // -- Initialisation -----------------------------------------------------

    private void BuildCategories()
    {
        _categories = _catalog.Query()
            .HasSyntaxDefinition()
            .OrderByName()
            .GroupByCategory()
            .Select(kvp =>
            {
                var node = new CategoryNode(kvp.Key);
                foreach (var entry in kvp.Value)
                    node.Items.Add(new SyntaxRow(entry));
                return node;
            })
            .ToList();
    }

    private void BuildTree(string filter)
    {
        SyntaxTree.Items.Clear();

        var hasFilter = !string.IsNullOrWhiteSpace(filter);

        foreach (var category in _categories)
        {
            var matchingItems = hasFilter
                ? category.Items
                    .Where(r => r.Name.Contains(filter, OIC) || r.ExtDisplay.Contains(filter, OIC))
                    .ToList()
                : category.Items;

            if (matchingItems.Count == 0)
                continue;

            var catItem = new TreeViewItem
            {
                Header     = BuildCategoryHeader(category.Name, matchingItems.Count),
                IsExpanded = hasFilter,
                Tag        = category,
            };

            foreach (var row in matchingItems)
            {
                var langItem = new TreeViewItem
                {
                    Header  = BuildLanguageHeader(row),
                    Tag     = row,
                    Padding = new Thickness(2, 1, 2, 1),
                };
                langItem.Selected += OnTreeItemSelected;
                catItem.Items.Add(langItem);
            }

            SyntaxTree.Items.Add(catItem);
        }
    }

    private StackPanel BuildCategoryHeader(string categoryName, int count)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text              = categoryName,
            FontWeight        = FontWeights.SemiBold,
            FontSize          = 12,
            Foreground        = (Brush)(TryFindResource("DockMenuForegroundBrush") ?? Brushes.WhiteSmoke),
            VerticalAlignment = VerticalAlignment.Center,
        });
        panel.Children.Add(new Border
        {
            Margin            = new Thickness(6, 0, 0, 0),
            Padding           = new Thickness(5, 0, 5, 0),
            CornerRadius      = new CornerRadius(8),
            Background        = (Brush)(TryFindResource("ERR_FilterActiveBrush")
                                ?? new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42))),
            VerticalAlignment = VerticalAlignment.Center,
            Child             = new TextBlock { Text = count.ToString(), FontSize = 10 },
        });
        return panel;
    }

    private DockPanel BuildLanguageHeader(SyntaxRow row)
    {
        var panel = new DockPanel { LastChildFill = true };

        var checkBox = new CheckBox
        {
            IsChecked         = _checked.Contains(row),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 6, 0),
            Tag               = row,
        };
        checkBox.Checked   += OnCheckBoxChecked;
        checkBox.Unchecked += OnCheckBoxUnchecked;
        DockPanel.SetDock(checkBox, Dock.Left);
        panel.Children.Add(checkBox);

        if (row.ExtDisplay.Length > 0)
        {
            var extText = new TextBlock
            {
                Text              = row.ExtDisplay,
                FontSize          = 10,
                Opacity           = 0.6,
                FontFamily        = new FontFamily("Consolas, Courier New"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(4, 0, 0, 0),
            };
            DockPanel.SetDock(extText, Dock.Right);
            panel.Children.Add(extText);
        }

        panel.Children.Add(new TextBlock
        {
            Text              = row.Name,
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Center,
        });

        return panel;
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
            Tag     = folder.Id,
        });
        foreach (var child in folder.Children)
            AddFolderItem(child, indent + 1);
    }

    // -- Event handlers -----------------------------------------------------

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        BuildTree(filter: SearchBox.Text?.Trim() ?? "");
        RefreshStatus();
    }

    private void OnTreeItemSelected(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem { Tag: SyntaxRow row })
        {
            _lastSelected = row;
            RefreshDetail(row);
            RefreshPreview(row);
        }
        e.Handled = true;
    }

    private void OnCheckBoxChecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: SyntaxRow row })
        {
            _checked.Add(row);
            _lastSelected = row;
            RefreshDetail(row);
            RefreshPreview(row);
            RefreshStatus();
        }
    }

    private void OnCheckBoxUnchecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: SyntaxRow row })
        {
            _checked.Remove(row);
            RefreshStatus();
        }
    }

    private void OnPreviewToggle(object sender, RoutedEventArgs e)
    {
        var show = PreviewToggle.IsChecked == true;

        if (show)
        {
            PreviewColumn.Width         = new GridLength(260);
            PreviewSplitterColumn.Width = new GridLength(5);
            PreviewSplitter.Visibility  = Visibility.Visible;
            PreviewBorder.Visibility    = Visibility.Visible;
            RefreshPreview(_lastSelected);
        }
        else
        {
            PreviewColumn.Width         = new GridLength(0);
            PreviewSplitterColumn.Width = new GridLength(0);
            PreviewSplitter.Visibility  = Visibility.Collapsed;
            PreviewBorder.Visibility    = Visibility.Collapsed;
        }
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        SelectedEntries = _checked.Select(r => r.Entry).ToList();
        TargetFolderId  = (FolderCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        DialogResult    = true;
    }

    // -- Private helpers ----------------------------------------------------

    private void RefreshDetail(SyntaxRow? row)
    {
        if (row is null)
        {
            DetailName.Text     = "";
            DetailCategory.Text = "";
            DetailExtWrap.Children.Clear();
            DetailDescription.Visibility = Visibility.Collapsed;
            return;
        }

        DetailName.Text     = row.Name;
        DetailCategory.Text = row.Category;

        DetailExtWrap.Children.Clear();
        foreach (var ext in row.Entry.Extensions)
            DetailExtWrap.Children.Add(MakeExtBadge(ext));

        DetailDescription.Visibility = Visibility.Collapsed;
    }

    private void RefreshPreview(SyntaxRow? row)
    {
        if (PreviewToggle.IsChecked != true || row is null)
            return;

        try
        {
            // Show the syntaxDefinition JSON block from the .whfmt file.
            var syntaxJson = _catalog.GetSyntaxDefinitionJson(row.Entry.ResourceKey);
            PreviewEditor.LoadText(syntaxJson ?? "// No syntaxDefinition block found.");
        }
        catch
        {
            PreviewEditor.LoadText("// Could not load preview.");
        }
    }

    private void RefreshStatus()
    {
        var count = _checked.Count;
        SelectionCountText.Text = count == 0
            ? "No syntax selected"
            : $"{count} syntax definition{(count == 1 ? "" : "s")} selected";
        ImportButton.IsEnabled = count > 0;
    }

    private static Border MakeExtBadge(string ext) => new()
    {
        Margin       = new Thickness(0, 0, 4, 4),
        Padding      = new Thickness(5, 2, 5, 2),
        CornerRadius = new CornerRadius(3),
        Background   = (Brush)Application.Current.TryFindResource("ERR_FilterActiveBrush")
                       ?? new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
        Child = new TextBlock
        {
            Text       = ext,
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize   = 11,
        },
    };

    // -- Inner types --------------------------------------------------------

    private sealed class CategoryNode(string name)
    {
        public string          Name  { get; } = name;
        public List<SyntaxRow> Items { get; } = [];
    }

    private sealed class SyntaxRow(EmbeddedFormatEntry entry)
    {
        public EmbeddedFormatEntry Entry      { get; } = entry;
        public string              Name       => entry.Name;
        public string              Category   => entry.Category;
        public string              ExtDisplay => string.Join(", ", entry.Extensions);
    }
}
