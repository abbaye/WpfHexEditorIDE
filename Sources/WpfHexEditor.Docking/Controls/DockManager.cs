// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Docking.Drag;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Controls;

/// <summary>
/// The main host control for the docking framework. Place this in your Window.
/// It renders the entire docking layout from a LayoutRoot model.
///
/// Usage:
///   &lt;docking:DockManager x:Name="DockManager" Layout="{Binding MyLayout}"/&gt;
/// Or:
///   DockManager.Layout = new LayoutRoot { ... };
/// </summary>
[TemplatePart(Name = PART_RootPanel, Type = typeof(ContentPresenter))]
[TemplatePart(Name = PART_LeftAutoHideStrip, Type = typeof(AutoHideTabStrip))]
[TemplatePart(Name = PART_RightAutoHideStrip, Type = typeof(AutoHideTabStrip))]
[TemplatePart(Name = PART_TopAutoHideStrip, Type = typeof(AutoHideTabStrip))]
[TemplatePart(Name = PART_BottomAutoHideStrip, Type = typeof(AutoHideTabStrip))]
[TemplatePart(Name = PART_AutoHidePopup, Type = typeof(AutoHidePopup))]
public class DockManager : Control
{
    private const string PART_RootPanel = "PART_RootPanel";
    private const string PART_LeftAutoHideStrip = "PART_LeftAutoHideStrip";
    private const string PART_RightAutoHideStrip = "PART_RightAutoHideStrip";
    private const string PART_TopAutoHideStrip = "PART_TopAutoHideStrip";
    private const string PART_BottomAutoHideStrip = "PART_BottomAutoHideStrip";
    private const string PART_AutoHidePopup = "PART_AutoHidePopup";

    private ContentPresenter? _rootPanelHost;
    private AutoHideTabStrip? _leftStrip;
    private AutoHideTabStrip? _rightStrip;
    private AutoHideTabStrip? _topStrip;
    private AutoHideTabStrip? _bottomStrip;
    private AutoHidePopup? _autoHidePopup;
    private DockDragManager? _dragManager;

    /// <summary>The drag manager for this DockManager instance.</summary>
    internal DockDragManager DragManager => _dragManager ??= new DockDragManager(this);

