// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Collections.ObjectModel;
using WpfHexEditor.Docking.Controls;

namespace WpfHexEditor.Docking.Model;

/// <summary>
/// The root of the layout tree. Contains:
/// - RootPanel: the main split hierarchy
/// - Four anchor sides for auto-hide
/// - Hidden anchorables collection
/// - Back-reference to the hosting DockManager
/// </summary>
public class LayoutRoot : LayoutElement
{
    private LayoutPanel _rootPanel;

    public LayoutRoot()
    {
        _rootPanel = new LayoutPanel(LayoutOrientation.Horizontal);
        _rootPanel.Parent = this;

        LeftSide = new LayoutAnchorSide(DockSide.Left) { Parent = this };
        RightSide = new LayoutAnchorSide(DockSide.Right) { Parent = this };
        TopSide = new LayoutAnchorSide(DockSide.Top) { Parent = this };
        BottomSide = new LayoutAnchorSide(DockSide.Bottom) { Parent = this };

        HiddenAnchorables = new ObservableCollection<LayoutAnchorable>();
    }

    /// <summary>The main layout panel (root of the visible hierarchy).</summary>
    public LayoutPanel RootPanel
    {
        get => _rootPanel;
        set
        {
            if (_rootPanel == value) return;
            if (_rootPanel != null) _rootPanel.Parent = null;
            _rootPanel = value;
            if (_rootPanel != null) _rootPanel.Parent = this;
            OnPropertyChanged();
        }
    }

    /// <summary>Auto-hide area on the left side.</summary>
    public LayoutAnchorSide LeftSide { get; }

    /// <summary>Auto-hide area on the right side.</summary>
    public LayoutAnchorSide RightSide { get; }

    /// <summary>Auto-hide area on the top side.</summary>
    public LayoutAnchorSide TopSide { get; }

    /// <summary>Auto-hide area on the bottom side.</summary>
    public LayoutAnchorSide BottomSide { get; }

    /// <summary>Anchorables that are hidden (closed but not destroyed, can be restored).</summary>
    public ObservableCollection<LayoutAnchorable> HiddenAnchorables { get; }

    /// <summary>Back-reference to the hosting DockManager control.</summary>
    public DockManager? Manager { get; internal set; }

    /// <summary>Get the anchor side for a given dock side.</summary>
    public LayoutAnchorSide GetSide(DockSide side) => side switch
    {
        DockSide.Left => LeftSide,
        DockSide.Right => RightSide,
        DockSide.Top => TopSide,
        DockSide.Bottom => BottomSide,
        _ => LeftSide
    };
}
