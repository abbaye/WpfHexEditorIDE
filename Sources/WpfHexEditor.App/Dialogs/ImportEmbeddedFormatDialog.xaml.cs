// ==========================================================
// Project: WpfHexEditor.App
// File: ImportEmbeddedFormatDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Created: 2026-03-06
// Description:
//     Dialog that lets the user pick one or more embedded format definitions
//     (.whfmt) to import into a project.
//     Features collapsible categories (TreeView), checkbox multi-select,
//     detail panel and always-visible JSON preview (bottom of detail column).
//
// Architecture Notes:
//     Mirrors ImportEmbeddedSyntaxDialog pattern:
//       - BuildCategories() groups entries once
//       - BuildTree(filter) rebuilds tree after a 280ms debounce
//       - Search covers Name, ExtDisplay, Description (JSON excluded for performance)
//       - _checked HashSet tracks selection; Preview uses CodeEditor (read-only, always visible)
//
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Definitions;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Views;

namespace WpfHexEditor.App.Dialogs;

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
public partial class ImportEmbeddedFormatDialog : ThemedDialog
{
    // ── Output properties ──────────────────────────────────────────────────
    public IReadOnlyList<EmbeddedFormatEntry> SelectedEntries { get; private set; } = [];
    /// <summary>Id of the virtual folder, or <c>null</c> for the project root.</summary>
    public string? TargetFolderId { get; private set; }

    // ── Private state ──────────────────────────────────────────────────────
    private readonly IEmbeddedFormatCatalog _catalog;
    private          List<CategoryNode>     _categories   = [];
    private readonly HashSet<FormatRow>     _checked      = [];
    private          FormatRow?             _lastSelected;

    /// <summary>Lazy full-JSON cache keyed by ResourceKey.</summary>
    private readonly Dictionary<string, string> _jsonCache = [];

    /// <summary>Debounce timer — delays tree rebuild until user stops typing.</summary>
    private DispatcherTimer? _searchDebounce;

    private static readonly StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    // ── Constructor ────────────────────────────────────────────────────────
    /// <param name="catalog">Catalog of embedded format definitions.</param>
    /// <param name="project">Project that will receive the imported items (used for folder picker).</param>
    public ImportEmbeddedFormatDialog(IEmbeddedFormatCatalog catalog, IProject project, string? initialFolderId = null)
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

    // ── Initialisation ─────────────────────────────────────────────────────

