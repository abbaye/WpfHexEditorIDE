// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/FindReferencesPanel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Dockable "Find References" panel — VS2022 InlineHints style.
//     Shown when the user pins the references popup into the bottom
//     tool-window area.  Displays a collapsible tree of references
//     grouped by file with title bar, filter toolbar, scrollable body,
//     and footer with F8/Shift+F8 navigation.
//
// Architecture Notes:
//     UserControl (code-only) docked in the IDE bottom area.
//     Tree rendering delegated to ReferencesTreeBuilder (shared with popup).
//     WPF Theme — all brushes via SetResourceReference (CE_*, TE_*, Panel_*).
//     Scope filter uses snippet heuristics (comment prefix detection).
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// VS2022-style docked "Find References" panel.
/// Call <see cref="Refresh"/> to populate with new results.
/// </summary>
public sealed class FindReferencesPanel : UserControl
{
    #region Fields

    private TextBlock    _titleTb      = null!;
    private TextBlock    _counterTb    = null!;
    private TextBox      _searchBox    = null!;
    private ComboBox     _scopeCombo   = null!;
    private ScrollViewer _scroll       = null!;
    private TextBlock    _collapseLink = null!;

    private List<(StackPanel ItemsPanel, TextBlock Chevron)> _groupHandles = new();
    private IReadOnlyList<ReferenceGroup> _lastGroups = Array.Empty<ReferenceGroup>();
    private string _lastSymbol = string.Empty;
    private bool   _allCollapsed;

    // Ordered navigation list rebuilt on each filter apply
    private readonly List<ReferencesNavigationEventArgs> _navItems = new();
    private int _navIndex = -1;

    #endregion

    #region Events

    /// <summary>Fired when the user clicks a reference row.</summary>
    public event EventHandler<ReferencesNavigationEventArgs>? NavigationRequested;

    /// <summary>Fired when "Actualiser" is clicked — caller should re-run the search.</summary>
    public event EventHandler? RefreshRequested;

    /// <summary>Fired when the close button (✕) is clicked.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Raised after F8 navigation (for external callers to hook).</summary>
    public event EventHandler? NavigateNextRequested;

    /// <summary>Raised after Shift+F8 navigation.</summary>
    public event EventHandler? NavigatePrevRequested;

    #endregion

    #region Constructor

