// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

namespace WpfHexEditor.Docking.Model;

/// <summary>
/// Represents a single tool window (e.g., Parsed Fields, Data Inspector).
/// Can be docked, auto-hidden, floated, or hidden.
/// </summary>
public class LayoutAnchorable : LayoutContent
{
    private bool _canAutoHide = true;
    private bool _canFloat = true;
    private bool _canDockAsTabbedDocument;
    private DockState _dockState = DockState.Docked;
    private double _autoHideWidth = 300;
    private double _autoHideHeight = 200;
    private double _floatingWidth = 400;
    private double _floatingHeight = 300;
    private double _floatingLeft = double.NaN;
    private double _floatingTop = double.NaN;
    private DockSide _previousDockSide = DockSide.None;

    /// <summary>Whether the auto-hide pin toggle is available.</summary>
    public bool CanAutoHide
    {
        get => _canAutoHide;
        set => SetProperty(ref _canAutoHide, value);
    }

    /// <summary>Whether the float option is available.</summary>
    public bool CanFloat
    {
        get => _canFloat;
        set => SetProperty(ref _canFloat, value);
    }

    /// <summary>Whether this anchorable can be placed in the document pane area.</summary>
    public bool CanDockAsTabbedDocument
    {
        get => _canDockAsTabbedDocument;
        set => SetProperty(ref _canDockAsTabbedDocument, value);
    }

    /// <summary>Current visibility/docking state.</summary>
    public DockState DockState
    {
        get => _dockState;
        set => SetProperty(ref _dockState, value);
    }

    /// <summary>Remembered width when auto-hidden on left/right side.</summary>
    public double AutoHideWidth
    {
        get => _autoHideWidth;
        set => SetProperty(ref _autoHideWidth, value);
    }

    /// <summary>Remembered height when auto-hidden on top/bottom side.</summary>
    public double AutoHideHeight
    {
        get => _autoHideHeight;
        set => SetProperty(ref _autoHideHeight, value);
    }

    /// <summary>Width when floating.</summary>
    public double FloatingWidth
    {
        get => _floatingWidth;
        set => SetProperty(ref _floatingWidth, value);
    }

    /// <summary>Height when floating.</summary>
    public double FloatingHeight
    {
        get => _floatingHeight;
        set => SetProperty(ref _floatingHeight, value);
    }

    /// <summary>Left position when floating.</summary>
    public double FloatingLeft
    {
        get => _floatingLeft;
        set => SetProperty(ref _floatingLeft, value);
    }

    /// <summary>Top position when floating.</summary>
    public double FloatingTop
    {
        get => _floatingTop;
        set => SetProperty(ref _floatingTop, value);
    }

    /// <summary>Remembered dock side before auto-hide (used for re-docking).</summary>
    public DockSide PreviousDockSide
    {
        get => _previousDockSide;
        set => SetProperty(ref _previousDockSide, value);
    }

    /// <summary>Make this anchorable visible (DockState = Docked).</summary>
    public void Show()
    {
        if (DockState == DockState.Hidden)
        {
            Root?.HiddenAnchorables.Remove(this);
        }
        DockState = DockState.Docked;
    }

    /// <summary>Hide this anchorable completely (DockState = Hidden).</summary>
    public void Hide()
    {
        var root = Root; // Capture before RemoveFromParent sets Parent to null
        RemoveFromParent();
        DockState = DockState.Hidden;
        root?.HiddenAnchorables.Add(this);
    }

    /// <summary>Toggle between Docked and AutoHidden states.</summary>
    public void ToggleAutoHide()
    {
        if (!CanAutoHide) return;

        if (DockState == DockState.Docked)
        {
            // Remember dock side before auto-hiding
            if (Parent is LayoutAnchorablePane pane)
                PreviousDockSide = pane.DockSide;

            var root = Root; // Capture before RemoveFromParent sets Parent to null
            RemoveFromParent();
            DockState = DockState.AutoHidden;

            // Add to appropriate anchor side
            if (root != null)
            {
                var side = PreviousDockSide switch
                {
                    DockSide.Left => root.LeftSide,
                    DockSide.Right => root.RightSide,
                    DockSide.Top => root.TopSide,
                    DockSide.Bottom => root.BottomSide,
                    _ => root.LeftSide
                };

                var group = side.Children.FirstOrDefault();
                if (group == null)
                {
                    group = new LayoutAnchorGroup();
                    side.Children.Add(group);
                }
                group.Children.Add(this);
            }
        }
        else if (DockState == DockState.AutoHidden)
        {
            RemoveFromParent();
            DockState = DockState.Docked;
            // Re-docking logic is handled by DockManager
        }
    }
}