    private void BuildCategories()
    {
        _categories = _catalog.GetAll()
            .GroupBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var node = new CategoryNode(g.Key);
                foreach (var entry in g.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                    node.Items.Add(new FormatRow(entry));
                return node;
            })
            .ToList();
    }

    private void BuildTree(string filter)
    {
        FormatTree.Items.Clear();

        var hasFilter = !string.IsNullOrWhiteSpace(filter);

        foreach (var category in _categories)
        {
            var matchingItems = hasFilter
                ? category.Items
                    .Where(r => r.Name.Contains(filter, OIC)
                             || r.ExtDisplay.Contains(filter, OIC)
                             || r.Entry.Description.Contains(filter, OIC))
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
                var formatItem = new TreeViewItem
                {
                    Header  = BuildFormatHeader(row),
                    Tag     = row,
                    Padding = new Thickness(2, 1, 2, 1),
                };
                formatItem.Selected += OnTreeItemSelected;
                catItem.Items.Add(formatItem);
            }

            FormatTree.Items.Add(catItem);
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

    private DockPanel BuildFormatHeader(FormatRow row)
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

        // Quality badge (right-aligned, color-coded)
        var qualityBrush = row.QualityScore >= 70
            ? new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0x76))   // green
            : row.QualityScore >= 40
                ? new SolidColorBrush(Color.FromRgb(0xD7, 0x9B, 0x22)) // orange
                : new SolidColorBrush(Color.FromRgb(0xF4, 0x4A, 0x47)); // red

        var qualityBadge = new Border
        {
            Margin            = new Thickness(4, 0, 0, 0),
            Padding           = new Thickness(4, 0, 4, 0),
            CornerRadius      = new CornerRadius(3),
            Background        = qualityBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Child             = new TextBlock
            {
                Text      = $"{row.QualityScore}%",
                FontSize  = 10,
                Foreground = Brushes.White,
            },
        };
        DockPanel.SetDock(qualityBadge, Dock.Right);
        panel.Children.Add(qualityBadge);

        // Extensions (right of name, small italic)
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

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        // Debounce: rebuild tree only after user stops typing for 280 ms.
        if (_searchDebounce is null)
        {
            _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
            _searchDebounce.Tick += OnSearchDebounced;
        }
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void OnSearchDebounced(object? sender, EventArgs e)
    {
        _searchDebounce!.Stop();
        BuildTree(filter: SearchBox.Text?.Trim() ?? "");
        RefreshStatus();
    }

    private void OnTreeItemSelected(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem { Tag: FormatRow row })
        {
            _lastSelected = row;
            RefreshDetail(row);
            RefreshPreview(row);
        }
        e.Handled = true;
    }

    private void OnCheckBoxChecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: FormatRow row })
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
        if (sender is CheckBox { Tag: FormatRow row })
        {
            _checked.Remove(row);
            RefreshStatus();
        }
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        SelectedEntries = _checked.Select(r => r.Entry).ToList();
        TargetFolderId  = (FolderCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        DialogResult    = true;
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private void RefreshDetail(FormatRow? row)
    {
        if (row is null)
        {
            DetailName.Text        = "";
            DetailCategory.Text    = "";
            DetailDescription.Text = "";
            DetailVersionRow.Visibility = Visibility.Collapsed;
            DetailAuthorRow.Visibility  = Visibility.Collapsed;
            DetailVersionText.Text      = "";
            DetailAuthorText.Text       = "";
            DetailExtWrap.Children.Clear();
            DetailQualityBar.Value = 0;
            DetailQualityText.Text = "";
            return;
        }

        DetailName.Text        = row.Name;
        DetailCategory.Text    = row.Category;
        DetailDescription.Text = row.Entry.Description;

        DetailVersionText.Text      = row.Entry.Version;
        DetailVersionRow.Visibility = string.IsNullOrEmpty(row.Entry.Version)
                                      ? Visibility.Collapsed : Visibility.Visible;
        DetailAuthorText.Text       = row.Entry.Author;
        DetailAuthorRow.Visibility  = string.IsNullOrEmpty(row.Entry.Author)
                                      ? Visibility.Collapsed : Visibility.Visible;

        DetailExtWrap.Children.Clear();
        foreach (var ext in row.Entry.Extensions)
            DetailExtWrap.Children.Add(MakeExtBadge(ext));

        DetailQualityBar.Value = row.QualityScore;
        DetailQualityText.Text = $"{row.QualityScore}%";
    }

    private void RefreshPreview(FormatRow? row)
    {
        if (row is null)
        {
            PreviewEditor.LoadText(string.Empty);
            return;
        }

        try
        {
            PreviewEditor.LoadText(GetJsonText(row.Entry));
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
            ? "No format selected"
            : $"{count} format{(count == 1 ? "" : "s")} selected";
        ImportButton.IsEnabled = count > 0;
    }

    /// <summary>Returns full JSON text for the entry, loading lazily and caching.</summary>
    private string GetJsonText(EmbeddedFormatEntry entry)
    {
        if (!_jsonCache.TryGetValue(entry.ResourceKey, out var json))
            _jsonCache[entry.ResourceKey] = json = _catalog.GetJson(entry.ResourceKey);
        return json;
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

    // ── Inner types ────────────────────────────────────────────────────────

    private sealed class CategoryNode(string name)
    {
        public string          Name  { get; } = name;
        public List<FormatRow> Items { get; } = [];
    }

    private sealed class FormatRow(EmbeddedFormatEntry entry)
    {
        public EmbeddedFormatEntry Entry        { get; } = entry;
        public string              Name         => entry.Name;
        public string              Category     => entry.Category;
        public string              ExtDisplay   => string.Join(", ", entry.Extensions);
        public int                 QualityScore => entry.QualityScore;
    }
}
