// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Options;

/// <summary>
/// VS2026-style Options editor — opened as a document tab in the docking area.
/// Changes are auto-saved immediately when any control value changes.
/// </summary>
public sealed partial class OptionsEditorControl : UserControl
{
    // ── State ─────────────────────────────────────────────────────────────
    private readonly Dictionary<OptionsPageDescriptor, UserControl> _pageCache = new();
    private readonly List<IOptionsPage> _shownPages = new();
    private OptionsPageDescriptor? _currentDesc;
    private bool _initialized;

    // ── Events (consumed by MainWindow) ───────────────────────────────────

    /// <summary>Fired after any setting is auto-saved.</summary>
    public event Action? SettingsChanged;

    /// <summary>Fired when the user clicks the "Edit JSON" button.</summary>
    public event Action<string>? EditJsonRequested;

    // ── Construction ──────────────────────────────────────────────────────

    public OptionsEditorControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        BuildTree();
        PopulateFilterCombo();
        SelectFirstPage();
    }

    // ── Tree building ─────────────────────────────────────────────────────

    private void BuildTree()
    {
        PageTree.Items.Clear();
        var groups = OptionsPageRegistry.Pages.GroupBy(p => p.Category);

        foreach (var group in groups)
        {
            var catItem = new TreeViewItem
            {
                Header     = group.Key,
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
                    Padding = new Thickness(16, 3, 4, 3),
                };
                pageItem.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
                catItem.Items.Add(pageItem);
            }

            PageTree.Items.Add(catItem);
        }
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

    private void PopulateFilterCombo()
    {
        FilterCombo.Items.Clear();
        FilterCombo.Items.Add(new ComboBoxItem { Content = "All settings", Tag = "" });

        foreach (var cat in OptionsPageRegistry.Pages.Select(p => p.Category).Distinct())
            FilterCombo.Items.Add(new ComboBoxItem { Content = cat, Tag = cat });

        FilterCombo.SelectedIndex = 0;
    }

    // ── Navigation ────────────────────────────────────────────────────────

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

    // ── Auto-save ─────────────────────────────────────────────────────────

    private void OnPageChanged(object? sender, EventArgs e)
    {
        if (sender is not IOptionsPage page) return;
        page.Flush(AppSettingsService.Instance.Current);
        AppSettingsService.Instance.Save();
        SettingsChanged?.Invoke();
    }

    // ── Search & Filter ───────────────────────────────────────────────────

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

    // ── Edit JSON ─────────────────────────────────────────────────────────

    private void OnEditJson(object sender, RoutedEventArgs e)
        => EditJsonRequested?.Invoke(AppSettingsService.Instance.FilePath);
}
