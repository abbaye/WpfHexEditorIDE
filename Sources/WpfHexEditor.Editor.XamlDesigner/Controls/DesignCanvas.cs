// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignCanvas.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Updated: 2026-03-17 — Phase 1: UID injection, XamlElementMapper, ResizeAdorner,
//                        DesignInteractionService wiring, element-to-XElement mapping.
//                        Phase 3: ZoomPanCanvas host integration.
//          2026-03-18 — Phase E2: Page boundary shadow + border drawn via OnRender override.
// Description:
//     Live WPF rendering surface for the XAML designer.
//     Parses XAML via XamlReader.Parse() and presents the result
//     in a ContentPresenter + AdornerLayer.
//     When InteractionEnabled=true, wraps selection with ResizeAdorner
//     and delegates move/resize events to DesignInteractionService.
//
// Architecture Notes:
//     Inherits Border. Contains a ContentPresenter + AdornerLayer.
//     XamlReader.Parse() runs on the UI thread inside a try/catch.
//     UID injection: DesignToXamlSyncService.InjectUids() tags every element
//       with Tag="xd_N" before parsing, then XamlElementMapper reads them
//       back from the rendered tree to build UIElement→XElement mapping.
// ==========================================================

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Design surface that renders live XAML inside a sandboxed WPF content host.
/// </summary>
public sealed class DesignCanvas : Border
{
    // ── Child controls ────────────────────────────────────────────────────────

    private readonly ContentPresenter _presenter;

    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty XamlSourceProperty =
        DependencyProperty.Register(
            nameof(XamlSource),
            typeof(string),
            typeof(DesignCanvas),
            new FrameworkPropertyMetadata(string.Empty, OnXamlSourceChanged));

    // ── Interaction ───────────────────────────────────────────────────────────

    private DesignInteractionService? _interaction;
    private readonly XamlElementMapper _mapper = new();
    private readonly DesignToXamlSyncService _syncService = new();

    // Alt+Click cycling state — tracks the last hit list and current depth.
    private int               _altClickDepth    = 0;
    private List<UIElement>   _lastHitElements  = new();

    /// <summary>
    /// When true, selection uses ResizeAdorner instead of SelectionAdorner
    /// and wires DesignInteractionService for drag-move/resize.
    /// Set by XamlDesignerSplitHost once it has wired the interaction service.
    /// </summary>
    public bool InteractionEnabled { get; private set; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public DesignCanvas()
    {
        SetResourceReference(BackgroundProperty, "XD_CanvasBackground");
        SetResourceReference(BorderBrushProperty, "XD_CanvasBorderBrush");
        BorderThickness = new Thickness(1);

        // Explicit natural size so ZoomPanCanvas formulas (ClampOffsets, FitToContent,
        // zoom-toward-mouse) see the real design dimensions rather than the viewport size.
        // Updated in RenderXaml() to match the root element's declared Width/Height.
        Width  = 1280;
        Height = 720;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment   = VerticalAlignment.Top;

        _presenter = new ContentPresenter
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Top,
            Margin              = new Thickness(8)
        };

        Child = _presenter;

        PreviewMouseLeftButtonDown += OnCanvasMouseDown;

        // Escape key deselects the current element.
        Focusable = true;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                SelectElement(null);
                e.Handled = true;
            }
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>XAML text to render. Triggers a re-render on change.</summary>
    public string XamlSource
    {
        get => (string)GetValue(XamlSourceProperty);
        set => SetValue(XamlSourceProperty, value);
    }

    /// <summary>The last successfully rendered root UIElement.</summary>
    public UIElement? DesignRoot { get; private set; }

    /// <summary>The currently selected element.</summary>
    public UIElement? SelectedElement { get; private set; }

    /// <summary>XElement in the source document corresponding to the selected element.</summary>
    public System.Xml.Linq.XElement? SelectedXElement { get; private set; }

    /// <summary>UID of the selected element (-1 if none).</summary>
    public int SelectedElementUid { get; private set; } = -1;

    /// <summary>Fired after each render attempt. Null = success, non-null = error message.</summary>
    public event EventHandler<string?>? RenderError;

    /// <summary>Fired when the selected element changes.</summary>
    public event EventHandler? SelectedElementChanged;

