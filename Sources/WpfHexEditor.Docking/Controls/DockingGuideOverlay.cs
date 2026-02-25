// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Controls;

/// <summary>
/// The compass-rose docking guide overlay. Shows 5 dock indicators (Center + 4 directional)
/// over a target pane, and optionally 4 root-level indicators at the DockManager edges.
/// Non-focusable adorner that appears during drag operations.
/// </summary>
internal class DockingGuideOverlay : Window
{
    private const double GuideSize = 40;
    private const double GuideSpacing = 6;
    private const double RootGuideSize = 32;
    private const double HitTestPadding = 6; // Extra pixels around guides for easier hit-testing

    private readonly Canvas _canvas;
    private readonly DockGuideButton _centerGuide;
    private readonly DockGuideButton _leftGuide;
    private readonly DockGuideButton _rightGuide;
    private readonly DockGuideButton _topGuide;
    private readonly DockGuideButton _bottomGuide;

    // Root-level guides (displayed at DockManager edges)
    private readonly DockGuideButton _rootLeftGuide;
    private readonly DockGuideButton _rootRightGuide;
    private readonly DockGuideButton _rootTopGuide;
    private readonly DockGuideButton _rootBottomGuide;

    private DockSide _hoveredSide = DockSide.None;
    private bool _isRootHovered;

    public DockingGuideOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        IsHitTestVisible = true;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        Topmost = true;

        _canvas = new Canvas();
        Content = _canvas;

        // Create compass rose guides (pane-level)
        _centerGuide = CreateGuide(CreateTabIcon(), DockSide.None);
        _leftGuide = CreateGuide(CreateArrowIcon(DockSide.Left), DockSide.Left);
        _rightGuide = CreateGuide(CreateArrowIcon(DockSide.Right), DockSide.Right);
        _topGuide = CreateGuide(CreateArrowIcon(DockSide.Top), DockSide.Top);
        _bottomGuide = CreateGuide(CreateArrowIcon(DockSide.Bottom), DockSide.Bottom);

        _canvas.Children.Add(_centerGuide);
        _canvas.Children.Add(_leftGuide);
        _canvas.Children.Add(_rightGuide);
        _canvas.Children.Add(_topGuide);
        _canvas.Children.Add(_bottomGuide);

        // Create root-level guides
        _rootLeftGuide = CreateRootGuide(CreateArrowIcon(DockSide.Left), DockSide.Left);
        _rootRightGuide = CreateRootGuide(CreateArrowIcon(DockSide.Right), DockSide.Right);
        _rootTopGuide = CreateRootGuide(CreateArrowIcon(DockSide.Top), DockSide.Top);
        _rootBottomGuide = CreateRootGuide(CreateArrowIcon(DockSide.Bottom), DockSide.Bottom);

