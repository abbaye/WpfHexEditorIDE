// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/FindReferencesPanel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Dockable "Find References" panel — VS2022 CodeLens style.
//     Shown when the user pins the references popup into the bottom
//     tool-window area.  Displays a collapsible tree of references
//     grouped by file with title bar, scrollable body, and footer.
//
// Architecture Notes:
//     UserControl (code-only) docked in the IDE bottom area.
//     Tree rendering delegated to ReferencesTreeBuilder (shared with popup).
//     WPF Theme — all brushes via SetResourceReference (CE_*, TE_*, Panel_*).
// ==========================================================

using System;
using System.Collections.Generic;
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

    private TextBlock  _titleTb      = null!;
    private ScrollViewer _scroll     = null!;
    private TextBlock  _collapseLink = null!;

    private List<(StackPanel ItemsPanel, TextBlock Chevron)> _groupHandles = new();
    private bool _allCollapsed;

    #endregion

    #region Events

    /// <summary>Fired when the user clicks a reference row.</summary>
    public event EventHandler<ReferencesNavigationEventArgs>? NavigationRequested;

    /// <summary>Fired when "Actualiser" is clicked — caller should re-run the search.</summary>
    public event EventHandler? RefreshRequested;

    /// <summary>Fired when the close button (✕) is clicked.</summary>
    public event EventHandler? CloseRequested;

    #endregion

    #region Constructor

    public FindReferencesPanel()
    {
        BuildUI();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Replaces the displayed results with <paramref name="groups"/> for <paramref name="symbolName"/>.
    /// Safe to call multiple times (subsequent calls update title and content).
    /// </summary>
    public void Refresh(IReadOnlyList<ReferenceGroup> groups, string symbolName)
    {
        _titleTb.Text      = $"CodeLens Références — {symbolName}";
        _allCollapsed      = false;
        _collapseLink.Text = "Tout réduire";

        var panel = ReferencesTreeBuilder.BuildGroupsPanel(
            groups,
            symbolName,
            e => NavigationRequested?.Invoke(this, e),
            out _groupHandles);

        _scroll.Content = panel;
    }

    #endregion

    #region UI Construction

    private void BuildUI()
    {
        // ── Root grid: title bar (row 0) + body (row 1) + footer (row 2) ──────
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
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

        // Close button (✕) — right-aligned
        var closeBtn = new TextBlock
        {
            Text              = "\uE711",    // Segoe MDL2 — close
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 10,
            Cursor            = Cursors.Hand,
            ToolTip           = "Fermer",
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(4, 0, 0, 0),
            Padding           = new Thickness(4, 2, 4, 2)
        };
        closeBtn.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");
        closeBtn.MouseEnter += (_, _) => closeBtn.SetResourceReference(
            TextBlock.ForegroundProperty, "CE_Keyword");
        closeBtn.MouseLeave += (_, _) => closeBtn.SetResourceReference(
            TextBlock.ForegroundProperty, "TE_Foreground");
        closeBtn.MouseLeftButtonDown += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        DockPanel.SetDock(closeBtn, Dock.Right);

        // Title text — "CodeLens Références — {symbol}"
        _titleTb = new TextBlock
        {
            FontSize          = 11,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis
        };
        _titleTb.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");

        titleRow.Children.Add(closeBtn);
        titleRow.Children.Add(_titleTb);
        titleBar.Child = titleRow;

        // ── Scrollable body ───────────────────────────────────────────────────
        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _scroll.SetResourceReference(ScrollViewer.BackgroundProperty, "TE_Background");
        Grid.SetRow(_scroll, 1);

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new Border
        {
            Padding         = new Thickness(10, 5, 10, 5),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        footer.SetResourceReference(Border.BackgroundProperty,  "TE_Background");
        footer.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");
        Grid.SetRow(footer, 2);

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
        refreshLink.MouseLeftButtonDown += (_, _) =>
            RefreshRequested?.Invoke(this, EventArgs.Empty);

        footerRow.Children.Add(_collapseLink);
        footerRow.Children.Add(refreshLink);
        footer.Child = footerRow;

        root.Children.Add(titleBar);
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
