// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Options.ViewModels;

namespace WpfHexEditor.Core.Options;

/// <summary>
/// VS2026-style Options editor — opened as a document tab in the docking area.
/// Changes are auto-saved immediately when any control value changes.
/// Automatically refreshes when plugins register or unregister options pages.
/// </summary>
public sealed partial class OptionsEditorControl : UserControl
{
    // -- State -------------------------------------------------------------
    private readonly Dictionary<OptionsPageDescriptor, UserControl> _pageCache = new();
    private readonly List<IOptionsPage> _shownPages = new();
    private readonly ObservableCollection<OptionsTreeItemViewModel> _treeItems = new();
    private OptionsPageDescriptor? _currentDesc;
    private bool _initialized;
    private string? _currentSelectionPath; // To restore selection after rebuild

    // -- Events (consumed by MainWindow) -----------------------------------

    /// <summary>Fired after any setting is auto-saved.</summary>
    public event Action? SettingsChanged;

    /// <summary>Fired when the user clicks the "Edit JSON" button.</summary>
    public event Action<string>? EditJsonRequested;

    // -- Construction ------------------------------------------------------

    public OptionsEditorControl()
    {
        InitializeComponent();

        // Subscribe to registry events for auto-refresh
        OptionsPageRegistry.PageRegistered += OnPageRegistered;
        OptionsPageRegistry.PageUnregistered += OnPageUnregistered;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        BuildTree();
        PopulateFilterCombo();
        SelectFirstPage();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Unsubscribe from events to prevent memory leaks
        OptionsPageRegistry.PageRegistered -= OnPageRegistered;
        OptionsPageRegistry.PageUnregistered -= OnPageUnregistered;
    }

    // -- Event handlers for dynamic page registration ---------------------

    private void OnPageRegistered(object? sender, OptionsPageDescriptor descriptor)
    {
        // Rebuild the tree on the UI thread
        Dispatcher.InvokeAsync(() =>
        {
            SaveCurrentSelection();
            RebuildTree();
            RestoreSelection();
        });
    }

    private void OnPageUnregistered(object? sender, (string Category, string PageName) info)
    {
        // Rebuild the tree on the UI thread
        Dispatcher.InvokeAsync(() =>
        {
            // Clear cache if it contains the removed page
            var toRemove = _pageCache.Keys.FirstOrDefault(d =>
                d.Category == info.Category && d.PageName == info.PageName);
            if (toRemove != null)
            {
                _pageCache.Remove(toRemove);
            }

            SaveCurrentSelection();
            RebuildTree();
            RestoreSelection();
        });
    }

    // -- Tree building -----------------------------------------------------

    private void BuildTree()
    {
        PageTree.Items.Clear();

        // Group pages by category
        var groups = OptionsPageRegistry.Pages.GroupBy(p => p.Category);

        foreach (var group in groups)
        {
            // Use the icon from the first descriptor in the group (all pages in same category should have same icon)
            var icon = group.FirstOrDefault()?.CategoryIcon ?? "📂";

            var catItem = new TreeViewItem
            {
                Header     = $"{icon}  {group.Key}",
                IsExpanded = true,
                FontWeight = FontWeights.SemiBold,
                Focusable  = false,    // category headers are not selectable
            };
            catItem.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");

            foreach (var desc in group)
            {
                var pageItem = new TreeViewItem
                {
                    Header  = desc.PageName,
                    Tag     = desc,
                    Padding = new Thickness(20, 3, 4, 3),  // Increased indent for hierarchy
                };
                pageItem.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
                catItem.Items.Add(pageItem);
            }

            PageTree.Items.Add(catItem);
        }
    }

    /// <summary>
    /// Rebuilds the entire tree (used when pages are dynamically added/removed).
    /// </summary>
    private void RebuildTree()
    {
        BuildTree();
        PopulateFilterCombo();
    }

    /// <summary>
    /// Saves the current selection path so it can be restored after rebuild.
    /// </summary>
    private void SaveCurrentSelection()
    {
        if (PageTree.SelectedItem is TreeViewItem { Tag: OptionsPageDescriptor desc })
        {
            _currentSelectionPath = $"{desc.Category}|{desc.PageName}";
        }
        else
        {
            _currentSelectionPath = null;
        }
    }

