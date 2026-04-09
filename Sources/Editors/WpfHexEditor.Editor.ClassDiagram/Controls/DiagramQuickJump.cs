// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/DiagramQuickJump.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-09
// Description:
//     Ctrl+G quick-jump popup. Fuzzy-searches all class nodes and lets
//     the user navigate by keyboard (↑↓ + Enter) or mouse double-click.
//     Shows class name (bold), project name · namespace (subtitle).
//
// Architecture Notes:
//     Implemented as an overlay Border on the diagram host (ZIndex=250).
//     Removed from visual tree by ClassDiagramSplitHost on confirm/close.
//     150ms debounce on text input to avoid refiltering on every keystroke.
//     Max 60 results shown (ListBox with fixed height).
// ==========================================================

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>
/// Ctrl+G fuzzy class finder.  Overlay shown at top-center of the diagram host.
/// </summary>
public sealed class DiagramQuickJump : Border
{
    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired when the user confirms a selection — host should zoom to this node.</summary>
    public event EventHandler<ClassNode>? NodeChosen;
    /// <summary>Fired on Escape or when the overlay should close without a selection.</summary>
    public event EventHandler?           Closed;

    // ── Controls ──────────────────────────────────────────────────────────────
    private readonly TextBox  _searchBox;
    private readonly ListBox  _results;
    private readonly TextBlock _hintLabel;

    // ── State ─────────────────────────────────────────────────────────────────
    private DiagramDocument?                   _doc;
    private IReadOnlyList<DiagramProjectGroup> _groups = [];
    private readonly DispatcherTimer           _debounce;
    private const    int                       MaxResults = 60;

    // ── Constructor ───────────────────────────────────────────────────────────

    public DiagramQuickJump()
    {
        Width           = 520;
        CornerRadius    = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        Padding         = new Thickness(0);
        Panel.SetZIndex(this, 250);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment   = VerticalAlignment.Top;
        Margin              = new Thickness(0, 44, 0, 0); // below nav bar

        this.SetResourceReference(BackgroundProperty,  "CD_QuickJumpBackground");
        this.SetResourceReference(BorderBrushProperty, "CD_NavBarBorder");

        // Search box row
        _searchBox = new TextBox
        {
            Height            = 32,
            FontSize          = 13,
            Padding           = new Thickness(8, 4, 8, 4),
            BorderThickness   = new Thickness(0, 0, 0, 1),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _searchBox.SetResourceReference(Control.ForegroundProperty,         "CD_NavBarComboForeground");
        _searchBox.SetResourceReference(Control.BackgroundProperty,         "CD_QuickJumpBackground");
        _searchBox.SetResourceReference(Control.BorderBrushProperty,        "CD_NavBarBorder");
        _searchBox.TextChanged  += OnSearchTextChanged;
        _searchBox.PreviewKeyDown += OnSearchKeyDown;

        // Results list
        _results = new ListBox
        {
            MaxHeight         = 300,
            BorderThickness   = new Thickness(0),
            ItemTemplate      = BuildItemTemplate(),
            FontSize          = 12
        };
        _results.SetResourceReference(BackgroundProperty,           "CD_QuickJumpBackground");
        _results.MouseDoubleClick += (_, _) => ConfirmSelection();
        _results.PreviewKeyDown   += OnResultsKeyDown;

        // Hint row
        _hintLabel = new TextBlock
        {
            Text      = "↑↓ navigate  ·  Enter jump  ·  Esc cancel",
            FontSize  = 10,
            Opacity   = 0.5,
            Padding   = new Thickness(8, 3, 8, 3)
        };
        _hintLabel.SetResourceReference(TextBlock.ForegroundProperty, "CD_NavBarComboForeground");

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(_searchBox);
        stack.Children.Add(_results);
        stack.Children.Add(_hintLabel);
        Child = stack;

        // Debounce timer (150ms)
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); ApplyFilter(); };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetDocument(DiagramDocument doc, IReadOnlyList<DiagramProjectGroup> groups)
    {
        _doc    = doc;
        _groups = groups;
        _searchBox.Text = string.Empty;
        ApplyFilter();
    }

    /// <summary>Focuses the search box so the user can start typing immediately.</summary>
    public void FocusSearch() => _searchBox.Focus();

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (_results.Items.Count > 0)
                {
                    _results.Focus();
                    _results.SelectedIndex = 0;
                }
                e.Handled = true;
                break;
            case Key.Enter:
                ConfirmSelection();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void OnResultsKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                ConfirmSelection();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void ConfirmSelection()
    {
        if (_results.SelectedItem is QuickJumpItem item)
        {
            NodeChosen?.Invoke(this, item.Node);
            Close();
        }
    }

    private void Close()
    {
        _debounce.Stop();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        _results.Items.Clear();
        if (_doc is null) return;

        string term = _searchBox.Text?.Trim() ?? string.Empty;
        IEnumerable<ClassNode> candidates = _doc.Classes;

        if (!string.IsNullOrEmpty(term))
        {
            candidates = candidates.Where(n =>
                n.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
             || MatchInitials(n.Name, term));
        }

        int count = 0;
        foreach (var node in candidates.OrderBy(n => n.Name))
        {
            if (++count > MaxResults) break;
            string? proj = GetNodeProject(node);
            string subtitle = string.IsNullOrEmpty(node.Namespace)
                ? (proj ?? string.Empty)
                : $"{proj}  ·  {node.Namespace}";
            _results.Items.Add(new QuickJumpItem(node, subtitle));
        }

        if (_results.Items.Count > 0)
            _results.SelectedIndex = 0;
    }

    private static bool MatchInitials(string name, string term)
    {
        // "CD" matches "ClassDiagram", "DiagramCanvas" etc.
        if (term.Length < 2) return false;
        int ti = 0;
        foreach (char c in name)
        {
            if (char.IsUpper(c) && ti < term.Length && char.ToUpperInvariant(c) == char.ToUpperInvariant(term[ti]))
                ti++;
        }
        return ti >= term.Length;
    }

    private string? GetNodeProject(ClassNode node)
    {
        foreach (var g in _groups)
            if (g.ClassIds.Contains(node.Id))
                return g.ProjectName;
        return null;
    }

    // ── Item template ─────────────────────────────────────────────────────────

    private static DataTemplate BuildItemTemplate()
    {
        var dt     = new DataTemplate(typeof(QuickJumpItem));
        var outer  = new FrameworkElementFactory(typeof(StackPanel));
        outer.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        outer.SetValue(StackPanel.MarginProperty,      new Thickness(4, 2, 4, 2));

        var nameBlock = new FrameworkElementFactory(typeof(TextBlock));
        nameBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Node.Name"));
        nameBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        nameBlock.SetValue(TextBlock.FontSizeProperty,   12.0);

        var subBlock = new FrameworkElementFactory(typeof(TextBlock));
        subBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Subtitle"));
        subBlock.SetValue(TextBlock.FontSizeProperty,   10.0);
        subBlock.SetValue(TextBlock.OpacityProperty,    0.55);

        outer.AppendChild(nameBlock);
        outer.AppendChild(subBlock);
        dt.VisualTree = outer;
        return dt;
    }

    // ── Inner type ────────────────────────────────────────────────────────────

    private sealed record QuickJumpItem(ClassNode Node, string Subtitle);
}
