// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: Services/XamlDesignerServiceImpl.cs
// Created: 2026-04-06
// Description:
//     Implements IXamlDesignerService — the SDK bridge that exposes the active
//     XAML Designer's live element tree to other plugins via ExtensionRegistry.
//
// Architecture Notes:
//     Registered by XamlDesignerPlugin as IXamlDesignerService via ExtensionRegistry.
//     Tracks the active XamlDesignerSplitHost via IFocusContextService.FocusChanged.
//     Builds XamlDesignerNode trees by walking DesignCanvas.DesignRoot with VisualTreeHelper.
//     DesignCanvas.GetUidOf() maps each UIElement to its XamlElementMapper UID.
//     Max depth 32; unnamed children beyond depth 8 are pruned to avoid noise.
// ==========================================================

using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Editor.XamlDesigner.Controls;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.ExtensionPoints.XamlDesigner;

namespace WpfHexEditor.Plugins.XamlDesigner.Services;

/// <summary>
/// Implements <see cref="IXamlDesignerService"/> — exposes the active XAML Designer's
/// element tree to other plugins via the SDK ExtensionRegistry bridge.
/// </summary>
internal sealed class XamlDesignerServiceImpl : IXamlDesignerService
{
    private readonly IFocusContextService    _focusContext;
    private readonly IDocumentHostService    _documentHost;
    private readonly Dispatcher              _dispatcher = Application.Current.Dispatcher;
    private XamlDesignerSplitHost?           _activeHost;

    // Cached on the UI thread so CanProvide() is safe from background threads.
    private volatile bool _isDesignerActive;

    // ── IXamlDesignerService ─────────────────────────────────────────────────

    public bool IsDesignerActive => _isDesignerActive;

    public int SelectedElementUid => _activeHost?.Canvas?.SelectedElementUid ?? -1;

    public event EventHandler?    ElementTreeChanged;
    public event EventHandler<int>? SelectedElementChanged;

    // ── Constructor ──────────────────────────────────────────────────────────

    public XamlDesignerServiceImpl(IFocusContextService focusContext, IDocumentHostService documentHost)
    {
        _focusContext = focusContext;
        _documentHost = documentHost;
        _focusContext.FocusChanged += OnFocusChanged;
    }

    public void Dispose()
    {
        _focusContext.FocusChanged -= OnFocusChanged;
        UnwireHost();
    }

    // ── IXamlDesignerService: GetElementTree ─────────────────────────────────

    public IReadOnlyList<XamlDesignerNode> GetElementTree()
    {
        // Must walk the WPF visual tree on the UI thread — marshal if called from background.
        if (!_dispatcher.CheckAccess())
            return _dispatcher.Invoke(GetElementTree);

        var root = _activeHost?.Canvas?.DesignRoot;
        if (root is null) return [];

        var node = BuildNode(root, _activeHost!.Canvas!, depth: 0);
        return node is not null ? [node] : [];
    }

    public void SelectElement(int uid)
        => _activeHost?.Canvas?.SelectElementByUid(uid);

    public void NavigateToElement(int uid)
    {
        if (uid < 0 || _activeHost is not { } host) return;

        // Ensure we're on the UI thread (canvas operations + DocumentManager SetActive).
        if (!_dispatcher.CheckAccess()) { _dispatcher.Invoke(() => NavigateToElement(uid)); return; }

        // Bring the designer tab to front.
        var model = _documentHost.Documents.OpenDocuments
            .FirstOrDefault(d => ReferenceEquals(d.AssociatedEditor, host));
        if (model is not null)
            _documentHost.Documents.SetActive(model.ContentId);

        // Select in canvas + navigate code editor to source line.
        host.Canvas?.SelectElementByUid(uid);
        host.NavigateCodeEditorToUid(uid);
    }

    // ── Focus tracking ───────────────────────────────────────────────────────

