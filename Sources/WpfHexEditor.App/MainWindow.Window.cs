// ==========================================================
// Project      : WpfHexEditor.App
// File         : MainWindow.Window.cs
// Description  : Partial class of MainWindow — Window menu handlers:
//                close variants, document cycling, full screen (F11),
//                and dynamic open-documents list.
// Architecture : All document enumeration via _layout.GetAllGroups().
//                Full screen state stored in Layout.cs fields.
//                ActivateExistingDockPanel (PluginSystem.cs) used for focus.
// ==========================================================

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // ── F11 RoutedCommand ────────────────────────────────────────────────────

    public static readonly RoutedCommand ToggleFullScreenCommand =
        new(nameof(ToggleFullScreenCommand), typeof(MainWindow));

    private void OnToggleFullScreenRoutedCommand(object sender, ExecutedRoutedEventArgs e)
        => OnToggleFullScreen();

    private void OnToggleFullScreenMenuClick(object sender, RoutedEventArgs e)
        => OnToggleFullScreen();

    /// <summary>
    /// PreviewKeyDown intercepts F11 before any focused child (code editor, DockHost)
    /// can consume the KeyDown event. Tunneling ensures Window-level shortcuts work
    /// regardless of which control has keyboard focus.
    /// </summary>
    private void OnMainWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            OnToggleFullScreen();
            e.Handled = true;
        }
    }

    // ── Close All But This ───────────────────────────────────────────────────

    internal void OnCloseAllButThis(object sender, RoutedEventArgs e)
    {
        var active = _layout.GetAllGroups()
            .SelectMany(g => g.Items)
            .FirstOrDefault(i => i == i.Owner?.ActiveItem && !i.ContentId.StartsWith("panel-"));

        var toClose = _layout.GetAllGroups()
            .SelectMany(g => g.Items)
            .Where(i => !i.ContentId.StartsWith("panel-") && i != active)
            .ToList();

        foreach (var item in toClose)
            OnTabCloseRequested(item);
    }

    // ── Document cycling ─────────────────────────────────────────────────────

    internal void OnNextDocument(object sender, RoutedEventArgs e)     => CycleDocument(+1);
    internal void OnPreviousDocument(object sender, RoutedEventArgs e) => CycleDocument(-1);

    private void CycleDocument(int direction)
    {
        var docs = _layout.GetAllGroups()
            .SelectMany(g => g.Items)
            .Where(i => !i.ContentId.StartsWith("panel-"))
            .ToList();

        if (docs.Count < 2) return;

        var active = docs.FirstOrDefault(i => i == i.Owner?.ActiveItem);
        var idx    = active is null ? 0 : docs.IndexOf(active);
        var next   = docs[(idx + direction + docs.Count) % docs.Count];

        ActivateExistingDockPanel(next.ContentId);
    }

    // ── Dynamic open-documents list ──────────────────────────────────────────

    internal void OnWindowMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem windowMenu) return;

        // Find the sentinel separator and remove all items after it.
        var sep = windowMenu.Items
            .OfType<FrameworkElement>()
            .FirstOrDefault(x => x.Tag is "doc-sep");

        if (sep is not null)
        {
            var sepIdx = windowMenu.Items.IndexOf(sep);
            while (windowMenu.Items.Count > sepIdx + 1)
                windowMenu.Items.RemoveAt(sepIdx + 1);
        }

        var docs = _layout.GetAllGroups()
            .SelectMany(g => g.Items)
            .Where(i => !i.ContentId.StartsWith("panel-"))
            .ToList();

        if (docs.Count == 0) return;

        var activeItem = docs.FirstOrDefault(i => i == i.Owner?.ActiveItem);

        foreach (var doc in docs)
        {
            var isActive = doc == activeItem;
            var mi = new MenuItem
            {
                Header     = (isActive ? "● " : "    ") + doc.Title,
                Tag        = doc.ContentId,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
            };
            mi.Click += OnWindowDocumentActivate;
            windowMenu.Items.Add(mi);
        }
    }

    private void OnWindowDocumentActivate(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || mi.Tag is not string contentId) return;
        ActivateExistingDockPanel(contentId);
    }
}
