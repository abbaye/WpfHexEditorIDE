// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;

namespace WpfHexEditor.Docking.Model;

/// <summary>
/// A tabbed pane that holds LayoutAnchorable items (tool windows).
/// Supports pin/unpin (auto-hide toggle).
/// </summary>
public class LayoutAnchorablePane : LayoutElement
{
    private LayoutAnchorable? _selectedContent;
    private int _selectedContentIndex = -1;
    private GridLength _dockWidth = new(200, GridUnitType.Pixel);
    private GridLength _dockHeight = new(200, GridUnitType.Pixel);
    private DockSide _dockSide = DockSide.None;
    private double _dockMinWidth = 100;
    private double _dockMinHeight = 100;

    public LayoutAnchorablePane()
    {
        Children = new ObservableCollection<LayoutAnchorable>();
        Children.CollectionChanged += OnChildrenChanged;
    }

    /// <summary>Anchorables in this pane (tabs).</summary>
    public ObservableCollection<LayoutAnchorable> Children { get; }

    /// <summary>The currently selected/active anchorable.</summary>
    public LayoutAnchorable? SelectedContent
    {
        get => _selectedContent;
        set
        {
            if (SetProperty(ref _selectedContent, value))
            {
                _selectedContentIndex = value != null ? Children.IndexOf(value) : -1;
                OnPropertyChanged(nameof(SelectedContentIndex));

                foreach (var child in Children)
                    child.IsSelected = child == value;
            }
        }
    }

    /// <summary>Index of the selected anchorable.</summary>
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

    /// <summary>Width in parent Grid.</summary>
    public GridLength DockWidth
    {
        get => _dockWidth;
        set => SetProperty(ref _dockWidth, value);
    }

    /// <summary>Height in parent Grid.</summary>
    public GridLength DockHeight
    {
        get => _dockHeight;
        set => SetProperty(ref _dockHeight, value);
    }

    /// <summary>Which edge this pane is docked to.</summary>
    public DockSide DockSide
    {
        get => _dockSide;
        set => SetProperty(ref _dockSide, value);
    }

    /// <summary>Minimum width constraint.</summary>
    public double DockMinWidth
    {
        get => _dockMinWidth;
        set => SetProperty(ref _dockMinWidth, value);
    }

    /// <summary>Minimum height constraint.</summary>
    public double DockMinHeight
    {
        get => _dockMinHeight;
        set => SetProperty(ref _dockMinHeight, value);
    }

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

        // Auto-select
        if (SelectedContent == null && Children.Count > 0)
            SelectedContent = Children[0];

        if (SelectedContent != null && !Children.Contains(SelectedContent))
            SelectedContent = Children.Count > 0 ? Children[^1] : null;

        // Auto-remove empty pane from parent panel
        if (Children.Count == 0 && Parent is LayoutPanel panel)
        {
            panel.Children.Remove(this);
        }

        OnPropertyChanged(nameof(Children));
    }
}