    private void OnFocusChanged(object? sender, FocusChangedEventArgs e)
    {
        var host = ResolveHost(e.ActiveDocument);
        if (ReferenceEquals(host, _activeHost)) return;

        UnwireHost();
        _activeHost = host;
        _isDesignerActive = host?.Canvas?.DesignRoot is not null;
        WireHost(host);

        ElementTreeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void WireHost(XamlDesignerSplitHost? host)
    {
        if (host?.Canvas is not { } canvas) return;
        canvas.DesignRendered      += OnDesignRendered;
        canvas.SelectedElementChanged += OnCanvasSelectionChanged;
    }

    private void UnwireHost()
    {
        if (_activeHost?.Canvas is not { } canvas) return;
        canvas.DesignRendered         -= OnDesignRendered;
        canvas.SelectedElementChanged -= OnCanvasSelectionChanged;
    }

    private void OnDesignRendered(object? sender, UIElement? _)
    {
        _isDesignerActive = _activeHost?.Canvas?.DesignRoot is not null;
        ElementTreeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCanvasSelectionChanged(object? sender, EventArgs _)
        => SelectedElementChanged?.Invoke(this, _activeHost?.Canvas?.SelectedElementUid ?? -1);

    // ── Host resolution ───────────────────────────────────────────────────────

    private XamlDesignerSplitHost? ResolveHost(IDocument? doc)
    {
        if (doc is null) return null;
        var model = _documentHost.Documents.OpenDocuments
            .FirstOrDefault(d => d.ContentId == doc.ContentId);
        return model?.AssociatedEditor as XamlDesignerSplitHost;
    }

    // ── Tree builder ─────────────────────────────────────────────────────────

    private const int MaxDepth       = 32;
    private const int PruneDepth     = 8;   // unnamed children pruned beyond this depth

    // WPF internal types that add noise without semantic value.
    private static readonly HashSet<string> SkippedTypes = new(StringComparer.Ordinal)
    {
        "AdornerDecorator", "AdornerLayer", "ContentPresenter",
        "ScrollContentPresenter", "TemplatedAdorner",
        "InkPresenter", "GlyphsHostVisual",
    };

    private static XamlDesignerNode? BuildNode(UIElement el, DesignCanvas canvas, int depth)
    {
        if (depth > MaxDepth) return null;

        var typeName = el.GetType().Name;

        // Transparent containers (ContentPresenter, AdornerDecorator, etc.) are invisible
        // framework plumbing — skip creating a node but still collect their children so
        // user content nested inside a ContentPresenter is visible in the outline.
        if (SkippedTypes.Contains(typeName))
        {
            // Return a sentinel-less pass-through: we signal the parent to hoist
            // children by returning null, but first we call CollectChildren below.
            // This is handled at the call site by collecting into the parent's list.
            return null;
        }

        var name = el is FrameworkElement fe ? (string.IsNullOrEmpty(fe.Name) ? null : fe.Name) : null;

        // Prune anonymous elements at deep levels to avoid inflating the tree.
        if (depth > PruneDepth && name is null) return null;

        var uid = canvas.GetUidOf(el);

        var children = new List<XamlDesignerNode>();
        CollectChildren(el, canvas, depth, children);

        return new XamlDesignerNode
        {
            Uid      = uid,
            TypeName = typeName,
            Name     = name,
            Children = children,
        };
    }

    /// <summary>
    /// Walks the immediate visual children of <paramref name="el"/>, adding non-null
    /// <see cref="XamlDesignerNode"/> results to <paramref name="sink"/>.
    /// When a child is a <see cref="SkippedTypes"/> transparent container, it is
    /// not added as a node but its own children are recursively collected (hoist).
    /// </summary>
    private static void CollectChildren(UIElement el, DesignCanvas canvas, int depth, List<XamlDesignerNode> sink)
    {
        int count = VisualTreeHelper.GetChildrenCount(el);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(el, i) is not UIElement child) continue;

            if (SkippedTypes.Contains(child.GetType().Name))
            {
                // Hoist: skip this layer, recurse directly into its children.
                CollectChildren(child, canvas, depth, sink);
            }
            else
            {
                var node = BuildNode(child, canvas, depth + 1);
                if (node is not null)
                    sink.Add(node);
            }
        }
    }
}
