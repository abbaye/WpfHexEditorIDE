// ==========================================================
// Project: WpfHexEditor.App
// File: MainWindow.CommandPalette.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Description:
//     Command Palette handler (Ctrl+Shift+P) and Options page registration.
//     Builds the command catalog from the central CommandRegistry
//     (all built-in IDE commands) + plugin-contributed menu items,
//     then opens the CommandPaletteWindow overlay with settings and context.
//
// Architecture Notes:
//     Partial class of MainWindow. Depends on _commandRegistry and
//     _keyBindingService (initialised by InitCommands in MainWindow.Commands.cs).
// ==========================================================

using WpfHexEditor.App.Dialogs;
using WpfHexEditor.App.Models;
using WpfHexEditor.App.Options;
using WpfHexEditor.App.Services;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // ── Initialization ────────────────────────────────────────────────────────

    private void InitCommandPaletteOptions()
    {
        OptionsPageRegistry.RegisterDynamic(
            category:     "Tools",
            pageName:     "Command Palette",
            factory:      () => new CommandPaletteOptionsPage(),
            categoryIcon: "🛠");
    }

    // ── Quick File Open (Ctrl+P) ──────────────────────────────────────────────

    /// <summary>
    /// Opens the Command Palette pre-populated with the <c>#</c> file-search prefix.
    /// Registered as the Ctrl+P gesture (CommandIds.File.QuickOpen).
    /// </summary>
    private void OnQuickOpen() => OpenCommandPaletteWithPrefix("# ");

    /// <summary>
    /// Opens a fresh Command Palette window with <paramref name="initialQuery"/> pre-filled.
    /// Shares all palette infrastructure (service, LSP client, navigation callbacks).
    /// </summary>
    private void OpenCommandPaletteWithPrefix(string initialQuery)
    {
        var cpSettings = AppSettingsService.Instance.Current.CommandPalette;

        var entries = _commandRegistry
            .GetAll()
            .Select(cmd => new CommandPaletteEntry(
                Name:        StripAccessKey(cmd.Name),
                Category:    cmd.Category,
                GestureText: _keyBindingService.ResolveGesture(cmd.Id),
                IconGlyph:   cmd.IconGlyph,
                Command:     cmd.Command))
            .ToList();

        if (_menuAdapter is not null)
        {
            foreach (var (uiId, descriptor) in _menuAdapter.GetAllMenuItems())
            {
                if (descriptor.Command is null) continue;
                if (string.Equals(descriptor.ParentPath?.TrimStart('_'), "View", StringComparison.OrdinalIgnoreCase))
                    continue; // View items enriched below
                var category = string.IsNullOrWhiteSpace(descriptor.ParentPath)
                    ? "Plugins"
                    : StripAccessKey(descriptor.ParentPath);
                entries.Add(new CommandPaletteEntry(
                    Name:             StripAccessKey(descriptor.Header?.ToString() ?? uiId),
                    Category:         category,
                    GestureText:      descriptor.GestureText,
                    IconGlyph:        descriptor.IconGlyph,
                    Command:          descriptor.Command,
                    CommandParameter: descriptor.CommandParameter));
            }
        }

        if (_viewMenuOrganizer is not null)
        {
            var vmSettings = AppSettingsService.Instance.Current.ViewMenu;
            foreach (var ve in _viewMenuOrganizer.GetAllEntries())
            {
                if (ve.Command is null) continue;
                var classified = Services.ViewMenu.ViewMenuClassifier.Classify(ve, vmSettings);
                var paletteCat = string.IsNullOrWhiteSpace(classified) || classified == "Other"
                    ? "View" : $"View / {classified}";
                entries.Add(new CommandPaletteEntry(
                    Name:             StripAccessKey(ve.Header),
                    Category:         paletteCat,
                    GestureText:      ve.GestureText,
                    IconGlyph:        ve.IconGlyph,
                    Command:          ve.Command,
                    CommandParameter: ve.CommandParameter));
            }
        }

        var service = new CommandPaletteService(entries);

        WpfHexEditor.Editor.Core.LSP.ILspClient? lspClient = null;
        if (_lspBridgeService is not null && _documentManager?.ActiveDocument?.Buffer is { } buf
            && !string.IsNullOrEmpty(buf.LanguageId))
            lspClient = _lspBridgeService.TryGetClient(buf.LanguageId);

        System.Action<int>? goToLine = null;
        if (_documentManager?.ActiveDocument?.AssociatedEditor is INavigableDocument nav)
            goToLine = line => nav.NavigateTo(line - 1, 0);

        System.Action<string, int> openAndNavigate = (path, line) =>
        {
            var existing = _documentManager?.OpenDocuments
                .FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                _documentManager!.SetActive(existing.ContentId);
                if (line > 0 && existing.AssociatedEditor is INavigableDocument navExisting)
                    navExisting.NavigateTo(line, 0);
                return;
            }
            OpenFileDirectly(path);
            if (line > 0) NavigateAfterOpen(path, line);
        };

        System.Windows.Point? anchor = null;
        if (TitleBarSearchButton is { IsLoaded: true })
        {
            var physPt = TitleBarSearchButton.PointToScreen(
                new System.Windows.Point(
                    TitleBarSearchButton.ActualWidth  / 2,
                    TitleBarSearchButton.ActualHeight));
            var src  = System.Windows.PresentationSource.FromVisual(this);
            var dpiX = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            var dpiY = src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
            anchor   = new System.Windows.Point(physPt.X / dpiX, physPt.Y / dpiY);
        }

        new CommandPaletteWindow(
            service:         service,
            settings:        cpSettings,
            owner:           this,
            anchor:          anchor,
            lspClient:       lspClient,
            documentManager: _documentManager,
            goToLine:        goToLine,
            solutionManager: _solutionManager,
            openAndNavigate: openAndNavigate,
            initialQuery:    initialQuery).Show();
    }

    // ── Handler wired in MainWindow.xaml CommandBindings ──────────────────────

    private void OnShowCommandPalette(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        var cpSettings = AppSettingsService.Instance.Current.CommandPalette;

        // 1. All built-in IDE commands from the central registry
        var entries = _commandRegistry
            .GetAll()
            .Select(cmd => new CommandPaletteEntry(
                Name:        cmd.Name,
                Category:    cmd.Category,
                GestureText: _keyBindingService.ResolveGesture(cmd.Id),
                IconGlyph:   cmd.IconGlyph,
                Command:     cmd.Command))
            .ToList();

        // 2. Plugin-contributed menu items (non-View paths handled here;
        //    View-path items are enriched with subcategory from ViewMenuOrganizer below).
        if (_menuAdapter is not null)
        {
            // Dedup set: built-in registry entries take precedence over plugin re-registrations.
            // Keyed by "Name|Category" (case-insensitive) so plugins that mirror a built-in
            // command (e.g. DebuggerPlugin re-registering Continue, Step Over, etc.) are silently
            // skipped, preventing duplicates in the palette without requiring per-plugin coordination.
            var registryKeys = entries
                .Select(e => $"{e.Name.Trim()}|{e.Category?.Trim()}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (uiId, descriptor) in _menuAdapter.GetAllMenuItems())
            {
                if (descriptor.Command is null) continue;

                // Skip View-parented items — they're added with richer categories below.
                if (string.Equals(descriptor.ParentPath?.TrimStart('_'), "View", StringComparison.OrdinalIgnoreCase))
                    continue;

                var category = string.IsNullOrWhiteSpace(descriptor.ParentPath)
                    ? "Plugins"
                    : StripAccessKey(descriptor.ParentPath);

                var name = StripAccessKey(descriptor.Header?.ToString() ?? uiId);

                // Skip if already contributed by the central command registry.
                if (registryKeys.Contains($"{name.Trim()}|{category.Trim()}"))
                    continue;

                entries.Add(new CommandPaletteEntry(
                    Name:             name,
                    Category:         category,
                    GestureText:      descriptor.GestureText,
                    IconGlyph:        descriptor.IconGlyph,
                    Command:          descriptor.Command,
                    CommandParameter: descriptor.CommandParameter));
            }
        }

        // 2b. View menu entries (built-in + plugin) with rich subcategories.
        //     Uses the ViewMenuClassifier to produce "View / Analysis", "View / Design", etc.
        if (_viewMenuOrganizer is not null)
        {
            var vmSettings = AppSettingsService.Instance.Current.ViewMenu;
            foreach (var ve in _viewMenuOrganizer.GetAllEntries())
            {
                if (ve.Command is null) continue;
                var classified = Services.ViewMenu.ViewMenuClassifier.Classify(ve, vmSettings);
                var paletteCat = string.IsNullOrWhiteSpace(classified) || classified == "Other"
                    ? "View"
                    : $"View / {classified}";
                entries.Add(new CommandPaletteEntry(
                    Name:             StripAccessKey(ve.Header),
                    Category:         paletteCat,
                    GestureText:      ve.GestureText,
                    IconGlyph:        ve.IconGlyph,
                    Command:          ve.Command,
                    CommandParameter: ve.CommandParameter));
            }
        }

        // 3. Build service and inject active editor category for context boost
        var service = new CommandPaletteService(entries);
        if (_documentManager?.ActiveDocument?.AssociatedEditor is { } activeEditor)
        {
            // Infer editor category from document type
            var category = InferEditorCategory(activeEditor);
            service.SetActiveCategory(category);
        }

        // 4. Resolve active LSP client (best-effort: use active document's language)
        WpfHexEditor.Editor.Core.LSP.ILspClient? lspClient = null;
        if (_lspBridgeService is not null && _documentManager?.ActiveDocument?.Buffer is { } buf
            && !string.IsNullOrEmpty(buf.LanguageId))
            lspClient = _lspBridgeService.TryGetClient(buf.LanguageId);

        // 5. GoToLine callback: navigate active INavigableDocument
        System.Action<int>? goToLine = null;
        if (_documentManager?.ActiveDocument?.AssociatedEditor is INavigableDocument nav)
            goToLine = line => nav.NavigateTo(line - 1, 0);   // INavigableDocument is 0-based

        // 6. Open-and-navigate callback for # (files) and % (grep) modes
        System.Action<string, int> openAndNavigate = (path, line) =>
        {
            // Already open in a tab? Activate it and optionally jump to line
            var existing = _documentManager?.OpenDocuments
                .FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                _documentManager!.SetActive(existing.ContentId);
                if (line > 0 && existing.AssociatedEditor is INavigableDocument navExisting)
                    navExisting.NavigateTo(line, 0);
                return;
            }

            // Try to find as a project item for proper tab metadata
            if (_solutionManager.CurrentSolution is { } sol)
            {
                foreach (var proj in sol.Projects)
                {
                    var item = proj.Items.FirstOrDefault(i =>
                        string.Equals(i.AbsolutePath, path, StringComparison.OrdinalIgnoreCase));
                    if (item is not null)
                    {
                        OpenProjectItem(item, proj);
                        if (line > 0) NavigateAfterOpen(path, line);
                        return;
                    }
                }
            }

            // Standalone file fallback
            OpenFileDirectly(path);
            if (line > 0) NavigateAfterOpen(path, line);
        };

        // 7. Anchor palette below title bar launcher button
        System.Windows.Point? anchor = null;
        if (TitleBarSearchButton is { IsLoaded: true })
        {
            var physPt = TitleBarSearchButton.PointToScreen(
                new System.Windows.Point(
                    TitleBarSearchButton.ActualWidth  / 2,
                    TitleBarSearchButton.ActualHeight));

            var src  = System.Windows.PresentationSource.FromVisual(this);
            var dpiX = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            var dpiY = src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
            anchor   = new System.Windows.Point(physPt.X / dpiX, physPt.Y / dpiY);
        }

        new CommandPaletteWindow(
            service:          service,
            settings:         cpSettings,
            owner:            this,
            anchor:           anchor,
            lspClient:        lspClient,
            documentManager:  _documentManager,
            goToLine:         goToLine,
            solutionManager:  _solutionManager,
            openAndNavigate:  openAndNavigate).Show();
    }

    // ── Navigation helper ────────────────────────────────────────────────────

    /// <summary>
    /// Defers navigation to <paramref name="line"/> in the editor that will be
    /// associated with <paramref name="path"/> after the tab finishes loading.
    /// </summary>
    private void NavigateAfterOpen(string path, int line)
        => Dispatcher.InvokeAsync(() =>
        {
            var doc = _documentManager?.OpenDocuments
                .FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (doc?.AssociatedEditor is INavigableDocument nav)
                nav.NavigateTo(line, 0);
        }, System.Windows.Threading.DispatcherPriority.Background);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes WPF access-key mnemonic markers from a menu header string.
    /// Rules: <c>__</c> → <c>_</c> (escaped literal),  <c>_X</c> → <c>X</c> (access key stripped).
    /// </summary>
    private static string StripAccessKey(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '_' && i + 1 < text.Length)
            {
                if (text[i + 1] == '_')
                {
                    sb.Append('_'); // __ → literal _
                    i++;            // skip second _
                }
                // else: _X → skip the _ (access key marker)
            }
            else
            {
                sb.Append(text[i]);
            }
        }
        return sb.ToString();
    }

    private static string? InferEditorCategory(IDocumentEditor editor) => editor switch
    {
        WpfHexEditor.Editor.CodeEditor.Controls.CodeEditorSplitHost  => "Build",
        WpfHexEditor.Editor.MarkdownEditor.Controls.MarkdownEditorHost => "View",
        _                                                              => null
    };
}
