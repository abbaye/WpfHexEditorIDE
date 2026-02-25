// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Threading;

namespace WpfHexEditor.Docking.Model;

/// <summary>
/// A split container that arranges its children horizontally or vertically.
/// Children can be other LayoutPanels (nested splits), LayoutDocumentPane, or LayoutAnchorablePane.
/// Grid + GridSplitter layout with proportional sizing.
/// </summary>
public class LayoutPanel : LayoutElement
{
    private LayoutOrientation _orientation = LayoutOrientation.Horizontal;

    public LayoutPanel()
    {
        Children = new ObservableCollection<LayoutElement>();
        Children.CollectionChanged += OnChildrenChanged;
    }

    public LayoutPanel(LayoutOrientation orientation) : this()
    {
        _orientation = orientation;
    }

    /// <summary>Direction in which children are arranged.</summary>
    public LayoutOrientation Orientation
    {
        get => _orientation;
        set => SetProperty(ref _orientation, value);
    }

    /// <summary>
    /// Child elements. Allowed types: LayoutPanel, LayoutDocumentPane, LayoutAnchorablePane.
    /// </summary>
    public ObservableCollection<LayoutElement> Children { get; }

    /// <summary>Insert a child at a specific index.</summary>
    public void InsertChildAt(int index, LayoutElement child)
    {
        Children.Insert(index, child);
    }

    /// <summary>Remove a child element.</summary>
    public void RemoveChild(LayoutElement child)
    {
        Children.Remove(child);
    }

    /// <summary>Replace one child with another.</summary>
    public void ReplaceChild(LayoutElement oldChild, LayoutElement newChild)
    {
        var index = Children.IndexOf(oldChild);
        if (index >= 0)
        {
            Children[index] = newChild;
        }
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Set parent on added items
        if (e.NewItems != null)
        {
            foreach (LayoutElement item in e.NewItems)
                item.Parent = this;
        }

        // Clear parent on removed items
        if (e.OldItems != null)
        {
            foreach (LayoutElement item in e.OldItems)
            {
                if (item.Parent == this)
                    item.Parent = null;
            }
        }

        OnPropertyChanged(nameof(Children));

        // Defer auto-cleanup to avoid modifying the collection during its own event
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, AutoCleanup);
    }

    private void AutoCleanup()
    {
        // Promote single child: if this panel has only one child, replace self with that child
        if (Children.Count == 1 && Parent is LayoutPanel parentPanel)
        {
            var onlyChild = Children[0];
            Children.Clear();
            parentPanel.ReplaceChild(this, onlyChild);
        }
        // Remove empty panel from parent
        else if (Children.Count == 0 && Parent is LayoutPanel pp)
        {
            pp.Children.Remove(this);
        }
    }
}
