// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
    private readonly DispatcherTimer _searchDebounce;
    private OptionsPageDescriptor? _currentDesc;
    private bool _initialized;
    private string? _currentSelectionPath; // To restore selection after rebuild

    // -- Content search state ----------------------------------------------
    // Built lazily on first non-empty search; invalidated on tree rebuild.
    private Dictionary<OptionsPageDescriptor, List<ContentEntry>>? _contentIndex;
    // Tracks elements whose Background was replaced for highlight (so we can restore them).
    private readonly Dictionary<FrameworkElement, Brush?> _savedBackgrounds = new();

    private readonly record struct ContentEntry(string LowerText, FrameworkElement Element);

    private static readonly Brush HighlightBrush = CreateHighlightBrush();
    private static Brush CreateHighlightBrush()
    {
        var b = new SolidColorBrush(Color.FromArgb(90, 255, 200, 0)); // semi-transparent amber
        b.Freeze();
        return b;
    }

    // -- Events (consumed by MainWindow) -----------------------------------

    /// <summary>Fired after any setting is auto-saved.</summary>
    public event Action? SettingsChanged;

    /// <summary>Fired when the user clicks the "Edit JSON" button.</summary>
    public event Action<string>? EditJsonRequested;

    // -- Construction ------------------------------------------------------

    public OptionsEditorControl()
    {
        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); ApplyFilter(); };

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
                _contentIndex?.Remove(toRemove);
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
            var icon = group.FirstOrDefault()?.CategoryIcon ?? "⚙";

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
        _contentIndex = null; // invalidate — will be rebuilt on next search
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
        var query = SearchBox.Text.Trim().ToLowerInvariant();
        ClearPageHighlights();

        if (_currentDesc == desc)
        {
            // Same page re-selected: just refresh highlights for new query
            if (!string.IsNullOrEmpty(query))
                HighlightPageMatches(desc, query);
            return;
        }
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

            // Register this newly instantiated page in an already-built index
            if (_contentIndex is not null)
                _contentIndex[desc] = ExtractContentEntries(ctrl);
        }

        PageHost.Content = ctrl;

        // Highlight must run after Content is set so the visual tree is attached
        if (!string.IsNullOrEmpty(query))
            Dispatcher.InvokeAsync(() => HighlightPageMatches(desc, query),
                System.Windows.Threading.DispatcherPriority.Loaded);
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
    {
        var hasText = SearchBox.Text.Length > 0;
        SearchWatermark.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        SearchClearBtn.Visibility  = hasText ? Visibility.Visible   : Visibility.Collapsed;
        ApplyFilter();
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void OnSearchClear(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                SearchBox.Clear();
                PageTree.Focus();
                e.Handled = true;
                break;
            case Key.Down:
                SelectFirstVisible();
                (PageTree.SelectedItem as TreeViewItem)?.Focus();
                e.Handled = true;
                break;
        }
    }

    private void OnControlKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void ApplyFilter()
    {
        var text      = SearchBox.Text.Trim();
        var lower     = text.ToLowerInvariant();
        var catFilter = (FilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        bool hasQuery   = !string.IsNullOrEmpty(lower);
        bool anyVisible = false;

        // Build content index on first non-empty search (one-time cost)
        if (hasQuery) EnsureContentIndex();
        // Clear page highlights when search changes
        else ClearPageHighlights();

        foreach (TreeViewItem catItem in PageTree.Items)
        {
            bool catVisible = false;
            foreach (TreeViewItem pageItem in catItem.Items)
            {
                if (pageItem.Tag is not OptionsPageDescriptor desc) continue;

                bool passesCombo = string.IsNullOrEmpty(catFilter) || desc.Category == catFilter;

                bool passesContent = hasQuery
                    && _contentIndex?.TryGetValue(desc, out var entries) == true
                    && entries.Any(e => e.LowerText.Contains(lower));

                bool passesText = !hasQuery
                    || desc.Category.Contains(lower, StringComparison.OrdinalIgnoreCase)
                    || desc.PageName.Contains(lower,  StringComparison.OrdinalIgnoreCase)
                    || (desc.SearchKeywords?.Any(k =>
                            k.Contains(lower, StringComparison.OrdinalIgnoreCase)) ?? false)
                    || passesContent;

                bool match = passesCombo && passesText;
                pageItem.Visibility = match ? Visibility.Visible : Visibility.Collapsed;

                if (match)
                {
                    catVisible = true;
                    anyVisible = true;
                    pageItem.Header = hasQuery
                        ? BuildHighlightedHeader(desc.PageName, lower)
                        : (object)desc.PageName;
                }
                else
                {
                    pageItem.Header = desc.PageName;
                }
            }

            catItem.Visibility = catVisible ? Visibility.Visible : Visibility.Collapsed;
            if (catVisible && hasQuery) catItem.IsExpanded = true;
        }

        NoResultsText.Visibility = !anyVisible && hasQuery
            ? Visibility.Visible : Visibility.Collapsed;

        if (hasQuery) SelectFirstVisible();
    }

    private static object BuildHighlightedHeader(string name, string query)
    {
        int idx = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return name;

        var tb = new TextBlock();
        if (idx > 0)
            tb.Inlines.Add(new Run(name[..idx]));

        // Bold only — no color change so the match stays readable on any background
        // (accent color would be invisible on a selected item that already uses that color)
        tb.Inlines.Add(new Run(name.Substring(idx, query.Length))
        {
            FontWeight = FontWeights.Bold,
        });

        if (idx + query.Length < name.Length)
            tb.Inlines.Add(new Run(name[(idx + query.Length)..]));

        return tb;
    }

    private void SelectFirstVisible()
    {
        foreach (TreeViewItem catItem in PageTree.Items)
        {
            if (catItem.Visibility != Visibility.Visible) continue;
            foreach (TreeViewItem pageItem in catItem.Items)
            {
                if (pageItem.Visibility == Visibility.Visible)
                {
                    pageItem.IsSelected = true;
                    return;
                }
            }
        }
    }

    // -- Content search index ----------------------------------------------

    /// <summary>
    /// Instantiates all pages (if needed) and builds the flat text-element index.
    /// Called once per search session; invalidated on tree rebuild.
    /// </summary>
    private void EnsureContentIndex()
    {
        if (_contentIndex is not null) return;
        _contentIndex = new();
        foreach (var desc in OptionsPageRegistry.Pages)
        {
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
            _contentIndex[desc] = ExtractContentEntries(ctrl);
        }
    }

    private static List<ContentEntry> ExtractContentEntries(DependencyObject root)
    {
        var result = new List<ContentEntry>();
        WalkLogicalTree(root, result);
        return result;
    }

    private static void WalkLogicalTree(DependencyObject node, List<ContentEntry> result)
    {
        var text = node switch
        {
            TextBlock  tb when !string.IsNullOrWhiteSpace(tb.Text)          => tb.Text,
            Label      lb when lb.Content  is string s && s.Length > 0      => s,
            CheckBox   cb when cb.Content  is string s && s.Length > 0      => s,
            RadioButton rb when rb.Content is string s && s.Length > 0      => s,
            GroupBox   gb when gb.Header   is string s && s.Length > 0      => s,
            Button    btn when btn.Content is string s && s.Length > 0      => s,
            _                                                                => null
        };

        if (text is not null && node is FrameworkElement fe)
            result.Add(new ContentEntry(text.ToLowerInvariant(), fe));

        foreach (var child in LogicalTreeHelper.GetChildren(node))
            if (child is DependencyObject dep)
                WalkLogicalTree(dep, result);
    }

    // -- Page highlight ----------------------------------------------------

    private void ClearPageHighlights()
    {
        foreach (var (element, savedBrush) in _savedBackgrounds)
            SetElementBackground(element, savedBrush);
        _savedBackgrounds.Clear();
    }

    private void HighlightPageMatches(OptionsPageDescriptor desc, string lower)
    {
        ClearPageHighlights();
        if (_contentIndex is null || !_contentIndex.TryGetValue(desc, out var entries)) return;

        foreach (var entry in entries)
        {
            if (!entry.LowerText.Contains(lower)) continue;
            _savedBackgrounds[entry.Element] = GetElementBackground(entry.Element);
            SetElementBackground(entry.Element, HighlightBrush);
        }
    }

    private static Brush? GetElementBackground(FrameworkElement el) => el switch
    {
        TextBlock tb => tb.Background,
        Control   c  => c.Background,
        _            => null
    };

    private static void SetElementBackground(FrameworkElement el, Brush? brush)
    {
        switch (el)
        {
            case TextBlock tb: tb.Background = brush; break;
            case Control   c:  c.Background  = brush; break;
        }
    }

    // -- Edit JSON ---------------------------------------------------------

    private void OnEditJson(object sender, RoutedEventArgs e)
        => EditJsonRequested?.Invoke(AppSettingsService.Instance.FilePath);
}