    static DockManager()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DockManager),
            new FrameworkPropertyMetadata(typeof(DockManager)));
    }

    public DockManager()
    {
        // Register command bindings for dock operations
        CommandBindings.Add(new System.Windows.Input.CommandBinding(
            DockCommands.SelectDocumentCommand, OnSelectDocument));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(
            DockCommands.CloseDocumentCommand, OnCloseDocument));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(
            DockCommands.SelectAnchorableCommand, OnSelectAnchorable));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(
            DockCommands.CloseAnchorableCommand, OnCloseAnchorable));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(
            DockCommands.ToggleAutoHideCommand, OnToggleAutoHide));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(
            DockCommands.PinAnchorableCommand, OnPinAnchorable));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(
            DockCommands.ShowAutoHidePopupCommand, OnShowAutoHidePopup));
    }

    private void OnSelectDocument(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is LayoutDocument doc && doc.Parent is LayoutDocumentPane pane)
            pane.SelectedContent = doc;
    }

    private void OnCloseDocument(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is LayoutDocument doc)
            doc.Close();
    }

    private void OnSelectAnchorable(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is LayoutAnchorable anch && anch.Parent is LayoutAnchorablePane pane)
            pane.SelectedContent = anch;
    }

    private void OnCloseAnchorable(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is LayoutAnchorable anch)
            anch.Hide();
    }

    private void OnToggleAutoHide(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is LayoutAnchorable anch)
        {
            if (anch.DockState == DockState.Docked)
            {
                anch.ToggleAutoHide();
                RebuildLayoutControls();
            }
            else if (anch.DockState == DockState.AutoHidden)
            {
                // Same as pin
                OnPinAnchorable(sender, e);
            }
        }
    }

    private void OnShowAutoHidePopup(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is LayoutAnchorable anch && anch.DockState == DockState.AutoHidden)
        {
            var side = anch.PreviousDockSide;
            if (side == DockSide.None) side = DockSide.Left;
            ShowAutoHidePopup(anch, side);
        }
    }

    private void OnPinAnchorable(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is LayoutAnchorable anch && anch.DockState == DockState.AutoHidden)
        {
            // Hide the auto-hide popup first
            HideAutoHidePopup();

            // Toggle back to docked (removes from anchor group)
            anch.ToggleAutoHide();

            // Re-dock: create a new pane at the remembered dock side
            ReDockAnchorable(anch);
        }
    }

    /// <summary>
    /// Re-dock an anchorable that was auto-hidden back into the layout tree.
    /// Places it at the previously remembered dock side.
    /// </summary>
    internal void ReDockAnchorable(LayoutAnchorable anchorable)
    {
        if (Layout?.RootPanel == null) return;

        var side = anchorable.PreviousDockSide;
        if (side == DockSide.None) side = DockSide.Left;

        // Try to find an existing anchorable pane on the same side
        var existingPane = FindAnchorablePaneOnSide(Layout.RootPanel, side);
        if (existingPane != null)
        {
            existingPane.Children.Add(anchorable);
            existingPane.SelectedContent = anchorable;
        }
        else
        {
            // Create a new pane and dock at root level
            var newPane = new LayoutAnchorablePane
            {
                DockWidth = new System.Windows.GridLength(250, System.Windows.GridUnitType.Pixel),
                DockHeight = new System.Windows.GridLength(200, System.Windows.GridUnitType.Pixel),
                DockSide = side
            };
            newPane.Children.Add(anchorable);

            var isHorizontal = side is DockSide.Left or DockSide.Right;
            var insertBefore = side is DockSide.Left or DockSide.Top;
            var newOrientation = isHorizontal ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;

            if (Layout.RootPanel.Orientation == newOrientation)
            {
                if (insertBefore)
                    Layout.RootPanel.InsertChildAt(0, newPane);
                else
                    Layout.RootPanel.Children.Add(newPane);
            }
            else
            {
                var newRoot = new LayoutPanel(newOrientation);
                var oldRoot = Layout.RootPanel;
                Layout.RootPanel = newRoot;

                if (insertBefore)
                {
                    newRoot.Children.Add(newPane);
                    newRoot.Children.Add(oldRoot);
                }
                else
                {
                    newRoot.Children.Add(oldRoot);
                    newRoot.Children.Add(newPane);
                }
            }
        }

        RebuildLayoutControls();
    }

    private static LayoutAnchorablePane? FindAnchorablePaneOnSide(LayoutElement element, DockSide side)
    {
        return element switch
        {
            LayoutAnchorablePane pane when pane.DockSide == side => pane,
            LayoutPanel panel => panel.Children
                .Select(c => FindAnchorablePaneOnSide(c, side))
                .FirstOrDefault(p => p != null),
            _ => null
        };
    }

    #region Dependency Properties

    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(nameof(Layout), typeof(LayoutRoot), typeof(DockManager),
            new PropertyMetadata(null, OnLayoutChanged));

    public static readonly DependencyProperty ActiveDocumentProperty =
        DependencyProperty.Register(nameof(ActiveDocument), typeof(LayoutDocument), typeof(DockManager),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ActiveContentProperty =
        DependencyProperty.Register(nameof(ActiveContent), typeof(LayoutContent), typeof(DockManager),
            new PropertyMetadata(null));

    /// <summary>The layout model root. Set this to display a docking layout.</summary>
    public LayoutRoot? Layout
    {
        get => (LayoutRoot?)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <summary>The currently active document.</summary>
    public LayoutDocument? ActiveDocument
    {
        get => (LayoutDocument?)GetValue(ActiveDocumentProperty);
        set => SetValue(ActiveDocumentProperty, value);
    }

    /// <summary>The currently active content (document or anchorable).</summary>
    public LayoutContent? ActiveContent
    {
        get => (LayoutContent?)GetValue(ActiveContentProperty);
        set => SetValue(ActiveContentProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<LayoutDocument>? ActiveDocumentChanged;
    public event EventHandler<LayoutContent>? ActiveContentChanged;

    #endregion

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var manager = (DockManager)d;

        // Detach from old layout
        if (e.OldValue is LayoutRoot oldRoot)
        {
            oldRoot.Manager = null;
            oldRoot.PropertyChanged -= manager.OnLayoutRootPropertyChanged;
        }

        // Attach to new layout
        if (e.NewValue is LayoutRoot newRoot)
        {
            newRoot.Manager = manager;
            newRoot.PropertyChanged += manager.OnLayoutRootPropertyChanged;
        }

        manager.RebuildLayoutControls();
    }

    private void OnLayoutRootPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutRoot.RootPanel))
            RebuildLayoutControls();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _rootPanelHost = GetTemplateChild(PART_RootPanel) as ContentPresenter;
        _leftStrip = GetTemplateChild(PART_LeftAutoHideStrip) as AutoHideTabStrip;
        _rightStrip = GetTemplateChild(PART_RightAutoHideStrip) as AutoHideTabStrip;
        _topStrip = GetTemplateChild(PART_TopAutoHideStrip) as AutoHideTabStrip;
        _bottomStrip = GetTemplateChild(PART_BottomAutoHideStrip) as AutoHideTabStrip;
        _autoHidePopup = GetTemplateChild(PART_AutoHidePopup) as AutoHidePopup;

        // Wire auto-hide strips to model
        if (Layout != null)
        {
            WireAutoHideStrips();
        }

        RebuildLayoutControls();
    }

    /// <summary>
    /// Rebuilds the visual tree from the Layout model.
    /// </summary>
    internal void RebuildLayoutControls()
    {
        if (_rootPanelHost == null) return;

        if (Layout?.RootPanel != null)
        {
            _rootPanelHost.Content = new LayoutPanelControl { Model = Layout.RootPanel };
        }
        else
        {
            _rootPanelHost.Content = null;
        }

        WireAutoHideStrips();
    }

    private void WireAutoHideStrips()
    {
        if (Layout == null) return;

        if (_leftStrip != null) { _leftStrip.Model = Layout.LeftSide; _leftStrip.Side = DockSide.Left; }
        if (_rightStrip != null) { _rightStrip.Model = Layout.RightSide; _rightStrip.Side = DockSide.Right; }
        if (_topStrip != null) { _topStrip.Model = Layout.TopSide; _topStrip.Side = DockSide.Top; }
        if (_bottomStrip != null) { _bottomStrip.Model = Layout.BottomSide; _bottomStrip.Side = DockSide.Bottom; }
    }

    /// <summary>Show the auto-hide popup for a specific anchorable.</summary>
    internal void ShowAutoHidePopup(LayoutAnchorable anchorable, DockSide side)
    {
        _autoHidePopup?.ShowForAnchorable(anchorable, side);
    }

    /// <summary>Hide the auto-hide popup.</summary>
    internal void HideAutoHidePopup()
    {
        _autoHidePopup?.HidePopup();
    }

    /// <summary>
    /// Find the first LayoutDocumentPane in the layout tree.
    /// </summary>
    public LayoutDocumentPane? FindDocumentPane()
    {
        return FindDocumentPaneRecursive(Layout?.RootPanel);
    }

    private static LayoutDocumentPane? FindDocumentPaneRecursive(LayoutElement? element)
    {
        return element switch
        {
            LayoutDocumentPane pane => pane,
            LayoutPanel panel => panel.Children
                .Select(FindDocumentPaneRecursive)
                .FirstOrDefault(p => p != null),
            _ => null
        };
    }
}