    /// <summary>
    /// Restores the previously selected item after tree rebuild.
    /// </summary>
    private void RestoreSelection()
    {
        if (string.IsNullOrEmpty(_currentSelectionPath))
        {
            SelectFirstPage();
            return;
        }

        var parts = _currentSelectionPath.Split('|');
        if (parts.Length != 2) return;

        var category = parts[0];
        var pageName = parts[1];

        // Find and select the matching item
        foreach (TreeViewItem catItem in PageTree.Items)
        {
            foreach (TreeViewItem pageItem in catItem.Items)
            {
                if (pageItem.Tag is OptionsPageDescriptor desc &&
                    desc.Category == category &&
                    desc.PageName == pageName)
                {
                    pageItem.IsSelected = true;
                    catItem.IsExpanded = true;
                    return;
                }
            }
        }

        // Fallback: select first page if not found
        SelectFirstPage();
    }

    private void SelectFirstPage()
    {
        if (PageTree.Items.Count > 0 &&
            PageTree.Items[0] is TreeViewItem cat &&
            cat.Items.Count > 0 &&
            cat.Items[0] is TreeViewItem first)
        {
            first.IsSelected = true;
        }
    }

    /// <summary>
    /// Navigates directly to the specified category/page combination.
    /// If the panel is not yet loaded, the selection is deferred to the Loaded event.
    /// </summary>
    public void NavigateTo(string category, string pageName)
    {
        if (!_initialized)
        {
            // Defer until the control is fully loaded and the tree is built.
            void OnFirstLoad(object s, RoutedEventArgs ev)
            {
                Loaded -= OnFirstLoad;
                SelectPage(category, pageName);
            }
            Loaded += OnFirstLoad;
            return;
        }

        SelectPage(category, pageName);
    }

    private void SelectPage(string category, string pageName)
    {
        foreach (TreeViewItem catItem in PageTree.Items)
        {
            foreach (TreeViewItem pageItem in catItem.Items)
            {
                if (pageItem.Tag is OptionsPageDescriptor desc &&
                    desc.Category == category &&
                    desc.PageName  == pageName)
                {
                    catItem.IsExpanded = true;
                    pageItem.IsSelected = true;
                    pageItem.BringIntoView();
                    return;
                }
            }
        }
    }

    private void PopulateFilterCombo()
    {
        FilterCombo.Items.Clear();
        FilterCombo.Items.Add(new ComboBoxItem { Content = "All settings", Tag = "" });

        foreach (var cat in OptionsPageRegistry.Pages.Select(p => p.Category).Distinct())
            FilterCombo.Items.Add(new ComboBoxItem { Content = cat, Tag = cat });

        FilterCombo.SelectedIndex = 0;
    }

    // -- Navigation --------------------------------------------------------

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem { Tag: OptionsPageDescriptor desc })
            NavigateTo(desc);
    }

    private void NavigateTo(OptionsPageDescriptor desc)
    {
        if (_currentDesc == desc) return;
        _currentDesc = desc;

        if (!_pageCache.TryGetValue(desc, out var ctrl))
        {
            ctrl = desc.Factory();
            _pageCache[desc] = ctrl;

            if (ctrl is IOptionsPage page)
            {
                page.Load(AppSettingsService.Instance.Current);
                page.Changed += OnPageChanged;
                _shownPages.Add(page);
            }
        }

        PageHost.Content = ctrl;
    }

    // -- Auto-save ---------------------------------------------------------

    private void OnPageChanged(object? sender, EventArgs e)
    {
        if (sender is not IOptionsPage page) return;
        page.Flush(AppSettingsService.Instance.Current);
        AppSettingsService.Instance.Save();
        SettingsChanged?.Invoke();
    }

    // -- Search & Filter ---------------------------------------------------

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        => ApplyFilter(SearchBox.Text.Trim());

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilterCombo.SelectedItem is ComboBoxItem item)
            ApplyFilter(item.Tag?.ToString() ?? "");
    }

    private void ApplyFilter(string text)
    {
        var lower = text.ToLowerInvariant();

        foreach (TreeViewItem catItem in PageTree.Items)
        {
            bool catVisible = false;
            foreach (TreeViewItem pageItem in catItem.Items)
            {
                if (pageItem.Tag is not OptionsPageDescriptor desc) continue;

                bool match = string.IsNullOrEmpty(lower)
                    || desc.Category.ToLowerInvariant().Contains(lower)
                    || desc.PageName.ToLowerInvariant().Contains(lower);

                pageItem.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                if (match) catVisible = true;
            }
            catItem.Visibility = catVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // -- Edit JSON ---------------------------------------------------------

    private void OnEditJson(object sender, RoutedEventArgs e)
        => EditJsonRequested?.Invoke(AppSettingsService.Instance.FilePath);
}
