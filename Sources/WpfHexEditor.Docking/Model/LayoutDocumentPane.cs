// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;

namespace WpfHexEditor.Docking.Model;

/// <summary>
/// A tabbed pane that holds LayoutDocument items (center content like open files).
/// Exactly one document can be selected (active tab).
/// </summary>
public class LayoutDocumentPane : LayoutElement
{
    private LayoutDocument? _selectedContent;
    private int _selectedContentIndex = -1;
    private GridLength _dockWidth = new(1, GridUnitType.Star);
    private GridLength _dockHeight = new(1, GridUnitType.Star);

    public LayoutDocumentPane()
    {
        Children = new ObservableCollection<LayoutDocument>();
        Children.CollectionChanged += OnChildrenChanged;
    }

    /// <summary>Documents in this pane (tabs).</summary>
    public ObservableCollection<LayoutDocument> Children { get; }

    /// <summary>The currently selected/active document.</summary>
    public LayoutDocument? SelectedContent
    {
        get => _selectedContent;
        set
        {
            if (SetProperty(ref _selectedContent, value))
            {
                _selectedContentIndex = value != null ? Children.IndexOf(value) : -1;
                OnPropertyChanged(nameof(SelectedContentIndex));

                // Update IsSelected on all children
                foreach (var child in Children)
                    child.IsSelected = child == value;
            }
        }
    }

    /// <summary>Index of the selected document.</summary>
    public int SelectedContentIndex
    {
        get => _selectedContentIndex;
        set
        {
            if (value >= 0 && value < Children.Count)
                SelectedContent = Children[value];
            else
                SelectedContent = null;
        }
    }

    /// <summary>Width proportion in parent Grid.</summary>
    public GridLength DockWidth
    {
        get => _dockWidth;
        set => SetProperty(ref _dockWidth, value);
    }

    /// <summary>Height proportion in parent Grid.</summary>
    public GridLength DockHeight
    {
        get => _dockHeight;
        set => SetProperty(ref _dockHeight, value);
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Set parent on added items
        if (e.NewItems != null)
        {
            foreach (LayoutDocument item in e.NewItems)
                item.Parent = this;
        }

        // Clear parent on removed items
        if (e.OldItems != null)
        {
            foreach (LayoutDocument item in e.OldItems)
            {
                if (item.Parent == this)
                    item.Parent = null;
            }
        }

        // Auto-select first if nothing is selected
        if (SelectedContent == null && Children.Count > 0)
            SelectedContent = Children[0];

        // If selected was removed, select another
        if (SelectedContent != null && !Children.Contains(SelectedContent))
            SelectedContent = Children.Count > 0 ? Children[^1] : null;

        OnPropertyChanged(nameof(Children));
    }
}