    /// <summary>
    /// Wires the DesignInteractionService and enables interactive adorners.
    /// </summary>
    public void EnableInteraction(DesignInteractionService service)
    {
        _interaction     = service;
        InteractionEnabled = true;
    }

    /// <summary>Programmatically selects an element and places the adorner.</summary>
    public void SelectElement(UIElement? el)
    {
        RemoveSelectionAdorner();

        SelectedElement    = el;
        SelectedXElement   = el is not null ? _mapper.GetXElement(el) : null;
        SelectedElementUid = el is not null ? _mapper.GetUid(el) : -1;

        if (el is not null)
            PlaceSelectionAdorner(el);

        SelectedElementChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private static void OnXamlSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DesignCanvas)d).RenderXaml((string)e.NewValue);

    private void RenderXaml(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
        {
            _presenter.Content = null;
            DesignRoot         = null;
            SelectElement(null);
            RenderError?.Invoke(this, null);
            return;
        }

        try
        {
            var sanitized = SanitizeForPreview(xaml);

            // Inject UIDs so the element mapper can link UIElements → XElements.
            var withUids  = _syncService.InjectUids(sanitized, out var uidMap);
            var prepared  = EnsureWpfNamespaces(withUids);
            var result    = XamlReader.Parse(prepared);

            if (result is UIElement uiResult)
            {
                _presenter.Content = uiResult;
                DesignRoot         = uiResult;

                // Resize canvas to match root element's declared dimensions so that
                // ZoomPanCanvas always sees a natural (non-viewport-dependent) size.
                // +18 accounts for: 2×BorderThickness(1px) + 2×ContentPresenter.Margin(8px)
                if (uiResult is FrameworkElement rootFe)
                {
                    const double Extra = 18.0;
                    if (!double.IsNaN(rootFe.Width)  && rootFe.Width  > 0) Width  = rootFe.Width  + Extra;
                    if (!double.IsNaN(rootFe.Height) && rootFe.Height > 0) Height = rootFe.Height + Extra;
                }

                SelectElement(null);

                // Build the UIElement → XElement map after the element is in the tree.
                Dispatcher.InvokeAsync(() =>
                {
                    _mapper.Build(uidMap, uiResult);
                }, System.Windows.Threading.DispatcherPriority.Loaded);

                RenderError?.Invoke(this, null);
            }
            else
            {
                _presenter.Content = new TextBlock
                {
                    Text       = $"[{result?.GetType().Name ?? "null"} — non-visual root]",
                    Foreground = Brushes.Gray,
                    Margin     = new Thickness(4)
                };
                DesignRoot = null;
                RenderError?.Invoke(this, null);
            }
        }
        catch (Exception ex)
        {
            DesignRoot         = null;
            _presenter.Content = BuildRenderErrorCard(ex.Message);
            RenderError?.Invoke(this, ex.Message);
        }
    }

    /// <summary>
    /// Builds a centered error card displayed on the design surface when XAML
    /// parsing or rendering fails. Never throws — inner exceptions are swallowed
    /// to prevent cascading failures from crashing the IDE.
    /// </summary>
    private static UIElement BuildRenderErrorCard(string message)
    {
        try
        {
            var icon = new TextBlock
            {
                Text                = "\uE783",   // Segoe MDL2: Error circle
                FontFamily          = new FontFamily("Segoe MDL2 Assets"),
                FontSize            = 32,
                Foreground          = new SolidColorBrush(Color.FromRgb(0xF4, 0x85, 0x57)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 8)
            };

            var title = new TextBlock
            {
                Text                = "XAML Parse Error",
                FontSize            = 14,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = new SolidColorBrush(Color.FromRgb(0xF4, 0x85, 0x57)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 6)
            };

            var detail = new TextBlock
            {
                Text                = message,
                FontSize            = 11,
                Foreground          = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                TextWrapping        = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                MaxWidth            = 480
            };

            var stack = new StackPanel
            {
                Orientation         = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            stack.Children.Add(icon);
            stack.Children.Add(title);
            stack.Children.Add(detail);

            return new Border
            {
                Background          = new SolidColorBrush(Color.FromArgb(0xCC, 0x1E, 0x1E, 0x1E)),
                BorderBrush         = new SolidColorBrush(Color.FromRgb(0xF4, 0x85, 0x57)),
                BorderThickness     = new Thickness(1),
                CornerRadius        = new CornerRadius(6),
                Padding             = new Thickness(24, 20, 24, 20),
                MaxWidth            = 560,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Child               = stack
            };
        }
        catch
        {
            // Last-resort fallback — never return null.
            return new TextBlock
            {
                Text       = $"[Render error] {message}",
                Foreground = Brushes.OrangeRed,
                Margin     = new Thickness(12)
            };
        }
    }

    // ── Adorner management ────────────────────────────────────────────────────

    private void RemoveSelectionAdorner()
    {
        if (SelectedElement is null) return;

        var layer = AdornerLayer.GetAdornerLayer(SelectedElement);
        if (layer is null) return;

        var adorners = layer.GetAdorners(SelectedElement);
        if (adorners is null) return;

        foreach (var a in adorners)
        {
            if (a is SelectionAdorner or ResizeAdorner)
                layer.Remove(a);
        }
    }

    private void PlaceSelectionAdorner(UIElement el)
    {
        var layer = AdornerLayer.GetAdornerLayer(el);
        if (layer is null) return;

        if (InteractionEnabled && _interaction is not null)
        {
            int uid = _mapper.GetUid(el);
            layer.Add(new ResizeAdorner(el, _interaction, uid));
        }
        else
        {
            layer.Add(new SelectionAdorner(el));
        }
    }

    // ── Mouse selection ───────────────────────────────────────────────────────

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DesignRoot is null) return;

        // Don't reselect when clicking on resize/move handles.
        if (e.OriginalSource is System.Windows.Controls.Primitives.Thumb) return;

        var clickPoint = e.GetPosition(_presenter);
        bool isAlt     = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

        if (!isAlt)
        {
            // Fresh click — rebuild the full hit list (z-order: topmost first).
            _lastHitElements.Clear();
            _altClickDepth = 0;
            VisualTreeHelper.HitTest(
                _presenter,
                d => d is Adorner
                    ? HitTestFilterBehavior.ContinueSkipSelfAndChildren
                    : HitTestFilterBehavior.Continue,
                r =>
                {
                    if (r.VisualHit is UIElement u && !ReferenceEquals(u, _presenter))
                        _lastHitElements.Add(u);
                    return HitTestResultBehavior.Continue;
                },
                new PointHitTestParameters(clickPoint));

            // Prefer a leaf element (not a container with children); fall back to topmost.
            var target = _lastHitElements.FirstOrDefault(IsLeafElement)
                      ?? _lastHitElements.FirstOrDefault();
            SelectElement(target);
        }
        else
        {
            // Alt+Click — cycle to the next element in the existing hit list.
            if (_lastHitElements.Count == 0)
            {
                VisualTreeHelper.HitTest(
                    _presenter,
                    d => d is Adorner
                        ? HitTestFilterBehavior.ContinueSkipSelfAndChildren
                        : HitTestFilterBehavior.Continue,
                    r =>
                    {
                        if (r.VisualHit is UIElement u && !ReferenceEquals(u, _presenter))
                            _lastHitElements.Add(u);
                        return HitTestResultBehavior.Continue;
                    },
                    new PointHitTestParameters(clickPoint));
            }

            if (_lastHitElements.Count > 0)
            {
                _altClickDepth = (_altClickDepth + 1) % _lastHitElements.Count;
                SelectElement(_lastHitElements[_altClickDepth]);
            }
        }

        e.Handled = false;
    }

    /// <summary>
    /// Returns true for visual leaf elements that are meaningful design targets.
    /// Container panels and decorator elements with children are excluded so that
    /// the topmost rendered control is preferred over its invisible host panel.
    /// </summary>
    private static bool IsLeafElement(DependencyObject obj)
    {
        if (obj is Panel p && p.Children.Count > 0) return false;
        if (obj is ContentControl cc && cc.Content is UIElement) return false;
        if (obj is Decorator d && d.Child is not null) return false;
        return obj is UIElement;
    }

    // ── XAML preprocessing ────────────────────────────────────────────────────

    private static readonly string[] WindowOnlyAttributes =
    [
        "Title", "Icon", "WindowStyle", "WindowStartupLocation", "WindowState",
        "ResizeMode", "ShowInTaskbar", "Topmost", "AllowsTransparency",
        "SizeToContent", "ShowActivated",
        "Closed", "Closing", "Activated", "Deactivated",
        "StateChanged", "LocationChanged", "ContentRendered", "SourceInitialized"
    ];

    private static string SanitizeForPreview(string xaml)
    {
        xaml = Regex.Replace(xaml, @"\s+x:(Class|Subclass|FieldModifier)=""[^""]*""", string.Empty);

        xaml = Regex.Replace(
            xaml,
            @"\s+\w+=""(On[A-Za-z][A-Za-z0-9_]*|[A-Za-z][A-Za-z0-9]*_[A-Za-z0-9_]+)""",
            string.Empty);

        xaml = Regex.Replace(
            xaml,
            @"<WindowChrome\.\w+>[\s\S]*?</WindowChrome\.\w+>",
            string.Empty,
            RegexOptions.Singleline);

        xaml = Regex.Replace(
            xaml,
            @"<TaskbarItemInfo\.\w+>[\s\S]*?</TaskbarItemInfo\.\w+>",
            string.Empty,
            RegexOptions.Singleline);

        xaml = ReplaceWindowRoot(xaml);

        return xaml;
    }

    private static string ReplaceWindowRoot(string xaml)
    {
        var openTag = Regex.Match(xaml, @"<Window(\s[^>]*)?>", RegexOptions.Singleline);
        if (!openTag.Success) return xaml;

        var attrs = openTag.Groups[1].Value;

        foreach (var attr in WindowOnlyAttributes)
            attrs = Regex.Replace(attrs, $@"\s+{attr}=""[^""]*""", string.Empty);

        var newOpen = $"<Border{attrs}>";
        xaml = xaml[..openTag.Index] + newOpen + xaml[(openTag.Index + openTag.Length)..];
        xaml = xaml.Replace("</Window>", "</Border>");

        return xaml;
    }

    private static string EnsureWpfNamespaces(string xaml)
    {
        const string wpfNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        const string xNs   = "http://schemas.microsoft.com/winfx/2006/xaml";

        if (xaml.Contains(wpfNs) && xaml.Contains(xNs))
            return xaml;

        int tagStart = xaml.IndexOf('<');
        if (tagStart < 0) return xaml;

        int insertPos = FindAttributeInsertPosition(xaml, tagStart);
        if (insertPos < 0) return xaml;

        string injection = string.Empty;

        if (!xaml.Contains(wpfNs))
            injection += $" xmlns=\"{wpfNs}\"";

        if (!xaml.Contains(xNs))
            injection += $" xmlns:x=\"{xNs}\"";

        return xaml.Insert(insertPos, injection);
    }

    private static int FindAttributeInsertPosition(string xaml, int tagStart)
    {
        int i = tagStart + 1;
        while (i < xaml.Length && !char.IsWhiteSpace(xaml[i]) && xaml[i] != '>' && xaml[i] != '/')
            i++;
        return i < xaml.Length ? i : -1;
    }

    // ── Phase E2 — Page boundary rendering ────────────────────────────────────

    /// <summary>
    /// Draws a drop-shadow and a 1-pixel accent border around the rendered design root
    /// to indicate the page / form boundary on the design canvas.
    /// </summary>
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        DrawPageBoundary(dc);
    }

    /// <summary>
    /// Renders a soft shadow offset and a subtle 1px border around the design root's
    /// bounding rectangle. Called every layout cycle via <see cref="OnRender"/>.
    /// </summary>
    private void DrawPageBoundary(DrawingContext dc)
    {
        if (DesignRoot is not FrameworkElement root) return;

        var pos  = root.TranslatePoint(new Point(0, 0), this);
        var rect = new Rect(pos, new Size(root.ActualWidth, root.ActualHeight));

        // Semi-transparent shadow (3px offset, 40-alpha black).
        var shadowBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        shadowBrush.Freeze();
        dc.DrawRectangle(shadowBrush, null,
            new Rect(rect.X + 3, rect.Y + 3, rect.Width, rect.Height));

        // 1px page-boundary accent border.
        var pen = new Pen(SystemColors.ControlDarkBrush, 1.0);
        pen.Freeze();
        dc.DrawRectangle(null, pen, rect);
    }
}
