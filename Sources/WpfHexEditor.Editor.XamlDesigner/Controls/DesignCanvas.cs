// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignCanvas.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Updated: 2026-03-17 — Phase 1: UID injection, XamlElementMapper, ResizeAdorner,
//                        DesignInteractionService wiring, element-to-XElement mapping.
//                        Phase 3: ZoomPanCanvas host integration.
//          2026-03-18 — Phase E2: Page boundary shadow + border drawn via OnRender override.
//                        Phase EL: RenderError upgraded to XamlRenderError? (line/col extracted).
//                        Phase MS: Multi-selection — rubber-band marquee, Ctrl+Click toggle,
//                                  SelectElements/ToggleElementInSelection API, MultiSelectionAdorner,
//                                  RubberBandAdorner, GetUidOf helper for alignment service wiring.
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
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

    // Hover adorner state — the element currently highlighted under the cursor.
    private UIElement? _hoveredElement;

    // ── Multi-selection ────────────────────────────────────────────────────────

    // Canonical ordered list of all currently selected elements (0 = none, 1 = single, 2+ = multi).
    private readonly List<UIElement> _selectedElements = new();

    // Combined-bounds adorner placed on DesignRoot when 2+ elements are selected.
    private MultiSelectionAdorner? _multiAdorner;

    // Rubber-band marquee adorner shown while the user drag-selects on empty canvas space.
    private RubberBandAdorner? _rubberBandAdorner;
    private bool               _isRubberBanding;
    private Point              _rubberBandStart;   // in DesignRoot coordinate space

    // ── Grid guide adorner ─────────────────────────────────────────────────────

    private GridGuideAdorner?      _gridAdorner;
    private UIElement?             _gridAdornedElement;
    private readonly GridDefinitionService _gridService = new();

    // ── Grid insert adorner ────────────────────────────────────────────────────

    private GridInsertAdorner?                    _insertAdorner;
    private System.Windows.Controls.Grid?         _insertAdornerGrid;
    // Set to true by SelectElement so the spurious MouseLeave fired by WPF when
    // a new adorner (ResizeAdorner) appears over the canvas is ignored once.
    private bool                                  _suppressNextMouseLeave;

    /// <summary>
    /// When true, selection uses ResizeAdorner instead of SelectionAdorner
    /// and wires DesignInteractionService for drag-move/resize.
    /// Set by XamlDesignerSplitHost once it has wired the interaction service.
    /// </summary>
    public bool InteractionEnabled { get; private set; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public DesignCanvas()
    {
        // Transparent background: lets the IDE panel background show through instead of
        // the dark XD_CanvasBackground, which was visually covering transparent XAML roots
        // (Grid, UserControl, StackPanel) and making them appear "hidden behind" a dark layer.
        Background = Brushes.Transparent;
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

        // Force the Arrow cursor at all times so child controls rendered inside the
        // design surface (TextBox, RichTextBox, etc.) cannot propagate their own
        // cursor (I-beam caret) up to the canvas level.
        Cursor      = Cursors.Arrow;
        ForceCursor = true;

        PreviewMouseLeftButtonDown += OnCanvasMouseDown;
        PreviewMouseLeftButtonUp   += OnCanvasMouseUp;
        PreviewMouseMove += OnCanvasMouseMove;   // Preview = tunnel, not intercepted by adorners/children
        MouseLeave       += OnCanvasMouseLeave;
        MouseEnter       += OnCanvasMouseEnter;  // restore guide when mouse re-enters after adorner flicker

        // Escape key:
        //   • 2+ elements selected → narrow to primary (first in list).
        //   • 1 element selected   → walk up to nearest selectable parent (deselects at root).
        //   • nothing selected     → no-op.
        Focusable = true;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (_selectedElements.Count > 1)
                    SelectElement(SelectedElement);   // narrow multi-selection → primary
                else
                    SelectElement(SelectedElement is not null
                        ? FindSelectableParent(SelectedElement)  // null when at root → deselects
                        : null);
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

    /// <summary>
    /// Fired after each render attempt.
    /// <c>null</c> = success; non-null = structured error with message, line, and column.
    /// The line/column are 1-based source coordinates extracted from
    /// <see cref="System.Xml.XmlException"/> / <see cref="System.Windows.Markup.XamlParseException"/>.
    /// </summary>
    public event EventHandler<XamlRenderError?>? RenderError;

    /// <summary>The file path used to populate <see cref="XamlRenderError.FilePath"/>.</summary>
    public string? SourceFilePath { get; set; }

    /// <summary>Fired when the selected element changes.</summary>
    public event EventHandler? SelectedElementChanged;

    /// <summary>
    /// Raised after a XAML render completes and <see cref="DesignRoot"/> is stable
    /// (fires inside <c>DispatcherPriority.Loaded</c>, after the UIElement mapper builds).
    /// Subscribe here — not to <c>XamlChanged</c> — to safely walk the visual tree.
    /// </summary>
    public event EventHandler<UIElement?>? DesignRendered;

    /// <summary>
    /// Wires the DesignInteractionService and enables interactive adorners.
    /// </summary>
    public void EnableInteraction(DesignInteractionService service)
    {
        _interaction     = service;
        InteractionEnabled = true;
    }

    /// <summary>
    /// Selects the nearest selectable UIElement ancestor of the current selection.
    /// Selecting null deselects entirely when the element has no parent within the canvas.
    /// </summary>
    public void SelectParent()
        => SelectElement(SelectedElement is not null ? FindSelectableParent(SelectedElement) : null);

    /// <summary>
    /// Walks the visual tree upward from <paramref name="current"/> and returns the first
    /// UIElement ancestor that is still within the <see cref="_presenter"/> boundary.
    /// Returns null when <paramref name="current"/> is already the root child of the presenter.
    /// </summary>
    internal UIElement? FindSelectableParent(UIElement current)
    {
        var node = VisualTreeHelper.GetParent(current);
        while (node is not null && !ReferenceEquals(node, _presenter))
        {
            if (node is UIElement u) return u;
            node = VisualTreeHelper.GetParent(node);
        }
        return null; // reached presenter boundary → caller should deselect
    }

    /// <summary>
    /// Selects the element whose injected Tag matches <c>xd_<paramref name="uid"/></c>.
    /// Used by the bidirectional code↔canvas sync to drive selection from the code editor.
    /// Does nothing when the canvas has no content or the UID is not found.
    /// </summary>
    /// <param name="suppressEvent">Forwarded to <see cref="SelectElement"/>.</param>
    public void SelectElementByUid(int uid, bool suppressEvent = false)
    {
        if (uid < 0 || _presenter.Content is not UIElement root) return;
        var target = FindElementWithUid(root, uid);
        if (target is not null) SelectElement(target, suppressEvent);
    }

    /// <summary>
    /// Recursively walks the visual tree looking for a <see cref="FrameworkElement"/>
    /// whose Tag equals <c>xd_<paramref name="uid"/></c>.
    /// </summary>
    private static UIElement? FindElementWithUid(UIElement root, int uid)
    {
        var tag = $"xd_{uid}";
        if (root is FrameworkElement fe && fe.Tag is string t && t == tag) return root;

        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is UIElement child)
            {
                var found = FindElementWithUid(child, uid);
                if (found is not null) return found;
            }
        }

        return null;
    }

    /// <summary>Programmatically selects an element and places the adorner.</summary>
    /// <param name="suppressEvent">
    /// When <c>true</c>, <see cref="SelectedElementChanged"/> is NOT raised.
    /// Use for programmatic/restore selections (e.g. after re-render) to avoid
    /// triggering the canvas→code caret navigation on the user's behalf.
    /// </param>
    public void SelectElement(UIElement? el, bool suppressEvent = false)
    {
        RemoveHoverAdorner();
        ClearAllSelectionAdorners();

        _selectedElements.Clear();
        SelectedElement    = el;
        SelectedXElement   = el is not null ? _mapper.GetXElement(el) : null;
        SelectedElementUid = el is not null ? _mapper.GetUid(el) : -1;

        if (el is not null)
        {
            _selectedElements.Add(el);
            PlaceSelectionAdorner(el);
        }

        // Resolve the nearest Grid ancestor (the element itself or the first Grid parent).
        // Used by both the GridGuideAdorner (column/row chips) and GridInsertAdorner (insert guide).
        var nearestGrid = FindNearestGridAncestor(el);

        // Grid guide adorner: place when a Grid is selected (directly or via ancestor).
        UpdateGridGuideAdorner(nearestGrid);

        // Grid insert adorner: keep alive on the same Grid after selection changes.
        // Update _insertAdornerGrid to the new instance (after re-render, objects are replaced).
        // If the new selection resolves to a different Grid (by UID), hide and recreate.
        if (nearestGrid is null)
        {
            HideGridInsertAdorner();
        }
        else if (_insertAdornerGrid is not null && !ReferenceEquals(_insertAdornerGrid, nearestGrid))
        {
            // Same Grid by UID? → retarget adorner to new instance without hiding.
            var oldUid = _mapper.GetUid(_insertAdornerGrid);
            var newUid = _mapper.GetUid(nearestGrid);
            if (oldUid >= 0 && oldUid == newUid)
                _insertAdornerGrid = nearestGrid; // retarget: same logical grid, new WPF object
            else
                HideGridInsertAdorner();
        }

        // WPF fires a spurious MouseLeave on the canvas after PlaceSelectionAdorner adds the
        // ResizeAdorner to the AdornerLayer — even though the mouse is still inside the canvas.
        // Suppress that single MouseLeave so the insert guide is not immediately destroyed.
        if (nearestGrid is not null)
            _suppressNextMouseLeave = true;

        // Immediately refresh the insert guide at the current mouse position so it
        // appears right after selection without waiting for the next MouseMove event.
        if (nearestGrid is not null)
        {
            var mouseNow = Mouse.GetPosition(this);
            UpdateGridInsertAdorner(HitTestElement(Mouse.GetPosition(_presenter)), mouseNow);
        }

        if (!suppressEvent)
            SelectedElementChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Returns <paramref name="el"/> itself if it is a <see cref="System.Windows.Controls.Grid"/>,
    /// otherwise walks up the visual tree and returns the first Grid ancestor found within
    /// the <see cref="_presenter"/> boundary. Returns <see langword="null"/> when none is found.
    /// </summary>
    private System.Windows.Controls.Grid? FindNearestGridAncestor(UIElement? el)
    {
        var node = el as DependencyObject;
        while (node is not null && !ReferenceEquals(node, _presenter))
        {
            if (node is System.Windows.Controls.Grid g) return g;
            if (node is not Visual and not System.Windows.Media.Media3D.Visual3D) break;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    /// <summary>All currently selected elements (0 = none, 1 = single, 2+ = multi).</summary>
    public IReadOnlyList<UIElement> SelectedElements => _selectedElements;

    /// <summary>
    /// Returns the UID assigned to <paramref name="el"/> by the element mapper after the last
    /// successful render, or -1 when the element is not mapped.
    /// </summary>
    public int GetUidOf(UIElement el) => _mapper.GetUid(el);

    /// <summary>
    /// Replaces the current selection with <paramref name="elements"/>, placing per-element
    /// adorners and a combined <see cref="MultiSelectionAdorner"/> when 2+ are selected.
    /// </summary>
    public void SelectElements(IEnumerable<UIElement> elements, bool suppressEvent = false)
    {
        RemoveHoverAdorner();
        ClearAllSelectionAdorners();

        _selectedElements.Clear();
        _selectedElements.AddRange(elements.Where(e => e is not null).Distinct());

        var primary        = _selectedElements.Count > 0 ? _selectedElements[0] : null;
        SelectedElement    = primary;
        SelectedXElement   = primary is not null ? _mapper.GetXElement(primary) : null;
        SelectedElementUid = primary is not null ? _mapper.GetUid(primary) : -1;

        if (_selectedElements.Count == 1)
        {
            PlaceSelectionAdorner(_selectedElements[0]);
            UpdateGridGuideAdorner(_selectedElements[0] as System.Windows.Controls.Grid);
        }
        else if (_selectedElements.Count > 1)
            PlaceMultiSelectionAdorners();

        if (!suppressEvent)
            SelectedElementChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Adds or removes <paramref name="el"/> from the selection (Ctrl+Click toggle).
    /// Rebuilds adorners for the resulting set.
    /// </summary>
    public void ToggleElementInSelection(UIElement el, bool suppressEvent = false)
    {
        if (_selectedElements.Contains(el))
            _selectedElements.Remove(el);
        else
            _selectedElements.Add(el);

        RebuildSelectionAdorners();

        var primary        = _selectedElements.Count > 0 ? _selectedElements[0] : null;
        SelectedElement    = primary;
        SelectedXElement   = primary is not null ? _mapper.GetXElement(primary) : null;
        SelectedElementUid = primary is not null ? _mapper.GetUid(primary) : -1;

        if (!suppressEvent)
            SelectedElementChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Grid guide events (forwarded from GridGuideAdorner) ───────────────────

    /// <summary>Fired when the user drags a grid boundary grip to resize a column/row.</summary>
    public event EventHandler<GridGuideResizedEventArgs>?     GridGuideResized;

    /// <summary>Fired when the user clicks "+" to add a column or row to the selected Grid.</summary>
    public event EventHandler<GridGuideAddedEventArgs>?       GridGuideAdded;

    /// <summary>Fired when the user clicks "×" on a handle chip to remove a column/row.</summary>
    public event EventHandler<GridGuideRemovedEventArgs>?     GridGuideRemoved;

    /// <summary>Fired when the user selects a new size type via the chip dropdown.</summary>
    public event EventHandler<GridGuideTypeChangedEventArgs>? GridGuideTypeChanged;

    /// <summary>
    /// Refreshes the grid guide adorner from the current live Grid layout.
    /// Called by XamlDesignerSplitHost after every successful re-render.
    /// </summary>
    public void RefreshGridGuide()
    {
        if (_gridAdorner is null || _gridAdornedElement is not System.Windows.Controls.Grid g) return;
        var info = _gridService.GetGridInfo(g);
        _gridAdorner.Refresh(info.Columns, info.Rows);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private static void OnXamlSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Wrap the call so no exception ever escapes a DependencyProperty callback.
        // An unhandled exception here would propagate into the WPF property system and
        // crash the IDE instead of displaying the error card on the design surface.
        try   { ((DesignCanvas)d).RenderXaml((string)e.NewValue); }
        catch { /* RenderXaml has its own catch; this is a last-resort safety net. */ }
    }

    private void RenderXaml(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
        {
            // Clear adorners BEFORE removing content so AdornerLayer is still reachable.
            SelectElement(null);
            _presenter.Content = null;
            DesignRoot         = null;
            RenderError?.Invoke(this, null);
            return;
        }

        try
        {
            var sanitized = SanitizeForPreview(xaml);

            // Inject UIDs so the element mapper can link UIElements → XElements.
            var withUids  = _syncService.InjectUids(sanitized, out var uidMap);
            var prepared  = EnsureWpfNamespaces(withUids);

            // Pre-validate XML syntax before calling XamlReader.Parse().
            // XmlException is caught inside TryValidateXml — it never propagates,
            // so the VS debugger has no first-chance exception to pause on.
            var (xmlOk, xmlDiag) = TryValidateXmlStructured(prepared);
            if (!xmlOk)
            {
                SelectElement(null, suppressEvent: true);
                DesignRoot         = null;
                _presenter.Content = null;
                RenderError?.Invoke(this, xmlDiag with { FilePath = SourceFilePath });
                return;
            }

            var result    = ParseXaml(prepared);

            if (result is UIElement uiResult)
            {
                // Capture previous selection UID before clearing adorners.
                var prevUid = SelectedElementUid;

                // Clear adorners WHILE the OLD _presenter.Content is still connected to the
                // AdornerDecorator visual tree.  If called after _presenter.Content = uiResult,
                // SelectedElement is disconnected → AdornerLayer.GetAdornerLayer returns null →
                // SelectionAdorner / ResizeAdorner stays in the layer as an orphan, and its Thumbs
                // block all subsequent mouse events (hover, selection, zoom stability).
                SelectElement(null, suppressEvent: true);   // silent: no caret movement

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

                // Build the UIElement → XElement map after the element is in the tree,
                // then attempt to restore the previously selected element by UID.
                // suppressEvent: true — the restore is programmatic; we must NOT fire
                // SelectedElementChanged here or the canvas→code sync would steal the
                // code editor's caret while the user is typing.
                Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        _mapper.Build(uidMap, uiResult);
                        if (prevUid >= 0)
                            SelectElementByUid(prevUid, suppressEvent: true);

                        // Restore insert guide at current mouse position after re-render.
                        // Mouse.GetPosition is safe on the UI thread; IsMouseOver guards
                        // against refreshing when the mouse is outside the canvas.
                        if (IsMouseOver)
                        {
                            var mp = Mouse.GetPosition(_presenter);
                            UpdateGridInsertAdorner(HitTestElement(mp), mp);
                        }

                        DesignRendered?.Invoke(this, DesignRoot);
                    }
                    catch (Exception ex)
                    {
                        SelectElement(null, suppressEvent: true);
                        DesignRoot         = null;
                        _presenter.Content = null;
                        RenderError?.Invoke(this, ParseLineCol(ex.Message) with { FilePath = SourceFilePath });
                    }
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
            SelectElement(null, suppressEvent: true);
            DesignRoot         = null;
            _presenter.Content = null;
            RenderError?.Invoke(this, ParseLineCol(ex.Message) with { FilePath = SourceFilePath });
        }
    }

    /// <summary>
    /// Invokes <see cref="XamlReader.Parse"/> in a method marked <see cref="DebuggerHiddenAttribute"/>
    /// so that the Visual Studio debugger does not pause on the first-chance
    /// <see cref="System.Windows.Markup.XamlParseException"/> thrown by invalid XAML.
    /// The exception propagates normally and is caught by <see cref="RenderXaml"/>.
    /// </summary>
    [DebuggerHidden]
    private static object ParseXaml(string xaml) => XamlReader.Parse(xaml);

    /// <summary>
    /// Validates that <paramref name="xml"/> is well-formed XML without throwing.
    /// <see cref="XmlException"/> is caught internally so the VS debugger never sees a
    /// first-chance exception from XML-level syntax errors (invalid comments, encoding, etc.).
    /// </summary>
    [DebuggerNonUserCode]
    private static (bool Ok, XamlRenderError? Error) TryValidateXmlStructured(string xml)
    {
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read()) { }
            return (true, null);
        }
        catch (XmlException ex)
        {
            return (false, new XamlRenderError(ex.Message, ex.LineNumber, ex.LinePosition));
        }
    }

    /// <summary>
    /// Parses the line and column numbers embedded in a XAML/XML exception message
    /// (format: "… Line N, position M.") and wraps them in a <see cref="XamlRenderError"/>.
    /// Falls back to -1 when the pattern is not found.
    /// </summary>
    private static XamlRenderError ParseLineCol(string message)
    {
        // XamlParseException: "… 'foo' Line 26, position 17."
        // XmlException uses the same wording.
        var m = Regex.Match(message,
            @"[Ll]ine\s+(\d+)[,\s]+[Pp]os(?:ition)?\s+(\d+)",
            RegexOptions.CultureInvariant);

        if (m.Success
            && int.TryParse(m.Groups[1].Value, out int line)
            && int.TryParse(m.Groups[2].Value, out int col))
        {
            return new XamlRenderError(message, line, col);
        }

        return new XamlRenderError(message);
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

            var card = new Border
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

            // Wrap in a Grid that fills the ZoomPanCanvas viewport so the
            // HorizontalAlignment/VerticalAlignment = Center on the card
            // actually centres it in the visible area, not just in its DesiredSize.
            return new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch,
                Width               = 9999,
                Height              = 9999,
                Children            = { card }
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

    /// <summary>Removes adorners from all currently tracked selected elements and the combined overlay.</summary>
    private void ClearAllSelectionAdorners()
    {
        // Deduplicate: SelectedElement may already be in _selectedElements.
        var toClean = new HashSet<UIElement>();
        if (SelectedElement is not null) toClean.Add(SelectedElement);
        foreach (var el in _selectedElements) toClean.Add(el);
        foreach (var el in toClean) RemoveElementAdorners(el);
        RemoveMultiAdorner();

        // Remove the grid guide adorner whenever selection adorners are rebuilt
        // (handles deselect, multi-select, and XAML re-render restores).
        RemoveGridGuideAdorner();

        // Remove the insert guide (stale after selection / re-render; reappears on next mouse move).
        HideGridInsertAdorner();
    }

    /// <summary>Rebuilds adorners from the current <see cref="_selectedElements"/> list without changing the list.</summary>
    private void RebuildSelectionAdorners()
    {
        ClearAllSelectionAdorners();
        if (_selectedElements.Count == 1)
            PlaceSelectionAdorner(_selectedElements[0]);
        else if (_selectedElements.Count > 1)
            PlaceMultiSelectionAdorners();
    }

    private static void RemoveElementAdorners(UIElement el)
    {
        var layer    = AdornerLayer.GetAdornerLayer(el);
        if (layer is null) return;
        var adorners = layer.GetAdorners(el);
        if (adorners is null) return;
        foreach (var a in adorners)
            if (a is SelectionAdorner or ResizeAdorner)
                layer.Remove(a);
    }

    private void RemoveMultiAdorner()
    {
        if (_multiAdorner is null || DesignRoot is null) return;
        var layer = AdornerLayer.GetAdornerLayer(DesignRoot);
        layer?.Remove(_multiAdorner);
        _multiAdorner = null;
    }

    /// <summary>Places a <see cref="SelectionAdorner"/> on every element and a combined <see cref="MultiSelectionAdorner"/> on DesignRoot.</summary>
    private void PlaceMultiSelectionAdorners()
    {
        foreach (var el in _selectedElements)
        {
            var layer = AdornerLayer.GetAdornerLayer(el);
            layer?.Add(new SelectionAdorner(el));
        }

        if (DesignRoot is not null)
        {
            var rootLayer = AdornerLayer.GetAdornerLayer(DesignRoot);
            if (rootLayer is not null)
            {
                _multiAdorner = new MultiSelectionAdorner(DesignRoot);
                _multiAdorner.Refresh(_selectedElements);
                rootLayer.Add(_multiAdorner);
            }
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

    // ── Grid guide adorner management ─────────────────────────────────────────

    /// <summary>
    /// Places a <see cref="GridGuideAdorner"/> on <paramref name="grid"/> (or removes it
    /// when <paramref name="grid"/> is null, i.e. the selected element is not a Grid).
    /// </summary>
    private void UpdateGridGuideAdorner(System.Windows.Controls.Grid? grid)
    {
        // Remove existing adorner if it belongs to a different element.
        if (_gridAdorner is not null)
        {
            if (ReferenceEquals(_gridAdornedElement, grid)) return; // same Grid, no change
            RemoveGridGuideAdorner();
        }

        if (grid is null) return;

        var layer = AdornerLayer.GetAdornerLayer(grid);
        if (layer is null) return;

        _gridAdorner          = new GridGuideAdorner(grid);
        _gridAdornedElement   = grid;
        _gridAdorner.GuideResized     += (_, e) => GridGuideResized?.Invoke(this, e);
        _gridAdorner.GuideAdded       += (_, e) => GridGuideAdded?.Invoke(this, e);
        _gridAdorner.GuideRemoved     += (_, e) => GridGuideRemoved?.Invoke(this, e);
        _gridAdorner.GuideTypeChanged += (_, e) => GridGuideTypeChanged?.Invoke(this, e);

        var info = _gridService.GetGridInfo(grid);
        _gridAdorner.Refresh(info.Columns, info.Rows);
        layer.Add(_gridAdorner);
    }

    private void RemoveGridGuideAdorner()
    {
        if (_gridAdorner is null) return;
        if (_gridAdornedElement is not null)
        {
            var layer = AdornerLayer.GetAdornerLayer(_gridAdornedElement);
            layer?.Remove(_gridAdorner);
        }
        _gridAdorner        = null;
        _gridAdornedElement = null;
    }

    // ── Grid insert adorner management ─────────────────────────────────

    /// <summary>
    /// Creates or updates the <see cref="GridInsertAdorner"/> over the nearest Grid
    /// ancestor of <paramref name="hitElement"/>. Hides the adorner when no Grid
    /// is found under the cursor.
    /// </summary>
    private void UpdateGridInsertAdorner(UIElement? hitElement, Point mouseInPresenter)
    {
        // Resolve which Grid the mouse is over.
        var grid = FindNearestGrid(hitElement) ?? FindGridByBounds(mouseInPresenter);

        // If no grid found by hit-test but we already have an active adorner,
        // keep using that grid as long as the mouse is still inside its bounds.
        if (grid is null && _insertAdornerGrid is not null)
        {
            try
            {
                var localPt = TranslatePoint(mouseInPresenter, _insertAdornerGrid);
                var bounds  = new Rect(0, 0, _insertAdornerGrid.ActualWidth, _insertAdornerGrid.ActualHeight);
                if (bounds.Contains(localPt))
                    grid = _insertAdornerGrid;
            }
            catch { }
        }

        // Guide is only shown when the grid belongs to the active selection context.
        if (grid is null || !IsGridActiveForInsert(grid))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[InsertAdorner] HIDE — grid={grid?.GetHashCode()}, active={grid is not null && IsGridActiveForInsert(grid)}, selected={SelectedElement?.GetType().Name}({SelectedElement?.GetHashCode()})");
            HideGridInsertAdorner();
            return;
        }

        // Create adorner when we switch to a different grid.
        if (!ReferenceEquals(_insertAdornerGrid, grid))
        {
            HideGridInsertAdorner();
            var layer = AdornerLayer.GetAdornerLayer(grid);
            if (layer is null) return;
            _insertAdorner     = new GridInsertAdorner(grid);
            _insertAdornerGrid = grid;
            layer.Add(_insertAdorner);
        }

        var localPos = PresenterToGridLocal(mouseInPresenter, grid);
        var info     = _gridService.GetGridInfo(grid);

        System.Diagnostics.Debug.WriteLine(
            $"[InsertAdorner] UPDATE — localPos={localPos:F1}, grid={grid.ActualWidth:F0}x{grid.ActualHeight:F0}");

        // Auto-determine mode from edge band proximity:
        //   Left / right 18px  →  Row    (H guide follows mouse Y)
        //   Top  / bottom 18px →  Column (V guide follows mouse X)
        //   Corner / interior  →  keep current mode
        const double EdgeBand = 18.0;
        bool nearLR = localPos.X <= EdgeBand || localPos.X >= grid.ActualWidth  - EdgeBand;
        bool nearTB = localPos.Y <= EdgeBand || localPos.Y >= grid.ActualHeight - EdgeBand;

        var mode = _insertAdorner!.Mode;
        if      (nearLR && !nearTB) mode = GridInsertAdorner.InsertMode.Row;
        else if (nearTB && !nearLR) mode = GridInsertAdorner.InsertMode.Column;

        // Safety flip when the grid only has one definition type.
        if (mode == GridInsertAdorner.InsertMode.Row    && info.Rows.Count    == 0 && info.Columns.Count > 0)
            mode = GridInsertAdorner.InsertMode.Column;
        if (mode == GridInsertAdorner.InsertMode.Column && info.Columns.Count == 0 && info.Rows.Count    > 0)
            mode = GridInsertAdorner.InsertMode.Row;

        // Guide always follows the mouse.
        double linePos;
        int    insertAfter;
        if (mode == GridInsertAdorner.InsertMode.Row)
        {
            linePos     = Math.Clamp(localPos.Y, 0, grid.ActualHeight);
            insertAfter = ComputeInsertAfter(info.Rows, localPos.Y);
        }
        else
        {
            linePos     = Math.Clamp(localPos.X, 0, grid.ActualWidth);
            insertAfter = ComputeInsertAfter(info.Columns, localPos.X);
        }

        _insertAdorner.Update(linePos, mode, insertAfter);
    }

    private void HideGridInsertAdorner()
    {
        if (_insertAdorner is null) return;
        if (_insertAdornerGrid is not null)
        {
            var layer = AdornerLayer.GetAdornerLayer(_insertAdornerGrid);
            layer?.Remove(_insertAdorner);
        }
        _insertAdorner     = null;
        _insertAdornerGrid = null;
    }

    /// <summary>
    /// Handles a click that may have landed on the insert guide line or its toggle button.
    /// Returns <see langword="true"/> when the click was consumed (caller should return).
    /// </summary>
    private bool HandleGridInsertClick(MouseButtonEventArgs e)
    {
        if (_insertAdorner is null || _insertAdornerGrid is null || !_insertAdorner.IsVisible)
            return false;

        var localPos = PresenterToGridLocal(e.GetPosition(_presenter), _insertAdornerGrid);

        // Only the toggle button triggers insertion.
        // Clicking anywhere else on the grid uses the normal selection path.
        if (!_insertAdorner.ToggleBounds.Contains(localPos)) return false;

        // Capture values BEFORE SelectElement, which calls ClearAllSelectionAdorners
        // → HideGridInsertAdorner → nulls _insertAdorner.
        var adorner     = _insertAdorner;
        var grid        = _insertAdornerGrid;
        bool isColumn    = adorner.Mode        == GridInsertAdorner.InsertMode.Column;
        int  insertAfter = adorner.InsertAfter;

        if (!ReferenceEquals(SelectedElement, grid))
            SelectElement(grid); // may null _insertAdorner

        GridGuideAdded?.Invoke(this, new GridGuideAddedEventArgs
        {
            IsColumn    = isColumn,
            InsertAfter = insertAfter,
            Definition  = "*"
        });
        e.Handled = true;
        return true;
    }

    private System.Windows.Controls.Grid? FindNearestGrid(UIElement? element)
    {
        var node = element as DependencyObject;
        while (node is not null)
        {
            if (node is System.Windows.Controls.Grid g) return g;
            if (ReferenceEquals(node, _presenter)) break; // don't escape the design surface
            if (node is not Visual and not System.Windows.Media.Media3D.Visual3D) break;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    /// <summary>
    /// Walks the visual tree of <see cref="DesignRoot"/> and returns the innermost
    /// <see cref="System.Windows.Controls.Grid"/> whose rendered bounds (in
    /// <see cref="_presenter"/> coordinates) contain <paramref name="mouseInPresenter"/>.
    /// Used as a fallback when WPF hit-testing misses Grids with null/transparent
    /// Background (empty cells, edge bands with no child elements).
    /// </summary>
    private System.Windows.Controls.Grid? FindGridByBounds(Point mouseInCanvas)
    {
        if (DesignRoot is not UIElement root) return null;
        return FindGridByBoundsCore(root, mouseInCanvas);
    }

    private System.Windows.Controls.Grid? FindGridByBoundsCore(UIElement el, Point mouseInCanvas)
    {
        // Depth-first, reverse z-order so the innermost / topmost Grid wins.
        int n = VisualTreeHelper.GetChildrenCount(el);
        for (int i = n - 1; i >= 0; i--)
        {
            if (VisualTreeHelper.GetChild(el, i) is UIElement child)
            {
                var found = FindGridByBoundsCore(child, mouseInCanvas);
                if (found is not null) return found;
            }
        }

        if (el is not System.Windows.Controls.Grid g) return null;

        try
        {
            // mouseInCanvas is in DesignCanvas space; convert to Grid-local to check containment.
            var localPt = TranslatePoint(mouseInCanvas, g);
            var bounds  = new Rect(0, 0, g.ActualWidth, g.ActualHeight);
            return bounds.Contains(localPt) ? g : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Converts a point in <see cref="DesignCanvas"/> (this) coordinate space to Grid-local space.
    /// Uses <see cref="UIElement.TranslatePoint"/> which is fully transform-aware.
    /// </summary>
    private Point PresenterToGridLocal(Point canvasPt, UIElement grid)
    {
        try
        {
            return TranslatePoint(canvasPt, grid);
        }
        catch
        {
            return canvasPt; // element detached from tree
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="grid"/> should show the insert guide:
    ///   • the Grid itself is the selected element, OR
    ///   • the selected element is a visual descendant of the Grid (child selected inside it).
    /// This covers the case where <see cref="HitTestElement"/> returns a leaf child instead
    /// of the Grid, making a plain <see cref="ReferenceEquals"/> against SelectedElement fail.
    /// </summary>
    private bool IsGridActiveForInsert(System.Windows.Controls.Grid grid)
    {
        // Fast path: same instance.
        if (ReferenceEquals(FindNearestGridAncestor(SelectedElement), grid)) return true;

        // After a re-render WPF creates new object instances for the whole visual tree.
        // SelectedElement and grid are new objects; _insertAdornerGrid is the old one.
        // Compare by UID (injected Tag) so the guide survives re-renders.
        var selectedGrid = FindNearestGridAncestor(SelectedElement);
        if (selectedGrid is null) return false;

        var uidA = _mapper.GetUid(selectedGrid);
        var uidB = _mapper.GetUid(grid);
        return uidA >= 0 && uidA == uidB;
    }

    /// <summary>
    /// Returns the 0-based index after which a new definition should be inserted
    /// so that it appears at <paramref name="pos"/> pixels from the grid's origin.
    /// Returns -1 when <paramref name="pos"/> is before the midpoint of the first definition.
    /// </summary>
    private static int ComputeInsertAfter(
        IReadOnlyList<GridDefinitionModel> defs, double pos)
    {
        if (defs.Count == 0) return -1;
        for (int i = 0; i < defs.Count; i++)
        {
            double mid = defs[i].OffsetPixels + defs[i].ActualPixels / 2.0;
            if (pos <= mid) return i - 1;  // before definition i → after i-1 (-1 = prepend)
        }
        return defs.Count - 1; // after the last definition
    }

    // ── Mouse selection ────────────────────────────────────────────

    /// <summary>
    /// Width of the outer edge band (in presenter-space pixels) within which the
    /// cursor is considered to be on the DesignRoot outer frame rather than an interior child.
    /// </summary>
    private const double RootEdgeHitZone = 6.0;

    /// <summary>
    /// Returns the DesignRoot's bounding rect in <see cref="_presenter"/> coordinates,
    /// or <see cref="Rect.Empty"/> if DesignRoot is not a FrameworkElement.
    /// </summary>
    private Rect GetRootBoundsInPresenter()
    {
        if (DesignRoot is not FrameworkElement rootFe) return Rect.Empty;
        var origin = rootFe.TransformToAncestor(_presenter).Transform(new Point(0, 0));
        return new Rect(origin, rootFe.RenderSize);
    }

    /// <summary>
    /// Returns true when <paramref name="posInPresenter"/> falls inside the outer
    /// <see cref="RootEdgeHitZone"/>-pixel rim of DesignRoot — i.e., on the outer frame
    /// but not in the interior child area.
    /// </summary>
    private bool IsInRootEdgeZone(Point posInPresenter)
    {
        var outer = GetRootBoundsInPresenter();
        if (outer.IsEmpty || !outer.Contains(posInPresenter)) return false;
        var inner = new Rect(
            outer.X + RootEdgeHitZone,
            outer.Y + RootEdgeHitZone,
            Math.Max(0, outer.Width  - RootEdgeHitZone * 2),
            Math.Max(0, outer.Height - RootEdgeHitZone * 2));
        return !inner.Contains(posInPresenter);
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DesignRoot is null) return;

        // Grid insert guide takes first priority: the adorner is non-hit-testable so its
        // bounds must be checked explicitly before any other click handling.
        if (HandleGridInsertClick(e)) return;

        // Don't reselect when clicking on any interactive adorner element:
        // handles the Thumb body check, Border-inside-Thumb (template children),
        // GridGuideAdorner chips/buttons, and ResizeAdorner drag-move body.
        if (IsSourceFromAdorner(e.OriginalSource as DependencyObject)) return;

        var clickPoint = e.GetPosition(_presenter);
        bool isAlt  = (Keyboard.Modifiers & ModifierKeys.Alt)     != 0;
        bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        if (!isAlt)
        {
            // Outer-frame zone: clicking on the root element's edge band selects the root directly.
            if (IsInRootEdgeZone(clickPoint))
            {
                _lastHitElements.Clear();
                _altClickDepth = 0;
                if (isCtrl)
                    ToggleElementInSelection(DesignRoot!);
                else
                    SelectElement(DesignRoot);
                e.Handled = false;
                return;
            }

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

            // Ensure DesignRoot is always in the list for Alt+Click cycling,
            // even when it has a transparent/null background that WPF hit-test misses.
            var rootBounds = GetRootBoundsInPresenter();
            if (DesignRoot is UIElement rootEl
                && !_lastHitElements.Contains(rootEl)
                && !rootBounds.IsEmpty
                && rootBounds.Contains(clickPoint))
            {
                _lastHitElements.Add(rootEl);
            }

            // Prefer a leaf element (not a container with children); fall back to topmost.
            var target = _lastHitElements.FirstOrDefault(IsLeafElement)
                      ?? _lastHitElements.FirstOrDefault();

            if (isCtrl && target is not null)
            {
                // Ctrl+Click — toggle the element in / out of the multi-selection.
                ToggleElementInSelection(target);
            }
            else if (target is null)
            {
                // Click on empty canvas space — clear selection and start rubber-band marquee.
                SelectElement(null);
                if (DesignRoot is IInputElement rootIe)
                    StartRubberBand(e.GetPosition(rootIe));
            }
            else
            {
                SelectElement(target);
            }
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

                // Ensure DesignRoot is reachable via Alt+Click cycling.
                var rootBounds = GetRootBoundsInPresenter();
                if (DesignRoot is UIElement rootEl
                    && !_lastHitElements.Contains(rootEl)
                    && !rootBounds.IsEmpty
                    && rootBounds.Contains(clickPoint))
                {
                    _lastHitElements.Add(rootEl);
                }
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
    /// Returns true when <paramref name="obj"/> is an <see cref="Adorner"/> or a visual
    /// descendant of one (e.g. a Border inside a Thumb template inside a ResizeAdorner,
    /// or the GridGuideAdorner itself). Prevents the canvas selection logic from firing
    /// when the user clicks on interactive adorner elements.
    /// </summary>
    private static bool IsSourceFromAdorner(DependencyObject? obj)
    {
        var node = obj;
        while (node is not null)
        {
            if (node is Adorner) return true;
            // VisualTreeHelper.GetParent throws InvalidOperationException on DependencyObjects
            // that are neither Visual nor Visual3D (e.g. FlowDocument, TextElement).
            // Stop walking as soon as we leave the visual tree — can't be an Adorner ancestor.
            if (node is not Visual and not System.Windows.Media.Media3D.Visual3D) return false;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
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

    // ── Hover highlighting ────────────────────────────────────────────────────

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        // Update rubber-band adorner while the user is dragging a selection marquee.
        if (_isRubberBanding)
        {
            if (DesignRoot is IInputElement rootIe)
            {
                var current = e.GetPosition(rootIe);
                _rubberBandAdorner?.UpdateBounds(_rubberBandStart, current);
            }
            return;
        }

        if (DesignRoot is null) return;

        // e.GetPosition(this) = position in DesignCanvas (Border) coordinate space.
        // Using _presenter would add the 8px ContentPresenter.Margin offset.
        // All subsequent conversions (PresenterToGridLocal, FindGridByBounds) use
        // this.TranslatePoint(pt, target) which is transform-aware.
        var mousePos   = e.GetPosition(this);
        var hitElement = HitTestElement(e.GetPosition(_presenter));

        // Don't overlay hover on the already-selected element.
        UpdateHoverAdorner(ReferenceEquals(hitElement, SelectedElement) ? null : hitElement);

        // Grid insert guide: show only when the hovered Grid is selected.
        UpdateGridInsertAdorner(hitElement, mousePos);

        // Update cursor when hovering over insert-guide regions.
        if (_insertAdorner is not null && _insertAdornerGrid is not null && _insertAdorner.IsVisible)
        {
            var lp = PresenterToGridLocal(mousePos, _insertAdornerGrid);
            if (_insertAdorner.ToggleBounds.Contains(lp))
            {
                ForceCursor = false;
                Cursor = Cursors.Hand;
                return;
            }
            if (_insertAdorner.LineBounds.Contains(lp))
            {
                ForceCursor = false;
                Cursor = _insertAdorner.Mode == GridInsertAdorner.InsertMode.Row
                    ? Cursors.SizeNS
                    : Cursors.SizeWE;
                return;
            }
        }
        // Restore forced Arrow — blocks any child cursor propagation.
        Cursor      = Cursors.Arrow;
        ForceCursor = true;
    }

    private void OnCanvasMouseEnter(object sender, MouseEventArgs e)
    {
        if (DesignRoot is null) return;
        var mousePos   = e.GetPosition(this);
        var hitElement = HitTestElement(e.GetPosition(_presenter));
        UpdateGridInsertAdorner(hitElement, mousePos);
    }

    private void OnCanvasMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isRubberBanding) return;
        UpdateHoverAdorner(null);

        // WPF fires a spurious MouseLeave on the canvas each time a new adorner
        // (e.g. ResizeAdorner) is added to the AdornerLayer, even though the mouse
        // is still physically over the canvas.  SelectElement sets this flag so the
        // very next MouseLeave after a selection change is ignored.
        if (_suppressNextMouseLeave)
        {
            _suppressNextMouseLeave = false;
            return;
        }
        HideGridInsertAdorner();
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isRubberBanding) return;
        _isRubberBanding = false;
        ReleaseMouseCapture();

        // Remove the rubber-band adorner from the design root layer.
        if (DesignRoot is not null && _rubberBandAdorner is not null)
        {
            var layer = AdornerLayer.GetAdornerLayer(DesignRoot);
            layer?.Remove(_rubberBandAdorner);
            _rubberBandAdorner = null;
        }

        if (DesignRoot is not IInputElement rootIe) return;

        var end  = e.GetPosition(rootIe);
        var rect = new Rect(
            Math.Min(_rubberBandStart.X, end.X),
            Math.Min(_rubberBandStart.Y, end.Y),
            Math.Abs(end.X - _rubberBandStart.X),
            Math.Abs(end.Y - _rubberBandStart.Y));

        if (rect.Width < 2 || rect.Height < 2) return;

        var hits = CollectElementsInRubberBand(rect);
        if (hits.Count > 0)
            SelectElements(hits);
    }

    /// <summary>
    /// Returns the leaf-preferred UIElement at <paramref name="positionInPresenter"/>
    /// within the <see cref="_presenter"/> hit area, excluding adorners.
    /// When the cursor is in the outer <see cref="RootEdgeHitZone"/>-pixel rim of
    /// <see cref="DesignRoot"/>, the root element is returned directly so the outer
    /// frame is always hoverable and selectable.
    /// </summary>
    private UIElement? HitTestElement(Point positionInPresenter)
    {
        // Outer-frame priority: cursor on the root element's edge band → return root directly.
        if (IsInRootEdgeZone(positionInPresenter))
            return DesignRoot;

        var hits = new List<UIElement>();
        VisualTreeHelper.HitTest(
            _presenter,
            d => d is Adorner
                ? HitTestFilterBehavior.ContinueSkipSelfAndChildren
                : HitTestFilterBehavior.Continue,
            r =>
            {
                if (r.VisualHit is UIElement u && !ReferenceEquals(u, _presenter))
                    hits.Add(u);
                return HitTestResultBehavior.Continue;
            },
            new PointHitTestParameters(positionInPresenter));

        // Transparent-background guard: DesignRoot may be skipped by WPF hit-test when
        // Background is null. If the cursor is within its bounds and no other element was
        // hit, add it as a fallback candidate.
        var rootBounds = GetRootBoundsInPresenter();
        if (DesignRoot is UIElement rootEl
            && !hits.Contains(rootEl)
            && !rootBounds.IsEmpty
            && rootBounds.Contains(positionInPresenter))
        {
            hits.Add(rootEl);
        }

        return hits.FirstOrDefault(IsLeafElement) ?? hits.FirstOrDefault();
    }

    private void UpdateHoverAdorner(UIElement? target)
    {
        if (ReferenceEquals(target, _hoveredElement)) return;

        RemoveHoverAdorner();
        _hoveredElement = target;

        if (_hoveredElement is not null)
        {
            var layer = AdornerLayer.GetAdornerLayer(_hoveredElement);
            layer?.Add(new HoverAdorner(_hoveredElement));
        }
    }

    private void RemoveHoverAdorner()
    {
        if (_hoveredElement is null) return;

        var layer = AdornerLayer.GetAdornerLayer(_hoveredElement);
        if (layer is not null)
        {
            var adorners = layer.GetAdorners(_hoveredElement);
            if (adorners is not null)
                foreach (var a in adorners.OfType<HoverAdorner>().ToList())
                    layer.Remove(a);
        }

        _hoveredElement = null;
    }

    /// <summary>
    /// Draws a non-selecting hover overlay on <paramref name="element"/> — called by the
    /// Live Visual Tree panel when the user hovers a tree node so the canvas reflects it.
    /// Pass null to clear the overlay.  Does NOT change <see cref="SelectedElement"/>.
    /// </summary>
    public void HighlightHoverElement(UIElement? element)
    {
        // Skip if the element is already selected (selection adorner has priority).
        if (ReferenceEquals(element, SelectedElement)) return;
        if (element is not null && _selectedElements.Contains(element)) return;
        UpdateHoverAdorner(element);
    }

    // ── Rubber-band selection ─────────────────────────────────────────────────

    /// <summary>
    /// Begins a rubber-band marquee drag starting at <paramref name="startInRoot"/>.
    /// Places a <see cref="RubberBandAdorner"/> on DesignRoot and captures the mouse.
    /// </summary>
    private void StartRubberBand(Point startInRoot)
    {
        _rubberBandStart = startInRoot;
        _isRubberBanding = true;

        var layer = DesignRoot is not null ? AdornerLayer.GetAdornerLayer(DesignRoot) : null;
        if (layer is not null)
        {
            _rubberBandAdorner = new RubberBandAdorner(DesignRoot!);
            layer.Add(_rubberBandAdorner);
        }
        CaptureMouse();
    }

    /// <summary>
    /// Returns all <see cref="FrameworkElement"/>s whose bounds (in DesignRoot space)
    /// intersect <paramref name="rubberBandInRoot"/>. DesignRoot itself is excluded.
    /// </summary>
    private IReadOnlyList<UIElement> CollectElementsInRubberBand(Rect rubberBandInRoot)
    {
        if (DesignRoot is not FrameworkElement root) return Array.Empty<UIElement>();
        var result = new List<UIElement>();
        CollectElementsInRectCore(root, rubberBandInRoot, root, result);
        return result;
    }

    private static void CollectElementsInRectCore(
        UIElement el, Rect rect, UIElement root, List<UIElement> result)
    {
        if (!ReferenceEquals(el, root) && el is FrameworkElement fe
            && fe.IsVisible && fe.ActualWidth > 0 && fe.ActualHeight > 0)
        {
            var pos    = fe.TranslatePoint(new Point(0, 0), root);
            var bounds = new Rect(pos.X, pos.Y, fe.ActualWidth, fe.ActualHeight);
            if (rect.IntersectsWith(bounds))
                result.Add(el);
        }

        int n = VisualTreeHelper.GetChildrenCount(el);
        for (int i = 0; i < n; i++)
        {
            if (VisualTreeHelper.GetChild(el, i) is UIElement child)
                CollectElementsInRectCore(child, rect, root, result);
        }
    }

    // ── XAML preprocessing ────────────────────────────────────────────────────

    private static readonly string[] WindowOnlyAttributes =
    [
        // Properties
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