        _canvas.Children.Add(_rootLeftGuide);
        _canvas.Children.Add(_rootRightGuide);
        _canvas.Children.Add(_rootTopGuide);
        _canvas.Children.Add(_rootBottomGuide);
    }

    /// <summary>The currently hovered dock side from the pane-level compass rose.</summary>
    public DockSide HoveredSide => _hoveredSide;

    /// <summary>Whether a root-level guide is being hovered.</summary>
    public bool IsRootHovered => _isRootHovered;

    /// <summary>
    /// Position the overlay to cover the DockManager area.
    /// The compass rose will be centered on the given pane bounds.
    /// Root guides will be at the manager edges.
    /// </summary>
    public void ShowOverlay(Rect managerScreenBounds, Rect? paneScreenBounds)
    {
        Left = managerScreenBounds.Left;
        Top = managerScreenBounds.Top;
        Width = managerScreenBounds.Width;
        Height = managerScreenBounds.Height;

        // Position root-level guides at manager edges
        PositionRootGuides(managerScreenBounds);

        // Position compass rose at pane center
        if (paneScreenBounds.HasValue)
        {
            ShowCompassRose(paneScreenBounds.Value, managerScreenBounds);
        }
        else
        {
            HideCompassRose();
        }

        if (!IsVisible)
            Show();
    }

    /// <summary>Hide the entire overlay.</summary>
    public void HideOverlay()
    {
        if (IsVisible)
            Hide();
        _hoveredSide = DockSide.None;
        _isRootHovered = false;
    }

    /// <summary>
    /// Hit-test the screen point against the guide buttons.
    /// Returns: side = dock direction, isRoot = root-level guide, hitGuide = whether any guide was hit.
    /// </summary>
    public (DockSide side, bool isRoot, bool hitGuide) HitTestGuides(Point screenPoint)
    {
        var localPoint = new Point(screenPoint.X - Left, screenPoint.Y - Top);

        // Reset previous hover
        ClearHoverStates();

        // Check pane-level compass guides first
        if (HitTestGuide(_centerGuide, localPoint)) { _hoveredSide = DockSide.None; _isRootHovered = false; SetHover(_centerGuide); return (DockSide.None, false, true); }
        if (HitTestGuide(_leftGuide, localPoint)) { _hoveredSide = DockSide.Left; _isRootHovered = false; SetHover(_leftGuide); return (DockSide.Left, false, true); }
        if (HitTestGuide(_rightGuide, localPoint)) { _hoveredSide = DockSide.Right; _isRootHovered = false; SetHover(_rightGuide); return (DockSide.Right, false, true); }
        if (HitTestGuide(_topGuide, localPoint)) { _hoveredSide = DockSide.Top; _isRootHovered = false; SetHover(_topGuide); return (DockSide.Top, false, true); }
        if (HitTestGuide(_bottomGuide, localPoint)) { _hoveredSide = DockSide.Bottom; _isRootHovered = false; SetHover(_bottomGuide); return (DockSide.Bottom, false, true); }

        // Check root-level guides
        if (HitTestGuide(_rootLeftGuide, localPoint)) { _hoveredSide = DockSide.Left; _isRootHovered = true; SetHover(_rootLeftGuide); return (DockSide.Left, true, true); }
        if (HitTestGuide(_rootRightGuide, localPoint)) { _hoveredSide = DockSide.Right; _isRootHovered = true; SetHover(_rootRightGuide); return (DockSide.Right, true, true); }
        if (HitTestGuide(_rootTopGuide, localPoint)) { _hoveredSide = DockSide.Top; _isRootHovered = true; SetHover(_rootTopGuide); return (DockSide.Top, true, true); }
        if (HitTestGuide(_rootBottomGuide, localPoint)) { _hoveredSide = DockSide.Bottom; _isRootHovered = true; SetHover(_rootBottomGuide); return (DockSide.Bottom, true, true); }

        _hoveredSide = DockSide.None;
        _isRootHovered = false;
        return (DockSide.None, false, false);
    }

    /// <summary>
    /// Calculate the preview bounds for a given drop target on a pane.
    /// </summary>
    public static Rect CalculatePreviewBounds(Rect paneBounds, DockSide side, bool isRoot, Rect managerBounds)
    {
        if (isRoot)
        {
            return side switch
            {
                DockSide.Left => new Rect(managerBounds.Left, managerBounds.Top, managerBounds.Width * 0.25, managerBounds.Height),
                DockSide.Right => new Rect(managerBounds.Right - managerBounds.Width * 0.25, managerBounds.Top, managerBounds.Width * 0.25, managerBounds.Height),
                DockSide.Top => new Rect(managerBounds.Left, managerBounds.Top, managerBounds.Width, managerBounds.Height * 0.25),
                DockSide.Bottom => new Rect(managerBounds.Left, managerBounds.Bottom - managerBounds.Height * 0.25, managerBounds.Width, managerBounds.Height * 0.25),
                _ => paneBounds
            };
        }

        return side switch
        {
            DockSide.Left => new Rect(paneBounds.Left, paneBounds.Top, paneBounds.Width * 0.5, paneBounds.Height),
            DockSide.Right => new Rect(paneBounds.Left + paneBounds.Width * 0.5, paneBounds.Top, paneBounds.Width * 0.5, paneBounds.Height),
            DockSide.Top => new Rect(paneBounds.Left, paneBounds.Top, paneBounds.Width, paneBounds.Height * 0.5),
            DockSide.Bottom => new Rect(paneBounds.Left, paneBounds.Top + paneBounds.Height * 0.5, paneBounds.Width, paneBounds.Height * 0.5),
            _ => paneBounds // Center = full pane (tab into)
        };
    }

    #region Private helpers

    private void ShowCompassRose(Rect paneScreenBounds, Rect managerScreenBounds)
    {
        // Convert pane center to local coordinates
        var paneCenterX = paneScreenBounds.Left + paneScreenBounds.Width / 2 - managerScreenBounds.Left;
        var paneCenterY = paneScreenBounds.Top + paneScreenBounds.Height / 2 - managerScreenBounds.Top;

        // Center guide
        Canvas.SetLeft(_centerGuide, paneCenterX - GuideSize / 2);
        Canvas.SetTop(_centerGuide, paneCenterY - GuideSize / 2);
        _centerGuide.Visibility = Visibility.Visible;

        // Left
        Canvas.SetLeft(_leftGuide, paneCenterX - GuideSize / 2 - GuideSize - GuideSpacing);
        Canvas.SetTop(_leftGuide, paneCenterY - GuideSize / 2);
        _leftGuide.Visibility = Visibility.Visible;

        // Right
        Canvas.SetLeft(_rightGuide, paneCenterX + GuideSize / 2 + GuideSpacing);
        Canvas.SetTop(_rightGuide, paneCenterY - GuideSize / 2);
        _rightGuide.Visibility = Visibility.Visible;

        // Top
        Canvas.SetLeft(_topGuide, paneCenterX - GuideSize / 2);
        Canvas.SetTop(_topGuide, paneCenterY - GuideSize / 2 - GuideSize - GuideSpacing);
        _topGuide.Visibility = Visibility.Visible;

        // Bottom
        Canvas.SetLeft(_bottomGuide, paneCenterX - GuideSize / 2);
        Canvas.SetTop(_bottomGuide, paneCenterY + GuideSize / 2 + GuideSpacing);
        _bottomGuide.Visibility = Visibility.Visible;
    }

    private void HideCompassRose()
    {
        _centerGuide.Visibility = Visibility.Collapsed;
        _leftGuide.Visibility = Visibility.Collapsed;
        _rightGuide.Visibility = Visibility.Collapsed;
        _topGuide.Visibility = Visibility.Collapsed;
        _bottomGuide.Visibility = Visibility.Collapsed;
    }

    private void PositionRootGuides(Rect managerScreenBounds)
    {
        var mw = managerScreenBounds.Width;
        var mh = managerScreenBounds.Height;

        // Left edge, centered vertically
        Canvas.SetLeft(_rootLeftGuide, 8);
        Canvas.SetTop(_rootLeftGuide, mh / 2 - RootGuideSize / 2);

        // Right edge, centered vertically
        Canvas.SetLeft(_rootRightGuide, mw - RootGuideSize - 8);
        Canvas.SetTop(_rootRightGuide, mh / 2 - RootGuideSize / 2);

        // Top edge, centered horizontally
        Canvas.SetLeft(_rootTopGuide, mw / 2 - RootGuideSize / 2);
        Canvas.SetTop(_rootTopGuide, 8);

        // Bottom edge, centered horizontally
        Canvas.SetLeft(_rootBottomGuide, mw / 2 - RootGuideSize / 2);
        Canvas.SetTop(_rootBottomGuide, mh - RootGuideSize - 8);
    }

    private static DockGuideButton CreateGuide(Path icon, DockSide side)
    {
        return new DockGuideButton(icon, GuideSize, side);
    }

    private static DockGuideButton CreateRootGuide(Path icon, DockSide side)
    {
        return new DockGuideButton(icon, RootGuideSize, side);
    }

    private static bool HitTestGuide(DockGuideButton guide, Point localPoint)
    {
        if (guide.Visibility != Visibility.Visible)
            return false;

        var left = Canvas.GetLeft(guide) - HitTestPadding;
        var top = Canvas.GetTop(guide) - HitTestPadding;
        var width = guide.Width + HitTestPadding * 2;
        var height = guide.Height + HitTestPadding * 2;
        var bounds = new Rect(left, top, width, height);
        return bounds.Contains(localPoint);
    }

    private void ClearHoverStates()
    {
        _centerGuide.SetHovered(false);
        _leftGuide.SetHovered(false);
        _rightGuide.SetHovered(false);
        _topGuide.SetHovered(false);
        _bottomGuide.SetHovered(false);
        _rootLeftGuide.SetHovered(false);
        _rootRightGuide.SetHovered(false);
        _rootTopGuide.SetHovered(false);
        _rootBottomGuide.SetHovered(false);
    }

    private static void SetHover(DockGuideButton guide)
    {
        guide.SetHovered(true);
    }

    private static Path CreateArrowIcon(DockSide direction)
    {
        var path = new Path
        {
            StrokeThickness = 2.5,
            Stroke = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            Data = direction switch
            {
                DockSide.Left => Geometry.Parse("M 8,0 L 0,6 L 8,12"),
                DockSide.Right => Geometry.Parse("M 0,0 L 8,6 L 0,12"),
                DockSide.Top => Geometry.Parse("M 0,8 L 6,0 L 12,8"),
                DockSide.Bottom => Geometry.Parse("M 0,0 L 6,8 L 12,0"),
                _ => Geometry.Parse("M 0,0 L 12,0 L 12,12 L 0,12 Z")
            }
        };
        return path;
    }

    private static Path CreateTabIcon()
    {
        var path = new Path
        {
            StrokeThickness = 1.5,
            Stroke = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
            Width = 18,
            Height = 14,
            Stretch = Stretch.Uniform,
            // Tab icon: overlapping rectangles
            Data = Geometry.Parse("M 0,4 L 0,12 L 12,12 L 12,4 L 6,4 L 6,0 L 0,0 Z M 6,4 L 12,4")
        };
        return path;
    }

    #endregion
}

