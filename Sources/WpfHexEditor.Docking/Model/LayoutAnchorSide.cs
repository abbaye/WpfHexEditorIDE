// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace WpfHexEditor.Docking.Model;

/// <summary>
/// One side (Left/Right/Top/Bottom) of the auto-hide area.
/// Contains groups of auto-hidden anchorables.
/// </summary>
public class LayoutAnchorSide : LayoutElement
{
    private DockSide _side;

    public LayoutAnchorSide(DockSide side)
    {
        _side = side;
        Children = new ObservableCollection<LayoutAnchorGroup>();
        Children.CollectionChanged += OnChildrenChanged;
    }

    /// <summary>Which side of the dock manager this represents.</summary>
    public DockSide Side
    {
        get => _side;
        set => SetProperty(ref _side, value);
    }

    /// <summary>Groups of auto-hidden anchorables on this side.</summary>
    public ObservableCollection<LayoutAnchorGroup> Children { get; }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (LayoutAnchorGroup item in e.NewItems)
                item.Parent = this;
        }

        if (e.OldItems != null)
        {
            foreach (LayoutAnchorGroup item in e.OldItems)
            {
                if (item.Parent == this)
                    item.Parent = null;
            }
        }

        OnPropertyChanged(nameof(Children));
    }
}
