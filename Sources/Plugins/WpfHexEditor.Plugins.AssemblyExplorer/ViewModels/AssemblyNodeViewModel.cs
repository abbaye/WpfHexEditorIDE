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
}
