// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace WpfHexEditor.Docking.Model;

/// <summary>
/// A group of auto-hidden anchorables on one side.
/// Maps to one cluster of tabs on an auto-hide strip.
/// </summary>
public class LayoutAnchorGroup : LayoutElement
{
    public LayoutAnchorGroup()
    {
        Children = new ObservableCollection<LayoutAnchorable>();
        Children.CollectionChanged += OnChildrenChanged;
    }

    /// <summary>Auto-hidden anchorables in this group.</summary>
    public ObservableCollection<LayoutAnchorable> Children { get; }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (LayoutAnchorable item in e.NewItems)
                item.Parent = this;
        }

        if (e.OldItems != null)
        {
            foreach (LayoutAnchorable item in e.OldItems)
            {
                if (item.Parent == this)
                    item.Parent = null;
            }
        }

        // Auto-remove empty group
        if (Children.Count == 0 && Parent is LayoutAnchorSide side)
        {
            side.Children.Remove(this);
        }

        OnPropertyChanged(nameof(Children));
    }
}
