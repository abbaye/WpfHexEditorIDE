// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/ViewModels/WhfmtCategoryNodeVm.cs
// Description: TreeView group node for a single format category in the
//              Format Browser panel (e.g. "Archives", "Images").
// Architecture: Pure ViewModel; holds an ObservableCollection of child items.
// ==========================================================

using System.Collections.ObjectModel;
using System.Linq;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Shell.Panels.ViewModels;

/// <summary>
/// Represents a category group in the Format Browser tree.
/// </summary>
public sealed class WhfmtCategoryNodeVm : ViewModelBase
{
    private bool _isExpanded = true;
    private int  _visibleCount;
    private bool _hasFailures;

    public WhfmtCategoryNodeVm(string name)
    {
        Name  = name;
        Items = [];
        Items.CollectionChanged += (_, _) => Recalculate();
    }

    // ------------------------------------------------------------------
    // Properties
    // ------------------------------------------------------------------

    /// <summary>Category name displayed in the tree header.</summary>
    public string Name { get; }

    /// <summary>Child format items (may be filtered).</summary>
    public ObservableCollection<WhfmtFormatItemVm> Items { get; }

    /// <summary>Total visible item count — displayed as a badge on the header.</summary>
    public int VisibleCount
    {
        get => _visibleCount;
        private set => SetField(ref _visibleCount, value);
    }

    /// <summary>True when one or more child items failed to load.</summary>
    public bool HasFailures
    {
        get => _hasFailures;
        private set => SetField(ref _hasFailures, value);
    }

    /// <summary>Controls the expand/collapse state of the TreeViewItem.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    // ------------------------------------------------------------------
    // Public methods
    // ------------------------------------------------------------------

    /// <summary>
    /// Recomputes <see cref="VisibleCount"/> and <see cref="HasFailures"/>
    /// after items are added, removed, or the filter changes.
    /// </summary>
    public void Recalculate()
    {
        VisibleCount = Items.Count;
        HasFailures  = Items.Any(i => i.IsLoadFailure);
    }
}
