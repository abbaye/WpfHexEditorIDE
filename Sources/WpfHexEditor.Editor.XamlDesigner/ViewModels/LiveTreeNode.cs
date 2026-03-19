// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: LiveTreeNode.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-19 — Added IconGlyph/IconColor (VS-palette type icons),
//                        TooltipLines (rich hover tooltip), Parent backref
//                        for breadcrumb / expand-to-selection / full-path commands,
//                        copy commands (CopyTypeNameCommand, CopyElementNameCommand,
//                        CopyFullPathCommand) and SelectCommand for breadcrumb clicks.
//          2026-03-19 — Added IsSearchMatch/IsCurrentSearchMatch for search highlight,
//                        ShowDimensions toggle, RefreshDimsVisibility(),
//                        GenerateXamlSnippet(), CopyXamlSnippetCommand.
// Description:
//     ViewModel wrapping a live WPF DependencyObject for display in the
//     Visual Tree mode of the XamlOutlinePanel.
//     Shows the runtime type name, optional x:Name / Name, and the actual
//     dimensions if the object is a FrameworkElement.
//
// Architecture Notes:
//     INPC. IsSelected / IsExpanded support two-way TreeView binding.
//     Children populated by LiveVisualTreeService.
//     Parent set by LiveVisualTreeService after child construction.
//     IconGlyph/IconColor mapped from type name using static lookup table.
// ==========================================================

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// View model for a single node in the live visual tree.
/// </summary>
public sealed class LiveTreeNode : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isExpanded;
    private bool _isSearchMatch;
    private bool _isCurrentSearchMatch;
    private bool _showDimensions = true;

    // ── Constructor ───────────────────────────────────────────────────────────

    public LiveTreeNode(DependencyObject source)
    {
        Source      = source;
        TypeName    = source.GetType().Name;
        ElementName = (source as FrameworkElement)?.Name;

        if (source is FrameworkElement fe)
        {
            if (fe.ActualWidth > 0)
                DimensionsLabel = $"{fe.ActualWidth:F0}×{fe.ActualHeight:F0}";

            VisibilityBadgeText = fe.Visibility != Visibility.Visible
                ? fe.Visibility.ToString()
                : null;
            IsDisabled = !fe.IsEnabled;
        }

        DisplayLabel   = BuildLabel();
        IconGlyph      = ResolveIconGlyph(TypeName);
        IconBrush      = MakeBrush(ResolveIconColor(TypeName));
        TooltipLines   = BuildTooltipLines(source);
        QuickPeekLabel = BuildQuickPeek(source);

        CopyTypeNameCommand    = new RelayCommand(_ => Clipboard.SetText(TypeName));
        CopyElementNameCommand = new RelayCommand(_ => Clipboard.SetText(ElementName ?? string.Empty));
        CopyFullPathCommand    = new RelayCommand(_ => Clipboard.SetText(FullPath));
        CopyXamlSnippetCommand = new RelayCommand(_ => Clipboard.SetText(GenerateXamlSnippet()));
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public DependencyObject Source          { get; }
    public string           TypeName        { get; }
    public string?          ElementName     { get; }
    public string?          DimensionsLabel { get; }
    public string           DisplayLabel    { get; }

    /// <summary>True when x:Name / Name is non-empty.</summary>
    public bool HasName => !string.IsNullOrEmpty(ElementName);

    /// <summary>"Hidden" or "Collapsed" when the element is not Visible; null otherwise.</summary>
    public string? VisibilityBadgeText { get; private set; }

    /// <summary>True when IsEnabled = false.</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>Short discriminating value for unnamed nodes (e.g. text content, Grid "3×2").</summary>
    public string? QuickPeekLabel { get; }

    // ── Search match state (set by LiveVisualTreePanelViewModel.ExecuteSearch) ──

    /// <summary>True when this node matches the current search term (non-current match).</summary>
    public bool IsSearchMatch
    {
        get => _isSearchMatch;
        set { if (_isSearchMatch == value) return; _isSearchMatch = value; OnPropertyChanged(); }
    }

    /// <summary>True when this node is the active/current search match.</summary>
    public bool IsCurrentSearchMatch
    {
        get => _isCurrentSearchMatch;
        set { if (_isCurrentSearchMatch == value) return; _isCurrentSearchMatch = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Controls whether the dimensions label is shown for this node.
    /// Set to false by the ViewModel when the user toggles "Show Dimensions" off.
    /// </summary>
    public bool ShowDimensions
    {
        get => _showDimensions;
        set
        {
            if (_showDimensions == value) return;
            _showDimensions = value;
            OnPropertyChanged(nameof(DimsVisibility));
        }
    }

    /// <summary>Explicitly raises INPC for DimsVisibility (used after batch propagation).</summary>
    public void RefreshDimsVisibility() => OnPropertyChanged(nameof(DimsVisibility));

    // ── Visibility helpers (avoid converters in XAML) ─────────────────────
    public Visibility NameVisibility            => HasName               ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Collapsed when element has no dimensions or ShowDimensions is false.</summary>
    public Visibility DimsVisibility            => DimensionsLabel != null && _showDimensions ? Visibility.Visible : Visibility.Collapsed;

    public Visibility VisibilityBadgeVisibility => VisibilityBadgeText != null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DisabledBadgeVisibility   => IsDisabled            ? Visibility.Visible : Visibility.Collapsed;
    public Visibility QuickPeekVisibility       => !string.IsNullOrEmpty(QuickPeekLabel) && !HasName
                                                       ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Segoe MDL2 Assets glyph character for this element's type.</summary>
    public string IconGlyph  { get; }

    /// <summary>Frozen <see cref="SolidColorBrush"/> for the type icon (VS color palette).</summary>
    public Brush IconBrush   { get; }

    /// <summary>Multi-line tooltip content built from the element's runtime properties.</summary>
    public IReadOnlyList<string> TooltipLines { get; }

    /// <summary>
    /// Parent node in the live visual tree — set by <see cref="Services.LiveVisualTreeService"/>
    /// after child construction. Null for the root node.
    /// </summary>
    public LiveTreeNode? Parent { get; internal set; }

    /// <summary>Slash-delimited type path from root to this node (e.g. "Grid/StackPanel/Button").</summary>
    public string FullPath
    {
        get
        {
            var parts = new List<string>();
            var current = this;
            while (current is not null) { parts.Add(current.TypeName); current = current.Parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }

    public ObservableCollection<LiveTreeNode> Children { get; } = new();

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
    }

    // ── Commands (bound from DataTemplate / context menu) ─────────────────────

    public ICommand CopyTypeNameCommand    { get; }
    public ICommand CopyElementNameCommand { get; }
    public ICommand CopyFullPathCommand    { get; }
    public ICommand CopyXamlSnippetCommand { get; }

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a minimal XAML open/close tag with key properties (x:Name, Width, Height, Margin).
    /// </summary>
    public string GenerateXamlSnippet()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('<').Append(TypeName);

        if (!string.IsNullOrEmpty(ElementName))
            sb.Append($" x:Name=\"{ElementName}\"");

        if (Source is FrameworkElement fe)
        {
            if (fe.ActualWidth  > 0) sb.Append($" Width=\"{fe.ActualWidth:F0}\"");
            if (fe.ActualHeight > 0) sb.Append($" Height=\"{fe.ActualHeight:F0}\"");
            if (fe.Margin != default) sb.Append($" Margin=\"{fe.Margin}\"");
        }

        sb.Append(" />");
        return sb.ToString();
    }


    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string? BuildQuickPeek(DependencyObject source)
    {
        // TextBlock — show truncated text content
        if (source is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
            return tb.Text.Length > 25 ? '"' + tb.Text[..25] + "\u2026\"" : '"' + tb.Text + '"';

        // ContentControl (Button, Label, GroupBox…) — show string Content
        if (source is ContentControl cc && cc.Content is string cs && !string.IsNullOrWhiteSpace(cs))
            return cs.Length > 25 ? '"' + cs[..25] + "\u2026\"" : '"' + cs + '"';

        // Grid — show column × row dimensions when non-trivial
        if (source is Grid g && (g.ColumnDefinitions.Count > 1 || g.RowDefinitions.Count > 1))
            return $"{Math.Max(g.ColumnDefinitions.Count, 1)}\u00d7{Math.Max(g.RowDefinitions.Count, 1)}";

        // Image — show source filename
        if (source is System.Windows.Controls.Image img && img.Source is not null)
        {
            var uri   = img.Source.ToString();
            var slash = uri.LastIndexOf('/');
            return slash >= 0 ? uri[(slash + 1)..] : uri;
        }

        // TextBox — show first portion of text content
        if (source is TextBox txb && !string.IsNullOrWhiteSpace(txb.Text))
            return '"' + (txb.Text.Length > 20 ? txb.Text[..20] + "\u2026" : txb.Text) + '"';

        return null;
    }

    private string BuildLabel()
    {
        var label = TypeName;
        if (!string.IsNullOrEmpty(ElementName))
            label += $" [{ElementName}]";
        if (!string.IsNullOrEmpty(DimensionsLabel))
            label += $" ({DimensionsLabel})";
        return label;
    }

    private static IReadOnlyList<string> BuildTooltipLines(DependencyObject source)
    {
        var lines = new List<string>
        {
            source.GetType().FullName ?? source.GetType().Name
        };

        if (source is FrameworkElement fe)
        {
            if (!string.IsNullOrEmpty(fe.Name))
                lines.Add($"x:Name   {fe.Name}");

            lines.Add($"Size     {fe.ActualWidth:F0} × {fe.ActualHeight:F0}");
            lines.Add($"Margin   {fe.Margin}");
            lines.Add($"Visibility  {fe.Visibility}");
            lines.Add($"IsEnabled   {fe.IsEnabled}");

            if (source is Control ctrl)
                lines.Add($"Padding  {ctrl.Padding}");

            var bg = (source is Panel p ? p.Background :
                      source is Border b ? b.Background :
                      source is Control c2 ? c2.Background : null);
            if (bg is not null)
                lines.Add($"Background  {bg}");
        }

        return lines;
    }

    // ── Icon mapping (VS-palette) ──────────────────────────────────────────────

    private static string ResolveIconGlyph(string typeName) => typeName switch
    {
        "Grid" or "UniformGrid"                             => "\uE8A5",
        "StackPanel" or "WrapPanel" or "DockPanel"
            or "VirtualizingStackPanel"                     => "\uE8A5",
        "Border" or "Decorator" or "Viewbox"                => "\uE81E",
        "Button" or "ToggleButton" or "RepeatButton"
            or "RadioButton" or "CheckBox"                  => "\uE8FB",
        "TextBlock" or "Label" or "AccessText"              => "\uE8D2",
        "TextBox" or "RichTextBox" or "PasswordBox"         => "\uE8D2",
        "Canvas"                                            => "\uE930",
        "Image"                                             => "\uE8B9",
        "ListBox" or "ListView" or "DataGrid"               => "\uE8A9",
        "TreeView"                                          => "\uE8A0",
        "ScrollViewer" or "ScrollBar"                       => "\uE74A",
        "Popup" or "ToolTip"                                => "\uE8A9",
        "ComboBox" or "ComboBoxItem"                        => "\uE8A9",
        "TabControl" or "TabItem"                           => "\uE8A9",
        "Slider"                                            => "\uE74A",
        "ProgressBar"                                       => "\uE74A",
        "Expander"                                          => "\uE8B4",
        "GroupBox"                                          => "\uE8B4",
        "Separator"                                         => "\uE8EF",
        "Window" or "UserControl" or "ContentControl"
            or "ContentPresenter"                           => "\uE737",
        "AdornerDecorator" or "AdornerLayer"                => "\uE8A5",
        _                                                   => "\uE8A5"
    };

    private static string ResolveIconColor(string typeName) => typeName switch
    {
        "Grid" or "UniformGrid"                             => "#4FC1FF",
        "StackPanel" or "WrapPanel" or "DockPanel"
            or "VirtualizingStackPanel"                     => "#9CDCFE",
        "Border" or "Decorator" or "Viewbox"                => "#CE9178",
        "Button" or "ToggleButton" or "RepeatButton"
            or "RadioButton" or "CheckBox"                  => "#4EC9B0",
        "TextBlock" or "Label" or "AccessText"              => "#DCDCAA",
        "TextBox" or "RichTextBox" or "PasswordBox"         => "#B5CEA8",
        "Canvas"                                            => "#C586C0",
        "Image"                                             => "#4EC9B0",
        "ListBox" or "ListView" or "DataGrid"               => "#9B9B9B",
        "TreeView"                                          => "#9B9B9B",
        "ScrollViewer" or "ScrollBar"                       => "#9B9B9B",
        "Popup" or "ToolTip"                                => "#CE9178",
        "ComboBox" or "ComboBoxItem"                        => "#9CDCFE",
        "TabControl" or "TabItem"                           => "#9CDCFE",
        "Slider" or "ProgressBar"                           => "#9B9B9B",
        "Expander" or "GroupBox"                            => "#B8D7A3",
        "Window" or "UserControl" or "ContentControl"
            or "ContentPresenter"                           => "#B8D7A3",
        "AdornerDecorator" or "AdornerLayer"                => "#9B9B9B",
        _                                                   => "#9B9B9B"
    };

    /// <summary>Creates a frozen <see cref="SolidColorBrush"/> from a #RRGGBB hex string.</summary>
    private static Brush MakeBrush(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch
        {
            return Brushes.Gray;
        }
    }
}