    public FindReferencesPanel()
    {
        BuildUI();
        Focusable = true;
        KeyDown  += OnPanelKeyDown;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Replaces the displayed results with <paramref name="groups"/> for <paramref name="symbolName"/>.
    /// Safe to call multiple times.
    /// </summary>
    public void Refresh(IReadOnlyList<ReferenceGroup> groups, string symbolName)
    {
        _lastGroups   = groups;
        _lastSymbol   = symbolName;
        _navIndex     = -1;
        _allCollapsed = false;
        _collapseLink.Text = "Tout réduire";

        ApplyFilter();
    }

    /// <summary>Navigate to the next reference (F8).</summary>
    public void NavigateNext()
    {
        if (_navItems.Count == 0) return;
        _navIndex = (_navIndex + 1) % _navItems.Count;
        NavigationRequested?.Invoke(this, _navItems[_navIndex]);
        NavigateNextRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Navigate to the previous reference (Shift+F8).</summary>
    public void NavigatePrev()
    {
        if (_navItems.Count == 0) return;
        _navIndex = (_navIndex - 1 + _navItems.Count) % _navItems.Count;
        NavigationRequested?.Invoke(this, _navItems[_navIndex]);
        NavigatePrevRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Filter logic

    private void ApplyFilter()
    {
        var scope  = (_scopeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        var search = _searchBox.Text.Trim();

        var filtered = _lastGroups
            .Select(g => FilterGroup(g, scope, search))
            .Where(g => g.Items.Count > 0)
            .ToList();

        var totalRefs = filtered.Sum(g => g.Items.Count);

        _titleTb.Text   = $"Références — {_lastSymbol}";
        _counterTb.Text = $"{totalRefs} référence{(totalRefs != 1 ? "s" : "")}";

        _navItems.Clear();

        var panel = ReferencesTreeBuilder.BuildGroupsPanel(
            filtered,
            _lastSymbol,
            e =>
            {
                _navItems.Add(e);
                NavigationRequested?.Invoke(this, e);
            },
            out _groupHandles);

        _scroll.Content = panel;
    }

    private static ReferenceGroup FilterGroup(ReferenceGroup g, string scope, string search)
    {
        var items = g.Items.Where(item => MatchesScope(item, scope)).ToList();

        if (!string.IsNullOrEmpty(search))
            items = items
                .Where(item => item.Snippet.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        return new ReferenceGroup
        {
            FilePath     = g.FilePath,
            DisplayLabel = g.DisplayLabel,
            Items        = items
        };
    }

    /// <summary>
    /// Scope heuristic: "Comments" = snippet trimmed starts with "//" or "/*" or "'".
    /// "All" / "Code" = everything else.
    /// </summary>
    private static bool MatchesScope(ReferenceItem item, string scope)
    {
        if (scope == "All") return true;

        var trimmed    = item.Snippet.TrimStart();
        var isComment  = trimmed.StartsWith("//", StringComparison.Ordinal)
                      || trimmed.StartsWith("/*", StringComparison.Ordinal)
                      || trimmed.StartsWith("'",  StringComparison.Ordinal);   // VB.NET

        return scope == "Comments" ? isComment : !isComment;
    }

    #endregion

    #region Keyboard navigation

    private void OnPanelKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F8 && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            NavigatePrev();
            e.Handled = true;
        }
        else if (e.Key == Key.F8 && Keyboard.Modifiers == ModifierKeys.None)
        {
            NavigateNext();
            e.Handled = true;
        }
    }

    #endregion

    #region UI Construction

    private void BuildUI()
    {
        // ── Root grid: title bar / filter bar / body / footer ─────────────────
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // 0: title
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // 1: filter
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 2: body
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // 3: footer
        root.SetResourceReference(Grid.BackgroundProperty, "TE_Background");

        // ── Title bar ─────────────────────────────────────────────────────────
        var titleBar = new Border
        {
            Padding         = new Thickness(8, 4, 6, 4),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        titleBar.SetResourceReference(Border.BackgroundProperty,  "Panel_ToolbarBrush");
        titleBar.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");
        Grid.SetRow(titleBar, 0);

        var titleRow = new DockPanel { LastChildFill = true };

        var closeBtn = new TextBlock
        {
            Text              = "\uE711",
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 10,
            Cursor            = Cursors.Hand,
            ToolTip           = "Fermer",
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(4, 0, 0, 0),
            Padding           = new Thickness(4, 2, 4, 2)
        };
        closeBtn.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");
        closeBtn.MouseEnter += (_, _) => closeBtn.SetResourceReference(TextBlock.ForegroundProperty, "CE_Keyword");
        closeBtn.MouseLeave += (_, _) => closeBtn.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");
        closeBtn.MouseLeftButtonDown += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        DockPanel.SetDock(closeBtn, Dock.Right);

        _counterTb = new TextBlock
        {
            FontSize          = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 4, 0),
            Opacity           = 0.7
        };
        _counterTb.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");
        DockPanel.SetDock(_counterTb, Dock.Right);

        _titleTb = new TextBlock
        {
            FontSize          = 11,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis
        };
        _titleTb.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");

        titleRow.Children.Add(closeBtn);
        titleRow.Children.Add(_counterTb);
        titleRow.Children.Add(_titleTb);
        titleBar.Child = titleRow;

        // ── Filter toolbar ────────────────────────────────────────────────────
        var filterBar = new Border
        {
            Padding         = new Thickness(8, 3, 8, 3),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        filterBar.SetResourceReference(Border.BackgroundProperty,  "Panel_ToolbarBrush");
        filterBar.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");
        Grid.SetRow(filterBar, 1);

        var filterRow = new DockPanel { LastChildFill = true };

        _scopeCombo = new ComboBox
        {
            Width             = 130,
            FontSize          = 11,
            Margin            = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip           = "Filtrer par portée"
        };
        _scopeCombo.Items.Add(new ComboBoxItem { Content = "Toutes",       Tag = "All"      });
        _scopeCombo.Items.Add(new ComboBoxItem { Content = "Code",         Tag = "Code"     });
        _scopeCombo.Items.Add(new ComboBoxItem { Content = "Commentaires", Tag = "Comments" });
        _scopeCombo.SelectedIndex = 0;
        _scopeCombo.SelectionChanged += (_, _) => ApplyFilter();
        DockPanel.SetDock(_scopeCombo, Dock.Left);

        _searchBox = new TextBox
        {
            FontSize                 = 11,
            VerticalAlignment        = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Height                   = 22,
            ToolTip                  = "Rechercher dans les résultats"
        };
        _searchBox.SetResourceReference(TextBox.BackgroundProperty, "TE_Background");
        _searchBox.SetResourceReference(TextBox.ForegroundProperty, "TE_Foreground");
        _searchBox.TextChanged += (_, _) => ApplyFilter();

        filterRow.Children.Add(_scopeCombo);
        filterRow.Children.Add(_searchBox);
        filterBar.Child = filterRow;

        // ── Scrollable body ───────────────────────────────────────────────────
        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _scroll.SetResourceReference(ScrollViewer.BackgroundProperty, "TE_Background");
        Grid.SetRow(_scroll, 2);

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new Border
        {
            Padding         = new Thickness(10, 5, 10, 5),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        footer.SetResourceReference(Border.BackgroundProperty,  "TE_Background");
        footer.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");
        Grid.SetRow(footer, 3);

        var footerRow = new StackPanel { Orientation = Orientation.Horizontal };

        _collapseLink = new TextBlock
        {
            Text              = "Tout réduire",
            FontSize          = 11,
            Cursor            = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        _collapseLink.SetResourceReference(TextBlock.ForegroundProperty, "CE_Keyword");
        _collapseLink.MouseLeftButtonDown += (_, _) => OnCollapseAllClicked();

        var refreshLink = new TextBlock
        {
            Text              = "Actualiser",
            FontSize          = 11,
            Cursor            = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(16, 0, 0, 0)
        };
        refreshLink.SetResourceReference(TextBlock.ForegroundProperty, "CE_Keyword");
        refreshLink.MouseLeftButtonDown += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        var navHint = new TextBlock
        {
            Text              = "F8 / Shift+F8 pour naviguer",
            FontSize          = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(16, 0, 0, 0),
            Opacity           = 0.55
        };
        navHint.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");

        footerRow.Children.Add(_collapseLink);
        footerRow.Children.Add(refreshLink);
        footerRow.Children.Add(navHint);
        footer.Child = footerRow;

        root.Children.Add(titleBar);
        root.Children.Add(filterBar);
        root.Children.Add(_scroll);
        root.Children.Add(footer);

        Content = root;
    }

    #endregion

    #region Collapse / Expand

    private void OnCollapseAllClicked()
    {
        _allCollapsed = !_allCollapsed;
        foreach (var (panel, chevron) in _groupHandles)
        {
            panel.Visibility = _allCollapsed ? Visibility.Collapsed : Visibility.Visible;
            chevron.Text     = _allCollapsed ? "\uE76B" : "\uE70D";
        }
        _collapseLink.Text = _allCollapsed ? "Tout développer" : "Tout réduire";
    }

    #endregion
}