/// <summary>
/// A single guide button in the docking overlay compass rose.
/// </summary>
internal class DockGuideButton : Border
{
    private static readonly SolidColorBrush NormalBackground = new(Color.FromArgb(204, 45, 45, 48)); // Dark, 80% opacity
    private static readonly SolidColorBrush HoverBackground = new(Color.FromArgb(204, 0, 122, 204)); // Accent, 80% opacity
    private static readonly SolidColorBrush NormalBorder = new(Color.FromArgb(128, 100, 100, 100));
    private static readonly SolidColorBrush HoverBorder = new(Color.FromArgb(200, 0, 122, 204));

    static DockGuideButton()
    {
        NormalBackground.Freeze();
        HoverBackground.Freeze();
        NormalBorder.Freeze();
        HoverBorder.Freeze();
    }

    public DockSide Side { get; }

    public DockGuideButton(Path icon, double size, DockSide side)
    {
        Side = side;
        Width = size;
        Height = size;
        CornerRadius = new CornerRadius(3);
        Background = NormalBackground;
        BorderBrush = NormalBorder;
        BorderThickness = new Thickness(1);
        Child = icon;
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
    }

    public void SetHovered(bool hovered)
    {
        Background = hovered ? HoverBackground : NormalBackground;
        BorderBrush = hovered ? HoverBorder : NormalBorder;
    }
}
