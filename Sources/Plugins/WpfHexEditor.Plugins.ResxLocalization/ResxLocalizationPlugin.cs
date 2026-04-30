// ==========================================================
// Project: WpfHexEditor.Plugins.ResxLocalization
// File: ResxLocalizationPlugin.cs
// Description:
//     Entry point for the RESX Localization plugin (priority 55).
//     Registers:
//       - LocaleBrowserPanel        (Left, AutoHide, width=240)
//       - MissingTranslationsPanel  (Bottom, AutoHide, height=200)
//       - View > Locale Browser     (menu item)
//       - View > Missing Translations (menu item)
//     Subscribes to ResxLocaleDiscoveredEvent on IIDEEventBus to
//     populate panels without polling the file system.
//     Subscribes to FileOpenedEvent to mark the active locale row.
// Architecture Notes:
//     Pattern: event-driven — panels are passive; plugin wires them.
//     All UI constructed on UI thread (InitializeAsync is called on UI thread).
//     UIRegistry.UnregisterAllForPlugin called automatically by PluginHost on unload.
// ==========================================================

using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfHexEditor.Editor.ResxEditor.Models;
using WpfHexEditor.Editor.ResxEditor.Services;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Plugins.ResxLocalization.Panels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.ResxLocalization.Properties;

namespace WpfHexEditor.Plugins.ResxLocalization;

/// <summary>
/// Plugin that provides locale management UI for .resx files.
/// Requires <c>WpfHexEditor.Editor.ResxEditor</c> to be loaded
/// (ResxLocaleDiscoveredEvent published by the editor).
/// </summary>
public sealed class ResxLocalizationPlugin : IWpfHexEditorPlugin
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string  Id      => "WpfHexEditor.Plugins.ResxLocalization";
    public string  Name    => "RESX Localization";
    public Version Version => new(0, 1, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    // ── Panel content IDs ─────────────────────────────────────────────────────

    private const string LocaleBrowserPanelId   = "panel-resx-locales";
    private const string MissingPanelId         = "panel-resx-missing";

    // ── State ─────────────────────────────────────────────────────────────────

    private IIDEHostContext?         _context;
    private LocaleBrowserPanel?      _localeBrowser;
    private MissingTranslationsPanel? _missingPanel;

    private IDisposable? _localeDiscoveredSub;
    private IDisposable? _fileOpenedSub;

    // Most-recently-seen locale set — used to refresh missing translations
    private string   _lastBasePath  = string.Empty;
    private string[] _lastVariants  = [];

    // ── IWpfHexEditorPlugin ────────────────────────────────────────────────────

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct)
    {
        _context = context;

        // ── Panels ────────────────────────────────────────────────────────────

        _localeBrowser = new LocaleBrowserPanel();
        _missingPanel  = new MissingTranslationsPanel();

        // Wire open request: user clicks a locale row → open that file
        _localeBrowser.OpenLocaleRequested += path =>
        {
            if (_context is not null)
                _context.DocumentHost.OpenDocument(path, "resx-editor");
        };

        context.UIRegistry.RegisterPanel(
            LocaleBrowserPanelId,
            _localeBrowser,
            Id,
            new PanelDescriptor
            {
                Title           = ResxLocalizationResources.ResxLoc_LocaleBrowser,
                DefaultDockSide = "Left",
                DefaultAutoHide = true,
                PreferredWidth  = 240
            });

        context.UIRegistry.RegisterPanel(
            MissingPanelId,
            _missingPanel,
            Id,
            new PanelDescriptor
            {
                Title           = ResxLocalizationResources.ResxLoc_MissingTranslations,
                DefaultDockSide = "Bottom",
                DefaultAutoHide = true,
                PreferredHeight = 200
            });

        // ── Menu items ────────────────────────────────────────────────────────

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.LocaleBrowser",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Locale Browser",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE774",
                ToolTip    = "Show or hide the RESX Locale Browser panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(LocaleBrowserPanelId))
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.MissingTranslations",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Missing Translations",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE897",
                ToolTip    = "Show or hide the Missing Translations panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(MissingPanelId))
            });

        // ── Event bus subscriptions ───────────────────────────────────────────

        _localeDiscoveredSub = context.IDEEvents.Subscribe<ResxLocaleDiscoveredEvent>(OnLocaleDiscovered);
        _fileOpenedSub       = context.IDEEvents.Subscribe<FileOpenedEvent>(OnFileOpened);

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct)
    {
        _localeDiscoveredSub?.Dispose();
        _fileOpenedSub?.Dispose();

        _localeBrowser = null;
        _missingPanel  = null;
        _context       = null;

        return Task.CompletedTask;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnLocaleDiscovered(ResxLocaleDiscoveredEvent e)
    {
        _lastBasePath = e.BasePath;
        _lastVariants = e.Variants;

        if (_localeBrowser is null && _missingPanel is null) return;

        Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            // Refresh locale browser
            _localeBrowser?.Refresh(e.BasePath, e.Variants);

            // Load documents for missing-translations matrix
            try
            {
                var docs = new List<(string, ResxDocument)>();
                var baseDoc = await ResxDocumentParser.ParseAsync(e.BasePath);
                docs.Add(("Base", baseDoc));

                foreach (var varPath in e.Variants)
                {
                    try
                    {
                        var varDoc = await ResxDocumentParser.ParseAsync(varPath);
                        var fileName = System.IO.Path.GetFileName(varPath);
                        var parts    = fileName.Split('.');
                        var culture  = parts.Length >= 3 ? parts[^2] : fileName;
                        docs.Add((culture, varDoc));
                    }
                    catch { /* skip unreadable locale files */ }
                }

                _missingPanel?.Refresh(docs);
            }
            catch { /* base file unreadable — panels stay in last state */ }
        });
    }

    private void OnFileOpened(FileOpenedEvent e)
    {
        if (_localeBrowser is null) return;
        if (!string.Equals(System.IO.Path.GetExtension(e.FilePath), ".resx",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(System.IO.Path.GetExtension(e.FilePath), ".resw",
                StringComparison.OrdinalIgnoreCase))
            return;

        Application.Current?.Dispatcher.InvokeAsync(() =>
            _localeBrowser?.SetActiveFile(e.FilePath));
    }
}
