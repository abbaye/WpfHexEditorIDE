// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyNodeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Abstract base class for all TreeView node ViewModels in the
//     Assembly Explorer panel. Provides common state, children collection,
//     and INotifyPropertyChanged via CallerMemberName helper.
//
// Architecture Notes:
//     Pattern: MVVM — abstract base for Composite tree nodes.
//     Children is an ObservableCollection so the WPF TreeView can
//     react to lazy-loading insertions in future phases.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Abstract base for all Assembly Explorer tree node ViewModels.
/// Concrete subclasses must supply <see cref="DisplayName"/> and <see cref="IconGlyph"/>.
/// </summary>
public abstract class AssemblyNodeViewModel : INotifyPropertyChanged
{
    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Text shown in the TreeView item label.</summary>
    public abstract string DisplayName { get; }

    /// <summary>Segoe MDL2 Assets Unicode codepoint for the node icon.</summary>
    public abstract string IconGlyph { get; }

    /// <summary>
    /// Semantic color brush for the node icon. Each concrete node type returns
    /// a frozen SolidColorBrush matching its category (VS Code C# palette).
    /// </summary>
    public virtual Brush IconBrush => _defaultBrush;

    /// <summary>
    /// True when the member or type has public visibility.
    /// Grouping nodes (Namespace, Assembly, Reference, Resource, Metadata) are always true.
    /// </summary>
    public virtual bool IsPublic => true;

    // Frozen default brush (neutral gray) — used by grouping/structural nodes.
    private static readonly Brush _defaultBrush = MakeBrush("#9B9B9B");

    /// <summary>Creates and freezes a <see cref="SolidColorBrush"/> from a hex color string.</summary>
    protected static Brush MakeBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    /// <summary>Tooltip text; defaults to <see cref="DisplayName"/>.</summary>
    public virtual string ToolTipText => DisplayName;

    // ── PE sync ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Raw PE file byte offset of the metadata row, or 0 if not resolved.
    /// When 0, HexEditor sync is skipped for this node.
    /// </summary>
    public long PeOffset { get; protected init; }

    /// <summary>ECMA-335 metadata token, or 0 for virtual/grouping nodes.</summary>
    public int MetadataToken { get; protected init; }

    /// <summary>
    /// Byte length of the member body (IL header + code bytes) for method nodes.
    /// 0 for types, grouping nodes, and abstract/extern methods with no body.
    /// Populated after tree construction via <see cref="AssemblyExplorerViewModel"/>.
    /// Used to highlight the exact byte range in the hex editor.
    /// </summary>
    public int ByteLength { get; internal set; }

    /// <summary>
    /// Absolute file path of the assembly this node belongs to.
    /// Set by AssemblyExplorerViewModel after tree construction via PropagateOwnerFilePath.
    /// Allows the detail pane and hex editor integration to resolve the correct file
    /// when multiple assemblies are loaded simultaneously.
    /// </summary>
    public string? OwnerFilePath { get; internal set; }

    // ── Tree state ────────────────────────────────────────────────────────────

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>
    /// True while children are being loaded asynchronously.
    /// Shown as a spinner node placeholder in future lazy-load phases.
    /// </summary>
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    /// <summary>Child nodes. Populated eagerly in Phase 1; lazily in future phases.</summary>
    public ObservableCollection<AssemblyNodeViewModel> Children { get; } = [];

    // ── Filter state ──────────────────────────────────────────────────────────

    /// <summary>
    /// Controls TreeViewItem visibility during an active filter.
    /// False when the filter is active and neither this node nor any descendant
    /// matches the filter text. Defaults to true (always visible with no filter).
    /// </summary>
    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    /// <summary>
    /// True when the filter text directly matches this node's <see cref="DisplayName"/>.
    /// Used for potential highlight rendering in future phases.
    /// </summary>
    private bool _isMatch;
    public bool IsMatch
    {
        get => _isMatch;
        set => SetField(ref _isMatch, value);
    }

    /// <summary>
    /// True when this node was selected via reverse Hex → Tree navigation
    /// (i.e. the user moved the hex editor caret to the byte range of this member).
    /// The tree view uses this for a distinct highlight color (ASM-02-A).
    /// </summary>
    private bool _isReverseHighlighted;
    public bool IsReverseHighlighted
    {
        get => _isReverseHighlighted;
        set => SetField(ref _isReverseHighlighted, value);
    }

    // ── Lazy loading (ASM-02-D) ───────────────────────────────────────────────

    /// <summary>
    /// True while this node is loading its children asynchronously.
    /// A spinner DataTrigger in the TreeView uses this flag.
    /// </summary>
    private bool _isLoadingChildren;
    public bool IsLoadingChildren
    {
        get => _isLoadingChildren;
        set => SetField(ref _isLoadingChildren, value);
    }

    /// <summary>
    /// True when a dummy child placeholder has been inserted so the expand arrow
    /// is visible before the real children are loaded (virtual tree pattern).
    /// </summary>
    public bool HasDummyChild =>
        Children.Count == 1 && Children[0] is DummyChildNode;

    /// <summary>
    /// Inserts a <see cref="DummyChildNode"/> placeholder so the TreeView shows
    /// an expand arrow. Call <see cref="RemoveDummyChild"/> before inserting real children.
    /// </summary>
    protected void InsertDummyChild()
    {
        if (!HasDummyChild)
            Children.Add(new DummyChildNode());
    }

    /// <summary>Removes the dummy placeholder inserted by <see cref="InsertDummyChild"/>.</summary>
    protected void RemoveDummyChild()
    {
        if (HasDummyChild)
            Children.RemoveAt(0);
    }
}
