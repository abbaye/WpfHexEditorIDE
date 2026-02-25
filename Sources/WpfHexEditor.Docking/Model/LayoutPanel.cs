// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace WpfHexEditor.Docking.Model;

/// <summary>
/// A split container that arranges its children horizontally or vertically.
/// Children can be other LayoutPanels (nested splits), LayoutDocumentPane, or LayoutAnchorablePane.
/// Grid + GridSplitter layout with proportional sizing.
/// </summary>
public class LayoutPanel : LayoutElement
{
    private LayoutOrientation _orientation = LayoutOrientation.Horizontal;
    private bool _isProcessingCollectionChange;

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
        if (_isProcessingCollectionChange) return;
        _isProcessingCollectionChange = true;

        try
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

            // Auto-cleanup: if this panel has only one child, promote it
            if (Children.Count == 1 && Parent is LayoutPanel parentPanel)
            {
                var onlyChild = Children[0];
                Children.Clear();
                parentPanel.ReplaceChild(this, onlyChild);
            }
            // If this panel is now empty, remove from parent
            else if (Children.Count == 0 && Parent is LayoutPanel pp)
            {
                pp.Children.Remove(this);
            }

            OnPropertyChanged(nameof(Children));
        }
        finally
        {
            _isProcessingCollectionChange = false;
        }
    }
}
